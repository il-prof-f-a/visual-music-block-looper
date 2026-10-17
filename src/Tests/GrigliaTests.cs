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
