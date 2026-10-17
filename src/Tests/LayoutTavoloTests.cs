using NUnit.Framework;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class LayoutTavoloTests
    {
        private static RettangoloTavolo Punto(double x, double y)
        {
            return new RettangoloTavolo(x, y, 0, 0);
        }

        private static RettangoloTavolo CentroDi(RettangoloTavolo r)
        {
            return Punto(r.X + r.Larghezza / 2, r.Y + r.Altezza / 2);
        }

        private static bool SiSovrappongono(RettangoloTavolo a, RettangoloTavolo b)
        {
            double x0 = System.Math.Max(a.X, b.X);
            double x1 = System.Math.Min(a.X + a.Larghezza, b.X + b.Larghezza);
            double y0 = System.Math.Max(a.Y, b.Y);
            double y1 = System.Math.Min(a.Y + a.Altezza, b.Y + b.Altezza);
            return x0 < x1 && y0 < y1;
        }

        [Test]
        public void ZonaDelCentro_CentriDelleZoneDefault()
        {
            LayoutTavolo layout = LayoutTavolo.Default;

            Assert.That(layout.ZonaDelCentro(CentroDi(layout.Misura)), Is.EqualTo(ZonaSpeciale.Misura));
            Assert.That(layout.ZonaDelCentro(CentroDi(layout.Collegamento)), Is.EqualTo(ZonaSpeciale.Collegamento));
            Assert.That(layout.ZonaDelCentro(CentroDi(layout.Ab)), Is.EqualTo(ZonaSpeciale.Ab));
            Assert.That(layout.ZonaDelCentro(CentroDi(layout.Comandi)), Is.EqualTo(ZonaSpeciale.Comandi));
        }

        [Test]
        public void ZonaDelCentro_CentroDellaGrigliaNessuna()
        {
            LayoutTavolo layout = LayoutTavolo.Default;

            Assert.That(layout.ZonaDelCentro(CentroDi(LayoutTavolo.AreaGrigliaDefault)), Is.EqualTo(ZonaSpeciale.Nessuna));
        }

        [Test]
        public void ZonaDelCentro_EstremiInclusi()
        {
            LayoutTavolo layout = LayoutTavolo.Default;

            // Angolo in alto a sinistra della zona Misura: estremo incluso.
            Assert.That(layout.ZonaDelCentro(Punto(layout.Misura.X, layout.Misura.Y)), Is.EqualTo(ZonaSpeciale.Misura));
            // Angolo in basso a destra della zona Comandi: estremo incluso.
            Assert.That(
                layout.ZonaDelCentro(Punto(layout.Comandi.X + layout.Comandi.Larghezza, layout.Comandi.Y + layout.Comandi.Altezza)),
                Is.EqualTo(ZonaSpeciale.Comandi));
        }

        [Test]
        public void ZoneDefault_NonSiSovrappongono()
        {
            LayoutTavolo layout = LayoutTavolo.Default;
            RettangoloTavolo[] rettangoli =
            {
                layout.Misura,
                layout.Collegamento,
                layout.Ab,
                layout.Comandi,
                LayoutTavolo.AreaGrigliaDefault,
            };

            for (int i = 0; i < rettangoli.Length; i++)
            {
                for (int j = i + 1; j < rettangoli.Length; j++)
                {
                    Assert.That(SiSovrappongono(rettangoli[i], rettangoli[j]), Is.False,
                        $"Rettangoli {i} e {j} si sovrappongono");
                }
            }
        }

        [Test]
        public void ConSoloComandi_ZoneSpecialiVuote()
        {
            RettangoloTavolo comandi = new RettangoloTavolo(0.84, 0.04, 0.14, 0.92);
            LayoutTavolo layout = LayoutTavolo.ConSoloComandi(comandi);

            Assert.That(layout.ZonaDelCentro(CentroDi(LayoutTavolo.AreaGrigliaDefault)), Is.EqualTo(ZonaSpeciale.Nessuna));
            Assert.That(layout.ZonaDelCentro(CentroDi(LayoutTavolo.Default.Misura)), Is.EqualTo(ZonaSpeciale.Nessuna));
            Assert.That(layout.ZonaDelCentro(CentroDi(comandi)), Is.EqualTo(ZonaSpeciale.Comandi));
        }

        [Test]
        public void AreaGrigliaDefault_Valori()
        {
            RettangoloTavolo griglia = LayoutTavolo.AreaGrigliaDefault;

            Assert.That(griglia.X, Is.EqualTo(0.15));
            Assert.That(griglia.Y, Is.EqualTo(0.04));
            Assert.That(griglia.Larghezza, Is.EqualTo(0.67));
            Assert.That(griglia.Altezza, Is.EqualTo(0.92));
        }

        [Test]
        public void Default_ValoriDelleZone()
        {
            LayoutTavolo layout = LayoutTavolo.Default;

            Assert.That(layout.Misura.X, Is.EqualTo(0.01));
            Assert.That(layout.Misura.Y, Is.EqualTo(0.04));
            Assert.That(layout.Misura.Larghezza, Is.EqualTo(0.12));
            Assert.That(layout.Misura.Altezza, Is.EqualTo(0.28));

            Assert.That(layout.Collegamento.Y, Is.EqualTo(0.36));
            Assert.That(layout.Ab.Y, Is.EqualTo(0.68));

            Assert.That(layout.Comandi.X, Is.EqualTo(0.84));
            Assert.That(layout.Comandi.Larghezza, Is.EqualTo(0.14));
            Assert.That(layout.Comandi.Altezza, Is.EqualTo(0.92));
        }
    }
}
