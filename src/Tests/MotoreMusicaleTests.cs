using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class MotoreMusicaleTests
    {
        /// <summary>Uscita MIDI finta che registra ogni chiamata come stringa.</summary>
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

        /// <summary>Ingresso MIDI finto con metodi per sollevare gli eventi.</summary>
        private sealed class IngressoMidiFinto : IIngressoMidi
        {
            public event Action ClockRicevuto;
            public event Action StartRicevuto;
            public event Action StopRicevuto;
            public event Action ContinueRicevuto;

            public void RaiseClock() => ClockRicevuto?.Invoke();

            public void RaiseStart() => StartRicevuto?.Invoke();

            public void RaiseStop() => StopRicevuto?.Invoke();

            public void RaiseContinue() => ContinueRicevuto?.Invoke();
        }

        /// <summary>Mappatura finta: canale uguale alla traccia, nota 36 + idClip, velocity 100.</summary>
        private sealed class MappaturaFinta : IMappaturaMidi
        {
            public int Canale(int traccia) => traccia;

            public int Nota(int idClip) => 36 + idClip;

            public int Velocity => 100;
        }

        /// <summary>Insieme dei collaboratori del motore, con un orologio controllabile.</summary>
        private sealed class Banco
        {
            public Banco(double bpm = 120)
            {
                Sequencer = new Sequencer(new ConfigGriglia());
                Uscita = new UscitaMidiFinta();
                Ingresso = new IngressoMidiFinto();
                Motore = new MotoreMusicale(Sequencer, Uscita, Ingresso, new MappaturaFinta(), bpm, () => Ora);
            }

            public Sequencer Sequencer { get; }

            public UscitaMidiFinta Uscita { get; }

            public IngressoMidiFinto Ingresso { get; }

            public MotoreMusicale Motore { get; }

            /// <summary>Istante corrente dell'orologio finto, in millisecondi.</summary>
            public long Ora { get; set; }
        }

        private static readonly StrutturaTavolo UnaRiga = Crea(4, 1, 1, 1);

        private static StrutturaTavolo Crea(int battiti, int righeCollegate, int a, int b)
        {
            Assert.That(StrutturaTavolo.TryCrea(battiti, righeCollegate, a, b, out StrutturaTavolo struttura), Is.True);
            return struttura;
        }

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

        private static ComandoRilevato Comando(TipoComando tipo, int parametro = 0)
        {
            return new ComandoRilevato { Tipo = tipo, Parametro = parametro };
        }

        private static StatoTavolo Stato(IReadOnlyList<CartoncinoNota> note, params ComandoRilevato[] comandi)
        {
            return new StatoTavolo(note, comandi, null, UnaRiga);
        }

        /// <summary>Restituisce <paramref name="n"/> voci "Clock".</summary>
        private static IEnumerable<string> Clock(int n)
        {
            return Enumerable.Repeat("Clock", n);
        }

        private static void TickMaster(Banco banco, int n)
        {
            for (int i = 0; i < n; i++)
            {
                banco.Motore.TickInterno();
            }
        }

        [Test]
        public void MasterInviaClockAOgniTick()
        {
            Banco banco = new Banco();

            TickMaster(banco, 3);

            Assert.That(banco.Uscita.Registro, Is.EqualTo(Clock(3).ToList()));
        }

        [Test]
        public void ClockInMasterNonAvanzaSenzaPlay()
        {
            Banco banco = new Banco();
            List<CartoncinoNota> note = new List<CartoncinoNota> { Nota(0, 0, 1, 0) };
            banco.Motore.AggiornaTavolo(Stato(note));

            banco.Motore.TickInterno();
            banco.Motore.AggiornaTavolo(Stato(note, Comando(TipoComando.Play)));
            banco.Motore.TickInterno();

            Assert.That(banco.Uscita.Registro, Is.EqualTo(new[] { "Clock", "Start", "Clock", "NotaOn:0:36:100" }));
        }

        [Test]
        public void PlayInviaStartESuona()
        {
            Banco banco = new Banco();
            List<CartoncinoNota> note = new List<CartoncinoNota> { Nota(0, 0, 2, 0) };

            banco.Motore.AggiornaTavolo(Stato(note, Comando(TipoComando.Play)));
            TickMaster(banco, 13);

            List<string> attesi = new List<string> { "Start", "Clock", "NotaOn:0:36:100" };
            attesi.AddRange(Clock(11));
            attesi.Add("Clock");
            attesi.Add("NotaOff:0:36");
            Assert.That(banco.Uscita.Registro, Is.EqualTo(attesi));
            Assert.That(banco.Motore.InEsecuzione, Is.True);
        }

        [Test]
        public void StopInviaStopESpegneNote()
        {
            Banco banco = new Banco();
            List<CartoncinoNota> note = new List<CartoncinoNota> { Nota(0, 0, 2, 0) };
            banco.Motore.AggiornaTavolo(Stato(note, Comando(TipoComando.Play)));
            TickMaster(banco, 1);

            banco.Motore.AggiornaTavolo(Stato(note, Comando(TipoComando.Stop)));

            Assert.That(banco.Uscita.Registro, Is.EqualTo(new[] { "Start", "Clock", "NotaOn:0:36:100", "NotaOff:0:36", "Stop" }));
            Assert.That(banco.Motore.InEsecuzione, Is.False);
        }

        [Test]
        public void TapImpostaBpm()
        {
            Banco banco = new Banco();
            banco.Ora = 0;
            banco.Motore.AggiornaTavolo(Stato(null, Comando(TipoComando.Tap)));
            Assert.That(banco.Motore.Bpm, Is.EqualTo(120.0));

            banco.Ora = 250;
            banco.Motore.AggiornaTavolo(Stato(null, Comando(TipoComando.Tap)));

            Assert.That(banco.Motore.Bpm, Is.EqualTo(240.0));
        }

        [Test]
        public void ModoCommutaESpegneNote()
        {
            Banco banco = new Banco();
            List<CartoncinoNota> note = new List<CartoncinoNota> { Nota(0, 0, 2, 0) };
            banco.Motore.AggiornaTavolo(Stato(note, Comando(TipoComando.Play)));
            TickMaster(banco, 1);

            banco.Motore.AggiornaTavolo(Stato(note, Comando(TipoComando.Modo)));

            Assert.That(banco.Motore.Modo, Is.EqualTo(ModoSincronia.Slave));
            Assert.That(banco.Motore.InEsecuzione, Is.False);
            Assert.That(banco.Uscita.Registro.Skip(3).ToList(), Is.EqualTo(new[] { "NotaOff:0:36", "Stop" }));
        }

        [Test]
        public void SlaveSegueIlClockEsterno()
        {
            Banco banco = new Banco();
            banco.Motore.AggiornaTavolo(Stato(new List<CartoncinoNota> { Nota(0, 0, 2, 0) }, Comando(TipoComando.Modo)));

            banco.Ingresso.RaiseStart();
            for (int i = 0; i < 13; i++)
            {
                banco.Ingresso.RaiseClock();
            }

            Assert.That(banco.Uscita.Registro, Is.EqualTo(new[] { "NotaOn:0:36:100", "NotaOff:0:36" }));
        }

        [Test]
        public void SlaveIgnoraTapETickInterno()
        {
            Banco banco = new Banco();
            banco.Motore.AggiornaTavolo(Stato(null, Comando(TipoComando.Modo)));

            banco.Ora = 0;
            banco.Motore.AggiornaTavolo(Stato(null, Comando(TipoComando.Tap)));
            banco.Ora = 250;
            banco.Motore.AggiornaTavolo(Stato(null, Comando(TipoComando.Tap)));
            banco.Motore.TickInterno();

            Assert.That(banco.Motore.Bpm, Is.EqualTo(120.0));
            Assert.That(banco.Uscita.Registro, Is.Empty);
        }

        [Test]
        public void SlaveClockFermo_Timeout()
        {
            Banco banco = new Banco();
            banco.Motore.AggiornaTavolo(Stato(new List<CartoncinoNota> { Nota(0, 0, 2, 0) }, Comando(TipoComando.Modo)));
            banco.Ora = 0;
            banco.Ingresso.RaiseStart();
            banco.Ingresso.RaiseClock();

            banco.Motore.ControllaTimeoutClock(500);
            Assert.That(banco.Motore.InEsecuzione, Is.True);

            banco.Motore.ControllaTimeoutClock(501);

            Assert.That(banco.Motore.InEsecuzione, Is.False);
            Assert.That(banco.Uscita.Registro, Is.EqualTo(new[] { "NotaOn:0:36:100", "NotaOff:0:36" }));
        }

        [Test]
        public void SlaveTimeoutSpegneOneShotConDawFermo()
        {
            Banco banco = new Banco();
            banco.Ora = 1000;
            banco.Motore.AggiornaTavolo(Stato(null, Comando(TipoComando.Modo)));
            banco.Motore.AggiornaTavolo(Stato(null, Comando(TipoComando.OneShot, 60)));
            Assert.That(banco.Motore.InEsecuzione, Is.False);

            banco.Motore.ControllaTimeoutClock(1500);
            Assert.That(banco.Uscita.Registro, Is.EqualTo(new[] { "NotaOn:0:60:100" }));

            banco.Motore.ControllaTimeoutClock(1501);

            Assert.That(banco.Uscita.Registro, Is.EqualTo(new[] { "NotaOn:0:60:100", "NotaOff:0:60" }));
            banco.Motore.ControllaTimeoutClock(3000);
            Assert.That(banco.Uscita.Registro.Count(r => r == "NotaOff:0:60"), Is.EqualTo(1));
        }

        [Test]
        public void SilenziaInSlaveSpegneNotaESuonaOneShot()
        {
            Banco banco = new Banco();
            banco.Motore.AggiornaTavolo(Stato(new List<CartoncinoNota> { Nota(0, 0, 2, 0) }, Comando(TipoComando.Modo)));
            banco.Ingresso.RaiseStart();
            banco.Ingresso.RaiseClock();
            banco.Motore.AggiornaTavolo(Stato(new List<CartoncinoNota> { Nota(0, 0, 2, 0) }, Comando(TipoComando.OneShot, 60)));
            banco.Uscita.Registro.Clear();

            banco.Motore.Silenzia();

            Assert.That(banco.Motore.InEsecuzione, Is.False);
            Assert.That(banco.Uscita.Registro, Is.EquivalentTo(new[] { "NotaOff:0:36", "NotaOff:0:60" }));
            Assert.That(banco.Uscita.Registro, Has.None.EqualTo("Start"));
            Assert.That(banco.Uscita.Registro, Has.None.EqualTo("Stop"));
        }

        [Test]
        public void SilenziaEIdempotente()
        {
            Banco banco = new Banco();
            banco.Motore.AggiornaTavolo(Stato(new List<CartoncinoNota> { Nota(0, 0, 2, 0) }, Comando(TipoComando.Play), Comando(TipoComando.OneShot, 60)));
            TickMaster(banco, 1);
            banco.Motore.Silenzia();
            int dopoPrima = banco.Uscita.Registro.Count;

            banco.Motore.Silenzia();

            Assert.That(banco.Uscita.Registro.Count, Is.EqualTo(dopoPrima));
            Assert.That(banco.Uscita.Registro, Has.None.EqualTo("Stop"));
        }

        [Test]
        public void ArrestaSpegneOneShotPendenti()
        {
            Banco banco = new Banco();
            banco.Motore.AggiornaTavolo(Stato(null, Comando(TipoComando.OneShot, 60)));

            banco.Motore.Arresta();

            Assert.That(banco.Uscita.Registro, Is.EqualTo(new[] { "NotaOn:0:60:100", "NotaOff:0:60" }));
        }

        [Test]
        public void CartoncinoRimossoMentreSuonaRiceveNotaOff()
        {
            Banco banco = new Banco();
            banco.Motore.AggiornaTavolo(Stato(new List<CartoncinoNota> { Nota(0, 0, 8, 0) }, Comando(TipoComando.Play)));
            TickMaster(banco, 1);
            Assert.That(banco.Uscita.Registro, Contains.Item("NotaOn:0:36:100"));

            banco.Motore.AggiornaTavolo(Stato(new List<CartoncinoNota>()));
            TickMaster(banco, 8);

            Assert.That(banco.Uscita.Registro, Contains.Item("NotaOff:0:36"));
        }

        [Test]
        public void OneShotSuonaSubito()
        {
            Banco banco = new Banco();

            banco.Motore.AggiornaTavolo(Stato(null, Comando(TipoComando.OneShot, 60)));

            Assert.That(banco.Uscita.Registro, Is.EqualTo(new[] { "NotaOn:0:60:100" }));

            TickMaster(banco, 6);

            List<string> attesi = new List<string> { "NotaOn:0:60:100" };
            attesi.AddRange(Clock(6));
            attesi.Add("NotaOff:0:60");
            Assert.That(banco.Uscita.Registro, Is.EqualTo(attesi));
        }

        [Test]
        public void OneShotStessaNotaRiavviaConteggio()
        {
            Banco banco = new Banco();
            banco.Motore.AggiornaTavolo(Stato(null, Comando(TipoComando.OneShot, 60)));
            TickMaster(banco, 3);

            banco.Motore.AggiornaTavolo(Stato(null, Comando(TipoComando.OneShot, 60)));
            TickMaster(banco, 5);
            Assert.That(banco.Uscita.Registro.Last(), Is.EqualTo("Clock"));

            TickMaster(banco, 1);

            Assert.That(banco.Uscita.Registro.Last(), Is.EqualTo("NotaOff:0:60"));
            Assert.That(banco.Uscita.Registro.Count(r => r == "NotaOn:0:60:100"), Is.EqualTo(2));
            Assert.That(banco.Uscita.Registro.Count(r => r == "NotaOff:0:60"), Is.EqualTo(1));
        }

        [Test]
        public void MappaturaUsataPerCanaleENota()
        {
            Banco banco = new Banco();
            List<CartoncinoNota> note = new List<CartoncinoNota> { Nota(2, 0, 2, 5) };
            banco.Motore.AggiornaTavolo(Stato(note, Comando(TipoComando.Play)));
            TickMaster(banco, 1);

            banco.Motore.AggiornaTavolo(Stato(note, Comando(TipoComando.Stop)));

            Assert.That(banco.Uscita.Registro, Is.EqualTo(new[] { "Start", "Clock", "NotaOn:2:41:100", "NotaOff:2:41", "Stop" }));
        }

        [Test]
        public void PlayStopIgnoratiInSlave()
        {
            Banco banco = new Banco();
            banco.Motore.AggiornaTavolo(Stato(null, Comando(TipoComando.Modo)));

            banco.Motore.AggiornaTavolo(Stato(null, Comando(TipoComando.Play)));
            banco.Motore.AggiornaTavolo(Stato(null, Comando(TipoComando.Stop)));

            Assert.That(banco.Motore.InEsecuzione, Is.False);
            Assert.That(banco.Uscita.Registro, Is.Empty);
        }

        [Test]
        public void StartRicevutoInSlaveAzzeraPosizione()
        {
            Banco banco = new Banco();
            banco.Motore.AggiornaTavolo(Stato(new List<CartoncinoNota> { Nota(0, 0, 1, 0) }, Comando(TipoComando.Modo)));

            banco.Ingresso.RaiseStart();
            banco.Ingresso.RaiseClock();
            banco.Ingresso.RaiseClock();
            banco.Ingresso.RaiseClock();
            banco.Ingresso.RaiseStart();
            banco.Ingresso.RaiseClock();

            Assert.That(banco.Uscita.Registro, Is.EqualTo(new[] { "NotaOn:0:36:100", "NotaOff:0:36", "NotaOn:0:36:100" }));
        }

        [Test]
        public void StrutturaPassataAlSequencer()
        {
            Banco banco = new Banco();
            List<CartoncinoNota> note = new List<CartoncinoNota> { Nota(0, 0, 1, 0), Nota(1, 0, 1, 1) };
            banco.Motore.AggiornaTavolo(new StatoTavolo(note, new[] { Comando(TipoComando.Play) }, null, Crea(4, 1, 1, 1)));

            TickMaster(banco, 1);

            Assert.That(banco.Uscita.Registro.Count(r => r.StartsWith("NotaOn")), Is.EqualTo(2));
            Assert.That(banco.Motore.Struttura, Is.EqualTo(Crea(4, 1, 1, 1)));
        }

        [Test]
        public void PosizioneDelMotore()
        {
            Banco banco = new Banco();
            Assert.That(banco.Motore.Struttura, Is.SameAs(StrutturaTavolo.Default));
            Assert.That(banco.Motore.Posizione.StepInMisura, Is.EqualTo(0));

            banco.Motore.AggiornaTavolo(new StatoTavolo(null, new[] { Comando(TipoComando.Play) }, null, Crea(4, 3, 1, 3)));
            TickMaster(banco, 31);

            Assert.That(banco.Motore.Posizione.StepInMisura, Is.EqualTo(5));
            Assert.That(banco.Motore.StepCorrente, Is.EqualTo(5));
            Assert.That(banco.Motore.Posizione.RigheAttive, Is.EqualTo(new[] { 0, 3 }));
        }

        [Test]
        public void ArgomentiNulli()
        {
            UscitaMidiFinta uscita = new UscitaMidiFinta();
            MappaturaFinta mappatura = new MappaturaFinta();
            Sequencer sequencer = new Sequencer(new ConfigGriglia());

            Assert.That(() => new MotoreMusicale(null, uscita, null, mappatura), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new MotoreMusicale(sequencer, null, null, mappatura), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new MotoreMusicale(sequencer, uscita, null, null), Throws.TypeOf<ArgumentNullException>());
            Assert.DoesNotThrow(() => new MotoreMusicale(sequencer, uscita, null, mappatura));
        }
    }
}
