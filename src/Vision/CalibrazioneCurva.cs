using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using OpenCvSharp;

namespace VisualMusicBlockLooper.Vision
{
    public sealed partial class Calibrazione
    {
        private DeformazioneCalibrazione deformazione;
        private PuntoImmagine[] riferimentiCamera;
        private PuntoTavolo[] riferimentiTavolo;
        private readonly object sincronizzazioneMappe = new object();
        private MappaAnteprima mappaAnteprima;
        private byte[] mascheraCurva;
        private int larghezzaMaschera, altezzaMaschera, restringimentoMaschera;

        private sealed class MappaAnteprima
        {
            public int Larghezza, Altezza;
            public float[] X, Y;
        }

        /// <summary>True quando ai dati prospettici si aggiunge la correzione curva dei dodici riferimenti.</summary>
        public bool Curva => deformazione != null;

        public static Calibrazione CalcolaCurva(PuntoImmagine[] camera, PuntoTavolo[] tavolo,
            int larghezza, int altezza)
        {
            if (camera == null || tavolo == null) throw new ArgumentNullException(camera == null ? nameof(camera) : nameof(tavolo));
            if (camera.Length != 12 || tavolo.Length != 12) throw new ArgumentException("Servono dodici riferimenti.");
            if (larghezza <= 0 || altezza <= 0) throw new ArgumentException("La calibrazione curva richiede la risoluzione della camera.");
            var calibrazione = Calcola(camera, tavolo, larghezza, altezza, scartaAnomalie: false);
            calibrazione.AggiungiCurva(camera, tavolo);
            return calibrazione;
        }

        private void AggiungiCurva(PuntoImmagine[] camera, PuntoTavolo[] tavolo)
        {
            if (tavolo.Any(p => !Finito(p.X) || !Finito(p.Y) || p.X < 0 || p.X > 1 || p.Y < 0 || p.Y > 1))
                throw new ArgumentException("I riferimenti del tavolo devono essere normalizzati fra 0 e 1.");
            // La deformazione non è ancora impostata: qui si applica solo H.
            var osservati = camera.Select(DaCameraATavolo).ToArray();
            var candidata = new DeformazioneCalibrazione(tavolo, osservati);
            int segno = 0;
            for (int y = 0; y <= 40; y++)
                for (int x = 0; x <= 40; x++)
                {
                    var p = candidata.Applica(new PuntoTavolo(x / 40.0, y / 40.0));
                    double w = inversa[6] * p.X + inversa[7] * p.Y + inversa[8];
                    if (!Finito(w) || Math.Abs(w) < SogliaW || (segno != 0 && Math.Sign(w) != segno))
                        throw new ArgumentException("La calibrazione attraversa un punto all'infinito.");
                    segno = Math.Sign(w);
                    var c = Proietta(inversa, p.X, p.Y);
                    if (Math.Abs(c.X) > 100.0 * Larghezza || Math.Abs(c.Y) > 100.0 * Altezza)
                        throw new ArgumentException("La calibrazione produce coordinate camera fuori scala.");
                }
            deformazione = candidata;
            riferimentiCamera = (PuntoImmagine[])camera.Clone();
            riferimentiTavolo = (PuntoTavolo[])tavolo.Clone();
        }

        /// <summary>Mappa inversa della medesima calibrazione, per il ricampionamento del frame.</summary>
        public PuntoImmagine DaTavoloACamera(PuntoTavolo p)
        {
            if (Curva) p = deformazione.Applica(p);
            PuntoTavolo camera = Proietta(inversa, p.X, p.Y);
            return new PuntoImmagine(camera.X, camera.Y);
        }

        /// <summary>Campiona anche i bordi: con una curva gli estremi possono trovarsi fra gli angoli.</summary>
        public PuntoTavolo[] BordiDaCameraATavolo(Rect r)
        {
            int n = Curva ? 32 : 1;
            var punti = new PuntoTavolo[4 * n];
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / n;
                punti[i] = DaCameraATavolo(new PuntoImmagine(r.Left + r.Width * t, r.Top));
                punti[n + i] = DaCameraATavolo(new PuntoImmagine(r.Right, r.Top + r.Height * t));
                punti[2 * n + i] = DaCameraATavolo(new PuntoImmagine(r.Right - r.Width * t, r.Bottom));
                punti[3 * n + i] = DaCameraATavolo(new PuntoImmagine(r.Left, r.Bottom - r.Height * t));
            }
            return punti;
        }

