using System;
using NUnit.Framework;
using System.Threading.Tasks;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class TrasportoLineareTests
    {
        private sealed class UscitaNulla : IUscitaMidi
        {
            public void Clock(){}public void Start(){}public void Stop(){}public void Continue(){}
            public void NotaOn(int c,int n,int v){}public void NotaOff(int c,int n){}
        }
        [Test]
        public void SnapshotLettoInConcorrenzaNonMescolaTickEMisura()
        {
            var u=new UscitaNulla();var s=StrutturaTavolo.Default;
            var m=new MotorePartitura(u,new EsecutorePartituraTests.Mappa(),120,()=>0);
            var p=new CompilatorePartitura(new ConfigGriglia(),new EsecutorePartituraTests.Mappa())
                .Compila(new StatoTavolo(null,null,null,s),1,1);
            m.Pubblica(PartituraMidiPreparata.Prepara(p,u));m.Avvia();m.TickInterno();
            var writer=Task.Run(()=>{for(int n=0;n<20000;n++)m.TickInterno();});
            for(int i=0;i<20000;i++)
            {
                var t=m.Trasporto;
                Assert.That(t.IndiceMisura,Is.EqualTo(t.MisuraAssoluta%6));
                Assert.That(t.TickNelGiro/96,Is.EqualTo(t.IndiceMisura));
                Assert.That(t.Partitura.Struttura,Is.SameAs(t.Struttura));
            }
            Assert.That(writer.Wait(5000),Is.True);
        }
        [Test]
        public void MasterInterpolaIlTickRealeSenzaSuperareIlTickSuccessivo()
        {
            long ora=100;
            var u=new EsecutorePartituraTests.Uscita();
            var m=new MotorePartitura(u,new EsecutorePartituraTests.Mappa(),120,()=>ora);
            m.Pubblica(EsecutorePartituraTests.Partitura(u));m.Avvia();m.TickInterno();
            var t=m.Trasporto;
            Assert.That(t.PosizioneLineare(100).Step,Is.Zero);
            Assert.That(t.PosizioneLineare(110).Step,Is.EqualTo(.08).Within(1e-6));
            Assert.That(t.PosizioneLineare(200).Step,Is.EqualTo(1.0/6).Within(1e-6));
            int eventi=u.Eventi.Count;
            for(int i=0;i<1000;i++)m.Trasporto.PosizioneLineare(110);
            Assert.That(u.Eventi.Count,Is.EqualTo(eventi),"Leggere la proiezione non produce MIDI.");
            m.Arresta();
            Assert.That(m.Trasporto.InEsecuzione,Is.False);
            Assert.That(m.Trasporto.PosizioneLineare(110).Step,Is.EqualTo(m.Trasporto.PosizioneLineare(1000).Step));
        }

        [Test]
        public void SlaveUsaLIstanteDelClockRicevutoAncheSeIlConsumatoreLoElaboraDopo()
        {
            long ora=100;
            var u=new EsecutorePartituraTests.Uscita();
            var m=new MotorePartitura(u,new EsecutorePartituraTests.Mappa(),120,()=>ora);
            m.Pubblica(EsecutorePartituraTests.Partitura(u));m.CambiaModo();m.RiceviStart();
            ora=105;m.RiceviClock(100,20);
            ora=127;m.RiceviClock(120,20);
            Assert.That(m.Trasporto.PosizioneLineare(130).Step,Is.EqualTo(.25).Within(1e-6));
            m.RiceviStop();
            Assert.That(m.Trasporto.InEsecuzione,Is.False);
        }

        [Test]
        public void AnticipoConservaFrazioneERigheAlConfineDiUnGiroParziale()
        {
            StrutturaTavolo.TryCrea(4,2,2,6,out var s);
            var t=new IstantaneaTrasporto(s,1,1,true,0,0,36,100,20);
            var presente=t.PosizioneLineare(110);
            var futura=t.PosizioneLineare(110,1);
            Assert.That(presente.Step,Is.EqualTo(4+36.5/6).Within(1e-6));
            Assert.That(futura.MisuraAssoluta,Is.EqualTo(1));
            Assert.That(futura.RigheAttive,Is.EqualTo(new[]{1,3,5}));
            Assert.That(futura.Step,Is.EqualTo((36.5/72)*8).Within(1e-6),
                "La fase deve essere proiettata sulla lunghezza effettiva della misura parziale futura.");
        }
    }
}
