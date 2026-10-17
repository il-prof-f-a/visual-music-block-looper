using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Vision
{
    /// <summary>
    /// Rileva i cartoncini nel frame della camera.
    /// Modello fisico: il cartoncino e' un rettangolo il cui corpo e' di un colore pieno che identifica la traccia,
    /// con un codice QR (su un riquadro bianco con zona quieta) stampato sull'estremita' SINISTRA, anche a filo del bordo.
    /// La larghezza del cartoncino (in unita' tavolo) codifica la durata, quindi viene misurata dal corpo colorato.
    /// Cartoncini dello stesso colore possono toccarsi (note consecutive, righe adiacenti): il contorno della
    /// maschera li fonde, quindi il rettangolo di ciascuno viene ritagliato con i riquadri QR dei vicini dello
    /// stesso colore (a sinistra: bordo del proprio riquadro; a destra: bordo del riquadro del vicino;
    /// sopra/sotto: punto medio tra i due QR). Le estensioni sono misurate sulla maschera lungo la fascia del
    /// proprio QR (righe per la larghezza, colonne per l'altezza), cosi' vicini di dimensioni diverse non
    /// falsano la misura.
    /// </summary>
    public sealed class RilevatoreCartoncini : IRilevatoreCartoncini
    {
        private const double FattoreZonaQuieta = 0.2; // zona quieta ~4 moduli su 21 (frazione del lato del QR)
        private const int TolleranzaContatto = 3;
        private const int PuntiPerLato = 6;
        private const double ScalaAnello = 1.7;
        private const int VotiMinimi = 2;
        private const int LatoNucleoChiusura = 7;
        private const int VuotiTollerati = 8;

        private readonly IFonteImmagini fonte;
        private readonly Calibrazione calibrazione;

        public RilevatoreCartoncini(IFonteImmagini fonte, Calibrazione calibrazione)
        {
            this.fonte = fonte;
            this.calibrazione = calibrazione ?? throw new ArgumentNullException(nameof(calibrazione));
        }

        /// <summary>Calibrazione con cui il rilevatore converte la camera nelle coordinate del tavolo.</summary>
        public Calibrazione Calibrazione => calibrazione;

        public IReadOnlyList<CartoncinoRilevato> Rileva()
        {
            var risultato = new List<CartoncinoRilevato>();
            if (fonte == null)
            {
                return risultato;
            }

            using (Mat frame = fonte.Frame())
            {
                if (frame == null || frame.Empty())
                {
                    return risultato;
                }

                Mat convertito = null;
                Mat hsv = null;
                CacheCorpi corpi = null;
                try
                {
                    Mat bgr = frame;
                    if (frame.Channels() != 3)
                    {
                        convertito = new Mat();
                        if (frame.Channels() == 4)
                        {
                            Cv2.CvtColor(frame, convertito, ColorConversionCodes.BGRA2BGR);
                        }
                        else if (frame.Channels() == 1)
                        {
                            Cv2.CvtColor(frame, convertito, ColorConversionCodes.GRAY2BGR);
                        }
                        else
                        {
                            return risultato;
                        }

                        bgr = convertito;
                    }

                    List<QrTrovato> qr = DecodificaQr(bgr);
                    if (qr.Count == 0)
                    {
                        return risultato;
                    }

                    hsv = new Mat();
                    Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);
                    corpi = new CacheCorpi(hsv);
                    var candidati = new List<Candidato>();
                    foreach (QrTrovato q in qr)
                    {
                        try
                        {
                            Candidato c = Analizza(bgr, corpi, q);
                            if (c != null)
                            {
                                candidati.Add(c);
                            }
                        }
                        catch (OpenCVException)
                        {
                            // QR non elaborabile: ignorato.
                        }
                    }

                    foreach (Candidato c in candidati)
                    {
                        CartoncinoRilevato r = Converti(c, candidati, corpi);
                        if (r != null)
                        {
                            risultato.Add(r);
                        }
                    }
                }
                catch (OpenCVException)
                {
                    // Frame non elaborabile: nessun cartoncino.
                    risultato.Clear();
                }
                finally
                {
                    corpi?.Dispose();
                    hsv?.Dispose();
                    convertito?.Dispose();
                }
            }

            return risultato;
        }

        private sealed class QrTrovato
        {
            public string Testo;
            public Point2f[] Angoli;
        }

        private sealed class Candidato
        {
            public string Testo;
            public ColoreTraccia? Colore;
            public Rect Riquadro;   // bounding box del quadrilatero QR
            public Rect Zona;       // riquadro QR + zona quieta
            public Rect? Corpo;     // bounding rect del contorno colorato (eventualmente fuso con i vicini)
        }

        // Rettangoli dei contorni esterni di ogni colore, calcolati una sola volta per frame e solo su richiesta.
        private sealed class CacheCorpi : IDisposable
        {
            private readonly Mat hsv;
            private readonly Dictionary<ColoreTraccia, Rect[]> cache = new Dictionary<ColoreTraccia, Rect[]>();
            private readonly Dictionary<ColoreTraccia, Mat> maschere = new Dictionary<ColoreTraccia, Mat>();

            public CacheCorpi(Mat hsv)
            {
                this.hsv = hsv;
            }

            public Rect[] Per(ColoreTraccia colore)
            {
                if (!cache.TryGetValue(colore, out Rect[] rettangoli))
                {
                    Mat maschera = Calcola(hsv, colore, out rettangoli);
                    maschere[colore] = maschera;
                    cache[colore] = rettangoli;
                }

                return rettangoli;
            }

            public Mat Maschera(ColoreTraccia colore)
            {
                Per(colore);
                return maschere[colore];
            }

            public void Dispose()
            {
                foreach (Mat m in maschere.Values)
                {
                    m.Dispose();
                }

                maschere.Clear();
            }
        }

        private static List<QrTrovato> DecodificaQr(Mat bgr)
        {
            var trovati = new List<QrTrovato>();
            using (var rilevatore = new QRCodeDetector())
            {
                try
                {
                    // OpenCvSharp 4.9 non espone DetectAndDecodeMulti: si usano DetectMulti + DecodeMulti.
                    if (rilevatore.DetectMulti(bgr, out Point2f[] punti)
                        && punti != null
                        && rilevatore.DecodeMulti(bgr, punti, out string[] testi)
                        && testi != null)
                    {
                        for (int i = 0; i < testi.Length && (i + 1) * 4 <= punti.Length; i++)
                        {
                            if (!string.IsNullOrEmpty(testi[i]))
                            {
                                trovati.Add(new QrTrovato { Testo = testi[i], Angoli = punti.Skip(i * 4).Take(4).ToArray() });
                            }
                        }
                    }
                }
                catch (OpenCVException)
                {
                }

                if (trovati.Count == 0)
                {
                    try
                    {
                        string testo = rilevatore.DetectAndDecode(bgr, out Point2f[] angoli);
                        if (!string.IsNullOrEmpty(testo) && angoli != null && angoli.Length >= 4)
                        {
                            trovati.Add(new QrTrovato { Testo = testo, Angoli = angoli.Take(4).ToArray() });
                        }
                    }
                    catch (OpenCVException)
                    {
                    }
                }
            }

            return trovati;
        }

        // Determina colore e corpo colorato associati a un QR.
        private static Candidato Analizza(Mat bgr, CacheCorpi corpi, QrTrovato qr)
        {
            Point2f[] a = qr.Angoli;
            if (a.Any(p => float.IsNaN(p.X) || float.IsNaN(p.Y) || float.IsInfinity(p.X) || float.IsInfinity(p.Y)))
            {
                return null;
            }

            Rect riquadro = BoundingBox(a);
            int margine = (int)Math.Round(Math.Max(riquadro.Width, riquadro.Height) * FattoreZonaQuieta);
            Rect zona = Gonfia(riquadro, margine);
            Rect zonaContatto = Gonfia(zona, TolleranzaContatto);

            // Per ogni colore: contorno di maggiore sovrapposizione con la zona del QR (contatto incluso).
            var trovati = new List<KeyValuePair<ColoreTraccia, Rect>>();
            double[] punteggio = new double[6];
            for (int i = 0; i < 6; i++)
            {
                var colore = (ColoreTraccia)i;
                Rect migliore = default(Rect);
                int areaMigliore = 0;
                foreach (Rect r in corpi.Per(colore))
                {
                    Rect inter = r & zonaContatto;
                    int area = inter.Width * inter.Height;
                    if (inter.Width > 0 && inter.Height > 0 && area > areaMigliore)
                    {
                        areaMigliore = area;
                        migliore = r;
                    }
                }

                if (areaMigliore > 0)
                {
                    trovati.Add(new KeyValuePair<ColoreTraccia, Rect>(colore, migliore));
                    punteggio[i] = areaMigliore;
                }
            }

            var c = new Candidato { Testo = qr.Testo, Riquadro = riquadro, Zona = zona };
            if (trovati.Count == 0)
            {
                return c;
            }

            KeyValuePair<ColoreTraccia, Rect> scelto = trovati.OrderByDescending(t => punteggio[(int)t.Key]).First();
            if (trovati.Count > 1)
            {
                // Piu' colori a contatto: il voto sull'anello e' l'arbitro se raggiunge il quorum.
                int[] voti = VotiAnello(bgr, a);
                KeyValuePair<ColoreTraccia, Rect> votato = trovati.OrderByDescending(t => voti[(int)t.Key]).First();
                if (voti[(int)votato.Key] >= VotiMinimi)
                {
                    scelto = votato;
                }
            }

            c.Colore = scelto.Key;
            c.Corpo = scelto.Value;
            return c;
        }

        private CartoncinoRilevato Converti(Candidato c, List<Candidato> tutti, CacheCorpi corpi)
        {
            Rect rettangolo = c.Zona;
            ColoreTraccia? colore = c.Colore;
            if (c.Corpo.HasValue)
            {
                rettangolo = Ritaglia(c, tutti, corpi.Maschera(c.Colore.Value));
            }
            else
            {
                colore = null;
                rettangolo = c.Riquadro;
            }

            try
            {
                PuntoTavolo[] punti = new[]
                {
                    new PuntoImmagine(rettangolo.Left, rettangolo.Top),
                    new PuntoImmagine(rettangolo.Right, rettangolo.Top),
                    new PuntoImmagine(rettangolo.Right, rettangolo.Bottom),
                    new PuntoImmagine(rettangolo.Left, rettangolo.Bottom),
                }.Select(p => calibrazione.DaCameraATavolo(p)).ToArray();

                double minX = punti.Min(p => p.X), maxX = punti.Max(p => p.X);
                double minY = punti.Min(p => p.Y), maxY = punti.Max(p => p.Y);
                return new CartoncinoRilevato
                {
                    Payload = c.Testo,
                    Colore = colore,
                    Area = new RettangoloTavolo(minX, minY, maxX - minX, maxY - minY),
                };
            }
            catch (Exception)
            {
                // Conversione non possibile (es. punto sul piano all'infinito): cartoncino scartato.
                return null;
            }
        }

        // Unisce il contorno con la zona del QR e lo ritaglia con i vicini dello stesso colore.
        private static Rect Ritaglia(Candidato c, List<Candidato> tutti, Mat maschera)
        {
            // Estensione lungo la fascia del QR: righe del riquadro per sinistra/destra, colonne per alto/basso.
            // La zona del QR (riquadro bianco) conta come piena anche se fuori maschera.
            Rect z = c.Zona;
            int sinistra = z.Left, destra = z.Right, alto = z.Top, basso = z.Bottom;
            sinistra = Espandi(sinistra, -1, 0, maschera.Cols, x => ColonnaPiena(maschera, x, c.Riquadro.Top, c.Riquadro.Bottom));
            destra = Espandi(destra - 1, +1, 0, maschera.Cols, x => ColonnaPiena(maschera, x, c.Riquadro.Top, c.Riquadro.Bottom)) + 1;
            alto = Espandi(alto, -1, 0, maschera.Rows, y => RigaPiena(maschera, y, c.Riquadro.Left, c.Riquadro.Right));
            basso = Espandi(basso - 1, +1, 0, maschera.Rows, y => RigaPiena(maschera, y, c.Riquadro.Left, c.Riquadro.Right)) + 1;

            foreach (Candidato n in tutti)
            {
                if (ReferenceEquals(n, c) || !n.Colore.HasValue || n.Colore != c.Colore)
                {
                    continue;
                }

                bool sovrappostoY = Sovrappone(n.Riquadro.Top, n.Riquadro.Bottom, c.Riquadro.Top, c.Riquadro.Bottom);
                bool sovrappostoX = Sovrappone(n.Riquadro.Left, n.Riquadro.Right, c.Riquadro.Left, c.Riquadro.Right);
                if (sovrappostoY && n.Riquadro.Left > c.Riquadro.Left)
                {
                    destra = Math.Min(destra, n.Zona.Left);
                }
                else if (sovrappostoY && n.Riquadro.Left < c.Riquadro.Left)
                {
                    sinistra = Math.Max(sinistra, c.Zona.Left);
                }
                else if (sovrappostoX && n.Riquadro.Top < c.Riquadro.Top)
                {
                    alto = Math.Max(alto, (n.Riquadro.Bottom + c.Riquadro.Top) / 2);
                }
                else if (sovrappostoX && n.Riquadro.Top > c.Riquadro.Top)
                {
                    basso = Math.Min(basso, (c.Riquadro.Bottom + n.Riquadro.Top) / 2);
                }
            }

            return new Rect(sinistra, alto, Math.Max(1, destra - sinistra), Math.Max(1, basso - alto));
        }

        // Parte da "inizio" e avanza nel verso dato finche' trova posizioni piene; tollera piccoli vuoti
        // (imprecisione del quadrilatero QR rispetto al riquadro bianco). Restituisce l'ultima posizione piena.
        private static int Espandi(int inizio, int verso, int minimo, int massimoEscluso, Func<int, bool> piena)
        {
            int ultimo = inizio;
            int vuoti = 0;
            for (int p = inizio + verso; p >= minimo && p < massimoEscluso && vuoti <= VuotiTollerati; p += verso)
            {
                if (piena(p))
                {
                    ultimo = p;
                    vuoti = 0;
                }
                else
                {
                    vuoti++;
                }
            }

            return ultimo;
        }

        private static bool ColonnaPiena(Mat m, int x, int y0, int y1)
        {
            for (int y = Math.Max(0, y0); y < Math.Min(m.Rows, y1); y++)
            {
                if (m.At<byte>(y, x) != 0) return true;
            }

            return false;
        }

        private static bool RigaPiena(Mat m, int y, int x0, int x1)
        {
            for (int x = Math.Max(0, x0); x < Math.Min(m.Cols, x1); x++)
            {
                if (m.At<byte>(y, x) != 0) return true;
            }

            return false;
        }

        private static bool Sovrappone(int a0, int a1, int b0, int b1)
        {
            return a0 < b1 && b0 < a1;
        }

        private static Rect Gonfia(Rect r, int m)
        {
            return new Rect(r.X - m, r.Y - m, r.Width + 2 * m, r.Height + 2 * m);
        }

        private static Rect BoundingBox(Point2f[] a)
        {
            int x0 = (int)Math.Floor(a.Min(p => p.X)), x1 = (int)Math.Ceiling(a.Max(p => p.X));
            int y0 = (int)Math.Floor(a.Min(p => p.Y)), y1 = (int)Math.Ceiling(a.Max(p => p.Y));
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }

        // Voti dei punti di un anello appena fuori dal quadrilatero (lo sfondo neutro non vota).
        private static int[] VotiAnello(Mat bgr, Point2f[] a)
        {
            var centro = new Point2f(a.Average(p => p.X), a.Average(p => p.Y));
            var voti = new int[6];
            for (int lato = 0; lato < 4; lato++)
            {
                Point2f p0 = a[lato], p1 = a[(lato + 1) % 4];
                for (int i = 0; i < PuntiPerLato; i++)
                {
                    float t = (i + 0.5f) / PuntiPerLato;
                    float x = p0.X + (p1.X - p0.X) * t;
                    float y = p0.Y + (p1.Y - p0.Y) * t;
                    int px = (int)Math.Round(centro.X + (x - centro.X) * ScalaAnello);
                    int py = (int)Math.Round(centro.Y + (y - centro.Y) * ScalaAnello);
                    if (px < 0 || py < 0 || px >= bgr.Cols || py >= bgr.Rows)
                    {
                        continue;
                    }

                    Vec3b v = bgr.At<Vec3b>(py, px);
                    ColoreTraccia? k = ClassificatoreColore.Classifica(v.Item2, v.Item1, v.Item0);
                    if (k.HasValue)
                    {
                        voti[(int)k.Value]++;
                    }
                }
            }

            return voti;
        }

        // Maschera chiusa del colore (restituita al chiamante, che la rilascia) e rettangoli dei suoi contorni esterni.
        private static Mat Calcola(Mat hsv, ColoreTraccia colore, out Rect[] rettangoli)
        {
            Mat maschera = MascheraColore(hsv, colore);
            try
            {
                using (Mat nucleo = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(LatoNucleoChiusura, LatoNucleoChiusura)))
                {
                    Cv2.MorphologyEx(maschera, maschera, MorphTypes.Close, nucleo);
                }

                // FindContours puo' modificare l'immagine in ingresso: lavora su una copia.
                using (Mat copia = maschera.Clone())
                {
                    Cv2.FindContours(copia, out Point[][] contorni, out HierarchyIndex[] _,
                        RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                    rettangoli = contorni.Select(Cv2.BoundingRect).ToArray();
                }

                return maschera;
            }
            catch
            {
                maschera.Dispose();
                throw;
            }
        }

        private static Mat MascheraColore(Mat hsv, ColoreTraccia colore)
        {
            int sMin = (int)Math.Round(ClassificatoreColore.SaturazioneMinima * 255);
            int vMin = (int)Math.Round(ClassificatoreColore.ValoreMinimo * 255);
            double[] gradi = ClassificatoreColore.IntervalliGradi(colore);
            var maschera = new Mat(hsv.Size(), MatType.CV_8UC1, Scalar.All(0));
            try
            {
                for (int k = 0; k < gradi.Length; k += 2)
                {
                    // OpenCV usa H in 0..179 (gradi / 2); l'estremo superiore e' escluso.
                    int da = (int)Math.Ceiling(gradi[k] / 2.0);
                    int a = (int)Math.Ceiling(gradi[k + 1] / 2.0) - 1;
                    if (a < da)
                    {
                        continue;
                    }

                    using (var parziale = new Mat())
                    {
                        Cv2.InRange(hsv, new Scalar(da, sMin, vMin), new Scalar(a, 255, 255), parziale);
                        Cv2.BitwiseOr(maschera, parziale, maschera);
                    }
                }
            }
            catch
            {
                maschera.Dispose();
                throw;
            }

            return maschera;
        }
    }
}
