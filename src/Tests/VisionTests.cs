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
        public void Rileva_CartonciniAdiacentiStessoColore_ConfiniSeparati()
        {
            using (Mat img = Cv2.ImRead(Asset("cartoncini-adiacenti.png")))
            {
                Assert.IsFalse(img.Empty());
                var lista = new RilevatoreCartoncini(new FonteMat(img), CalibrazioneIdentita()).Rileva();
                Assert.AreEqual(4, lista.Count);

                // (payload, colore, x, larghezza, y, altezza) in pixel dal generatore.
                VerificaCartoncino(lista, "VMBL1;N;10", ColoreTraccia.Verde, 20, 160, 20, 140);
                VerificaCartoncino(lista, "VMBL1;N;11", ColoreTraccia.Verde, 180, 300, 20, 140);
                VerificaCartoncino(lista, "VMBL1;N;12", ColoreTraccia.Blu, 20, 200, 190, 130);
                VerificaCartoncino(lista, "VMBL1;N;13", ColoreTraccia.Blu, 20, 120, 320, 130);
            }
        }

        [Test]
        public void Rileva_QrAFiloConIlBordo_ColoreEdAreaCorretti()
        {
            using (Mat img = Cv2.ImRead(Asset("cartoncino-qr-a-filo.png")))
            {
                Assert.IsFalse(img.Empty());
                var lista = new RilevatoreCartoncini(new FonteMat(img), CalibrazioneIdentita()).Rileva();
                Assert.AreEqual(1, lista.Count);
                VerificaCartoncino(lista, "VMBL1;N;12", ColoreTraccia.Rosso, 100, 300, 100, 116);
            }
        }

        private static void VerificaCartoncino(System.Collections.Generic.IReadOnlyList<CartoncinoRilevato> lista,
            string payload, ColoreTraccia colore, double x, double larghezza, double y, double altezza)
        {
            CartoncinoRilevato c = null;
            foreach (CartoncinoRilevato k in lista)
            {
                if (k.Payload == payload) c = k;
            }

            Assert.IsNotNull(c, payload);
            Assert.AreEqual(colore, c.Colore, payload);
            Assert.AreEqual(x / 640.0, c.Area.X, 0.02, payload + " X");
            Assert.AreEqual(larghezza / 640.0, c.Area.Larghezza, 0.02, payload + " Larghezza");
            Assert.AreEqual(y / 480.0, c.Area.Y, 0.02, payload + " Y");
            Assert.AreEqual(altezza / 480.0, c.Area.Altezza, 0.02, payload + " Altezza");
        }

        private static void RgbDaTonalita(double h, out byte r, out byte g, out byte b)
        {
            double x = 1 - System.Math.Abs((h / 60.0) % 2 - 1);
            double rr, gg, bb;
            if (h < 60) { rr = 1; gg = x; bb = 0; }
            else if (h < 120) { rr = x; gg = 1; bb = 0; }
            else if (h < 180) { rr = 0; gg = 1; bb = x; }
            else if (h < 240) { rr = 0; gg = x; bb = 1; }
            else if (h < 300) { rr = x; gg = 0; bb = 1; }
            else { rr = 1; gg = 0; bb = x; }
            r = (byte)System.Math.Round(rr * 255);
            g = (byte)System.Math.Round(gg * 255);
            b = (byte)System.Math.Round(bb * 255);
        }

        [TestCase(15.0, ColoreTraccia.Rosso, ColoreTraccia.Arancione)]
        [TestCase(40.0, ColoreTraccia.Arancione, ColoreTraccia.Giallo)]
        [TestCase(70.0, ColoreTraccia.Giallo, ColoreTraccia.Verde)]
        [TestCase(170.0, ColoreTraccia.Verde, ColoreTraccia.Blu)]
        [TestCase(260.0, ColoreTraccia.Blu, ColoreTraccia.Viola)]
        [TestCase(345.0, ColoreTraccia.Viola, ColoreTraccia.Rosso)]
        public void Classifica_ConfiniTonalita(double confine, ColoreTraccia sotto, ColoreTraccia sopra)
        {
            RgbDaTonalita(confine - 1.5, out byte r1, out byte g1, out byte b1);
            RgbDaTonalita(confine + 1.5, out byte r2, out byte g2, out byte b2);
            Assert.AreEqual(sotto, ClassificatoreColore.Classifica(r1, g1, b1));
            Assert.AreEqual(sopra, ClassificatoreColore.Classifica(r2, g2, b2));
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
                Cv2.GaussianBlur(qr, sfocato, new Size(0, 0), 12);
                using (var rumore = new Mat(sfocato.Size(), sfocato.Type()))
                {
                    Cv2.Randn(rumore, Scalar.All(0), Scalar.All(60));
                    Cv2.Add(sfocato, rumore, sfocato);
                }

                var rilevatore = new RilevatoreCartoncini(new FonteMat(sfocato), CalibrazioneIdentita());
                System.Collections.Generic.IReadOnlyList<CartoncinoRilevato> lista = null;
                Assert.DoesNotThrow(() => lista = rilevatore.Rileva());
                Assert.IsEmpty(lista);
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
            using (Mat adiacenti = GeneratoreImmagini.CartonciniAdiacenti())
            using (Mat afilo = GeneratoreImmagini.CartoncinoQrAFilo())
            {
                Cv2.ImWrite(Path.Combine(dir, "cartoncini-adiacenti.png"), adiacenti);
                Cv2.ImWrite(Path.Combine(dir, "cartoncino-qr-a-filo.png"), afilo);
                Cv2.ImWrite(Path.Combine(dir, "qr-test.png"), qr);
                Cv2.ImWrite(Path.Combine(dir, "due-cartoncini.png"), due);
            }
        }
    }
}
