using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class SequencerTests
    {
        private static CartoncinoNota Nota(int traccia, int stepIniziale, int durataStep, int idClip)
        {
            return new CartoncinoNota
            {
                Riga = traccia,
                Traccia = traccia,
                StepIniziale = stepIniziale,
                DurataStep = durataStep,
                IdClip = idClip
            };
        }

        private static readonly StrutturaTavolo UnaRiga = Struttura(4, 1, 1, 1);

        private static StrutturaTavolo Struttura(int battiti, int righeCollegate, int a, int b)
        {
            Assert.That(StrutturaTavolo.TryCrea(battiti, righeCollegate, a, b, out StrutturaTavolo struttura), Is.True);
            return struttura;
        }

        private static CartoncinoNota NotaInRiga(int riga, int traccia, int step, int durata, int idClip)
        {
            CartoncinoNota nota = Nota(traccia, step, durata, idClip);
            nota.Riga = riga;
            return nota;
        }

        /// <summary>Esegue i tick da 0 a <paramref name="ultimoTick"/> compresi e descrive ogni evento come "tick:traccia:clip:ON|OFF".</summary>
        private static List<string> Raccogli(Sequencer sequencer, IReadOnlyList<CartoncinoNota> note, int ultimoTick, StrutturaTavolo struttura = null)
        {
            List<string> righe = new List<string>();
            for (int tick = 0; tick <= ultimoTick; tick++)
            {
                foreach (EventoNota evento in sequencer.Tick(note, struttura ?? UnaRiga))
                {
                    righe.Add($"{tick}:{evento.Traccia}:{evento.IdClip}:{(evento.NoteOn ? "ON" : "OFF")}");
                }
            }

            return righe;
        }

        [Test]
        public void NoteOnAlloStepGiusto()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());
            List<CartoncinoNota> note = new List<CartoncinoNota> { Nota(0, 3, 1, 7) };

            List<string> righe = Raccogli(sequencer, note, 18);

            Assert.That(righe, Is.EqualTo(new[] { "18:0:7:ON" }));
        }

        [Test]
        public void NoteOffDopoDurata()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());
            List<CartoncinoNota> note = new List<CartoncinoNota> { Nota(0, 0, 2, 1) };

            List<string> righe = Raccogli(sequencer, note, 24);

            Assert.That(righe, Is.EqualTo(new[] { "0:0:1:ON", "12:0:1:OFF" }));
        }

        [Test]
        public void LoopRipartiDaZero()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());
            List<CartoncinoNota> note = new List<CartoncinoNota> { Nota(0, 0, 1, 1) };

            List<string> righe = Raccogli(sequencer, note, 96);

            Assert.That(righe, Is.EqualTo(new[] { "0:0:1:ON", "6:0:1:OFF", "96:0:1:ON" }));
            Assert.That(sequencer.StepCorrente, Is.EqualTo(0));
        }

        [Test]
        public void DurataTroncataFineLoop()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());
            List<CartoncinoNota> note = new List<CartoncinoNota>
            {
                Nota(1, 15, 4, 2),
                Nota(0, 0, 1, 3)
            };

            List<string> righe = Raccogli(sequencer, note, 96);

            // La nota allo step 15 con durata 4 viene troncata a 1: il note-off precede il note-on del nuovo loop.
            Assert.That(righe, Is.EqualTo(new[]
            {
                "0:0:3:ON",
                "6:0:3:OFF",
                "90:1:2:ON",
                "96:1:2:OFF",
                "96:0:3:ON"
            }));
        }

        [Test]
        public void NotaRimossaMentreSuona()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());
            List<CartoncinoNota> notaPresente = new List<CartoncinoNota> { Nota(0, 0, 4, 1) };

            List<EventoNota> primo = new List<EventoNota>(sequencer.Tick(notaPresente, UnaRiga));
            List<EventoNota> secondo = new List<EventoNota>(sequencer.Tick(new List<CartoncinoNota>(), UnaRiga));
            List<EventoNota> terzo = new List<EventoNota>(sequencer.Tick(new List<CartoncinoNota>(), UnaRiga));

            Assert.That(primo.Count, Is.EqualTo(1));
            Assert.That(primo[0].NoteOn, Is.True);
            Assert.That(secondo.Count, Is.EqualTo(1));
            Assert.That(secondo[0].NoteOn, Is.False);
            Assert.That(secondo[0].IdClip, Is.EqualTo(1));
            Assert.That(terzo, Is.Empty);
        }

        [Test]
        public void FermaSpegneTutto()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());
            List<CartoncinoNota> note = new List<CartoncinoNota>
            {
                Nota(1, 0, 16, 5),
                Nota(0, 0, 16, 4)
            };
            sequencer.Tick(note, UnaRiga);
            sequencer.Tick(note, UnaRiga);

            sequencer.Ferma(out IReadOnlyList<EventoNota> noteOffPendenti);

            Assert.That(noteOffPendenti.Count, Is.EqualTo(2));
            Assert.That(noteOffPendenti[0].Traccia, Is.EqualTo(0));
            Assert.That(noteOffPendenti[0].IdClip, Is.EqualTo(4));
            Assert.That(noteOffPendenti[0].NoteOn, Is.False);
            Assert.That(noteOffPendenti[1].Traccia, Is.EqualTo(1));
            Assert.That(noteOffPendenti[1].IdClip, Is.EqualTo(5));
            Assert.That(noteOffPendenti[1].NoteOn, Is.False);
            Assert.That(sequencer.StepCorrente, Is.EqualTo(0));

            // Dopo il stop il contatore riparte da zero: il primo tick è di nuovo un boundary con note-on.
            List<string> dopoStop = Raccogli(sequencer, note, 0);
            Assert.That(dopoStop, Is.EqualTo(new[] { "0:0:4:ON", "0:1:5:ON" }));
        }

        [Test]
        public void NessunaNotaNessunEvento()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());

            for (int tick = 0; tick <= 30; tick++)
            {
                IReadOnlyList<EventoNota> eventi = sequencer.Tick(new List<CartoncinoNota>(), UnaRiga);

                Assert.That(eventi, Is.Not.Null);
                Assert.That(eventi, Is.Empty);
            }
        }

        [Test]
        public void TickNullTrattatoComeListaVuota()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());

            IReadOnlyList<EventoNota> eventi = null;
            Assert.DoesNotThrow(() => eventi = sequencer.Tick(null, UnaRiga));

            Assert.That(eventi, Is.Not.Null);
            Assert.That(eventi, Is.Empty);
        }

        [Test]
        public void GiroDiSeiMisure_NotaInRiga1SuonaAlTick96()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());
            List<CartoncinoNota> note = new List<CartoncinoNota> { NotaInRiga(1, 0, 0, 2, 1) };

            List<string> righe = Raccogli(sequencer, note, 672, StrutturaTavolo.Default);

            Assert.That(righe, Is.EqualTo(new[] { "96:0:1:ON", "108:0:1:OFF", "672:0:1:ON" }));
        }

        [Test]
        public void GruppiInContemporanea()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());
            List<CartoncinoNota> note = new List<CartoncinoNota>
            {
                NotaInRiga(0, 0, 0, 1, 1),
                NotaInRiga(3, 1, 0, 1, 2),
                NotaInRiga(1, 2, 0, 1, 3),
                NotaInRiga(4, 3, 0, 1, 4)
            };

            List<string> righe = Raccogli(sequencer, note, 96, Struttura(4, 3, 1, 3));

            Assert.That(righe, Is.EqualTo(new[]
            {
                "0:0:1:ON", "0:1:2:ON", "6:0:1:OFF", "6:1:2:OFF",
                "96:2:3:ON", "96:3:4:ON"
            }));
        }

        [Test]
        public void AbRiduceIlGiro()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());
            List<CartoncinoNota> note = new List<CartoncinoNota>
            {
                NotaInRiga(0, 0, 0, 1, 10),
                NotaInRiga(1, 1, 0, 1, 11),
                NotaInRiga(2, 2, 0, 1, 12)
            };

            List<string> righe = Raccogli(sequencer, note, 192, Struttura(4, 3, 2, 3));

            Assert.That(righe, Is.EqualTo(new[]
            {
                "0:1:11:ON", "6:1:11:OFF",
                "96:2:12:ON", "102:2:12:OFF",
                "192:1:11:ON"
            }));
        }

        [Test]
        public void TreQuartiTroncaAFineMisura()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());
            List<CartoncinoNota> note = new List<CartoncinoNota> { Nota(0, 0, 20, 1) };

            List<string> righe = Raccogli(sequencer, note, 72, Struttura(3, 1, 1, 1));

            Assert.That(righe, Is.EqualTo(new[] { "0:0:1:ON", "72:0:1:OFF", "72:0:1:ON" }));
        }

        [Test]
        public void DueQuarti()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());
            List<CartoncinoNota> note = new List<CartoncinoNota> { Nota(0, 6, 4, 1) };

            List<string> righe = Raccogli(sequencer, note, 48, Struttura(2, 1, 1, 1));

            Assert.That(righe, Is.EqualTo(new[] { "36:0:1:ON", "48:0:1:OFF" }));
        }

        [Test]
        public void CambioStrutturaSpegneLeNote()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());
            List<CartoncinoNota> note = new List<CartoncinoNota> { Nota(1, 0, 8, 5), Nota(0, 0, 8, 4) };
            sequencer.Tick(note, UnaRiga);

            IReadOnlyList<EventoNota> eventi = null;
            Assert.DoesNotThrow(() => eventi = sequencer.Tick(note, Struttura(3, 1, 1, 1)));

            Assert.That(eventi.Select(e => $"{e.Traccia}:{e.IdClip}:{e.NoteOn}"), Is.EqualTo(new[] { "0:4:False", "1:5:False" }));

            // Nessuna nota bloccata: a fine corsa non arrivano altri note-off.
            Assert.That(Raccogli(sequencer, new List<CartoncinoNota>(), 100, Struttura(3, 1, 1, 1)), Is.Empty);
        }

        [Test]
        public void CambioStrutturaSuTickNonDiConfineSpegneComunque()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());
            List<CartoncinoNota> note = new List<CartoncinoNota> { Nota(0, 0, 8, 4) };
            sequencer.Tick(note, UnaRiga);

            IReadOnlyList<EventoNota> eventi = sequencer.Tick(note, Struttura(2, 1, 1, 1));

            Assert.That(eventi.Count, Is.EqualTo(1));
            Assert.That(eventi[0].NoteOn, Is.False);
        }

        [Test]
        public void StessaStrutturaRipetutaNonSpegne()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());
            List<CartoncinoNota> note = new List<CartoncinoNota> { Nota(0, 0, 8, 4) };
            sequencer.Tick(note, UnaRiga);

            IReadOnlyList<EventoNota> eventi = sequencer.Tick(note, Struttura(4, 1, 1, 1));

            Assert.That(eventi, Is.Empty);
        }

        [Test]
        public void NotaInRigaNonAttivaNonSuonaMai()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());
            List<CartoncinoNota> note = new List<CartoncinoNota> { NotaInRiga(5, 0, 0, 1, 1) };

            // Default: la riga 5 e attiva solo dalla misura 5 (tick 480).
            List<string> righe = Raccogli(sequencer, note, 479, StrutturaTavolo.Default);

            Assert.That(righe, Is.Empty);
        }

        [Test]
        public void DueColoriNellaStessaRigaSuonanoInsieme()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());
            List<CartoncinoNota> note = new List<CartoncinoNota>
            {
                NotaInRiga(0, 2, 0, 1, 8),
                NotaInRiga(0, 1, 0, 1, 7)
            };

            List<string> righe = Raccogli(sequencer, note, 0, StrutturaTavolo.Default);

            Assert.That(righe, Is.EqualTo(new[] { "0:1:7:ON", "0:2:8:ON" }));
        }

        [Test]
        public void PosizioneEsposta()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());
            StrutturaTavolo struttura = Struttura(4, 3, 1, 3);
            Assert.That(sequencer.Posizione.StepInMisura, Is.EqualTo(0));
            Assert.That(sequencer.Posizione.RigheAttive, Is.EqualTo(new[] { 0 }));

            for (int tick = 0; tick <= 100; tick++)
            {
                sequencer.Tick(null, struttura);
                if (tick == 30)
                {
                    Assert.That(sequencer.Posizione.StepInMisura, Is.EqualTo(5));
                    Assert.That(sequencer.Posizione.RigheAttive, Is.EqualTo(new[] { 0, 3 }));
                    Assert.That(sequencer.StepCorrente, Is.EqualTo(5));
                }
            }

            Assert.That(sequencer.Posizione.StepInMisura, Is.EqualTo(0));
            Assert.That(sequencer.Posizione.MisuraNelGiro, Is.EqualTo(1));
            Assert.That(sequencer.Posizione.RigheAttive, Is.EqualTo(new[] { 1, 4 }));
        }

        [Test]
        public void FermaAzzeraLaPosizione()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());
            for (int tick = 0; tick <= 100; tick++)
            {
                sequencer.Tick(null, StrutturaTavolo.Default);
            }

            sequencer.Ferma(out IReadOnlyList<EventoNota> _);

            Assert.That(sequencer.Posizione.StepInMisura, Is.EqualTo(0));
            Assert.That(sequencer.Posizione.MisuraNelGiro, Is.EqualTo(0));
            Assert.That(sequencer.Posizione.RigheAttive, Is.EqualTo(new[] { 0 }));
            Assert.That(sequencer.StepCorrente, Is.EqualTo(0));
        }

        [Test]
        public void ConfigNullLanciaArgumentNullException()
        {
            Assert.That(() => new Sequencer(null), Throws.InstanceOf<ArgumentNullException>());
        }
    }
}
