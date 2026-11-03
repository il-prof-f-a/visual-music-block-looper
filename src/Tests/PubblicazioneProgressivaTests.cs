using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    public class PubblicazioneProgressivaTests
    {
        private sealed class Uscita : IUscitaMidi,IUscitaMidiPreparabile
        {
            public int Attacchi,Arresti;public bool Blocca,FallisceUnaVolta;
            public readonly ManualResetEventSlim Entrato=new ManualResetEventSlim(),Libera=new ManualResetEventSlim();
            public void Clock(){}public void Start(){}public void Stop(){Arresti++;}public void Continue(){}
            public void NotaOn(int c,int n,int v){Attacchi++;}public void NotaOff(int c,int n){}
            public Action PreparaNotaOn(int c,int n,int v)
            {
                if(FallisceUnaVolta&&n==77){FallisceUnaVolta=false;throw new InvalidOperationException("Driver temporaneamente indisponibile");}
                if(Blocca&&n==77){Entrato.Set();if(!Libera.Wait(5000))throw new TimeoutException();}return ()=>NotaOn(c,n,v);
            }
            public Action PreparaNotaOff(int c,int n)=>()=>NotaOff(c,n);public Action PreparaClock()=>Clock;
            public Action PreparaStart()=>Start;public Action PreparaStop()=>Stop;public Action PreparaContinue()=>Continue;
        }
        [TestCase(false)] [TestCase(true)]
        public void PrimoGruppoProntoAvviaSenzaAspettareIlRestoMaSimultaneiRestanoUniti(bool simultanei)
        {
            var u=new Uscita();
            using(var e=new EsecutoreMidi(u,null,new EsecutorePartituraTests.Mappa(),new ConfigGriglia(),()=>0))
            {
                u.Blocca=true;StrutturaTavolo.TryCrea(2,1,1,2,out var struttura);
                var foglio=new StatoTavolo(new[]{new CartoncinoNota{Riga=0,Traccia=0,IdClip=40,DurataStep=1},
                    new CartoncinoNota{Riga=simultanei?1:0,Traccia=1,IdClip=41,StepIniziale=simultanei?0:4,DurataStep=1}},null,null,struttura);
                e.Avvia();e.ElaboraPendenti();
                var produttore=Task.Run(()=>e.PreparaProgressiva(foglio,1,1,0,p=>e.Pubblica(p)));
                try
                {
                    Assert.That(u.Entrato.Wait(2000));e.TickInterno();
                    Assert.That(e.InEsecuzione,Is.EqualTo(!simultanei));Assert.That(u.Attacchi,Is.EqualTo(simultanei?0:1));
                    if(!simultanei)
                    {
                        for(int i=0;i<24;i++)e.TickInterno();
                        Assert.That(e.InEsecuzione,Is.False);Assert.That(u.Arresti,Is.EqualTo(1));
                        Assert.That(e.UltimoErrore,Does.Contain("produttore"));
                    }
                    else e.Arresta();
                    u.Libera.Set();Assert.That(produttore.Wait(2000));e.ElaboraPendenti();e.TickInterno();
                    Assert.That(e.InEsecuzione,Is.False,"Una pubblicazione tardiva non deve annullare Stop o riavviare da sola.");
                }
                finally{u.Libera.Set();produttore.Wait(5000);u.Entrato.Dispose();u.Libera.Dispose();}
            }
        }
        [Test]
        public void UnErroreDopoLaPrimaPubblicazionePermetteDiRiprovareLoStessoFoglio()
        {
            var u=new Uscita();
            using(var e=new EsecutoreMidi(u,null,new EsecutorePartituraTests.Mappa(),new ConfigGriglia(),()=>0))
            {
                var p=new PreparatoreFoglio(e);u.FallisceUnaVolta=true;
                var foglio=new StatoTavolo(new[]{new CartoncinoNota{Riga=0,Traccia=0,IdClip=40,DurataStep=1},
                    new CartoncinoNota{Riga=0,Traccia=0,IdClip=41,StepIniziale=4,DurataStep=1}},null,null);
                Assert.That(p.PreparaAvvio(foglio,1,0),Is.False);e.ElaboraPendenti();
                Assert.That(p.PreparaAvvio(foglio,1,1),Is.True);
                e.Avvia();e.TickInterno();
                Assert.That(e.InEsecuzione,Is.True,"Il candidato guasto non deve impedire il retry della stessa scena.");
                Assert.That(e.Stato.VersioneAttiva,Is.EqualTo(2));
            }
            u.Entrato.Dispose();u.Libera.Dispose();
        }
        [TestCase(true)] [TestCase(false)]
        public void CambiareGenerazioneDuranteLaPreparazioneInvalidaIlRisultato(bool bloccaPrimoGruppo)
        {
            var u=new Uscita();
            using(var e=new EsecutoreMidi(u,null,new EsecutorePartituraTests.Mappa(),new ConfigGriglia(),()=>0))
            {
                var p=new PreparatoreFoglio(e);u.Blocca=true;int notifiche=0;
                var foglio=new StatoTavolo(new[]{new CartoncinoNota{Riga=0,Traccia=0,IdClip=bloccaPrimoGruppo?41:40,DurataStep=1},
                    new CartoncinoNota{Riga=0,Traccia=0,IdClip=41,StepIniziale=4,DurataStep=1}},null,null);
                var lavoro=Task.Run(()=>p.PreparaAvvio(foglio,1,0,()=>Interlocked.Increment(ref notifiche)));
                try
                {
                    Assert.That(u.Entrato.Wait(2000));p.ImpostaGenerazione(2);u.Libera.Set();
                    Assert.That(lavoro.Wait(2000));Assert.That(lavoro.Result,Is.False);
                    Assert.That(notifiche,Is.EqualTo(bloccaPrimoGruppo?0:1));Assert.That(e.Stato.VersionePronta,Is.Zero);
                }
                finally{u.Libera.Set();lavoro.Wait(5000);u.Entrato.Dispose();u.Libera.Dispose();}
            }
        }
        [Test]
        public void SbloccandoIlPrimoGruppoSimultaneoPartonoEntrambeLeVoci()
        {
            var u=new Uscita();
            using(var e=new EsecutoreMidi(u,null,new EsecutorePartituraTests.Mappa(),new ConfigGriglia(),()=>0))
            {
                u.Blocca=true;StrutturaTavolo.TryCrea(2,1,1,2,out var struttura);
                var foglio=new StatoTavolo(new[]{new CartoncinoNota{Riga=0,Traccia=0,IdClip=40,DurataStep=1},
                    new CartoncinoNota{Riga=1,Traccia=1,IdClip=41,DurataStep=1}},null,null,struttura);
                e.Avvia();e.ElaboraPendenti();var lavoro=Task.Run(()=>e.PreparaProgressiva(foglio,1,1,0,p=>e.Pubblica(p)));
                try
                {
                    Assert.That(u.Entrato.Wait(2000));e.TickInterno();Assert.That(e.InEsecuzione,Is.False);
                    u.Libera.Set();Assert.That(lavoro.Wait(2000));e.TickInterno();
                    Assert.That(e.InEsecuzione,Is.True);Assert.That(u.Attacchi,Is.EqualTo(2));
                }
                finally{u.Libera.Set();lavoro.Wait(5000);u.Entrato.Dispose();u.Libera.Dispose();}
            }
        }
    }
}
