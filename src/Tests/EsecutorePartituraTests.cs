using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class EsecutorePartituraTests
    {
        public sealed class Uscita : IUscitaMidi
        {
            public readonly List<string> Eventi = new List<string>();
            public void Clock() => Eventi.Add("Clock"); public void Start() => Eventi.Add("Start");
            public void Stop() => Eventi.Add("Stop"); public void Continue() => Eventi.Add("Continue");
            public void NotaOn(int c, int n, int v) => Eventi.Add($"On:{c}:{n}");
            public void NotaOff(int c, int n) => Eventi.Add($"Off:{c}:{n}");
        }
        public sealed class Mappa : IMappaturaMidi
        { public int Canale(int t) => t; public int Nota(int clip) => clip + 36; public int Velocity => 100; }
        public static PartituraMidiPreparata Partitura(Uscita u, long versione = 1, int clip = 40, int step = 0,
            int durata = 1, long generazione = 1, long primaFrontiera = 0)
        {
            StrutturaTavolo s; StrutturaTavolo.TryCrea(2, 1, 1, 2, out s);
            var n = new CartoncinoNota { Riga = 0, Traccia = 0, IdClip = clip, StepIniziale = step, DurataStep = durata };
            var p = new CompilatorePartitura(new ConfigGriglia(), new Mappa()).Compila(new StatoTavolo(new[] { n }, null, null, s), versione, generazione, primaFrontiera);
            return PartituraMidiPreparata.Prepara(p, u);
        }
        private static void Tick(MotorePartitura m, int n) { for (int i = 0; i < n; i++) m.TickInterno(); }

        [Test]
        public void NuovaPartituraNonInterrompeIlGiroAttivo()
        {
            var u = new Uscita(); var m = new MotorePartitura(u, new Mappa());
            m.Pubblica(Partitura(u, durata: 8)); m.Avvia(); Tick(m, 3);
            m.Pubblica(Partitura(u, 2, 41)); Tick(m, 45);
            Assert.That(m.VersioneAttiva, Is.EqualTo(1));
            Assert.That(u.Eventi.Where(e => e.StartsWith("On")), Is.EqualTo(new[] { "On:0:76" }));
            m.TickInterno();
            Assert.That(m.VersioneAttiva, Is.EqualTo(2));
            Assert.That(u.Eventi.Where(e => e.StartsWith("On") || e.StartsWith("Off")),
                Is.EqualTo(new[] { "On:0:76", "Off:0:76", "On:0:77" }));
        }

        [Test]
        public void NessunaNuovaVersioneRipeteLaPartituraValidaConNuovoAttacco()
        {
            var u = new Uscita(); var m = new MotorePartitura(u, new Mappa());
            m.Pubblica(Partitura(u, durata: 8)); m.Avvia(); Tick(m, 97);
            Assert.That(u.Eventi.Count(e => e == "On:0:76"), Is.EqualTo(3));
            Assert.That(u.Eventi.Count(e => e == "Off:0:76"), Is.EqualTo(2));
        }

        [Test]
        public void PubblicazioneVecchiaNonSostituisceLaVersionePiuRecente()
        {
            var u = new Uscita(); var m = new MotorePartitura(u, new Mappa());
            Assert.That(m.Pubblica(Partitura(u, 3, 43)), Is.True);
            Assert.That(m.Pubblica(Partitura(u, 2, 42)), Is.False);
            m.Avvia(); m.TickInterno(); Assert.That(m.VersioneAttiva, Is.EqualTo(3));
        }

        [Test]
        public void GenerazioneSuperataNonVieneAttivata()
        {
            var u = new Uscita(); var m = new MotorePartitura(u, new Mappa());
            m.Pubblica(Partitura(u)); m.ImpostaGenerazione(2); m.Avvia(); m.TickInterno();
            Assert.That(u.Eventi.Any(e => e.StartsWith("On")), Is.False);
            Assert.That(m.InPreparazione, Is.True);
            Assert.That(m.Pubblica(Partitura(u, 2, generazione: 1)), Is.False);
            m.Pubblica(Partitura(u, 3, generazione: 2)); m.TickInterno();
            Assert.That(m.InEsecuzione, Is.True);
        }

        [Test]
        public void PrimaFrontieraImpedisceAttivazioneAnticipata()
        {
            var u = new Uscita(); var m = new MotorePartitura(u, new Mappa());
            m.Pubblica(Partitura(u)); m.Avvia(); Tick(m, 1);
            m.Pubblica(Partitura(u, 2, 41, primaFrontiera: 2)); Tick(m, 48);
            Assert.That(m.VersioneAttiva, Is.EqualTo(1)); Tick(m, 48);
            Assert.That(m.VersioneAttiva, Is.EqualTo(2));
        }

        [Test]
        public void StopEPanicSpengonoLeNoteSenzaAttendereLaFrontiera()
        {
            var u = new Uscita(); var m = new MotorePartitura(u, new Mappa());
            m.Pubblica(Partitura(u, durata: 8)); m.Avvia(); m.TickInterno(); m.Arresta();
            Assert.That(u.Eventi.Skip(u.Eventi.Count - 2), Is.EqualTo(new[] { "Off:0:76", "Stop" }));
            Assert.That(m.InEsecuzione, Is.False); Tick(m, 10);
            Assert.That(u.Eventi.Count(e => e.StartsWith("On")), Is.EqualTo(1));
        }

        [Test]
        public void SlaveNonGeneraUnSecondoClockESegueStartStopETimeout()
        {
            long ora = 0; var u = new Uscita(); var m = new MotorePartitura(u, new Mappa(), 120, () => ora);
            m.Pubblica(Partitura(u, durata: 8)); m.CambiaModo(); m.RiceviStart();
            m.TickInterno(); Assert.That(u.Eventi, Is.Empty);
            m.RiceviClock(0); Assert.That(u.Eventi, Is.EqualTo(new[] { "On:0:76" }));
            m.ControllaTimeoutClock(501);
            Assert.That(u.Eventi.Last(), Is.EqualTo("Off:0:76")); Assert.That(m.InEsecuzione, Is.False);
        }

        [Test]
        public void PlayAttendeUnaPartituraValidaENonPerdeLaRichiesta()
        {
            var u = new Uscita(); var m = new MotorePartitura(u, new Mappa());
            m.Avvia(); m.TickInterno(); Assert.That(m.InPreparazione, Is.True);
            Assert.That(u.Eventi, Is.EqualTo(new[] { "Clock" }));
            m.Pubblica(Partitura(u)); m.TickInterno();
            Assert.That(u.Eventi.Skip(1), Is.EqualTo(new[] { "Start", "Clock", "On:0:76" }));
        }

        [Test]
        public void OneShotNonSiRipeteConIlGiroESiSpegneDopoSeiTick()
        {
            var u = new Uscita(); var m = new MotorePartitura(u, new Mappa());
            m.OneShot(60); Tick(m, 6);
            Assert.That(u.Eventi.Count(e => e == "On:0:60"), Is.EqualTo(1));
            Assert.That(u.Eventi.Last(), Is.EqualTo("Off:0:60"));
        }

        [Test]
        public void CambioBpmAttendeLaPulsazioneSuccessiva()
        {
            long ora = 0; var u = new Uscita(); var m = new MotorePartitura(u, new Mappa(), 120, () => ora);
            m.Pubblica(Partitura(u)); m.Avvia(); Tick(m, 2);
            m.Tap(0); m.Tap(1000); Assert.That(m.Bpm, Is.EqualTo(120));
            Tick(m, 22); Assert.That(m.Bpm, Is.EqualTo(120));
            m.TickInterno(); Assert.That(m.Bpm, Is.EqualTo(60));
        }

        [Test]
        public void FaseBpmRiparteDalloStartAncheDopoClockInStop()
        {
            var u = new Uscita(); var m = new MotorePartitura(u, new Mappa()); Tick(m, 13);
            m.Pubblica(Partitura(u)); m.Avvia(); m.TickInterno(); m.Tap(0); m.Tap(1000);
            Tick(m, 23); Assert.That(m.Bpm, Is.EqualTo(120));
            m.TickInterno(); Assert.That(m.Bpm, Is.EqualTo(60));
        }
        private sealed class OffFallisceUnaVolta : IUscitaMidi
        {
            public int TentativiOff;
            public void Clock() { } public void Start() { } public void Stop() { } public void Continue() { }
            public void NotaOn(int c, int n, int v) { }
            public void NotaOff(int c, int n) { if (++TentativiOff == 1) throw new InvalidOperationException("Driver temporaneamente occupato"); }
        }
        [Test]
        public void RitentaUnNoteOffFallitoDuranteLArresto()
        {
            var u = new OffFallisceUnaVolta(); var m = new MotorePartitura(u, new Mappa());
            var s = new StatoTavolo(new[] { new CartoncinoNota { Riga = 0, Traccia = 0, IdClip = 40, DurataStep = 1 } }, null, null);
            var p = new CompilatorePartitura(new ConfigGriglia(), new Mappa()).Compila(s, 1, 1);
            m.Pubblica(PartituraMidiPreparata.Prepara(p, u)); m.Avvia(); Tick(m, 6);
            Assert.Throws<InvalidOperationException>(() => m.TickInterno()); m.Arresta();
            Assert.That(u.TentativiOff, Is.EqualTo(2));
        }
    }
}
