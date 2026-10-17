using System.IO;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class VisionTests
    {
        private sealed class FonteMat : IFonteImmagini
        {
            private readonly Mat mat;

            public FonteMat(Mat mat)
            {
                this.mat = mat;
            }

            public Mat Frame()
            {
                return mat?.Clone();
            }
        }

        private static Calibrazione CalibrazioneIdentita()
        {
            return Calibrazione.Calcola(
                new[] { new PuntoImmagine(0, 0), new PuntoImmagine(640, 0), new PuntoImmagine(640, 480), new PuntoImmagine(0, 480) },
                new[] { new PuntoTavolo(0, 0), new PuntoTavolo(1, 0), new PuntoTavolo(1, 1), new PuntoTavolo(0, 1) });
        }

        private static string Asset(string nome)
        {
            return Path.Combine(TestContext.CurrentContext.TestDirectory, "prova", nome);
        }

        [Test]
        public void Classifica_SeiColoriPuri()
        {
            Assert.AreEqual(ColoreTraccia.Rosso, ClassificatoreColore.Classifica(255, 0, 0));
            Assert.AreEqual(ColoreTraccia.Arancione, ClassificatoreColore.Classifica(255, 128, 0));
            Assert.AreEqual(ColoreTraccia.Giallo, ClassificatoreColore.Classifica(255, 255, 0));
            Assert.AreEqual(ColoreTraccia.Verde, ClassificatoreColore.Classifica(0, 200, 0));
            Assert.AreEqual(ColoreTraccia.Blu, ClassificatoreColore.Classifica(0, 0, 255));
            Assert.AreEqual(ColoreTraccia.Viola, ClassificatoreColore.Classifica(160, 0, 200));
        }

        [Test]
        public void Classifica_GrigioNull()
        {
            Assert.IsNull(ClassificatoreColore.Classifica(128, 128, 128));
        }

        [Test]
        public void Classifica_NeroNull()
        {
            Assert.IsNull(ClassificatoreColore.Classifica(0, 0, 0));
        }

        [Test]
        public void QrDetectorDecodificaQrDiProva()
        {
            using (Mat img = Cv2.ImRead(Asset("qr-test.png")))
            using (var det = new QRCodeDetector())
            {
                Assert.IsFalse(img.Empty());
                Assert.AreEqual("VMBL1;N;12", det.DetectAndDecode(img, out Point2f[] _));
            }
        }

        [Test]
        public void Rileva_ImmagineConDueCartoncini()
        {
            using (Mat img = Cv2.ImRead(Asset("due-cartoncini.png")))
            {
                Assert.IsFalse(img.Empty());
                var rilevatore = new RilevatoreCartoncini(new FonteMat(img), CalibrazioneIdentita());
                var lista = rilevatore.Rileva();
                Assert.AreEqual(2, lista.Count);

                CartoncinoRilevato rosso = null, verde = null;
                foreach (CartoncinoRilevato c in lista)
                {
                    if (c.Payload == "VMBL1;N;12") rosso = c;
                    if (c.Payload == "VMBL1;N;14") verde = c;
                }

                Assert.IsNotNull(rosso);
                Assert.IsNotNull(verde);
                Assert.AreEqual(ColoreTraccia.Rosso, rosso.Colore);
                Assert.AreEqual(ColoreTraccia.Verde, verde.Colore);
                Assert.AreEqual(40 / 640.0, rosso.Area.X, 0.02);
                Assert.AreEqual(160 / 640.0, rosso.Area.Larghezza, 0.02);
                Assert.AreEqual(260 / 640.0, verde.Area.X, 0.02);
                Assert.AreEqual(340 / 640.0, verde.Area.Larghezza, 0.02);
                Assert.Greater(verde.Area.Larghezza, rosso.Area.Larghezza);
            }
        }

        [Test]
        public void Rileva_ImmagineVuota_ListaVuota()
        {
            using (var bianco = new Mat(new Size(640, 480), MatType.CV_8UC3, Scalar.All(255)))
            {
                var rilevatore = new RilevatoreCartoncini(new FonteMat(bianco), CalibrazioneIdentita());
                Assert.IsEmpty(rilevatore.Rileva());
            }
        }

        [Test]
        public void Rileva_FonteNull_ListaVuota()
        {
            Assert.IsEmpty(new RilevatoreCartoncini(null, CalibrazioneIdentita()).Rileva());
            Assert.IsEmpty(new RilevatoreCartoncini(new FonteMat(null), CalibrazioneIdentita()).Rileva());
        }

        [Test]
        public void Rileva_QrIllegibile_NonLanciaEccezioni()
        {
            using (Mat qr = GeneratoreImmagini.QrSingolo("VMBL1;N;12"))
            using (var sfocato = new Mat())
            {
                Cv2.GaussianBlur(qr, sfocato, new Size(0, 0), 6);
                using (var rumore = new Mat(sfocato.Size(), sfocato.Type()))
                {
                    Cv2.Randn(rumore, Scalar.All(0), Scalar.All(60));
                    Cv2.Add(sfocato, rumore, sfocato);
                }

                var rilevatore = new RilevatoreCartoncini(new FonteMat(sfocato), CalibrazioneIdentita());
                Assert.DoesNotThrow(() => rilevatore.Rileva());
            }
        }

        // Rigenera le immagini committate in assets/prova (eseguire a mano con --filter Name=GeneraAsset).
        [Test, Explicit("Genera gli asset di prova")]
        public void GeneraAsset()
        {
            string dir = GeneratoreImmagini.CartellaAsset();
            Assert.IsNotNull(dir);
            using (Mat qr = GeneratoreImmagini.QrSingolo("VMBL1;N;12"))
            using (Mat due = GeneratoreImmagini.DueCartoncini())
            {
                Cv2.ImWrite(Path.Combine(dir, "qr-test.png"), qr);
                Cv2.ImWrite(Path.Combine(dir, "due-cartoncini.png"), due);
            }
        }
    }
}
