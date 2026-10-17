using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class SmokeApplicazioneTests
    {
        private sealed class UscitaMidiFinta : IUscitaMidi
        {
            public List<string> Registro { get; } = new List<string>();

            public void NotaOn(int canale, int nota, int velocity) => Registro.Add($"NotaOn:{canale}:{nota}:{velocity}");

            public void NotaOff(int canale, int nota) => Registro.Add($"NotaOff:{canale}:{nota}");

            public void Clock() => Registro.Add("Clock");

            public void Start() => Registro.Add("Start");

            public void Stop() => Registro.Add("Stop");

            public void Continue() => Registro.Add("Continue");
        }

        private sealed class MappaturaFinta : IMappaturaMidi
        {
            public int Canale(int traccia) => traccia + 1;

            public int Nota(int idClip) => 36 + idClip;

            public int Velocity => 100;
        }

        private sealed class RilevatoreFinto : IRilevatoreCartoncini
        {
            public IReadOnlyList<CartoncinoRilevato> Cartoncini { get; set; } = new List<CartoncinoRilevato>();

            public Exception Eccezione { get; set; }

            public IReadOnlyList<CartoncinoRilevato> Rileva()
            {
                if (Eccezione != null)
                {
                    throw Eccezione;
                }

                return Cartoncini;
            }
        }

        private static ConfigGriglia Config()
        {
            return new ConfigGriglia { Area = new RettangoloTavolo(0.02, 0.04, 0.80, 0.92) };
        }

        private static CartoncinoRilevato NotaTraccia0Step0Durata2()
        {
            double larghezzaStep = 0.80 / 16;
            double centroY = 0.04 + 0.92 / 12;
            return new CartoncinoRilevato
            {
                Payload = "VMBL1;N;12",
                Colore = ColoreTraccia.Rosso,
                Area = new RettangoloTavolo(0.02, centroY - 0.03, 2 * larghezzaStep, 0.06)
            };
        }

        private static CartoncinoRilevato Comando(string nome)
        {
            return new CartoncinoRilevato
            {
                Payload = "VMBL1;C;" + nome,
                Area = new RettangoloTavolo(0.88, 0.50, 0.06, 0.06)
            };
        }

        private static void Passi(Compositore compositore, int quanti)
        {
            for (int i = 0; i < quanti; i++)
            {
                compositore.Passo();
            }
        }

        [Test]
        public void Smoke_PartenzaConFinti_EmetteNoteAttese()
        {
            var rilevatore = new RilevatoreFinto
            {
                Cartoncini = new[] { NotaTraccia0Step0Durata2(), Comando("PLAY") }
            };
            var uscita = new UscitaMidiFinta();
            var compositore = new Compositore(rilevatore, uscita, null, new MappaturaFinta(), Config());

            Passi(compositore, 3);
            for (int i = 0; i < 13; i++)
            {
                compositore.Motore.TickInterno();
            }

            // Start, poi 13 tick: la nota inizia al tick 0 e termina al tick 12 (tredicesima chiamata).
            var attesi = new List<string> { "Start", "Clock", "NotaOn:1:48:100" };
            attesi.AddRange(Enumerable.Repeat("Clock", 11));
            attesi.Add("Clock");
            attesi.Add("NotaOff:1:48");
            Assert.That(uscita.Registro, Is.EqualTo(attesi));
            Assert.That(compositore.Motore.InEsecuzione, Is.True);
        }

        [Test]
        public void Smoke_ComandoTapAggiornaBpm()
        {
            long ora = 0;
            var rilevatore = new RilevatoreFinto();
            var compositore = new Compositore(
                rilevatore, new UscitaMidiFinta(), null, new MappaturaFinta(), Config(), null, () => ora);

            rilevatore.Cartoncini = new[] { Comando("TAP") };
            Passi(compositore, 3);
            rilevatore.Cartoncini = new List<CartoncinoRilevato>();
            Passi(compositore, 5);

            ora = 600;
            rilevatore.Cartoncini = new[] { Comando("TAP") };
            Passi(compositore, 3);

            Assert.That(compositore.Motore.Bpm, Is.EqualTo(100).Within(0.001));
        }

        [Test]
        public void Smoke_SenzaWebcam_NessunaEccezione()
        {
            var compositore = new Compositore(
                new RilevatoreFinto(), new UscitaMidiFinta(), null, new MappaturaFinta(), Config());

            Assert.DoesNotThrow(() => Passi(compositore, 5));
            Assert.That(compositore.UltimoStato.Note, Is.Empty);
            Assert.That(compositore.Motore.InEsecuzione, Is.False);
        }

        [Test]
        public void Smoke_RilevatoreCheLanciaEccezione_NonPropaga()
        {
            var rilevatore = new RilevatoreFinto { Cartoncini = new[] { NotaTraccia0Step0Durata2() } };
            var compositore = new Compositore(
                rilevatore, new UscitaMidiFinta(), null, new MappaturaFinta(), Config());
            Passi(compositore, 3);
            StatoTavolo prima = compositore.UltimoStato;
            Assert.That(prima.Note.Count, Is.EqualTo(1));

            rilevatore.Eccezione = new InvalidOperationException("webcam persa");

            Assert.DoesNotThrow(() => compositore.Passo());
            Assert.That(compositore.UltimoStato, Is.SameAs(prima));
        }

        [Test]
        public void Compositore_ParametriObbligatoriNull_Lanciano()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new Compositore(null, new UscitaMidiFinta(), null, new MappaturaFinta(), Config()));
            Assert.Throws<ArgumentNullException>(() =>
                new Compositore(new RilevatoreFinto(), null, null, new MappaturaFinta(), Config()));
            Assert.Throws<ArgumentNullException>(() =>
                new Compositore(new RilevatoreFinto(), new UscitaMidiFinta(), null, null, Config()));
            Assert.Throws<ArgumentNullException>(() =>
                new Compositore(new RilevatoreFinto(), new UscitaMidiFinta(), null, new MappaturaFinta(), null));
        }

        [Test]
        public void Motore_ComandiPubbliciEStepCorrente()
        {
            var uscita = new UscitaMidiFinta();
            var motore = new MotoreMusicale(
                new Sequencer(new ConfigGriglia()), uscita, null, new MappaturaFinta(), 120, () => 0);

            motore.Avvia();
            Assert.That(motore.InEsecuzione, Is.True);
            for (int i = 0; i < 7; i++)
            {
                motore.TickInterno();
            }

            Assert.That(motore.StepCorrente, Is.EqualTo(1));
            motore.Arresta();
            Assert.That(motore.InEsecuzione, Is.False);
            Assert.That(motore.StepCorrente, Is.EqualTo(0));
            motore.CambiaModo();
            Assert.That(motore.Modo, Is.EqualTo(ModoSincronia.Slave));
        }

        [Test]
        public void UscitaMidiSilenziosa_NonLancia()
        {
            IUscitaMidi uscita = new UscitaMidiSilenziosa();
            Assert.DoesNotThrow(() =>
            {
                uscita.NotaOn(0, 60, 100);
                uscita.NotaOff(0, 60);
                uscita.Clock();
                uscita.Start();
                uscita.Stop();
                uscita.Continue();
            });
        }

        [Test]
        public void IngressoMidiNullo_EsponeEventiSenzaSollevarli()
        {
            var ingresso = new IngressoMidiNullo();
            int chiamate = 0;
            ingresso.ClockRicevuto += () => chiamate++;
            ingresso.StartRicevuto += () => chiamate++;
            ingresso.StopRicevuto += () => chiamate++;
            ingresso.ContinueRicevuto += () => chiamate++;
            Assert.That(chiamate, Is.EqualTo(0));
        }
    }
}
