using System;
using System.Linq;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class IncrementalitaFoglioTests
    {
        [Test]
        public void DueModificheIsolateNonConfermanoUnaNuovaPartitura()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};var c=NoteContigueVisionTests.Calibra(false);
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var vuoto=new Mat(900,1600,MatType.CV_8UC3,Scalar.White))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);
                using(var a=new AnalizzatoreFoglio(r,()=>c,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>ora))
                {
                    foreach(var frame in new[]{vuoto,vuoto,f,vuoto,f}){ora+=100;r.Pubblica(frame,ora);a.Passo(true);}
                    Assert.That(a.UltimoStato.Note,Is.Empty,"A,A,B,A,B non sono due conferme consecutive di B.");
                    Assert.That(p.NumeroCompilazioni,Is.EqualTo(1));
                    ora+=100;r.Pubblica(f,ora);a.Passo(true);Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10));
                }
            }
        }
        [Test]
        public void FoglioFermoNonVieneRidecodificatoONuovamenteCompilato()
        {
            long ora=0;var u=new EsecutorePartituraTests.Uscita();var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(u,null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);var c=NoteContigueVisionTests.Calibra(false);
                using(var a=new AnalizzatoreFoglio(r,()=>c,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>ora))
                {
                    for(int i=0;i<2;i++){ora+=100;r.Pubblica(f,ora);a.Passo(true);}
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10));Assert.That(p.NumeroCompilazioni,Is.EqualTo(1));
                    long letture=a.NumeroLettureZone;
                    for(int i=0;i<6;i++){ora+=100;r.Pubblica(f,ora);a.Passo(true);}
                    Assert.That(a.NumeroLettureZone,Is.EqualTo(letture));Assert.That(p.NumeroCompilazioni,Is.EqualTo(1));
                }
            }
        }
        [Test]
        public void UnaRimozioneConfermataAggiornaSoltantoLaRegioneCoinvolta()
        {
            long ora=0;var u=new EsecutorePartituraTests.Uscita();var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(u,null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);var c=NoteContigueVisionTests.Calibra(false);
                using(var a=new AnalizzatoreFoglio(r,()=>c,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>ora))
                {
                    for(int i=0;i<2;i++){ora+=100;r.Pubblica(f,ora);a.Passo(true);}
                    Cv2.Rectangle(f,new Rect(225,310,200,140),Scalar.White,-1);
                    for(int i=0;i<2;i++){ora+=100;r.Pubblica(f,ora);a.Passo(true);}
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(9));
                    Assert.That(a.UltimeRegioni.All(z=>z.Larghezza<.4&&z.Altezza<.5));
                    Assert.That(p.NumeroCompilazioni,Is.EqualTo(2));
                }
            }
        }
        [Test]
        public void UnFotogrammaGiaUsatoNonContaComeSecondaConferma()
        {
            var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>0))
            {
                var p=new PreparatoreFoglio(e);var c=NoteContigueVisionTests.Calibra(false);
                using(var a=new AnalizzatoreFoglio(r,()=>c,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>0))
                {
                    r.Pubblica(f,0);a.Passo(true);a.Passo(true);
                    Assert.That(p.NumeroCompilazioni,Is.EqualTo(0));
                    r.Pubblica(f,1);a.Passo(true);Assert.That(p.NumeroCompilazioni,Is.EqualTo(1));
                }
            }
        }
    }
}
