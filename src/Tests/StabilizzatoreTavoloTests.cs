using System;
using System.Collections.Generic;
using NUnit.Framework;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class StabilizzatoreTavoloTests
    {
        [Test]
        public void NotaAppareDopoNFrame()
        {
            var stabilizzatore = new StabilizzatoreTavolo();
            CartoncinoNota nota = Nota(3, 2);

            StatoTavolo fotogramma1 = stabilizzatore.Aggiorna(Stato(note: new[] { nota }));
            StatoTavolo fotogramma2 = stabilizzatore.Aggiorna(Stato(note: new[] { nota }));
            StatoTavolo fotogramma3 = stabilizzatore.Aggiorna(Stato(note: new[] { nota }));

            Assert.That(fotogramma1.Note, Is.Empty);
            Assert.That(fotogramma2.Note, Is.Empty);
            Assert.That(fotogramma3.Note, Has.Count.EqualTo(1));
            Assert.That(fotogramma3.Note[0].Chiave, Is.EqualTo("12:3:0:2"));
        }

        [Test]
        public void NotaPersisteAlTremolio()
        {
            var stabilizzatore = new StabilizzatoreTavolo();
            CartoncinoNota nota = Nota(3, 2);
            StabilizzaNota(stabilizzatore, nota);

            // Assente per 4 frame consecutivi (meno di 5): resta nell'output.
            for (int frame = 1; frame <= 4; frame++)
            {
                StatoTavolo assente = stabilizzatore.Aggiorna(Stato());
                Assert.That(assente.Note, Has.Count.EqualTo(1), $"frame di assenza {frame}");
                Assert.That(assente.Note[0].Chiave, Is.EqualTo("12:3:0:2"));
            }

            // Ricomparsa: il contatore di assenza si azzera.
            StatoTavolo ricomparsa = stabilizzatore.Aggiorna(Stato(note: new[] { nota }));
            Assert.That(ricomparsa.Note, Has.Count.EqualTo(1));

            // Di nuovo 4 frame di assenza: ancora presente.
            for (int frame = 1; frame <= 4; frame++)
            {
                StatoTavolo assente = stabilizzatore.Aggiorna(Stato());
                Assert.That(assente.Note, Has.Count.EqualTo(1), $"secondo ciclo, frame di assenza {frame}");
            }
        }

        [Test]
        public void NotaSparisceDopoMFrame()
        {
            var stabilizzatore = new StabilizzatoreTavolo();
            CartoncinoNota nota = Nota(3, 2);
            StabilizzaNota(stabilizzatore, nota);

            for (int frame = 1; frame <= 4; frame++)
            {
                stabilizzatore.Aggiorna(Stato());
            }

            // Quinta assenza consecutiva: la nota viene rimossa.
            StatoTavolo quintoFrame = stabilizzatore.Aggiorna(Stato());
            Assert.That(quintoFrame.Note, Is.Empty);

            // Dopo la rimozione lo stato è dimenticato: servono di nuovo 3 frame di presenza.
            Assert.That(stabilizzatore.Aggiorna(Stato(note: new[] { nota })).Note, Is.Empty);
            Assert.That(stabilizzatore.Aggiorna(Stato(note: new[] { nota })).Note, Is.Empty);
            Assert.That(stabilizzatore.Aggiorna(Stato(note: new[] { nota })).Note, Has.Count.EqualTo(1));
        }

        [Test]
        public void ComandoScattaUnaSolaVolta()
        {
            var stabilizzatore = new StabilizzatoreTavolo();
            ComandoRilevato play = new ComandoRilevato { Tipo = TipoComando.Play, Parametro = 0 };

            var comandiPerFrame = new List<int>();
            for (int frame = 1; frame <= 6; frame++)
            {
                StatoTavolo stato = stabilizzatore.Aggiorna(Stato(comandi: new[] { play }));
                comandiPerFrame.Add(stato.Comandi.Count);
            }

            // Esposto solo al frame 3, quando diventa stabile; poi tenuto internamente.
            Assert.That(comandiPerFrame, Is.EqualTo(new[] { 0, 0, 1, 0, 0, 0 }));
        }

        [Test]
        public void AggiornaNullTrattaLoStatoComeVuoto()
        {
            var stabilizzatore = new StabilizzatoreTavolo();
            CartoncinoNota nota = Nota(3, 2);
            StabilizzaNota(stabilizzatore, nota);

            StatoTavolo vuoto = null;
            Assert.DoesNotThrow(() => vuoto = stabilizzatore.Aggiorna(null));

            // Null significa nessun candidato presente: la nota stabile entra nel conteggio di assenza.
            Assert.That(vuoto.Note, Has.Count.EqualTo(1));
            Assert.That(vuoto.Comandi, Is.Empty);
            Assert.That(vuoto.Errori, Is.Empty);
        }

        [Test]
        public void ErroriPassanoSenzaFiltro()
        {
            var stabilizzatore = new StabilizzatoreTavolo();
            var errore = new ErroreTavolo
            {
                Tipo = TipoErrore.Sovrapposto,
                Area = new RettangoloTavolo(0.1, 0.1, 0.05, 0.05)
            };

            for (int frame = 1; frame <= 3; frame++)
            {
                StatoTavolo stato = stabilizzatore.Aggiorna(Stato(errori: new[] { errore }));
                Assert.That(stato.Errori, Has.Count.EqualTo(1), $"frame {frame}");
                Assert.That(stato.Errori[0], Is.SameAs(errore));
            }
        }

        [Test]
        public void ParametriNonValidiLanciano()
        {
            Assert.That(() => new StabilizzatoreTavolo(framePresenza: 0),
                Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new StabilizzatoreTavolo(frameAssenza: 0),
                Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        private static void StabilizzaNota(StabilizzatoreTavolo stabilizzatore, CartoncinoNota nota)
        {
            for (int frame = 1; frame <= 3; frame++)
            {
                stabilizzatore.Aggiorna(Stato(note: new[] { nota }));
            }
        }

        private static CartoncinoNota Nota(int traccia, int stepIniziale)
        {
            return new CartoncinoNota
            {
                Traccia = traccia,
                StepIniziale = stepIniziale,
                DurataStep = 1,
                IdClip = 12,
                Colore = ColoreTraccia.Verde
            };
        }

        private static StatoTavolo Stato(
            IReadOnlyList<CartoncinoNota> note = null,
            IReadOnlyList<ComandoRilevato> comandi = null,
            IReadOnlyList<ErroreTavolo> errori = null)
        {
            return new StatoTavolo(
                note ?? new List<CartoncinoNota>(),
                comandi ?? new List<ComandoRilevato>(),
                errori ?? new List<ErroreTavolo>());
        }

    }
}
