using System;
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
                Riga = 1,
                Traccia = 1,
                StepIniziale = 2,
                DurataStep = 3,
                IdClip = 7,
                Colore = ColoreTraccia.Arancione
            };

            using (Bitmap bmp = Disegna(ConfigPiena(), StatoCon(nota), 0, false))
            {
                // Solo il bordo: il centro resta bianco, altrimenti il proiettore colorerebbe il cartoncino.
                int mezzaCella = (CentroPieno(1, 3).X - CentroPieno(1, 2).X) / 2;
                Point sinistra = new Point(CentroPieno(1, 2).X - mezzaCella + 1, CentroPieno(1, 2).Y);
                Point destra = new Point(CentroPieno(1, 4).X + mezzaCella - 2, CentroPieno(1, 4).Y);
                foreach (Point p in new[] { sinistra, destra })
                {
                    Color c = bmp.GetPixel(p.X, p.Y);
                    Assert.That(c.G, Is.GreaterThan(c.R), $"bordo {p}");
                    Assert.That(c.G, Is.GreaterThan(c.B), $"bordo {p}");
                }

                foreach (int step in new[] { 2, 3, 4 })
                {
                    Point centro = CentroPieno(1, step);
                    Assert.That(bmp.GetPixel(centro.X, centro.Y).ToArgb(), Is.EqualTo(Color.White.ToArgb()), $"centro step {step}");
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
                Riga = 1,
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

                Color bordo = bmp.GetPixel(128 + 2 * 64 + 1, 72 + 1 * 96 + 48);
                Assert.That(bordo.G, Is.GreaterThan(bordo.R));
                Assert.That(bordo.G, Is.GreaterThan(bordo.B));
                Assert.That(bmp.GetPixel(128 + 2 * 64 + 32, 72 + 1 * 96 + 48).ToArgb(), Is.EqualTo(Color.White.ToArgb()), "Il centro non va colorato");
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
                Riga = 1,
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
                // Ultima colonna della griglia (step 15, x 1088..1152): il bordo destro, dentro la griglia, è verde.
                Color dentro = bmp.GetPixel(1152 - 2, 216);
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

        // --- Tavolo con zone, gruppi e giro: geometria calcolata dall'area di default su 1280x720 ---
        private const double GrigliaX = 0.15 * 1280;
        private const double GrigliaY = 0.04 * 720;
        private const double CellaL = 0.67 * 1280 / 16;
        private const double CellaA = 0.92 * 720 / 6;

        private static readonly int Bianco = Color.White.ToArgb();

        private static ConfigGriglia ConfigTavolo()
        {
            return new ConfigGriglia { Area = LayoutTavolo.AreaGrigliaDefault };
        }

        private static StatoTavolo StatoStruttura(int battiti, int collegate, int a, int b)
        {
            Assert.That(StrutturaTavolo.TryCrea(battiti, collegate, a, b, out StrutturaTavolo s), Is.True);
            return new StatoTavolo(null, null, null, s);
        }

        private static Bitmap DisegnaTavolo(StatoTavolo stato, PosizioneGiro posizione, bool inEsecuzione)
        {
            var bmp = new Bitmap(Dimensione.Width, Dimensione.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                new RenderGuida(ConfigTavolo(), LayoutTavolo.Default).Disegna(g, Dimensione, stato, posizione, inEsecuzione);
            }

            return bmp;
        }

        private static Point CentroTavolo(int riga, int colonna)
        {
            return new Point((int)(GrigliaX + (colonna + 0.5) * CellaL), (int)(GrigliaY + (riga + 0.5) * CellaA));
        }

        // Le note sono disegnate solo col bordo: si campiona appena dentro il bordo sinistro della prima colonna.
        private static Point BordoSinistroNota(int riga, int colonna)
        {
            return new Point((int)(GrigliaX + colonna * CellaL) + 1, (int)(GrigliaY + (riga + 0.5) * CellaA));
        }

        private static bool BordoSinistroDisegnato(Bitmap bmp, RettangoloTavolo zona)
        {
            int xAtteso = (int)Math.Round(zona.X * 1280);
            int y = (int)Math.Round((zona.Y + zona.Altezza / 2) * 720);
            for (int x = xAtteso - 2; x <= xAtteso + 2; x++)
            {
                if (bmp.GetPixel(x, y).ToArgb() != Bianco) return true;
            }

            return false;
        }

        [Test]
        public void ZoneDisegnate()
        {
            using (Bitmap bmp = DisegnaTavolo(null, new PosizioneGiro(0, 0, new[] { 0 }), false))
            {
                Assert.That(BordoSinistroDisegnato(bmp, LayoutTavolo.Default.Misura), Is.True);
                Assert.That(BordoSinistroDisegnato(bmp, LayoutTavolo.Default.Collegamento), Is.True);
                Assert.That(BordoSinistroDisegnato(bmp, LayoutTavolo.Default.Ab), Is.True);
            }
        }

        [Test]
        public void EtichettaMisuraDisegnata()
        {
            using (Bitmap bmp = DisegnaTavolo(null, new PosizioneGiro(0, 0, new[] { 0 }), false))
            {
                RettangoloTavolo z = LayoutTavolo.Default.Misura;
                int x0 = (int)(z.X * 1280) + 6, x1 = (int)((z.X + z.Larghezza) * 1280) - 6;
                int y0 = (int)(z.Y * 720) + 6, y1 = (int)((z.Y + z.Altezza) * 720) - 6;
                bool trovato = false;
                for (int y = y0; y <= y1 && !trovato; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        if (bmp.GetPixel(x, y).ToArgb() != Bianco) { trovato = true; break; }
                    }
                }

                Assert.That(trovato, Is.True);
            }
        }

        [Test]
        public void SenzaLayout_NessunaZonaDisegnata()
        {
            using (Bitmap bmp = Disegna(ConfigTavolo(), null, 0, false))
            {
                Assert.That(BordoSinistroDisegnato(bmp, LayoutTavolo.Default.Misura), Is.False);
            }
        }

        [Test]
        public void ColonneOltreLaMisuraOscurate()
        {
            using (Bitmap bmp = DisegnaTavolo(StatoStruttura(3, 6, 1, 6), new PosizioneGiro(0, 0, new[] { 3 }), false))
            {
                Point p = CentroTavolo(0, 12);
                Assert.That(bmp.GetPixel(p.X, p.Y).ToArgb(), Is.EqualTo(Color.FromArgb(255, 215, 215, 215).ToArgb()));
                Point q = CentroTavolo(0, 4);
                Assert.That(bmp.GetPixel(q.X, q.Y).ToArgb(), Is.EqualTo(Bianco));
            }
        }

        [Test]
        public void RigheFuoriDaAbAttenuate()
        {
            using (Bitmap bmp = DisegnaTavolo(StatoStruttura(4, 3, 2, 3), new PosizioneGiro(0, 0, new[] { 1, 4 }), false))
            {
                int attenuato = Color.FromArgb(255, 240, 240, 240).ToArgb();
                Point p0 = CentroTavolo(0, 5), p1 = CentroTavolo(1, 5), p3 = CentroTavolo(3, 5), p4 = CentroTavolo(4, 5);
                Assert.That(bmp.GetPixel(p0.X, p0.Y).ToArgb(), Is.EqualTo(attenuato));
                Assert.That(bmp.GetPixel(p1.X, p1.Y).ToArgb(), Is.EqualTo(Bianco));
                Assert.That(bmp.GetPixel(p3.X, p3.Y).ToArgb(), Is.EqualTo(attenuato));
                Assert.That(bmp.GetPixel(p4.X, p4.Y).ToArgb(), Is.EqualTo(Bianco));

                // Le righe esattamente su B (posizione 3 nel gruppo) restano piene, quelle sopra A sono attenuate.
                Point p2 = CentroTavolo(2, 5), p5 = CentroTavolo(5, 5);
                Assert.That(bmp.GetPixel(p2.X, p2.Y).ToArgb(), Is.EqualTo(Bianco));
                Assert.That(bmp.GetPixel(p5.X, p5.Y).ToArgb(), Is.EqualTo(Bianco));
            }
        }

        private static bool EVerde(Color c)
        {
            return c.G > c.R && c.G > c.B;
        }

        private static Bitmap DisegnaTavoloConNote(StrutturaTavolo struttura, params CartoncinoNota[] note)
        {
            var stato = new StatoTavolo(new List<CartoncinoNota>(note), null, null, struttura);
            return DisegnaTavolo(stato, new PosizioneGiro(0, 0, new[] { 0 }), false);
        }

        private static CartoncinoNota Nota(int riga, int traccia, int step, int durata)
        {
            return new CartoncinoNota
            {
                Riga = riga,
                Traccia = traccia,
                StepIniziale = step,
                DurataStep = durata,
                IdClip = 1,
                Colore = ColoreTraccia.Arancione
            };
        }

        [Test]
        public void NotaDisegnataNellaRiga_NonNellaRigaDelColore()
        {
            using (Bitmap bmp = DisegnaTavoloConNote(StrutturaTavolo.Default, Nota(3, 0, 2, 2)))
            {
                Point dentro = BordoSinistroNota(3, 2), sbagliata = CentroTavolo(0, 2);
                Assert.That(EVerde(bmp.GetPixel(dentro.X, dentro.Y)), Is.True);
                Assert.That(EVerde(bmp.GetPixel(sbagliata.X, sbagliata.Y)), Is.False);
            }
        }

        [Test]
        public void NotaInRigaZeroColoreQuattro_RiempieRigaZero()
        {
            using (Bitmap bmp = DisegnaTavoloConNote(StrutturaTavolo.Default, Nota(0, 4, 2, 2)))
            {
                Point dentro = BordoSinistroNota(0, 2), sbagliata = CentroTavolo(4, 2);
                Assert.That(EVerde(bmp.GetPixel(dentro.X, dentro.Y)), Is.True);
                Assert.That(EVerde(bmp.GetPixel(sbagliata.X, sbagliata.Y)), Is.False);
            }
        }

        [Test]
        public void DueColoriNellaStessaRiga_SonoEntrambiDisegnati()
        {
            using (Bitmap bmp = DisegnaTavoloConNote(StrutturaTavolo.Default, Nota(2, 0, 1, 2), Nota(2, 3, 6, 2)))
            {
                Point a = BordoSinistroNota(2, 1), b = BordoSinistroNota(2, 6);
                Assert.That(EVerde(bmp.GetPixel(a.X, a.Y)), Is.True);
                Assert.That(EVerde(bmp.GetPixel(b.X, b.Y)), Is.True);
            }
        }

        [Test]
        public void NotaOltreLaMisuraNonDisegnata()
        {
            Assert.That(StrutturaTavolo.TryCrea(3, 6, 1, 6, out StrutturaTavolo s), Is.True);
            using (Bitmap bmp = DisegnaTavoloConNote(s, Nota(1, 1, 13, 2)))
            {
                Point p = CentroTavolo(1, 13);
                Assert.That(bmp.GetPixel(p.X, p.Y).ToArgb(), Is.EqualTo(Color.FromArgb(255, 215, 215, 215).ToArgb()));
            }
        }

        [Test]
        public void NotaLungaTroncataAFineMisura()
        {
            // Misura 3/4 = 12 step: la nota dallo step 10 lunga 6 arriva al massimo allo step 11.
            Assert.That(StrutturaTavolo.TryCrea(3, 6, 1, 6, out StrutturaTavolo s), Is.True);
            using (Bitmap bmp = DisegnaTavoloConNote(s, Nota(1, 1, 10, 6)))
            {
                Point ultimo = new Point((int)(GrigliaX + 12 * CellaL) - 3, CentroTavolo(1, 11).Y), oltre = CentroTavolo(1, 12);
                Assert.That(EVerde(bmp.GetPixel(ultimo.X, ultimo.Y)), Is.True);
                Assert.That(bmp.GetPixel(oltre.X, oltre.Y).ToArgb(), Is.EqualTo(Color.FromArgb(255, 215, 215, 215).ToArgb()));
            }
        }

        [Test]
        public void EtichettaRighe_SingolarePlurale()
        {
            Assert.That(RenderGuida.EtichettaRighe(1), Is.EqualTo("1 riga"));
            Assert.That(RenderGuida.EtichettaRighe(2), Is.EqualTo("2 righe"));
            Assert.That(RenderGuida.EtichettaRighe(6), Is.EqualTo("6 righe"));
        }

        [Test]
        public void PlayheadSoloNelleRigheAttive()
        {
            using (Bitmap bmp = DisegnaTavolo(StatoStruttura(4, 2, 1, 2), new PosizioneGiro(3, 0, new[] { 1, 3, 5 }), true))
            {
                foreach (int riga in new[] { 1, 3, 5 })
                {
                    Point p = CentroTavolo(riga, 3);
                    Color c = bmp.GetPixel(p.X, p.Y);
                    Assert.That(c.R, Is.EqualTo(255), "riga " + riga);
                    Assert.That(c.B, Is.LessThan(200), "riga " + riga);
                }

                foreach (int riga in new[] { 0, 2, 4 })
                {
                    Point p = CentroTavolo(riga, 3);
                    Assert.That(bmp.GetPixel(p.X, p.Y).ToArgb(), Is.EqualTo(Bianco), "riga " + riga);
                }
            }
        }

        [Test]
        public void OverloadVecchio_TutteLeRigheAttive()
        {
            using (Bitmap bmp = Disegna(ConfigTavolo(), null, 3, true))
            {
                for (int riga = 0; riga < 6; riga++)
                {
                    Point p = CentroTavolo(riga, 3);
                    Assert.That(bmp.GetPixel(p.X, p.Y).ToArgb(), Is.Not.EqualTo(Bianco), "riga " + riga);
                }
            }
        }

        [Test]
        public void BarreDiGruppo()
        {
            using (Bitmap bmp = DisegnaTavolo(StatoStruttura(4, 2, 1, 2), new PosizioneGiro(0, 0, new[] { 0, 2, 4 }), false))
            {
                int x = (int)(GrigliaX - 10);
                int unione = (int)Math.Round(GrigliaY + CellaA);
                int stacco = (int)Math.Round(GrigliaY + 2 * CellaA);
                Assert.That(bmp.GetPixel(x, unione).ToArgb(), Is.EqualTo(Color.FromArgb(255, 70, 130, 180).ToArgb()));
                Assert.That(bmp.GetPixel(x, stacco).ToArgb(), Is.EqualTo(Bianco));
                Assert.That(bmp.GetPixel(x, stacco + 4).ToArgb(), Is.EqualTo(Color.FromArgb(255, 120, 170, 210).ToArgb()));
            }
        }

        [Test]
        public void StrutturaDelloStatoUsata()
        {
            using (Bitmap bmp = DisegnaTavolo(StatoStruttura(2, 6, 1, 6), new PosizioneGiro(0, 0, new[] { 0 }), false))
            {
                Point p = CentroTavolo(2, 9);
                Assert.That(bmp.GetPixel(p.X, p.Y).ToArgb(), Is.EqualTo(Color.FromArgb(255, 215, 215, 215).ToArgb()));
            }
        }

        [Test]
        public void FormProiettore_ConservaLaStrutturaNellaCopia()
        {
            var form = new FormProiettore(ConfigTavolo(), LayoutTavolo.Default);
            try
            {
                StatoTavolo origine = StatoStruttura(3, 2, 1, 2);
                form.Aggiorna(origine, 0, false);
                Assert.That(form.StatoCorrente.Struttura, Is.EqualTo(origine.Struttura));
            }
            finally
            {
                form.Dispose();
            }
        }
    }
}
