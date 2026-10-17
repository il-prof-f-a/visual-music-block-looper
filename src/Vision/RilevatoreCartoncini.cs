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
    /// con un codice QR stampato sull'estremita' SINISTRA. La larghezza del cartoncino (in unita' tavolo) codifica
    /// la durata, quindi viene misurata dal corpo colorato e non dal solo QR.
    /// </summary>
    public sealed class RilevatoreCartoncini : IRilevatoreCartoncini
    {
        private const double ScalaAnello = 1.7;
        private const int PuntiPerLato = 6;
        private const int LatoNucleoChiusura = 7;

        private readonly IFonteImmagini fonte;
        private readonly Calibrazione calibrazione;

        public RilevatoreCartoncini(IFonteImmagini fonte, Calibrazione calibrazione)
        {
            this.fonte = fonte;
            this.calibrazione = calibrazione ?? throw new ArgumentNullException(nameof(calibrazione));
        }

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

                using (Mat bgr = ComeBgr(frame))
                {
                    if (bgr == null)
                    {
                        return risultato;
                    }

                    using (Mat hsv = new Mat())
                    {
                        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);
                        foreach (QrTrovato qr in DecodificaQr(bgr))
                        {
                            try
                            {
                                CartoncinoRilevato c = Costruisci(bgr, hsv, qr);
                                if (c != null)
                                {
                                    risultato.Add(c);
                                }
                            }
                            catch (OpenCVException)
                            {
                                // QR non elaborabile: ignorato.
                            }
                        }
                    }
                }
            }

            return risultato;
        }

        private static Mat ComeBgr(Mat frame)
        {
            var m = new Mat();
            switch (frame.Channels())
            {
                case 3:
                    frame.CopyTo(m);
                    break;
                case 4:
                    Cv2.CvtColor(frame, m, ColorConversionCodes.BGRA2BGR);
                    break;
                case 1:
                    Cv2.CvtColor(frame, m, ColorConversionCodes.GRAY2BGR);
                    break;
                default:
                    m.Dispose();
                    return null;
            }

            return m;
        }

        private sealed class QrTrovato
        {
            public string Testo;
            public Point2f[] Angoli;
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

        private CartoncinoRilevato Costruisci(Mat bgr, Mat hsv, QrTrovato qr)
        {
            Point2f[] a = qr.Angoli;
            if (a.Any(p => float.IsNaN(p.X) || float.IsNaN(p.Y)))
            {
                return null;
            }

            ColoreTraccia? colore = CampionaColore(bgr, a);
            Rect rettangolo = BoundingBox(a);
            if (colore.HasValue)
            {
                Rect? corpo = TrovaCorpo(hsv, colore.Value, CentroQr(a));
                if (corpo.HasValue)
                {
                    rettangolo = corpo.Value;
                }
                else
                {
                    colore = null;
                }
            }

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
                Payload = qr.Testo,
                Colore = colore,
                Area = new RettangoloTavolo(minX, minY, maxX - minX, maxY - minY),
            };
        }

        private static Point2f CentroQr(Point2f[] a)
        {
            return new Point2f(a.Average(p => p.X), a.Average(p => p.Y));
        }

        private static Rect BoundingBox(Point2f[] a)
        {
            int x0 = (int)Math.Floor(a.Min(p => p.X)), x1 = (int)Math.Ceiling(a.Max(p => p.X));
            int y0 = (int)Math.Floor(a.Min(p => p.Y)), y1 = (int)Math.Ceiling(a.Max(p => p.Y));
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }

        // Campiona un anello di punti appena fuori dal quadrilatero del QR e sceglie il colore di maggioranza
        // tra quelli classificabili (lo sfondo bianco/grigio non vota).
        private static ColoreTraccia? CampionaColore(Mat bgr, Point2f[] a)
        {
            Point2f c = CentroQr(a);
            var voti = new int[6];
            for (int lato = 0; lato < 4; lato++)
            {
                Point2f p0 = a[lato], p1 = a[(lato + 1) % 4];
                for (int i = 0; i < PuntiPerLato; i++)
                {
                    float t = (i + 0.5f) / PuntiPerLato;
                    float x = p0.X + (p1.X - p0.X) * t;
                    float y = p0.Y + (p1.Y - p0.Y) * t;
                    int px = (int)Math.Round(c.X + (x - c.X) * ScalaAnello);
                    int py = (int)Math.Round(c.Y + (y - c.Y) * ScalaAnello);
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

            int migliore = 0;
            for (int i = 1; i < voti.Length; i++)
            {
                if (voti[i] > voti[migliore])
                {
                    migliore = i;
                }
            }

            return voti[migliore] > 0 ? (ColoreTraccia?)(ColoreTraccia)migliore : null;
        }

        private static Rect? TrovaCorpo(Mat hsv, ColoreTraccia colore, Point2f centro)
        {
            using (Mat maschera = MascheraColore(hsv, colore))
            using (Mat nucleo = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(LatoNucleoChiusura, LatoNucleoChiusura)))
            {
                Cv2.MorphologyEx(maschera, maschera, MorphTypes.Close, nucleo);
                Cv2.FindContours(maschera, out Point[][] contorni, out HierarchyIndex[] _,
                    RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                foreach (Point[] contorno in contorni)
                {
                    if (Cv2.PointPolygonTest(contorno, centro, false) >= 0)
                    {
                        return Cv2.BoundingRect(contorno);
                    }
                }
            }

            return null;
        }

        private static Mat MascheraColore(Mat hsv, ColoreTraccia colore)
        {
            int sMin = (int)Math.Round(ClassificatoreColore.SaturazioneMinima * 255);
            int vMin = (int)Math.Round(ClassificatoreColore.ValoreMinimo * 255);
            double[] gradi = ClassificatoreColore.IntervalliGradi(colore);
            var maschera = new Mat(hsv.Size(), MatType.CV_8UC1, Scalar.All(0));
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

            return maschera;
        }
    }
}
