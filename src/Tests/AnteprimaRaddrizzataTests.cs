using System;
using System.Collections.Generic;
using System.Drawing;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;
using Point = OpenCvSharp.Point;
using Size = System.Drawing.Size;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class CalibrazioneRaddrizzaTests
    {
        private static readonly PuntoTavolo[] AngoliTavolo =
        {
            new PuntoTavolo(0, 0), new PuntoTavolo(1, 0), new PuntoTavolo(1, 1), new PuntoTavolo(0, 1)
        };

        private static PuntoImmagine[] Quad(params double[] coordinate)
        {
            PuntoImmagine[] punti = new PuntoImmagine[4];
            for (int i = 0; i < 4; i++)
            {
                punti[i] = new PuntoImmagine(coordinate[i * 2], coordinate[i * 2 + 1]);
            }

            return punti;
        }

        private static Calibrazione DaQuad(params double[] coordinate)
        {
            return Calibrazione.Calcola(Quad(coordinate), AngoliTavolo, 640, 480);
        }

        private static Point[] PuntiOpenCv(params int[] coordinate)
        {
            return new[]
            {
                new Point(coordinate[0], coordinate[1]), new Point(coordinate[2], coordinate[3]),
                new Point(coordinate[4], coordinate[5]), new Point(coordinate[6], coordinate[7])
            };
        }

        private static bool Bianco(Mat immagine, int x, int y)
        {
            Vec3b p = immagine.At<Vec3b>(y, x);
            return p.Item0 > 200 && p.Item1 > 200 && p.Item2 > 200;
        }

        [Test]
        public void QuadrilateroBiancoSuNero_DiventaTuttoBiancoEHaLeDimensioniRichieste()
        {
            using (Mat origine = new Mat(480, 640, MatType.CV_8UC3, Scalar.All(0)))
            {
                Cv2.FillConvexPoly(origine, PuntiOpenCv(100, 80, 540, 100, 520, 420, 120, 400), Scalar.All(255));
                Calibrazione calibrazione = DaQuad(100, 80, 540, 100, 520, 420, 120, 400);

                using (Mat raddrizzata = calibrazione.Raddrizza(origine, 400, 300))
                {
                    Assert.That(raddrizzata.Width, Is.EqualTo(400));
                    Assert.That(raddrizzata.Height, Is.EqualTo(300));
                    Assert.That(Bianco(raddrizzata, 4, 4), Is.True, "angolo alto-sinistra");
                    Assert.That(Bianco(raddrizzata, 395, 4), Is.True, "angolo alto-destra");
                    Assert.That(Bianco(raddrizzata, 395, 295), Is.True, "angolo basso-destra");
                    Assert.That(Bianco(raddrizzata, 4, 295), Is.True, "angolo basso-sinistra");
                    Assert.That(Bianco(raddrizzata, 200, 150), Is.True, "centro");
                }
            }
        }

        [Test]
        public void ContrassegnoRossoVicinoAlloAngoloAltoSinistro_RestaInAltoASinistra()
        {
            using (Mat origine = new Mat(480, 640, MatType.CV_8UC3, Scalar.All(0)))
            {
                Cv2.FillConvexPoly(origine, PuntiOpenCv(100, 80, 540, 100, 520, 420, 120, 400), Scalar.All(255));
                Cv2.Circle(origine, new Point(150, 120), 10, new Scalar(0, 0, 255), -1);
                Calibrazione calibrazione = DaQuad(100, 80, 540, 100, 520, 420, 120, 400);

                using (Mat raddrizzata = calibrazione.Raddrizza(origine, 400, 300))
                {
                    double sommaX = 0;
                    double sommaY = 0;
                    int conteggio = 0;
                    for (int y = 0; y < raddrizzata.Height; y++)
                    {
                        for (int x = 0; x < raddrizzata.Width; x++)
                        {
                            Vec3b p = raddrizzata.At<Vec3b>(y, x);
                            if (p.Item2 > 200 && p.Item0 < 80 && p.Item1 < 80)
                            {
                                sommaX += x;
                                sommaY += y;
                                conteggio++;
                            }
                        }
                    }

                    Assert.That(conteggio, Is.GreaterThan(20), "il contrassegno rosso deve comparire");
                    Assert.That(sommaX / conteggio, Is.LessThan(100), "in alto a sinistra, non specchiato");
                    Assert.That(sommaY / conteggio, Is.LessThan(75));
                }
            }
        }

        [Test]
        public void PartiFuoriDallAreaDiCalibrazione_NonCompaiono()
        {
            // Tutto bianco tranne una fascia nera in alto, FUORI dall'area calibrata (righe 0..50).
            using (Mat origine = new Mat(480, 640, MatType.CV_8UC3, Scalar.All(255)))
            {
                origine.Rectangle(new Rect(0, 0, 640, 50), Scalar.All(0), -1);
                Calibrazione calibrazione = DaQuad(100, 100, 540, 100, 540, 400, 100, 400);

                using (Mat raddrizzata = calibrazione.Raddrizza(origine, 440, 300))
                {
                    for (int y = 4; y < raddrizzata.Height - 4; y += 20)
                    {
                        Assert.That(Bianco(raddrizzata, 220, y), Is.True, "riga " + y);
                    }
                }
            }
        }

        [Test]
        public void ArgomentiNonValidi_Lanciano()
        {
            Calibrazione calibrazione = DaQuad(0, 0, 640, 0, 640, 480, 0, 480);

            Assert.Throws<ArgumentException>(() => calibrazione.Raddrizza(null, 100, 100));
            using (Mat vuota = new Mat())
            {
                Assert.Throws<ArgumentException>(() => calibrazione.Raddrizza(vuota, 100, 100));
            }

            using (Mat immagine = new Mat(10, 10, MatType.CV_8UC3, Scalar.All(0)))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => calibrazione.Raddrizza(immagine, 0, 100));
                Assert.Throws<ArgumentOutOfRangeException>(() => calibrazione.Raddrizza(immagine, 100, -1));
            }
        }
    }

    [TestFixture]
    public class AnteprimaTavoloTests
    {
        [Test]
        public void Dimensioni_LimitaPerLarghezza()
        {
            Assert.That(AnteprimaTavolo.Dimensioni(2.0, 520, 390), Is.EqualTo(new Size(520, 260)));
        }

        [Test]
        public void Dimensioni_LimitaPerAltezza()
        {
            Assert.That(AnteprimaTavolo.Dimensioni(1.0, 520, 390), Is.EqualTo(new Size(390, 390)));
            Assert.That(AnteprimaTavolo.Dimensioni(0.5, 520, 390), Is.EqualTo(new Size(195, 390)));
        }

        [TestCase(0.0)]
        [TestCase(-3.0)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void Dimensioni_AspettoNonValido_Usa16a9(double aspetto)
        {
            Size dimensioni = AnteprimaTavolo.Dimensioni(aspetto, 520, 390);

            Assert.That(dimensioni.Width, Is.EqualTo(520));
            Assert.That((double)dimensioni.Width / dimensioni.Height, Is.EqualTo(16.0 / 9.0).Within(0.01));
        }

        [Test]
        public void Dimensioni_NonScendeSottoUnPixel()
        {
            Size dimensioni = AnteprimaTavolo.Dimensioni(1000.0, 10, 10);

            Assert.That(dimensioni.Width, Is.GreaterThanOrEqualTo(1));
            Assert.That(dimensioni.Height, Is.GreaterThanOrEqualTo(1));
        }

        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        public void Raddrizzata_SoloConCalibrazioneAttivaEFuoriDallaCalibrazione(
            bool calibrazioneAttiva, bool inCalibrazione, bool atteso)
        {
            Assert.That(AnteprimaTavolo.Raddrizzata(calibrazioneAttiva, inCalibrazione), Is.EqualTo(atteso));
        }

        [Test]
        public void IlTestoDellImmagineRaddrizzata_DiceCheEStirataERimandaAlPulsanteCalibra()
        {
            Assert.That(AnteprimaTavolo.TestoRaddrizzata, Does.Contain("stirata"));
            Assert.That(AnteprimaTavolo.TestoRaddrizzata, Does.Contain("tavolo"));
            Assert.That(AnteprimaTavolo.TestoRaddrizzata, Does.Contain("rifai la calibrazione"));
            Assert.That(AnteprimaTavolo.TestoRaddrizzata, Does.Contain("«Calibra»"));
        }

        [Test]
        public void IlTestoDellImmagineGrezza_NonParlaDiImmagineStirata()
        {
            Assert.That(AnteprimaTavolo.TestoGrezza, Does.Not.Contain("stirata"));
            Assert.That(AnteprimaTavolo.TestoGrezza, Is.Not.Empty);
        }
    }

    [TestFixture]
    public class RilevatoreCommutabileCalibrazioneTests
    {
        [Test]
        public void SenzaRilevatore_NessunaCalibrazioneAttiva()
        {
            Assert.That(new RilevatoreCommutabile().CalibrazioneAttiva, Is.Null);
        }

        [Test]
        public void ConRilevatoreCalibrato_EspongaLaCalibrazione_EDopoImpostaNullTorna()
        {
            Calibrazione calibrazione = Calibrazione.Calcola(
                new[] { new PuntoImmagine(0, 0), new PuntoImmagine(640, 0), new PuntoImmagine(640, 480), new PuntoImmagine(0, 480) },
                new[] { new PuntoTavolo(0, 0), new PuntoTavolo(1, 0), new PuntoTavolo(1, 1), new PuntoTavolo(0, 1) },
                640,
                480);
            RilevatoreCommutabile commutabile = new RilevatoreCommutabile();

            commutabile.Imposta(new RilevatoreCartoncini(null, calibrazione));
            Assert.That(commutabile.CalibrazioneAttiva, Is.SameAs(calibrazione));

            commutabile.Imposta(null);
            Assert.That(commutabile.CalibrazioneAttiva, Is.Null);
        }

        [Test]
        public void ConUnRilevatoreDiversoDaQuelloReale_NessunaCalibrazione()
        {
            RilevatoreCommutabile commutabile = new RilevatoreCommutabile();

            commutabile.Imposta(new RilevatoreFinto());

            Assert.That(commutabile.Attivo, Is.True);
            Assert.That(commutabile.CalibrazioneAttiva, Is.Null);
        }

        private sealed class RilevatoreFinto : IRilevatoreCartoncini
        {
            public IReadOnlyList<CartoncinoRilevato> Rileva()
            {
                return new List<CartoncinoRilevato>();
            }
        }
    }
}
