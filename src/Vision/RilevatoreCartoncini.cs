using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;
using ZXing;
using ZXing.Common;
using ZXing.Multi.QrCode;

namespace VisualMusicBlockLooper.Vision
{
    /// <summary>
    /// Rileva i cartoncini nel frame della camera.
    /// Modello fisico: il cartoncino e' un rettangolo con un codice QR (su un riquadro bianco con zona quieta)
    /// stampato sull'estremita' SINISTRA, anche a filo del bordo. Il QR contiene tutte le informazioni; il corpo
    /// del cartoncino (di qualsiasi colore, piu' scuro della carta/sfondo) serve solo a misurarne la larghezza,
    /// che codifica la durata della nota. Il colore non viene interpretato.
    /// Cartoncini adiacenti possono toccarsi (note consecutive, righe adiacenti): il contorno della maschera li
    /// fonde, quindi il rettangolo di ciascuno viene ritagliato con i riquadri QR dei vicini (a sinistra: bordo
    /// del proprio riquadro; a destra: bordo del riquadro del vicino; sopra/sotto: punto medio tra i due QR).
    /// Le estensioni sono misurate sulla maschera lungo la fascia del proprio QR (righe per la larghezza,
    /// colonne per l'altezza), cosi' vicini di dimensioni diverse non falsano la misura.
    /// La lettura del QR usa OpenCV; per i cartoncini il cui corpo e' visibile ma il QR non e' stato letto
    /// (piccolo, sfocato, sovraesposto) si ritenta sul ritaglio ingrandito e livellato con ZXing e OpenCV.
    /// </summary>
    public sealed class RilevatoreCartoncini : IRilevatoreCartoncini
    {
        private const double FattoreZonaQuieta = 0.2; // zona quieta ~4 moduli su 21 (frazione del lato del QR)
        private const int TolleranzaContatto = 3;
        private const int LatoNucleoChiusura = 7;
        private const int LatoNucleoApertura = 5;
        private const int LatoFondoLocale = 101;      // finestra per stimare il livello locale della carta
        private const double SogliaCorpo = 0.55;      // corpo = canale minimo sotto questa frazione del fondo locale
        private const int FondoMinimo = 80;           // sotto questo livello l'immagine e' troppo scura per il corpo
        private const int AreaMinimaCorpo = 400;      // pixel: scarta macchie piu' piccole di un cartoncino
        private const int VuotiTollerati = 8;
        private const double AltezzaTargetQr = 320;   // altezza in pixel a cui si ingrandisce il cartoncino per leggere il QR
        private const double AreaMassimaCorpo = 0.25; // frazione del frame: contorni piu' grandi non sono cartoncini
        private const int RestringiTavolo = 6;        // pixel: esclude la cornice dello schermo dal bordo dell'area calibrata

        private readonly IFonteImmagini fonte;
        private readonly Calibrazione calibrazione;

        // QR recuperati con la lettura di riserva nel frame precedente, per corpo: la lettura di riserva e'
        // costosa, quindi un cartoncino fermo non viene riletto a ogni frame.
        private List<KeyValuePair<Rect, QrTrovato>> recuperati = new List<KeyValuePair<Rect, QrTrovato>>();

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
                Mat maschera = null;
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
                    maschera = MascheraCorpo(bgr, calibrazione, out Rect[] corpi);
                    qr.AddRange(RecuperaQrMancanti(bgr, qr, corpi));
                    if (qr.Count == 0)
                    {
                        return risultato;
                    }

