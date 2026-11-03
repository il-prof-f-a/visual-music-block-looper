using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    public class BpmManualeTests
    {
        [Test]
        public void SceltaManualePassaDalConsumatoreEInPlayAspettaLaPulsazione()
        {
            var u=new EsecutorePartituraTests.Uscita();
            using(var e=new EsecutoreMidi(u,null,new EsecutorePartituraTests.Mappa(),new ConfigGriglia(),()=>0))
            {
                e.ImpostaBpm(150.5);Assert.That(e.Bpm,Is.EqualTo(120));e.ElaboraPendenti();Assert.That(e.Bpm,Is.EqualTo(150.5));
                e.Pubblica(EsecutorePartituraTests.Partitura(u));e.Avvia();for(int i=0;i<4;i++)e.TickInterno();
                e.ImpostaBpm(90.5);e.ElaboraPendenti();Assert.That(e.Bpm,Is.EqualTo(150.5));
                for(int i=0;i<20;i++)e.TickInterno();Assert.That(e.Bpm,Is.EqualTo(150.5));
                e.TickInterno();Assert.That(e.Bpm,Is.EqualTo(90.5));Assert.That(e.StepCorrente,Is.EqualTo(4));
                Assert.That(e.Stato.VersioneAttiva,Is.EqualTo(1));
            }
        }
        [Test]
        public void TapDopoStopSostituisceIlBpmManualePendente()
        {
            var u=new EsecutorePartituraTests.Uscita();
            using(var e=new EsecutoreMidi(u,null,new EsecutorePartituraTests.Mappa(),new ConfigGriglia(),()=>0))
            {
                e.Pubblica(EsecutorePartituraTests.Partitura(u));e.Avvia();for(int i=0;i<4;i++)e.TickInterno();
                e.ImpostaBpm(90);e.ElaboraPendenti();e.Arresta();e.ElaboraPendenti();
                e.Tap(0);e.Tap(1000);e.ElaboraPendenti();Assert.That(e.Bpm,Is.EqualTo(60));
                for(int i=0;i<25;i++)e.TickInterno();
                Assert.That(e.Bpm,Is.EqualTo(60));Assert.That(e.Stato.BpmRichiesto,Is.EqualTo(60));
            }
        }
        [Test]
        public void SceltaManualeAzzeraLaStoriaTapERispettaSlaveELimiti()
        {
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),new ConfigGriglia(),()=>0))
            {
                e.Tap(0);e.Tap(500);e.ImpostaBpm(90);e.Tap(750);e.ElaboraPendenti();Assert.That(e.Bpm,Is.EqualTo(90));
                e.Tap(1750);e.ElaboraPendenti();Assert.That(e.Bpm,Is.EqualTo(60));
                e.ImpostaBpm(1000);e.ElaboraPendenti();Assert.That(e.Bpm,Is.EqualTo(300));
                e.ImpostaBpm(0);e.ElaboraPendenti();Assert.That(e.Bpm,Is.EqualTo(30));
                e.CambiaModo();e.ElaboraPendenti();e.ImpostaBpm(180);e.ElaboraPendenti();Assert.That(e.Stato.Bpm,Is.EqualTo(30));
            }
        }
    }
}
