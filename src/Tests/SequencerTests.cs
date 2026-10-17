using System;
using System.Collections.Generic;
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
                Traccia = traccia,
                StepIniziale = stepIniziale,
                DurataStep = durataStep,
                IdClip = idClip
            };
        }

        /// <summary>Esegue i tick da 0 a <paramref name="ultimoTick"/> compresi e descrive ogni evento come "tick:traccia:clip:ON|OFF".</summary>
        private static List<string> Raccogli(Sequencer sequencer, IReadOnlyList<CartoncinoNota> note, int ultimoTick)
        {
            List<string> righe = new List<string>();
            for (int tick = 0; tick <= ultimoTick; tick++)
            {
                foreach (EventoNota evento in sequencer.Tick(note))
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

            List<EventoNota> primo = new List<EventoNota>(sequencer.Tick(notaPresente));
            List<EventoNota> secondo = new List<EventoNota>(sequencer.Tick(new List<CartoncinoNota>()));
            List<EventoNota> terzo = new List<EventoNota>(sequencer.Tick(new List<CartoncinoNota>()));

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
            sequencer.Tick(note);
            sequencer.Tick(note);

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
                IReadOnlyList<EventoNota> eventi = sequencer.Tick(new List<CartoncinoNota>());

                Assert.That(eventi, Is.Not.Null);
                Assert.That(eventi, Is.Empty);
            }
        }

        [Test]
        public void TickNullTrattatoComeListaVuota()
        {
            Sequencer sequencer = new Sequencer(new ConfigGriglia());

            IReadOnlyList<EventoNota> eventi = null;
            Assert.DoesNotThrow(() => eventi = sequencer.Tick(null));

            Assert.That(eventi, Is.Not.Null);
            Assert.That(eventi, Is.Empty);
        }

        [Test]
        public void ConfigNullLanciaArgumentNullException()
        {
            Assert.That(() => new Sequencer(null), Throws.InstanceOf<ArgumentNullException>());
        }
    }
}