                    var candidati = new List<Candidato>();
                    foreach (QrTrovato q in qr)
                    {
                        try
                        {
                            Candidato c = Analizza(corpi, q);
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
                        CartoncinoRilevato r = Converti(c, candidati, maschera);
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
                    maschera?.Dispose();
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
            public Rect Riquadro;   // bounding box del quadrilatero QR
            public Rect Zona;       // riquadro QR + zona quieta
            public Rect? Corpo;     // bounding rect del corpo (eventualmente fuso con i vicini)
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

        // Per ogni corpo senza un QR letto vicino, ritenta la lettura sul ritaglio ingrandito e livellato.
        private List<QrTrovato> RecuperaQrMancanti(Mat bgr, List<QrTrovato> gia, Rect[] corpi)
        {
            var nuovi = new List<QrTrovato>();
            var attuali = new List<KeyValuePair<Rect, QrTrovato>>();
            foreach (Rect corpo in corpi)
            {
                // Il QR sta a un'estremita' del cartoncino (a sinistra, o a destra con camera capovolta):
                // si include un margine laterale pari all'altezza e uno verticale ridotto.
                int h = corpo.Height;
                int margineY = (int)Math.Round(h * 0.3);
                Rect regione = new Rect(corpo.X - h, corpo.Y - margineY, corpo.Width + 2 * h, corpo.Height + 2 * margineY)
                    & new Rect(0, 0, bgr.Cols, bgr.Rows);
                if (regione.Width < 8 || regione.Height < 8)
                {
                    continue;
                }

                if (gia.Concat(nuovi).Any(q => CentroDentro(q, regione)))
                {
                    continue;
                }

                // Stesso corpo (quasi fermo) del frame precedente: si riusa il QR gia' recuperato.
                KeyValuePair<Rect, QrTrovato>? memoria = recuperati
                    .Where(r => Sovrapposizione(r.Key, corpo) >= 0.7)
                    .Select(r => (KeyValuePair<Rect, QrTrovato>?)r)
                    .FirstOrDefault();
                List<QrTrovato> letti = memoria.HasValue
                    ? new List<QrTrovato> { memoria.Value.Value }
                    : LeggiRitaglio(bgr, regione);

                foreach (QrTrovato q in letti)
                {
                    if (!gia.Concat(nuovi).Any(e => e.Testo == q.Testo && Vicini(e, q)))
                    {
                        nuovi.Add(q);
                        attuali.Add(new KeyValuePair<Rect, QrTrovato>(corpo, q));
                    }
                }
            }

            recuperati = attuali;
            return nuovi;
        }

        // Intersezione su unione dei due rettangoli (0..1).
        private static double Sovrapposizione(Rect a, Rect b)
        {
            Rect inter = a & b;
            double area = inter.Width * inter.Height;
            double unione = a.Width * a.Height + b.Width * b.Height - area;
            return unione <= 0 ? 0 : area / unione;
        }

        private static bool CentroDentro(QrTrovato q, Rect regione)
        {
            float cx = q.Angoli.Average(p => p.X), cy = q.Angoli.Average(p => p.Y);
            return cx >= regione.Left && cx < regione.Right && cy >= regione.Top && cy < regione.Bottom;
        }

        private static bool Vicini(QrTrovato a, QrTrovato b)
        {
            float dx = a.Angoli.Average(p => p.X) - b.Angoli.Average(p => p.X);
            float dy = a.Angoli.Average(p => p.Y) - b.Angoli.Average(p => p.Y);
            double lato = Math.Max(BoundingBox(a.Angoli).Width, 1);
            return Math.Sqrt(dx * dx + dy * dy) < lato;
        }

        private static List<QrTrovato> LeggiRitaglio(Mat bgr, Rect regione)
        {
            var trovati = new List<QrTrovato>();
            double scala = Math.Max(2.0, Math.Min(6.0, AltezzaTargetQr / Math.Max(1.0, regione.Height / 1.6)));
            using (var ritaglio = new Mat(bgr, regione))
            using (var grigio = new Mat())
            using (var ingrandito = new Mat())
            {
                Cv2.CvtColor(ritaglio, grigio, ColorConversionCodes.BGR2GRAY);
                Cv2.Resize(grigio, ingrandito, new Size(), scala, scala, InterpolationFlags.Cubic);

                foreach (Mat variante in Varianti(ingrandito))
                {
                    using (variante)
                    {
                        foreach (QrTrovato q in DecodificaConZxing(variante).Concat(DecodificaConOpenCv(variante)))
                        {
                            Point2f[] angoli = q.Angoli
                                .Select(p => new Point2f((float)(p.X / scala + regione.X), (float)(p.Y / scala + regione.Y)))
                                .ToArray();
                            trovati.Add(new QrTrovato { Testo = q.Testo, Angoli = angoli });
                        }
                    }

                    if (trovati.Count > 0)
                    {
                        break;
                    }
                }
            }

            return trovati;
        }

        // Grigio, livellato (divide per lo sfondo sfocato: toglie bagliori e gradienti) e contrasto adattivo.
        private static IEnumerable<Mat> Varianti(Mat grigio)
        {
            yield return grigio.Clone();

            Mat livellato = new Mat();
            using (var sfondo = new Mat())
            {
                int sigma = Math.Max(5, grigio.Rows / 12);
                Cv2.GaussianBlur(grigio, sfondo, new Size(), sigma);
                Cv2.Divide(grigio, sfondo, livellato, 255);
            }

            yield return livellato;

            using (var clahe = Cv2.CreateCLAHE(2.0, new Size(8, 8)))
            {
                var contrasto = new Mat();
                clahe.Apply(grigio, contrasto);
                yield return contrasto;
            }
        }

        private static IEnumerable<QrTrovato> DecodificaConOpenCv(Mat grigio)
        {
            var trovati = new List<QrTrovato>();
            using (var rilevatore = new QRCodeDetector())
            {
                try
                {
                    string testo = rilevatore.DetectAndDecode(grigio, out Point2f[] angoli);
                    if (!string.IsNullOrEmpty(testo) && angoli != null && angoli.Length >= 4)
                    {
                        trovati.Add(new QrTrovato { Testo = testo, Angoli = angoli.Take(4).ToArray() });
                    }
                }
                catch (OpenCVException)
                {
                }
            }

            return trovati;
        }

        private static IEnumerable<QrTrovato> DecodificaConZxing(Mat grigio)
        {
            var trovati = new List<QrTrovato>();
            try
            {
                byte[] byteGrigi = new byte[grigio.Rows * grigio.Cols];
                System.Runtime.InteropServices.Marshal.Copy(grigio.Data, byteGrigi, 0, byteGrigi.Length);
                var luminanza = new RGBLuminanceSource(byteGrigi, grigio.Cols, grigio.Rows, RGBLuminanceSource.BitmapFormat.Gray8);
                var bitmap = new BinaryBitmap(new HybridBinarizer(luminanza));
                var suggerimenti = new Dictionary<DecodeHintType, object>
                {
                    { DecodeHintType.TRY_HARDER, true },
                    { DecodeHintType.POSSIBLE_FORMATS, new List<BarcodeFormat> { BarcodeFormat.QR_CODE } },
                };

                Result[] risultati = new QRCodeMultiReader().decodeMultiple(bitmap, suggerimenti);
                if (risultati != null)
                {
                    foreach (Result r in risultati)
                    {
                        Point2f[] angoli = AngoliDaPunti(r.ResultPoints);
                        if (!string.IsNullOrEmpty(r.Text) && angoli != null)
                        {
                            trovati.Add(new QrTrovato { Testo = r.Text, Angoli = angoli });
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Lettura non riuscita su questa variante: si passa alla successiva.
            }

            return trovati;
        }

        // ZXing restituisce i centri dei tre riquadri di posizione (basso-sinistra, alto-sinistra, alto-destra)
        // e, se presente, il quarto punto di allineamento: si ricostruiscono i quattro angoli del QR.
        private static Point2f[] AngoliDaPunti(ResultPoint[] punti)
        {
            if (punti == null || punti.Length < 3)
            {
                return null;
            }

            var bl = new Point2f(punti[0].X, punti[0].Y);
            var tl = new Point2f(punti[1].X, punti[1].Y);
            var tr = new Point2f(punti[2].X, punti[2].Y);
            double lunghezza = Distanza(tl, tr);
            double altezza = Distanza(tl, bl);
            if (lunghezza < 1 || altezza < 1)
            {
                return null;
            }

            double moduli = punti.Length >= 4 ? 18.0 : 14.0; // distanza tra i centri dei riquadri, in moduli
            double modulo = lunghezza / moduli;
            var u = new Point2f((float)((tr.X - tl.X) / lunghezza), (float)((tr.Y - tl.Y) / lunghezza));
            var v = new Point2f((float)((bl.X - tl.X) / altezza), (float)((bl.Y - tl.Y) / altezza));
            float m = (float)(3.5 * modulo);

            Point2f Angolo(Point2f p, float su, float sv)
            {
                return new Point2f(p.X + m * (su * u.X + sv * v.X), p.Y + m * (su * u.Y + sv * v.Y));
            }

            var br = new Point2f(tr.X + bl.X - tl.X, tr.Y + bl.Y - tl.Y);
            return new[] { Angolo(tl, -1, -1), Angolo(tr, 1, -1), Angolo(br, 1, 1), Angolo(bl, -1, 1) };
        }

        private static double Distanza(Point2f a, Point2f b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // Maschera del corpo: pixel il cui canale minimo e' molto sotto il livello locale della carta/sfondo
        // (qualsiasi colore pieno o grigio scuro; linee sottili e la griglia chiara sono escluse).
        private static Mat MascheraCorpo(Mat bgr, Calibrazione calibrazione, out Rect[] rettangoli)
        {
            var maschera = new Mat();
            try
            {
                Mat[] canali = Cv2.Split(bgr);
                using (var minimo = new Mat())
                using (var fondo = new Mat())
                using (var soglia = new Mat())
                using (var abbastanzaChiaro = new Mat())
                {
                    try
                    {
                        Cv2.Min(canali[0], canali[1], minimo);
                        Cv2.Min(minimo, canali[2], minimo);
                    }
                    finally
                    {
                        foreach (Mat c in canali)
                        {
                            c.Dispose();
                        }
                    }

                    using (var finestra = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(LatoFondoLocale, LatoFondoLocale)))
                    {
                        Cv2.Dilate(minimo, fondo, finestra);
                    }

                    Cv2.Multiply(fondo, Scalar.All(SogliaCorpo), soglia);
                    Cv2.Compare(minimo, soglia, maschera, CmpType.LT);
                    Cv2.Threshold(fondo, abbastanzaChiaro, FondoMinimo, 255, ThresholdTypes.Binary);
                    Cv2.BitwiseAnd(maschera, abbastanzaChiaro, maschera);
                }

                // Fuori dall'area calibrata (cornice dello schermo, mani, tavolo) non ci sono cartoncini.
                using (Mat tavolo = calibrazione.MascheraTavolo(bgr.Cols, bgr.Rows, RestringiTavolo))
                {
                    Cv2.BitwiseAnd(maschera, tavolo, maschera);
                }

                using (var apertura = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(LatoNucleoApertura, LatoNucleoApertura)))
                using (var chiusura = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(LatoNucleoChiusura, LatoNucleoChiusura)))
                {
                    Cv2.MorphologyEx(maschera, maschera, MorphTypes.Open, apertura);
                    Cv2.MorphologyEx(maschera, maschera, MorphTypes.Close, chiusura);
                }

                // FindContours puo' modificare l'immagine in ingresso: lavora su una copia.
                using (Mat copia = maschera.Clone())
                {
                    Cv2.FindContours(copia, out Point[][] contorni, out HierarchyIndex[] _,
                        RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                    rettangoli = contorni
                        .Select(Cv2.BoundingRect)
                        .Where(r => r.Width * r.Height >= AreaMinimaCorpo
                            && r.Width * r.Height <= AreaMassimaCorpo * bgr.Cols * bgr.Rows)
                        .ToArray();
                }

                return maschera;
            }
            catch
            {
                maschera.Dispose();
                throw;
            }
        }

        // Associa a un QR il corpo con maggiore sovrapposizione con la zona del QR (contatto incluso).
        private static Candidato Analizza(Rect[] corpi, QrTrovato qr)
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

            var c = new Candidato { Testo = qr.Testo, Riquadro = riquadro, Zona = zona };
            Rect migliore = default(Rect);
            int areaMigliore = 0;
            foreach (Rect r in corpi)
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
                c.Corpo = migliore;
            }

            return c;
        }

        private CartoncinoRilevato Converti(Candidato c, List<Candidato> tutti, Mat maschera)
        {
            Rect rettangolo = c.Corpo.HasValue ? Ritaglia(c, tutti, maschera) : c.Riquadro;

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
                    Area = new RettangoloTavolo(minX, minY, maxX - minX, maxY - minY),
                };
            }
            catch (Exception)
            {
                // Conversione non possibile (es. punto sul piano all'infinito): cartoncino scartato.
                return null;
            }
        }

        // Unisce il corpo con la zona del QR e lo ritaglia con i vicini.
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
                if (ReferenceEquals(n, c))
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
    }
}
