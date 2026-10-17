using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class RenderGuidaTests
    {
        // Griglia 6 tracce x 16 step su 1280x720: cella 80x120 px.
        private static readonly Size Dimensione = new Size(1280, 720);

        private static ConfigGriglia ConfigPiena()
        {
            return new ConfigGriglia
            {
                Tracce = 6,
                Step = 16,
                Area = new RettangoloTavolo(0, 0, 1, 1)
            };
        }

        private static StatoTavolo StatoCon(params CartoncinoNota[] note)
        {
            return new StatoTavolo(new List<CartoncinoNota>(note), null, null);
        }

        private static Bitmap Disegna(ConfigGriglia config, StatoTavolo stato, int stepCorrente, bool inEsecuzione)
        {
            var bmp = new Bitmap(Dimensione.Width, Dimensione.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                new RenderGuida(config).Disegna(g, Dimensione, stato, stepCorrente, inEsecuzione);
            }

            return bmp;
        }

        // Centro della cella (traccia, step) per la griglia piena 6x16 su 1280x720.
        private static Point CentroPieno(int traccia, int step)
        {
            return new Point(step * 80 + 40, traccia * 120 + 60);
        }

        [Test]
        public void FondoBianco()
        {
            using (Bitmap bmp = Disegna(ConfigPiena(), StatoCon(), 0, false))
            {
                Point p = CentroPieno(2, 5);
                Assert.That(bmp.GetPixel(p.X, p.Y).ToArgb(), Is.EqualTo(Color.White.ToArgb()));
            }
        }

        [Test]
        public void PlayheadVisibile()
        {
            using (Bitmap bmp = Disegna(ConfigPiena(), StatoCon(), 3, true))
            {
                Color colonna = bmp.GetPixel(CentroPieno(0, 3).X, CentroPieno(0, 3).Y);
                Color successiva = bmp.GetPixel(CentroPieno(0, 4).X, CentroPieno(0, 4).Y);

                Assert.That(colonna.ToArgb(), Is.Not.EqualTo(Color.White.ToArgb()));
                Assert.That(colonna.R, Is.GreaterThan(200));
                Assert.That(colonna.B, Is.LessThan(230));
                Assert.That(successiva.ToArgb(), Is.EqualTo(Color.White.ToArgb()));
            }
        }

        [Test]
        public void PlayheadFermoSoloContorno()
        {
            using (Bitmap bmp = Disegna(ConfigPiena(), StatoCon(), 3, false))
            {
                Point p = CentroPieno(0, 3);
                Assert.That(bmp.GetPixel(p.X, p.Y).ToArgb(), Is.EqualTo(Color.White.ToArgb()));

                // Il bordo sinistro della colonna dello step 3 (x = 240) è il contorno grigio del playhead.
                Assert.That(bmp.GetPixel(3 * 80, p.Y).ToArgb(), Is.EqualTo(Color.Gray.ToArgb()));
            }
        }

        [Test]
        public void NotaRiconosciutaVerde()
        {
            var nota = new CartoncinoNota
            {
                Traccia = 1,
                StepIniziale = 2,
                DurataStep = 3,
                IdClip = 7,
                Colore = ColoreTraccia.Arancione
            };

            using (Bitmap bmp = Disegna(ConfigPiena(), StatoCon(nota), 0, false))
            {
                foreach (int step in new[] { 2, 3, 4 })
                {
                    Point p = CentroPieno(1, step);
                    Color c = bmp.GetPixel(p.X, p.Y);
                    Assert.That(c.G, Is.GreaterThan(c.R), $"step {step}");
                    Assert.That(c.G, Is.GreaterThan(c.B), $"step {step}");
                }

                Point fuori = CentroPieno(1, 5);
                Assert.That(bmp.GetPixel(fuori.X, fuori.Y).ToArgb(), Is.EqualTo(Color.White.ToArgb()));
            }
        }

        [Test]
        public void ErroreRosso()
        {
            // Area della cella traccia 4, step 10: x 800..880, y 480..600.
            var errore = new ErroreTavolo
            {
                Tipo = TipoErrore.Sovrapposto,
                Area = new RettangoloTavolo(10.0 / 16, 4.0 / 6, 1.0 / 16, 1.0 / 6)
            };

            var stato = new StatoTavolo(null, null, new List<ErroreTavolo> { errore });

            using (Bitmap bmp = Disegna(ConfigPiena(), stato, 0, false))
            {
                Color bordo = bmp.GetPixel(800, 540);
                Assert.That(bordo.R, Is.GreaterThan(180));
                Assert.That(bordo.G, Is.LessThan(90));
                Assert.That(bordo.B, Is.LessThan(90));
            }
        }

        [Test]
        public void StatoNullNonLancia()
        {
            using (Bitmap bmp = Disegna(ConfigPiena(), null, 0, false))
            {
                Point p = CentroPieno(0, 0);
                Assert.That(bmp.GetPixel(p.X, p.Y).ToArgb(), Is.EqualTo(Color.White.ToArgb()));
            }
        }

        [Test]
        public void AreaGrigliaRidotta()
        {
            var config = new ConfigGriglia
            {
                Tracce = 6,
                Step = 16,
                Area = new RettangoloTavolo(0.1, 0.1, 0.8, 0.8)
            };

            var nota = new CartoncinoNota
            {
                Traccia = 1,
                StepIniziale = 2,
                DurataStep = 1,
                IdClip = 1,
                Colore = ColoreTraccia.Arancione
            };

            using (Bitmap bmp = Disegna(config, StatoCon(nota), 0, false))
            {
                // Griglia: x 128..1152, y 72..648; cella 64x96.
                Assert.That(bmp.GetPixel(0, 0).ToArgb(), Is.EqualTo(Color.White.ToArgb()));

                Color centro = bmp.GetPixel(128 + 2 * 64 + 32, 72 + 1 * 96 + 48);
                Assert.That(centro.G, Is.GreaterThan(centro.R));
                Assert.That(centro.G, Is.GreaterThan(centro.B));
            }
        }

        [Test]
        public void NoteOltreLaGrigliaTroncate()
        {
            // Griglia x 128..1152, y 72..648; cella 64x96. Lo step 15 è l'ultimo.
            var config = new ConfigGriglia
            {
                Tracce = 6,
                Step = 16,
                Area = new RettangoloTavolo(0.1, 0.1, 0.8, 0.8)
            };

            var nota = new CartoncinoNota
            {
                Traccia = 1,
                StepIniziale = 15,
                DurataStep = 4,
                IdClip = 1,
                Colore = ColoreTraccia.Arancione
            };

            Bitmap bmp = null;
            Assert.DoesNotThrow(() => bmp = Disegna(config, StatoCon(nota), 0, false));

            using (bmp)
            {
                // Ultima colonna della griglia (step 15, x 1088..1152): dentro la griglia è verde.
                Color dentro = bmp.GetPixel(1120, 216);
                Assert.That(dentro.G, Is.GreaterThan(dentro.R));
                Assert.That(dentro.G, Is.GreaterThan(dentro.B));

                // Appena a destra del bordo della griglia la nota non deve dipingere nulla.
                Assert.That(bmp.GetPixel(1152 + 4, 216).ToArgb(), Is.EqualTo(Color.White.ToArgb()));
            }
        }

        [Test]
        public void FormProiettore_SiCostruisceESiAggiorna_SenzaMostrarsi()
        {
            var form = new FormProiettore(ConfigPiena());
            try
            {
                form.Aggiorna(StatoCon(), 2, true);
                form.Aggiorna(null, 0, false);
                Assert.That(form.Visible, Is.False);
            }
            finally
            {
                form.Dispose();
            }
        }
    }
}
