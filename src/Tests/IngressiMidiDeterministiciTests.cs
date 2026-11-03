using System;
using System.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class IngressiMidiDeterministiciTests
    {
        private sealed class Ingresso : IIngressoMidi
        {
            public event Action ClockRicevuto, StartRicevuto, StopRicevuto, ContinueRicevuto;
            public void Clock() => ClockRicevuto?.Invoke(); public void Start() => StartRicevuto?.Invoke();
            public void Stop() => StopRicevuto?.Invoke(); public void Continue() => ContinueRicevuto?.Invoke();
        }
        [Test]
        public void ClockEsternoArretratoNonVieneRecuperatoConAttacchiARaffica()
        {
            long ora = 0; var u = new EsecutorePartituraTests.Uscita(); var i = new Ingresso();
            using (var e = new EsecutoreMidi(u, i, new EsecutorePartituraTests.Mappa(), new ConfigGriglia(), () => ora))
            {
                e.Pubblica(EsecutorePartituraTests.Partitura(u)); e.CambiaModo(); e.ElaboraPendenti();
                i.Start(); e.ElaboraPendenti(); i.Clock(); ora = 100; e.ElaboraPendenti();
                Assert.That(e.InEsecuzione, Is.False);
                Assert.That(u.Eventi.Any(x => x.StartsWith("On")), Is.False);
                Assert.That(e.UltimoErrore, Does.Contain("ritardo"));
            }
        }
        [Test]
        public void StopInIngressoSegueUnCambioModoGiaAccodato()
        {
            var u = new EsecutorePartituraTests.Uscita(); var i = new Ingresso();
            using (var e = new EsecutoreMidi(u, i, new EsecutorePartituraTests.Mappa(), new ConfigGriglia(), () => 0))
            {
                e.Pubblica(EsecutorePartituraTests.Partitura(u));
                e.CambiaModo(); i.Start(); i.Stop(); e.ElaboraPendenti();
                Assert.That(e.Modo, Is.EqualTo(ModoSincronia.Slave)); Assert.That(e.InEsecuzione, Is.False);
            }
        }
        [Test]
        public void MasterIgnoraAncheUnClockEsternoAccodatoInRitardo()
        {
            long ora = 0; var u = new EsecutorePartituraTests.Uscita(); var i = new Ingresso();
            using (var e = new EsecutoreMidi(u, i, new EsecutorePartituraTests.Mappa(), new ConfigGriglia(), () => ora))
            {
                e.Pubblica(EsecutorePartituraTests.Partitura(u)); e.Avvia(); e.ElaboraPendenti();
                i.Clock(); ora = 100; e.ElaboraPendenti();
                Assert.That(e.InEsecuzione, Is.True); Assert.That(e.UltimoErrore, Is.Null);
            }
        }
        [Test]
        public void StopEsternoPreservaUnCambioModoGiaAccodatoDaSlaveAMaster()
        {
            var u = new EsecutorePartituraTests.Uscita(); var i = new Ingresso();
            using (var e = new EsecutoreMidi(u, i, new EsecutorePartituraTests.Mappa(), new ConfigGriglia(), () => 0))
            {
                e.CambiaModo(); e.ElaboraPendenti(); e.CambiaModo(); i.Stop(); e.ElaboraPendenti();
                Assert.That(e.Modo, Is.EqualTo(ModoSincronia.Master));
            }
        }
    }
}
