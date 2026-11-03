using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class TickPrioritarioTests
    {
        private sealed class Uscita : IUscitaMidi
        {
            public int Conteggio;
            public void Clock() => Interlocked.Increment(ref Conteggio);
            public void Start() { } public void Stop() { } public void Continue() { }
            public void NotaOn(int c, int n, int v) { } public void NotaOff(int c, int n) { }
        }
        [Test]
        public void AggiornamentoGraficoBloccatoNonFermaIlClock()
        {
            var u = new Uscita(); var m = new MotoreMusicale(new Sequencer(new ConfigGriglia()), u, null, new EsecutorePartituraTests.Mappa(), 300);
            using (var entrato = new ManualResetEventSlim())
            using (var libera = new ManualResetEventSlim())
            using (var tick = new TickMaster(m, () => 0, () => { entrato.Set(); libera.Wait(2000); }))
            {
                try
                {
                    tick.Avvia(); Assert.That(entrato.Wait(1000));
                    Assert.That(SpinWait.SpinUntil(() => Volatile.Read(ref u.Conteggio) >= 10, 400), Is.True,
                        "La notifica grafica non deve essere eseguita sul thread MIDI.");
                }
                finally { libera.Set(); tick.Ferma(); }
            }
        }

        private sealed class UscitaLenta : IUscitaMidi
        {
            public readonly ManualResetEventSlim Ingresso = new ManualResetEventSlim();
            public readonly ManualResetEventSlim Libera = new ManualResetEventSlim();
            public readonly ManualResetEventSlim Arrestato = new ManualResetEventSlim();
            private int clock;
            public void Clock() { if (Interlocked.Increment(ref clock) == 1) { Ingresso.Set(); Libera.Wait(2000); } }
            public void Start() { } public void Stop() => Arrestato.Set(); public void Continue() { }
            public void NotaOn(int c, int n, int v) { } public void NotaOff(int c, int n) { }
        }

        [Test]
        public void StalloDelDriverArrestaIlTrasportoEConservaIlRitardoNellaDiagnostica()
        {
            var u = new UscitaLenta();
            using (var e = new EsecutoreMidi(u, null, new EsecutorePartituraTests.Mappa(), new ConfigGriglia(), () => 0))
            using (var t = new TickMaster(e, () => 0))
            {
                var n = new CartoncinoNota { Riga = 0, Traccia = 0, IdClip = 40, DurataStep = 4 };
                e.Pubblica(e.Prepara(new StatoTavolo(new[] { n }, null, null), 1, 1, 0)); e.Avvia();
                try
                {
                    t.Avvia(); Assert.That(u.Ingresso.Wait(1000)); Thread.Sleep(100); u.Libera.Set();
                    Assert.That(u.Arrestato.Wait(1000));
                    Assert.That(SpinWait.SpinUntil(() => t.RegistroTempi.TotaleCampioni >= 3, 1000));
                    Assert.That(e.InEsecuzione, Is.False);
                    Assert.That(e.UltimoErrore, Does.Contain("ritardo"));
                    Assert.That(t.RegistroTempi.Istantanea().Any(c =>
                        (c.InizioInvio - c.Previsto) * 1000.0 / System.Diagnostics.Stopwatch.Frequency > 50), Is.True,
                        "Il reset della scadenza non deve nascondere lo stallo già avvenuto.");
                }
                finally { u.Libera.Set(); t.Ferma(); }
            }
        }
        [Test]
        public void RiavvioRilasciaIlProprietarioSoloDopoLArrestoDelPrimoThread()
        {
            var u = new Uscita();
            using (var e = new EsecutoreMidi(u, null, new EsecutorePartituraTests.Mappa(), new ConfigGriglia(), () => 0))
            using (var t = new TickMaster(e, () => 0))
            {
                t.Avvia(); Assert.That(SpinWait.SpinUntil(() => Volatile.Read(ref u.Conteggio) >= 3, 1000)); t.Ferma();
                int prima = Volatile.Read(ref u.Conteggio); t.Avvia();
                Assert.That(SpinWait.SpinUntil(() => Volatile.Read(ref u.Conteggio) >= prima + 3, 1000));
                t.Ferma(); Assert.That(t.UltimoErrore, Is.Null);
            }
        }
    }
}
