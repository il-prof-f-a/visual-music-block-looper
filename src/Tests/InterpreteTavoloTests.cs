using NUnit.Framework;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class InterpreteTavoloTests
    {
        // Griglia nell'area (0,0,1,0.8) con 6 tracce e 16 step; la zona comandi occupa la fascia sotto.
        private static readonly RettangoloTavolo ZonaComandi = new RettangoloTavolo(0, 0.8, 1, 0.2);
        private const double AltezzaTraccia = 0.8 / 6;
        private const double LarghezzaStep = 1.0 / 16;
        private const double AltezzaCartoncino = 0.05;

        [Test]
        public void NotaValida()
        {
            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;N;12", ColoreTraccia.Verde, Cella(3, 2))
            });

            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Comandi, Is.Empty);
            Assert.That(stato.Note, Has.Count.EqualTo(1));

            CartoncinoNota nota = stato.Note[0];
            Assert.That(nota.Traccia, Is.EqualTo(3));
            Assert.That(nota.StepIniziale, Is.EqualTo(2));
            Assert.That(nota.DurataStep, Is.EqualTo(1));
            Assert.That(nota.IdClip, Is.EqualTo(12));
            Assert.That(nota.Colore, Is.EqualTo(ColoreTraccia.Verde));
            Assert.That(nota.Chiave, Is.EqualTo("12:3:2"));
        }

        [Test]
        public void ColoreIncoerente()
        {
            RettangoloTavolo area = Cella(3, 2);

            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;N;12", ColoreTraccia.Rosso, area)
            });

            Assert.That(stato.Note, Is.Empty);
            Assert.That(stato.Comandi, Is.Empty);
            Assert.That(stato.Errori, Has.Count.EqualTo(1));
            Assert.That(stato.Errori[0].Tipo, Is.EqualTo(TipoErrore.ColoreIncoerente));
            Assert.That(stato.Errori[0].Area.X, Is.EqualTo(area.X));
            Assert.That(stato.Errori[0].Area.Y, Is.EqualTo(area.Y));
        }

        [Test]
        public void ColoreMancante()
        {
            RettangoloTavolo area = Cella(3, 2);

            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;N;12", null, area)
            });

            Assert.That(stato.Note, Is.Empty);
            Assert.That(stato.Comandi, Is.Empty);
            Assert.That(stato.Errori, Has.Count.EqualTo(1));
            Assert.That(stato.Errori[0].Tipo, Is.EqualTo(TipoErrore.ColoreMancante));
            Assert.That(stato.Errori[0].Area.X, Is.EqualTo(area.X));
            Assert.That(stato.Errori[0].Area.Y, Is.EqualTo(area.Y));
        }

        [Test]
        public void ComandoInZonaComandi()
        {
            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;C;PLAY", null, AreaInZonaComandi())
            });

            Assert.That(stato.Note, Is.Empty);
            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Comandi, Has.Count.EqualTo(1));
            Assert.That(stato.Comandi[0].Tipo, Is.EqualTo(TipoComando.Play));
            Assert.That(stato.Comandi[0].Parametro, Is.EqualTo(0));
        }

        [Test]
        public void NotaInZonaComandiRifiutata()
        {
            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;N;12", ColoreTraccia.Verde, AreaInZonaComandi())
            });

            Assert.That(stato.Note, Is.Empty);
            Assert.That(stato.Comandi, Is.Empty);
            Assert.That(stato.Errori, Has.Count.EqualTo(1));
            Assert.That(stato.Errori[0].Tipo, Is.EqualTo(TipoErrore.PayloadNonValido));
        }

        [Test]
        public void ComandoFuoriZonaComandiRifiutato()
        {
            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;C;PLAY", null, Cella(3, 2))
            });

            Assert.That(stato.Note, Is.Empty);
            Assert.That(stato.Comandi, Is.Empty);
            Assert.That(stato.Errori, Has.Count.EqualTo(1));
            Assert.That(stato.Errori[0].Tipo, Is.EqualTo(TipoErrore.PayloadNonValido));
        }

        [Test]
        public void PayloadNonValido()
        {
            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;N;abc", ColoreTraccia.Verde, Cella(3, 2))
            });

            Assert.That(stato.Note, Is.Empty);
            Assert.That(stato.Comandi, Is.Empty);
            Assert.That(stato.Errori, Has.Count.EqualTo(1));
            Assert.That(stato.Errori[0].Tipo, Is.EqualTo(TipoErrore.PayloadNonValido));
        }

        [Test]
        public void SovrappostoStessaCella()
        {
            // La prima per Area.X (step 4-5) resta; la seconda (step 5) si sovrappone e va in errore.
            RettangoloTavolo primaArea = Cella(2, 4, 2);
            RettangoloTavolo secondaArea = Cella(2, 5, 1);

            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;N;2", ColoreTraccia.Giallo, secondaArea),
                Rilevato("VMBL1;N;1", ColoreTraccia.Giallo, primaArea)
            });

            Assert.That(stato.Comandi, Is.Empty);
            Assert.That(stato.Note, Has.Count.EqualTo(1));
            Assert.That(stato.Note[0].IdClip, Is.EqualTo(1));
            Assert.That(stato.Note[0].StepIniziale, Is.EqualTo(4));
            Assert.That(stato.Note[0].DurataStep, Is.EqualTo(2));
            Assert.That(stato.Errori, Has.Count.EqualTo(1));
            Assert.That(stato.Errori[0].Tipo, Is.EqualTo(TipoErrore.Sovrapposto));
            Assert.That(stato.Errori[0].Area.X, Is.EqualTo(secondaArea.X));
        }

        [Test]
        public void FuoriGriglia()
        {
            RettangoloTavolo area = new RettangoloTavolo(1.2, Cella(3, 0).Y, LarghezzaStep, AltezzaCartoncino);

            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;N;12", ColoreTraccia.Verde, area)
            });

            Assert.That(stato.Note, Is.Empty);
            Assert.That(stato.Comandi, Is.Empty);
            Assert.That(stato.Errori, Has.Count.EqualTo(1));
            Assert.That(stato.Errori[0].Tipo, Is.EqualTo(TipoErrore.FuoriGriglia));
            Assert.That(stato.Errori[0].Area.X, Is.EqualTo(area.X));
        }

        [Test]
        public void InputNull_StatoVuoto()
        {
            StatoTavolo stato = null;

            Assert.DoesNotThrow(() => stato = CreaInterprete().Interpreta(null));

            Assert.That(stato.Note, Is.Not.Null.And.Empty);
            Assert.That(stato.Comandi, Is.Not.Null.And.Empty);
            Assert.That(stato.Errori, Is.Not.Null.And.Empty);
        }

        [Test]
        public void ElementoNull_Ignorato()
        {
            StatoTavolo stato = null;

            Assert.DoesNotThrow(() => stato = CreaInterprete().Interpreta(new CartoncinoRilevato[]
            {
                null,
                Rilevato("VMBL1;N;12", ColoreTraccia.Verde, Cella(3, 2)),
                null
            }));

            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Comandi, Is.Empty);
            Assert.That(stato.Note, Has.Count.EqualTo(1));
            Assert.That(stato.Note[0].IdClip, Is.EqualTo(12));
        }

        [Test]
        public void SovrapposizioneParziale()
        {
            // Nota [2,5) e nota [4,7) sulla stessa traccia: la seconda si sovrappone solo in parte.
            RettangoloTavolo primaArea = Cella(2, 2, 3);
            RettangoloTavolo secondaArea = Cella(2, 4, 3);

            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;N;1", ColoreTraccia.Giallo, primaArea),
                Rilevato("VMBL1;N;2", ColoreTraccia.Giallo, secondaArea)
            });

            Assert.That(stato.Note, Has.Count.EqualTo(1));
            Assert.That(stato.Note[0].IdClip, Is.EqualTo(1));
            Assert.That(stato.Errori, Has.Count.EqualTo(1));
            Assert.That(stato.Errori[0].Tipo, Is.EqualTo(TipoErrore.Sovrapposto));
            Assert.That(stato.Errori[0].Area.X, Is.EqualTo(secondaArea.X));
        }

        [Test]
        public void StessiStepSuTracceDiverse_Accettati()
        {
            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;N;1", ColoreTraccia.Arancione, Cella(1, 4)),
                Rilevato("VMBL1;N;2", ColoreTraccia.Blu, Cella(4, 4))
            });

            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Note, Has.Count.EqualTo(2));
            Assert.That(stato.Note[0].Traccia, Is.EqualTo(1));
            Assert.That(stato.Note[0].StepIniziale, Is.EqualTo(4));
            Assert.That(stato.Note[1].Traccia, Is.EqualTo(4));
            Assert.That(stato.Note[1].StepIniziale, Is.EqualTo(4));
        }

        private static InterpreteTavolo CreaInterprete()
        {
            var config = new ConfigGriglia { Area = new RettangoloTavolo(0, 0, 1, 0.8) };
            return new InterpreteTavolo(new Griglia(config), ZonaComandi);
        }

        private static RettangoloTavolo Cella(int traccia, int step, int durataStep = 1)
        {
            return new RettangoloTavolo(
                step * LarghezzaStep,
                (traccia + 0.5) * AltezzaTraccia - AltezzaCartoncino / 2,
                durataStep * LarghezzaStep,
                AltezzaCartoncino);
        }

        private static RettangoloTavolo AreaInZonaComandi()
        {
            return new RettangoloTavolo(0.1, 0.85, LarghezzaStep, AltezzaCartoncino);
        }

        private static CartoncinoRilevato Rilevato(string payload, ColoreTraccia? colore, RettangoloTavolo area)
        {
            return new CartoncinoRilevato { Payload = payload, Colore = colore, Area = area };
        }
    }
}
