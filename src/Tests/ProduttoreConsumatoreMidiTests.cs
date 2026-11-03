using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    public class ProduttoreConsumatoreMidiTests
    {
        private sealed class Uscita : IUscitaMidi,IUscitaMidiPreparabile
        {
            public readonly ManualResetEventSlim PreparazioneEntrata=new ManualResetEventSlim();
            public readonly ManualResetEventSlim LiberaPreparazione=new ManualResetEventSlim();
            public bool Blocca; public int ClockTotali,Arresti;
            public void Clock()=>ClockTotali++;public void Stop()=>Arresti++;
            public void Start(){}public void Continue(){}public void NotaOn(int c,int n,int v){}public void NotaOff(int c,int n){}
            public Action PreparaClock()=>Clock;public Action PreparaStart()=>Start;public Action PreparaStop()=>Stop;public Action PreparaContinue()=>Continue;
            public Action PreparaNotaOff(int c,int n)=>()=>NotaOff(c,n);
            public Action PreparaNotaOn(int c,int n,int v)
            {
                if(Blocca){PreparazioneEntrata.Set();if(!LiberaPreparazione.Wait(5000))throw new TimeoutException();}
                return ()=>NotaOn(c,n,v);
            }
        }
        [Test]
        public void ProduttoreLentoNonBloccaConsumoStopOPassaggioAllaFrontieraSuccessiva()
        {
            var u=new Uscita();StrutturaTavolo.TryCrea(2,1,1,2,out var struttura);
            Func<int,StatoTavolo> foglio=clip=>new StatoTavolo(new[]{new CartoncinoNota{Riga=0,Traccia=0,IdClip=clip,DurataStep=2}},null,null,struttura);
            using(var e=new EsecutoreMidi(u,null,new EsecutorePartituraTests.Mappa(),new ConfigGriglia(),()=>0))
            {
                e.Pubblica(e.Prepara(foglio(40),1,1,0));e.Avvia();e.TickInterno();u.Blocca=true;
                Task produttore=Task.Run(()=>e.Pubblica(e.Prepara(foglio(41),2,1,1)));
                try
                {
                    Assert.That(u.PreparazioneEntrata.Wait(2000),Is.True);
                    for(int i=0;i<48;i++)e.TickInterno();
                    Assert.That(u.ClockTotali,Is.EqualTo(49));Assert.That(e.Stato.Giro,Is.EqualTo(1));
                    Assert.That(e.Stato.VersioneAttiva,Is.EqualTo(1),"Il giro valido si ripete se il produttore non ha finito.");
                    u.LiberaPreparazione.Set();Assert.That(produttore.Wait(2000));
                    for(int i=0;i<47;i++)e.TickInterno();Assert.That(e.Stato.VersioneAttiva,Is.EqualTo(1));
                    e.TickInterno();Assert.That(e.Stato.VersioneAttiva,Is.EqualTo(2));
                    u.LiberaPreparazione.Reset();u.PreparazioneEntrata.Reset();
                    produttore=Task.Run(()=>e.Pubblica(e.Prepara(foglio(42),3,1,3)));
                    Assert.That(u.PreparazioneEntrata.Wait(2000));e.Arresta();e.ElaboraPendenti();
                    Assert.That(e.InEsecuzione,Is.False);Assert.That(u.Arresti,Is.EqualTo(1));
                }
                finally{u.LiberaPreparazione.Set();produttore.Wait(5000);u.PreparazioneEntrata.Dispose();u.LiberaPreparazione.Dispose();}
            }
        }
    }
}
