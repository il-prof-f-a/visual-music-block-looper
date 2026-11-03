using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class ProprietaMidiTests
    {
        private sealed class Uscita : IUscitaMidi
        {
            public readonly ConcurrentQueue<int> ThreadInvio = new ConcurrentQueue<int>();
            public readonly ConcurrentQueue<string> Messaggi = new ConcurrentQueue<string>();
            private void Registra(string s) { ThreadInvio.Enqueue(Thread.CurrentThread.ManagedThreadId); Messaggi.Enqueue(s); }
            public void Clock() => Registra("Clock"); public void Start() => Registra("Start");
            public void Stop() => Registra("Stop"); public void Continue() => Registra("Continue");
            public void NotaOn(int c, int n, int v) => Registra("On"); public void NotaOff(int c, int n) => Registra("Off");
        }
        [Test]
        public void CodaLimitataRifiutaIlTroppoPienoSenzaSovrascrivereComandi()
        {
            var q = new CodaLimitata<int>(4);
            for (int i = 0; i < 4; i++) Assert.That(q.ProvaInserisci(i));
            Assert.That(q.ProvaInserisci(99), Is.False);
            for (int i = 0; i < 4; i++) { int n; Assert.That(q.ProvaEstrai(out n)); Assert.That(n, Is.EqualTo(i)); }
            int vuoto; Assert.That(q.ProvaEstrai(out vuoto), Is.False);
        }

        [Test]
        public void ProduttoriConcorrentiNonPerdonoONonDuplicanoRichieste()
        {
            var q = new CodaLimitata<int>(128); var ricevuti = new bool[12000];
            var tasks = Enumerable.Range(0, 4).Select(p => Task.Run(() =>
            { for (int i = 0; i < 3000; i++) while (!q.ProvaInserisci(p * 3000 + i)) Thread.Yield(); })).ToArray();
            int letti = 0;
            while (letti < ricevuti.Length)
            {
                int n; if (!q.ProvaEstrai(out n)) { Thread.Yield(); continue; }
                Assert.That(ricevuti[n], Is.False); ricevuti[n] = true; letti++;
            }
            Task.WaitAll(tasks); Assert.That(ricevuti.All(x => x));
        }

        [Test]
        public void ComandiDaAltriThreadNonInvianoMidiPrimaDelProprietario()
        {
            var u = new Uscita();
            using (var e = new EsecutoreMidi(u, null, new EsecutorePartituraTests.Mappa(), new ConfigGriglia(), () => 0))
            {
                Task.Run(() => e.OneShot(60)).Wait(); Assert.That(u.Messaggi, Is.Empty);
                e.ElaboraPendenti(); e.TickInterno();
                Assert.That(u.Messaggi.Contains("On"));
                Assert.That(u.ThreadInvio.All(id => id == Thread.CurrentThread.ManagedThreadId));
                Task.Run(() => e.Silenzia()).Wait();
                Assert.That(u.Messaggi.Contains("Off"), Is.False);
                e.ElaboraPendenti(); Assert.That(u.Messaggi.Last(), Is.EqualTo("Off"));
            }
        }

        [Test]
        public void StopRestaConsegnabileQuandoLaCodaOrdinariaEPiena()
        {
            var u = new Uscita();
            using (var e = new EsecutoreMidi(u, null, new EsecutorePartituraTests.Mappa(), new ConfigGriglia(), () => 0))
            {
                e.OneShot(60); e.ElaboraPendenti();
                for (int i = 0; i < 5000; i++) e.OneShot(61);
                e.Arresta(); e.ElaboraPendenti();
                Assert.That(u.Messaggi.Last(), Is.EqualTo("Off"));
                Assert.That(u.Messaggi.Count(x => x == "On"), Is.EqualTo(1));
                Assert.That(e.UltimoErrore, Is.Not.Empty);
            }
        }

        [Test]
        public void UnSecondoConsumatoreNonPuoInviareSullaStessaPorta()
        {
            using (var e = new EsecutoreMidi(new Uscita(), null, new EsecutorePartituraTests.Mappa(), new ConfigGriglia(), () => 0))
            {
                e.ElaboraPendenti();
                var errore = Task.Run(() => { try { e.TickInterno(); return null; } catch (Exception ex) { return ex; } }).Result;
                Assert.That(errore, Is.TypeOf<InvalidOperationException>());
            }
        }
    }
}
