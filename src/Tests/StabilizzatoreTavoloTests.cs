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

        [Test]
        public void StrutturaCambiaDopoNFrame()
        {
            var stabilizzatore = new StabilizzatoreTavolo(framePresenza: 3);
            StrutturaTavolo x = Struttura(3, 6, 1, 6);

            Assert.That(stabilizzatore.Aggiorna(Stato(struttura: x)).Struttura, Is.EqualTo(StrutturaTavolo.Default));
            Assert.That(stabilizzatore.Aggiorna(Stato(struttura: x)).Struttura, Is.EqualTo(StrutturaTavolo.Default));
            Assert.That(stabilizzatore.Aggiorna(Stato(struttura: x)).Struttura, Is.EqualTo(x));
        }

        [Test]
        public void StrutturaSfarfallio_NonCambia()
        {
            var stabilizzatore = new StabilizzatoreTavolo(framePresenza: 3);
            StrutturaTavolo x = Struttura(4, 2, 1, 2);

            for (int frame = 1; frame <= 6; frame++)
            {
                StrutturaTavolo struttura = frame % 2 == 1 ? x : StrutturaTavolo.Default;
                StatoTavolo stato = stabilizzatore.Aggiorna(Stato(struttura: struttura));
                Assert.That(stato.Struttura, Is.EqualTo(StrutturaTavolo.Default), $"frame {frame}");
            }
        }

        [Test]
        public void TornaAlDefaultDopoNFrameDiAssenza()
        {
            var stabilizzatore = new StabilizzatoreTavolo(framePresenza: 3);
            StrutturaTavolo x = Struttura(2, 1, 1, 1);
            for (int frame = 1; frame <= 3; frame++)
            {
                stabilizzatore.Aggiorna(Stato(struttura: x));
            }

            // Due frame con il default non bastano: la struttura resta X.
            Assert.That(stabilizzatore.Aggiorna(Stato(struttura: StrutturaTavolo.Default)).Struttura, Is.EqualTo(x));
            Assert.That(stabilizzatore.Aggiorna(Stato(struttura: StrutturaTavolo.Default)).Struttura, Is.EqualTo(x));

            // Il terzo frame consecutivo con il default riporta al default.
            Assert.That(stabilizzatore.Aggiorna(Stato(struttura: StrutturaTavolo.Default)).Struttura, Is.EqualTo(StrutturaTavolo.Default));
        }

        [Test]
        public void CandidataCheCambia_RipartiDaUno()
        {
            var stabilizzatore = new StabilizzatoreTavolo(framePresenza: 3);
            StrutturaTavolo x = Struttura(3, 6, 1, 6);
            StrutturaTavolo y = Struttura(4, 2, 1, 2);

            Assert.That(stabilizzatore.Aggiorna(Stato(struttura: x)).Struttura, Is.EqualTo(StrutturaTavolo.Default));
            Assert.That(stabilizzatore.Aggiorna(Stato(struttura: y)).Struttura, Is.EqualTo(StrutturaTavolo.Default));
            Assert.That(stabilizzatore.Aggiorna(Stato(struttura: y)).Struttura, Is.EqualTo(StrutturaTavolo.Default));

            // Solo al terzo Y consecutivo la struttura diventa Y: X non ha contribuito al conteggio.
            Assert.That(stabilizzatore.Aggiorna(Stato(struttura: y)).Struttura, Is.EqualTo(y));
        }

        [Test]
        public void CandidataCheCambia_ContatoreRipartiDaUnoAnchePoiCheXAvevaDueFrame()
        {
            var stabilizzatore = new StabilizzatoreTavolo(framePresenza: 3);
            StrutturaTavolo x = Struttura(3, 6, 1, 6);
            StrutturaTavolo y = Struttura(4, 2, 1, 2);

            var output = new List<StrutturaTavolo>();
            foreach (StrutturaTavolo struttura in new[] { x, x, y, y, y })
            {
                output.Add(stabilizzatore.Aggiorna(Stato(struttura: struttura)).Struttura);
            }

            // X ha contribuito con due frame, ma Y riparte da uno: Y compare solo al terzo Y consecutivo.
            Assert.That(output[0], Is.EqualTo(StrutturaTavolo.Default));
            Assert.That(output[1], Is.EqualTo(StrutturaTavolo.Default));
            Assert.That(output[2], Is.EqualTo(StrutturaTavolo.Default));
            Assert.That(output[3], Is.EqualTo(StrutturaTavolo.Default));
            Assert.That(output[4], Is.EqualTo(y));
        }

        [Test]
        public void CandidataCheCambia_NonEreditaIContatoriPrecedenti()
        {
            var stabilizzatore = new StabilizzatoreTavolo(framePresenza: 4);
            StrutturaTavolo x = Struttura(3, 6, 1, 6);
            StrutturaTavolo y = Struttura(4, 2, 1, 2);

            var output = new List<StrutturaTavolo>();
            foreach (StrutturaTavolo struttura in new[] { x, x, x, y, y, y, y })
            {
                output.Add(stabilizzatore.Aggiorna(Stato(struttura: struttura)).Struttura);
            }

            // X ha contribuito con tre frame; Y deve comunque comparire solo al quarto Y consecutivo (settimo frame).
            for (int frame = 0; frame < 6; frame++)
            {
                Assert.That(output[frame], Is.EqualTo(StrutturaTavolo.Default), $"frame {frame + 1}");
            }

            Assert.That(output[6], Is.EqualTo(y));
        }

        [Test]
        public void FramePresenzaUnoCambiaSubito()
        {
            var stabilizzatore = new StabilizzatoreTavolo(framePresenza: 1);
            StrutturaTavolo x = Struttura(3, 6, 1, 6);

            Assert.That(stabilizzatore.Aggiorna(Stato(struttura: x)).Struttura, Is.EqualTo(x));
        }

        [Test]
        public void NoteEComandiInvariati_AlCambioDiStruttura()
        {
            var stabilizzatore = new StabilizzatoreTavolo(framePresenza: 3);
            StrutturaTavolo x = Struttura(3, 6, 1, 6);
            CartoncinoNota nota = Nota(3, 2);
            ComandoRilevato play = new ComandoRilevato { Tipo = TipoComando.Play, Parametro = 0 };

            stabilizzatore.Aggiorna(Stato(note: new[] { nota }, comandi: new[] { play }, struttura: x));
            stabilizzatore.Aggiorna(Stato(note: new[] { nota }, comandi: new[] { play }, struttura: x));
            StatoTavolo terzo = stabilizzatore.Aggiorna(Stato(note: new[] { nota }, comandi: new[] { play }, struttura: x));

            Assert.That(terzo.Struttura, Is.EqualTo(x));
            Assert.That(terzo.Note, Has.Count.EqualTo(1));
            Assert.That(terzo.Note[0].Chiave, Is.EqualTo("12:3:0:2"));
            Assert.That(terzo.Comandi, Has.Count.EqualTo(1));

            // Il cambio di struttura non fa riscattare né duplicare il comando già esposto.
            StatoTavolo dopoCambio = stabilizzatore.Aggiorna(Stato(note: new[] { nota }, comandi: new[] { play }, struttura: StrutturaTavolo.Default));
            Assert.That(dopoCambio.Note, Has.Count.EqualTo(1));
            Assert.That(dopoCambio.Comandi, Is.Empty);
        }

        private static StrutturaTavolo Struttura(int battiti, int righeCollegate, int aInizio, int aFine)
        {
            Assert.That(StrutturaTavolo.TryCrea(battiti, righeCollegate, aInizio, aFine, out StrutturaTavolo struttura), Is.True);
            return struttura;
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
            IReadOnlyList<ErroreTavolo> errori = null,
            StrutturaTavolo struttura = null)
        {
            return new StatoTavolo(
                note ?? new List<CartoncinoNota>(),
                comandi ?? new List<ComandoRilevato>(),
                errori ?? new List<ErroreTavolo>(),
                struttura);
        }

    }
}
