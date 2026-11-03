using System.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class PubblicazioneFoglioTests
    {
        private static StatoTavolo Stato(int clip = 40) => new StatoTavolo(new[]
        { new CartoncinoNota { Riga = 0, Traccia = 0, IdClip = clip, DurataStep = 1 } }, null, null);
        private static void Tick(EsecutoreMidi e, int n) { for (int i = 0; i < n; i++) e.TickInterno(); }

        [Test]
        public void FoglioOsservatoNonRicompilaLeZoneInvariate()
        {
            var u = new EsecutorePartituraTests.Uscita();
            using (var e = new EsecutoreMidi(u, null, new EsecutorePartituraTests.Mappa(), new ConfigGriglia(), () => 0))
            {
                var p = new PreparatoreFoglio(e); p.Aggiorna(Stato(), true); p.Aggiorna(Stato(), true); p.ControllaScadenza();
                Assert.That(p.Foglio.Ultima.Versione, Is.EqualTo(1)); Assert.That(p.NumeroCompilazioni, Is.EqualTo(1));
                e.Avvia(); Tick(e, 1); Assert.That(e.Stato.VersioneAttiva, Is.EqualTo(1));
            }
        }
        [Test]
        public void LetturaIncertaNonCancellaUnaPartituraValida()
        {
            var f = new FoglioOsservato(); Assert.That(f.Aggiorna(Stato(), true, 1, 0));
            Assert.That(f.Aggiorna(new StatoTavolo(null, null, null), false, 1, 10), Is.False);
            Assert.That(f.Ultima.CopiaStato().Note.Count, Is.EqualTo(1));
            Assert.That(f.Ultima.Versione, Is.EqualTo(1));
        }
        [Test]
        public void ConfermaDiFoglioVuotoEUnaVersioneValida()
        {
            var f = new FoglioOsservato(); Assert.That(f.Aggiorna(new StatoTavolo(null, null, null), true, 1, 0));
            Assert.That(f.Ultima.CopiaStato().Note, Is.Empty); Assert.That(f.Ultima.Versione, Is.EqualTo(1));
        }
        [Test]
        public void ModificaAMetaGiroSiPreparaAllaScadenzaESuonaDalSuccessivo()
        {
            var u = new EsecutorePartituraTests.Uscita();
            using (var e = new EsecutoreMidi(u, null, new EsecutorePartituraTests.Mappa(), new ConfigGriglia(), () => 0))
            {
                var p = new PreparatoreFoglio(e); p.Aggiorna(Stato(), true); e.Avvia(); Tick(e, 1);
                p.Aggiorna(Stato(41), true); Assert.That(p.NumeroCompilazioni, Is.EqualTo(1));
                Tick(e, 480); p.ControllaScadenza(); Assert.That(p.NumeroCompilazioni, Is.EqualTo(2));
                Assert.That(e.Stato.VersioneAttiva, Is.EqualTo(1));
                Tick(e, 96); Assert.That(e.Stato.VersioneAttiva, Is.EqualTo(2));
                Assert.That(u.Eventi.Last(), Is.EqualTo("On:0:77"));
            }
        }
        [Test]
        public void OsservazioneSuccessivaAllaScadenzaNonEntraNellaFrontieraAppenaPreparata()
        {
            var u = new EsecutorePartituraTests.Uscita();
            using (var e = new EsecutoreMidi(u, null, new EsecutorePartituraTests.Mappa(), new ConfigGriglia(), () => 0))
            {
                var p = new PreparatoreFoglio(e); p.Aggiorna(Stato(), true); e.Avvia(); Tick(e, 481);
                p.Aggiorna(Stato(41), true);
                Assert.That(p.NumeroCompilazioni, Is.EqualTo(1)); Tick(e, 96);
                Assert.That(e.Stato.VersioneAttiva, Is.EqualTo(1));
                Tick(e, 480); p.ControllaScadenza(); Tick(e, 96);
                Assert.That(e.Stato.VersioneAttiva, Is.EqualTo(2));
            }
        }
        [Test]
        public void NuovaGenerazioneRichiedeUnaNuovaOsservazioneConfermata()
        {
            var u = new EsecutorePartituraTests.Uscita();
            using (var e = new EsecutoreMidi(u, null, new EsecutorePartituraTests.Mappa(), new ConfigGriglia(), () => 0))
            {
                var p = new PreparatoreFoglio(e); p.Aggiorna(Stato(), true); p.ImpostaGenerazione(2); p.ControllaScadenza();
                Assert.That(p.NumeroCompilazioni, Is.EqualTo(1));
                p.Aggiorna(Stato(41), false, 2); Assert.That(p.NumeroCompilazioni, Is.EqualTo(1));
                p.Aggiorna(Stato(41), true, 2); Assert.That(p.NumeroCompilazioni, Is.EqualTo(2));
                e.Avvia(); Tick(e, 1); Assert.That(e.Stato.PartituraAttiva.Generazione, Is.EqualTo(2));
            }
        }
    }
}