        private Mat RaddrizzaCurva(Mat sorgente, int larghezza, int altezza)
        {
            MappaAnteprima mappa;
            lock (sincronizzazioneMappe)
            {
                if (mappaAnteprima == null || mappaAnteprima.Larghezza != larghezza || mappaAnteprima.Altezza != altezza)
                {
                    int numero = checked(larghezza * altezza);
                    var nuova = new MappaAnteprima { Larghezza = larghezza, Altezza = altezza, X = new float[numero], Y = new float[numero] };
                    for (int y = 0; y < altezza; y++)
                        for (int x = 0; x < larghezza; x++)
                        {
                            // Stessa convenzione pixel/coordinate del precedente WarpPerspective.
                            var p = DaTavoloACamera(new PuntoTavolo((double)x / larghezza, (double)y / altezza));
                            nuova.X[y * larghezza + x] = (float)p.X;
                            nuova.Y[y * larghezza + x] = (float)p.Y;
                        }
                    mappaAnteprima = nuova;
                }
                mappa = mappaAnteprima;
            }
            // Solo array gestiti nella cache, al massimo un formato. I Mat sono locali alla chiamata:
            // nessuna risorsa nativa persistente da perdere al cambio di calibrazione o durante il resize.
            using (var mx = new Mat(altezza, larghezza, MatType.CV_32FC1))
            using (var my = new Mat(altezza, larghezza, MatType.CV_32FC1))
            {
                Marshal.Copy(mappa.X, 0, mx.Data, mappa.X.Length);
                Marshal.Copy(mappa.Y, 0, my.Data, mappa.Y.Length);
                var uscita = new Mat();
                try { Cv2.Remap(sorgente, uscita, mx, my, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0)); return uscita; }
                catch { uscita.Dispose(); throw; }
            }
        }

        private Mat MascheraDellaCurva(int larghezza, int altezza, int restringi)
        {
            if (larghezza <= 0 || altezza <= 0 || restringi < 0) throw new ArgumentOutOfRangeException(nameof(larghezza));
            byte[] dati;
            lock (sincronizzazioneMappe)
            {
                if (mascheraCurva == null || larghezzaMaschera != larghezza || altezzaMaschera != altezza || restringimentoMaschera != restringi)
                {
                    const int n = 256;
                    var contorno = new Point[4 * n];
                    for (int i = 0; i < n; i++)
                    {
                        double t = (double)i / n;
                        PuntoTavolo[] p = { new PuntoTavolo(t, 0), new PuntoTavolo(1, t), new PuntoTavolo(1-t, 1), new PuntoTavolo(0, 1-t) };
                        for (int lato = 0; lato < 4; lato++)
                        {
                            var c = DaTavoloACamera(p[lato]);
                            contorno[lato * n + i] = new Point((int)Math.Round(c.X), (int)Math.Round(c.Y));
                        }
                    }
                    using (var m = new Mat(altezza, larghezza, MatType.CV_8UC1, Scalar.All(0)))
                    {
                        Cv2.FillPoly(m, new[] { contorno }, Scalar.All(255));
                        if (restringi > 0)
                            using (var nucleo = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2 * restringi + 1, 2 * restringi + 1)))
                                Cv2.Erode(m, m, nucleo);
                        var nuoviDati = new byte[checked(larghezza * altezza)];
                        Marshal.Copy(m.Data, nuoviDati, 0, nuoviDati.Length);
                        mascheraCurva = nuoviDati;
                    }
                    larghezzaMaschera = larghezza; altezzaMaschera = altezza; restringimentoMaschera = restringi;
                }
                dati = mascheraCurva;
            }
            var copia = new Mat(altezza, larghezza, MatType.CV_8UC1);
            Marshal.Copy(dati, 0, copia.Data, dati.Length);
            return copia;
        }

        private void SalvaCurva(string percorso)
        {
            var righe = new string[15];
            righe[0] = "VMBL-CAL3";
            righe[1] = string.Join(";", matrice.Select(Numero));
            righe[2] = Larghezza + ";" + Altezza;
            for (int i = 0; i < 12; i++)
                righe[i + 3] = string.Join(";", new[] { riferimentiCamera[i].X, riferimentiCamera[i].Y,
                    riferimentiTavolo[i].X, riferimentiTavolo[i].Y }.Select(Numero));
            File.WriteAllLines(percorso, righe, Encoding.UTF8);
        }

        private static string Numero(double v) => v.ToString("G17", CultureInfo.InvariantCulture);

        private void LeggiCurva(string[] righe)
        {
            var camera = new PuntoImmagine[12]; var tavolo = new PuntoTavolo[12];
            for (int i = 0; i < 12; i++)
            {
                string[] parti = righe[i + 3].Split(';');
                if (parti.Length != 4) throw new InvalidDataException("Riferimento CAL3 non valido.");
                var v = new double[4];
                for (int j = 0; j < 4; j++)
                    if (!double.TryParse(parti[j], NumberStyles.Float, CultureInfo.InvariantCulture, out v[j]) || !Finito(v[j]))
                        throw new InvalidDataException("Coordinate CAL3 non valide.");
                camera[i] = new PuntoImmagine(v[0], v[1]); tavolo[i] = new PuntoTavolo(v[2], v[3]);
            }
            if (Larghezza <= 0 || Altezza <= 0) throw new InvalidDataException("CAL3 richiede una risoluzione positiva.");
            try { AggiungiCurva(camera, tavolo); }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            { throw new InvalidDataException("Deformazione CAL3 non valida: " + e.Message, e); }
        }

        private static PuntoTavolo Proietta(double[] m, double x, double y)
        {
            double w = m[6] * x + m[7] * y + m[8];
            if (!Finito(x) || !Finito(y) || !Finito(w) || Math.Abs(w) < SogliaW)
                throw new InvalidOperationException("Il punto non ha un'immagine finita nella calibrazione.");
            double u = (m[0] * x + m[1] * y + m[2]) / w;
            double v = (m[3] * x + m[4] * y + m[5]) / w;
            if (!Finito(u) || !Finito(v)) throw new InvalidOperationException("Coordinate trasformate non finite.");
            return new PuntoTavolo(u, v);
        }

        private static double[] InvertiMatrice(double[] m)
        {
            double d = Determinante(m);
            return new[] { (m[4]*m[8]-m[5]*m[7])/d, (m[2]*m[7]-m[1]*m[8])/d, (m[1]*m[5]-m[2]*m[4])/d,
                (m[5]*m[6]-m[3]*m[8])/d, (m[0]*m[8]-m[2]*m[6])/d, (m[2]*m[3]-m[0]*m[5])/d,
                (m[3]*m[7]-m[4]*m[6])/d, (m[1]*m[6]-m[0]*m[7])/d, (m[0]*m[4]-m[1]*m[3])/d };
        }
    }
}
