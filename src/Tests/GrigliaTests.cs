using NUnit.Framework;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class GrigliaTests
    {
        private static Griglia CreaGriglia()
        {
            return new Griglia(new ConfigGriglia { Area = new RettangoloTavolo(0, 0, 1, 1) });
        }

        [Test]
        public void CentroInPrimaCella()
        {
            Griglia griglia = CreaGriglia();
            RettangoloTavolo area = new RettangoloTavolo(0, 0, 1.0 / 16, 1.0 / 6);

            bool ok = griglia.TryCella(area, out int traccia, out int stepIniziale, out int durataStep);

            Assert.That(ok, Is.True);
            Assert.That(traccia, Is.EqualTo(0));
            Assert.That(stepIniziale, Is.EqualTo(0));
            Assert.That(durataStep, Is.EqualTo(1));
        }

        [Test]
        public void LarghezzaDueStep()
        {
            Griglia griglia = CreaGriglia();
            RettangoloTavolo area = new RettangoloTavolo(0, 0, 2.0 / 16, 0.1);

            bool ok = griglia.TryCella(area, out _, out _, out int durataStep);

            Assert.That(ok, Is.True);
            Assert.That(durataStep, Is.EqualTo(2));
        }

        [Test]
        public void LarghezzaArrotondataAlPiuVicino()
        {
            Griglia griglia = CreaGriglia();
            RettangoloTavolo area = new RettangoloTavolo(0, 0, 2.4 / 16, 0.1);

            bool ok = griglia.TryCella(area, out _, out _, out int durataStep);

            Assert.That(ok, Is.True);
            Assert.That(durataStep, Is.EqualTo(2));
        }

        [Test]
        public void DurataTroncataAFineGriglia()
        {
            Griglia griglia = CreaGriglia();
            RettangoloTavolo area = new RettangoloTavolo(15.0 / 16, 0, 4.0 / 16, 0.1);

            bool ok = griglia.TryCella(area, out _, out int stepIniziale, out int durataStep);

            Assert.That(ok, Is.True);
            Assert.That(stepIniziale, Is.EqualTo(15));
            Assert.That(durataStep, Is.EqualTo(1));
        }

        [Test]
        public void FuoriGriglia()
        {
            Griglia griglia = CreaGriglia();
            RettangoloTavolo area = new RettangoloTavolo(0, 1.5 - 0.05, 1.0 / 16, 0.1);

            bool ok = griglia.TryCella(area, out _, out _, out _);

            Assert.That(ok, Is.False);
            Assert.That(griglia.EsternoAllaGriglia(area), Is.True);
        }

        [Test]
        public void QrSpostatoDiTreDecimiCellaPrimaDelConfineVieneAgganciato()
        {
            const int riga = 2, step = 3;
            double larghezza = 1.0 / 16, altezza = 1.0 / 6;
            var griglia = new Griglia(new ConfigGriglia { Area = new RettangoloTavolo(0, 0, 1, 1) });
            var nota = new CartoncinoRilevato
            {
                AreaQr = new RettangoloTavolo((step - 0.3) * larghezza,
                    (riga + 0.3) * altezza, 0.55 * larghezza, 0.4 * altezza),
                Area = new RettangoloTavolo((step - 0.3) * larghezza, riga * altezza,
                    larghezza, altezza)
            };

            bool ok = griglia.TryCella(nota, out int traccia, out int stepIniziale, out _);

            Assert.That(ok, Is.True);
            Assert.That(traccia, Is.EqualTo(riga));
            Assert.That(stepIniziale, Is.EqualTo(step));
        }

        [Test]
        public void QrSpostatoDiUnQuintoCellaFuoriDalTavoloVieneAgganciatoAlPrimoConfine()
        {
            const double larghezza = 1.0 / 16, altezza = 1.0 / 6;
            var griglia = new Griglia(new ConfigGriglia { Area = new RettangoloTavolo(0, 0, 1, 1) });
            var nota = new CartoncinoRilevato
            {
                AreaQr = new RettangoloTavolo(-0.2 * larghezza, 0.3 * altezza, 0.55 * larghezza, 0.4 * altezza),
                Area = new RettangoloTavolo(-0.2 * larghezza, 0.1 * altezza, larghezza, 0.8 * altezza)
            };

            Assert.That(griglia.TryCella(nota, out int traccia, out int step, out _), Is.True);
            Assert.That(traccia, Is.EqualTo(0));
            Assert.That(step, Is.EqualTo(0));
        }

        [Test]
        public void QrVicinoAlConfineNonVieneAgganciatoAllaRigaSuccessiva()
        {
            const int riga = 2;
            const double larghezza = 1.0 / 16, altezza = 1.0 / 6;
            var griglia = new Griglia(new ConfigGriglia
            {
                Area = new RettangoloTavolo(0, 0, 1, 1),
                TolleranzaQrCelle = 0.25
            });
            var nota = new CartoncinoRilevato
            {
                AreaQr = new RettangoloTavolo(3 * larghezza, (riga + 0.7) * altezza, 0.55 * larghezza, 0.2 * altezza),
                Area = new RettangoloTavolo(3 * larghezza, riga * altezza, larghezza, altezza)
            };

            Assert.That(griglia.TryCella(nota, out int traccia, out _, out _), Is.True);
            Assert.That(traccia, Is.EqualTo(riga));
        }

        [Test]
        public void QrOltreLaTolleranzaRestaFuoriGriglia()
        {
            double larghezza = 1.0 / 16, altezza = 1.0 / 6;
            var griglia = new Griglia(new ConfigGriglia { Area = new RettangoloTavolo(0, 0, 1, 1) });
            var nota = new CartoncinoRilevato
            {
                AreaQr = new RettangoloTavolo(-0.4 * larghezza, 0.4 * altezza,
                    0.55 * larghezza, 0.4 * altezza),
                Area = new RettangoloTavolo(-0.4 * larghezza, 0, larghezza, altezza)
            };

            Assert.That(griglia.TryCella(nota, out _, out _, out _), Is.False);
        }

        [Test]
        public void LarghezzaMinimaUnoStep()
        {
            Griglia griglia = CreaGriglia();
            RettangoloTavolo area = new RettangoloTavolo(0, 0, 0.2 / 16, 0.1);

            bool ok = griglia.TryCella(area, out _, out _, out int durataStep);

            Assert.That(ok, Is.True);
            Assert.That(durataStep, Is.EqualTo(1));
        }
    }
}
