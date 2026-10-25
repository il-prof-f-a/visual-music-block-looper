using System;
using System.Drawing;
using System.IO;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;
using Size = OpenCvSharp.Size;

namespace VisualMusicBlockLooper.Tests
{
    /// <summary>
    /// QR dei cartoncini ripresi male (piccoli, sfocati, bagliore del proiettore, poco contrasto):
    /// OpenCV da solo non li legge, la lettura di riserva sul ritaglio si'.
    /// </summary>
    public class LetturaQrDegradatoTests
    {
        private const int Larghezza = 1280;
        private const int Altezza = 720;

        private sealed class FonteMat : IFonteImmagini
        {
            private readonly Mat mat;

            public FonteMat(Mat mat)
            {
                this.mat = mat;
            }

            public Mat Frame() => mat.Clone();
        }

        private static Calibrazione CalibrazioneSuFrame()
        {
            return Calibrazione.Calcola(
                new[]
                {
                    new PuntoImmagine(0, 0), new PuntoImmagine(Larghezza, 0),
                    new PuntoImmagine(Larghezza, Altezza), new PuntoImmagine(0, Altezza)
                },
                new[] { new PuntoTavolo(0, 0), new PuntoTavolo(1, 0), new PuntoTavolo(1, 1), new PuntoTavolo(0, 1) });
        }

        // Frame sintetico: sfondo bianco, cartoncino ridotto di "scala", sfocato, con bagliore e contrasto ridotto.
        private static Mat Frame(double scala, double sfocatura, double contrasto, bool bagliore)
        {
            Mat mat;
            using (Bitmap carta = GeneratoreCartoncini.CartoncinoNota(0, 3, passi: 2))
            using (Bitmap tavolo = new Bitmap(Larghezza, Altezza))
            using (Graphics g = Graphics.FromImage(tavolo))
            {
                g.Clear(Color.White);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                int w = (int)(carta.Width * scala), h = (int)(carta.Height * scala);
                g.DrawImage(carta, 200, 150, w, h);
                using (MemoryStream flusso = new MemoryStream())
                {
                    tavolo.Save(flusso, System.Drawing.Imaging.ImageFormat.Png);
                    mat = Cv2.ImDecode(flusso.ToArray(), ImreadModes.Color);
                }
            }

            if (sfocatura > 0)
            {
                Cv2.GaussianBlur(mat, mat, new Size(), sfocatura);
            }

            if (bagliore)
            {
                using (Mat luce = new Mat(mat.Size(), MatType.CV_8UC3, Scalar.All(0)))
                {
                    Cv2.Circle(luce, new OpenCvSharp.Point(260, 190), 150, Scalar.All(255), -1);
                    Cv2.GaussianBlur(luce, luce, new Size(), 60);
                    Cv2.AddWeighted(mat, 0.75, luce, 0.25, 0, mat);
                }
            }

            mat.ConvertTo(mat, -1, contrasto, 255 * (1 - contrasto) * 0.4);
            return mat;
        }

        private static bool OpenCvLegge(Mat mat)
        {
            using (var rilevatore = new QRCodeDetector())
            {
                try
                {
                    return !string.IsNullOrEmpty(rilevatore.DetectAndDecode(mat, out Point2f[] _));
                }
                catch (OpenCVException)
                {
                    return false;
                }
            }
        }

        [TestCase(0.22, 0.7, 0.7, true)]
        [TestCase(0.22, 1.2, 0.7, true)]
        [TestCase(0.18, 0.7, 0.7, true)]
        [TestCase(0.15, 0.7, 0.7, true)]
        public void QrPiccoloESfocato_LettoDallaRiserva(double scala, double sfocatura, double contrasto, bool bagliore)
        {
            using (Mat mat = Frame(scala, sfocatura, contrasto, bagliore))
            {
                bool baseline = OpenCvLegge(mat);
                var lista = new RilevatoreCartoncini(new FonteMat(mat), CalibrazioneSuFrame()).Rileva();
                // Il caso ha senso solo se OpenCV da solo non legge: altrimenti il test non dimostra la riserva.
                Assume.That(baseline, Is.False, "OpenCV legge gia' il QR senza la riserva");
                Assert.That(lista, Has.Count.EqualTo(1));
                Assert.That(lista[0].Payload, Is.EqualTo("VMBL1;N;3"));
            }
        }
    }
}
