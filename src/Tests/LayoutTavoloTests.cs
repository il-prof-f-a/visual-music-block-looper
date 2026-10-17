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
        public void Default_ValoriDelleZoneCompleti()
        {
            LayoutTavolo layout = LayoutTavolo.Default;
            (string nome, RettangoloTavolo r, double x, double y, double w, double h)[] attesi =
            {
                ("Misura", layout.Misura, 0.01, 0.04, 0.12, 0.28),
                ("Collegamento", layout.Collegamento, 0.01, 0.36, 0.12, 0.28),
                ("Ab", layout.Ab, 0.01, 0.68, 0.12, 0.28),
                ("Comandi", layout.Comandi, 0.84, 0.04, 0.14, 0.92),
                ("AreaGriglia", LayoutTavolo.AreaGrigliaDefault, 0.15, 0.04, 0.67, 0.92),
            };

            foreach (var a in attesi)
            {
                Assert.That(a.r.X, Is.EqualTo(a.x), a.nome + " X");
                Assert.That(a.r.Y, Is.EqualTo(a.y), a.nome + " Y");
                Assert.That(a.r.Larghezza, Is.EqualTo(a.w), a.nome + " Larghezza");
                Assert.That(a.r.Altezza, Is.EqualTo(a.h), a.nome + " Altezza");
            }
        }

        [Test]
        public void ZonaDelCentro_AreaNonDegenere_CentroInMisura()
        {
            // Centro (0.1, 0.1): dentro Misura. Con il centro errato (X + Larghezza) cadrebbe a 0.2 e direbbe Nessuna.
            LayoutTavolo layout = LayoutTavolo.Default;

            Assert.That(layout.ZonaDelCentro(new RettangoloTavolo(0.0, 0.0, 0.2, 0.2)), Is.EqualTo(ZonaSpeciale.Misura));
        }

        [Test]
        public void ZonaDelCentro_BordiSullaZonaCentroNellaFessura_Nessuna()
        {
            // Bordi (y 0.30..0.40) sovrapposti a Misura e Collegamento, ma centro (0.1, 0.35) nella fessura.
            LayoutTavolo layout = LayoutTavolo.Default;

            Assert.That(layout.ZonaDelCentro(new RettangoloTavolo(0.0, 0.30, 0.2, 0.1)), Is.EqualTo(ZonaSpeciale.Nessuna));
        }

        [Test]
        public void ZonaDelCentro_BordoSinistroNellaZonaCentroFuori_Nessuna()
        {
            // Bordo sinistro x=0.05 dentro Misura, ma centro (0.2, 0.15) fuori.
            LayoutTavolo layout = LayoutTavolo.Default;

            Assert.That(layout.ZonaDelCentro(new RettangoloTavolo(0.05, 0.10, 0.3, 0.1)), Is.EqualTo(ZonaSpeciale.Nessuna));
        }

        [Test]
        public void ZonaDelCentro_CentroDentroBordiSporgenti_ZonaDelCentro()
        {
            // Centro (0.05, 0.15) dentro Misura; il rettangolo sporge a sinistra (X = -0.05).
            LayoutTavolo layout = LayoutTavolo.Default;

            Assert.That(layout.ZonaDelCentro(new RettangoloTavolo(-0.05, 0.05, 0.2, 0.2)), Is.EqualTo(ZonaSpeciale.Misura));
        }

        [TestCase(-0.001, 0.18)]
        [TestCase(0.131, 0.18)]
        [TestCase(0.07, 0.039)]
        [TestCase(0.07, 0.321)]
        public void ZonaDelCentro_PocoFuoriMisura_Nessuna(double x, double y)
        {
            LayoutTavolo layout = LayoutTavolo.Default;

            Assert.That(layout.ZonaDelCentro(Punto(x, y)), Is.EqualTo(ZonaSpeciale.Nessuna));
        }

        [Test]
        public void SiSovrappongono_RettangoliSovrapposti_Vero()
        {
            // Controllo positivo: l'helper deve rilevare una vera sovrapposizione.
            Assert.That(SiSovrappongono(
                new RettangoloTavolo(0.0, 0.0, 0.2, 0.2),
                new RettangoloTavolo(0.1, 0.1, 0.2, 0.2)), Is.True);
        }
    }
}
