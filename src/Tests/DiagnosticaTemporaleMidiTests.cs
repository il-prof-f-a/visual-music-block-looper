using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using VisualMusicBlockLooper.App;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class DiagnosticaTemporaleMidiTests
    {
        [Test]
        public void RingMantieneSoloGliUltimiCampioniNellOrdineDiInvio()
        {
            var registro = new RegistroTempiMidi(3);
            for (int i = 0; i < 7; i++) registro.Registra(i, i * 10, i * 10 + 2, i * 10 + 3);
            Assert.That(registro.TotaleCampioni, Is.EqualTo(7));
            var letti = registro.Istantanea();
            Assert.That(letti.Select(c => c.Tick), Is.EqualTo(new long[] { 4, 5, 6 }));
            Assert.That(letti.All(c => c.InizioInvio - c.Previsto == 2 && c.FineInvio - c.InizioInvio == 1));
        }

        [Test]
        public void IstantaneaNonCambiaQuandoIlProduttoreSovrascriveIlRing()
        {
            var registro = new RegistroTempiMidi(2);
            registro.Registra(1, 10, 11, 12);
            var prima = registro.Istantanea();
            registro.Registra(2, 20, 21, 22); registro.Registra(3, 30, 31, 32);
            Assert.That(prima.Single().Tick, Is.EqualTo(1));
            Assert.That(registro.Istantanea().Select(c => c.Tick), Is.EqualTo(new long[] { 2, 3 }));
        }

        [Test]
        public void LetturaConcorrenteNonEsponeCampioniMisti()
        {
            var registro = new RegistroTempiMidi(64);
            var thread = new Thread(() => { for (int i = 1; i <= 50000; i++) registro.Registra(i, i * 10L, i * 10L + 1, i * 10L + 2); });
            thread.Start();
            do
            {
                foreach (var c in registro.Istantanea())
                    Assert.That(c.Previsto == c.Tick * 10 && c.InizioInvio == c.Previsto + 1 && c.FineInvio == c.Previsto + 2);
            } while (thread.IsAlive);
            thread.Join();
            Assert.That(registro.Istantanea().Last().Tick, Is.EqualTo(50000));
        }

        [Test]
        public void LetturaInBufferRiutilizzabileMantieneSoloICampioniRecenti()
        {
            var r=new RegistroTempiMidi(3);var buffer=new CampioneTempoMidi[2];
            for(int i=1;i<=5;i++)r.Registra(i,i*10,i*10+1,i*10+2);
            Assert.That(r.CopiaIn(buffer),Is.EqualTo(2));
            Assert.That(buffer.Select(c=>c.Tick),Is.EqualTo(new long[]{4,5}));
            r.Registra(6,60,61,62);Assert.That(r.CopiaIn(buffer),Is.EqualTo(2));
            Assert.That(buffer.Select(c=>c.Tick),Is.EqualTo(new long[]{5,6}));
        }
        [Test]
        public void CapacitaNullaNonValida() => Assert.Throws<ArgumentOutOfRangeException>(() => new RegistroTempiMidi(0));
    }
}
