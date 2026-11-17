using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;
namespace VisualMusicBlockLooper.Tests
{
    public class LettoreFinestraCelleTests
    {
        private static ConfigGriglia Griglia()=>new ConfigGriglia();
        private static CartoncinoRilevato Nota(double fine=.4)=>new CartoncinoRilevato { Payload="VMBL1;N;64",Colore=ColoreTraccia.Rosso,Area=new RettangoloTavolo(.1,.2,.3,.1),AreaQr=new RettangoloTavolo(.12,.22,.05,.05),FineEstensioneX=fine };
        private static LettoreFinestraCelle Lettore()=>new LettoreFinestraCelle(NoteContigueVisionTests.Calibra(false,1000,600),Griglia());
        [Test] public void MascheraEDecoderRestanoNelleTreCelle()
        {
            using(var f=new Mat(600,1000,MatType.CV_8UC3,Scalar.White))
            {
                var r=Lettore().Leggi(f,new RettangoloTavolo(.375,.17,3.0/16,1.0/6),new[]{Nota()});
                Assert.That(r.RegioneMaschera,Is.EqualTo(r.RegioneCamera));
                Assert.That(r.RegioneCamera.Width,Is.LessThan(210));
                Assert.That(r.RegioneCamera.Height,Is.LessThan(120));
                Assert.That(r.Catalogo,Has.Count.EqualTo(1));
            }
        }
        [TestCase(false)] [TestCase(true)] public void VuotoAffidabileRimuoveSoloQrLocale_OscuramentoLoMantiene(bool oscuro)
        {
            using(var f=new Mat(600,1000,MatType.CV_8UC3,oscuro?Scalar.All(15):Scalar.White))
            {
                var r=Lettore().Leggi(f,new RettangoloTavolo(.1,.17,.1875,1.0/6),new[]{Nota(),new CartoncinoRilevato{Payload="fuori",Area=new RettangoloTavolo(.8,.7,.05,.05)}});
                Assert.That(r.Catalogo.Count,Is.EqualTo(oscuro?2:1));
                Assert.That(r.Completa,Is.EqualTo(!oscuro));
            }
        }
        [TestCase(false)] [TestCase(true)] public void CodaNotaConQrASinistraSegueSoloColoreContiguo(bool interrompi)
        {
            using(var f=new Mat(600,1000,MatType.CV_8UC3,Scalar.White))
            {
                Cv2.Rectangle(f,new Rect(370,120,190,60),new Scalar(40,40,220),-1);
                if(interrompi)Cv2.Rectangle(f,new Rect(460,120,100,60),new Scalar(220,70,30),-1);
                var n=Nota();var r=Lettore().Leggi(f,new RettangoloTavolo(.375,.17,.1875,1.0/6),new[]{n});
                Assert.That(r.Catalogo.Single().FineEstensioneX,Is.EqualTo(interrompi?.46:.56).Within(.006));
                Assert.That(n.FineEstensioneX,Is.EqualTo(.4),"Il catalogo precedente e immutabile");
            }
        }
        [Test] public void VuotoPrimaDelColoreNonCollegaUnaCodaSeparata()
        {
            using(var f=new Mat(600,1000,MatType.CV_8UC3,Scalar.White))
            {
                Cv2.Rectangle(f,new Rect(430,120,130,60),new Scalar(40,40,220),-1);
                var r=Lettore().Leggi(f,new RettangoloTavolo(.375,.17,.1875,1.0/6),new[]{Nota()});
                Assert.That(r.Catalogo.Single().FineEstensioneX,Is.EqualTo(.375).Within(.006));
            }
        }
        [Test] public void FrontieraOsservataContinuaNellaFinestraSuccessiva()
        {
            using(var f=new Mat(600,1000,MatType.CV_8UC3,Scalar.White))
            {
                Cv2.Rectangle(f,new Rect(370,120,430,60),new Scalar(40,40,220),-1);
                var l=Lettore();var primo=l.Leggi(f,new RettangoloTavolo(.375,.17,.1875,1.0/6),new[]{Nota()});
                var secondo=l.Leggi(f,new RettangoloTavolo(.5625,.17,.1875,1.0/6),primo.Catalogo);
                Assert.That(secondo.Catalogo.Single().FineEstensioneX,Is.GreaterThan(.745));
            }
        }
        [Test] public void LeggeQrLocaleAncheCapovolto()
        {
            foreach(bool capovolta in new[]{false,true})using(var f=NoteContigueVisionTests.Scena(capovolta))
            {
                var r=new LettoreFinestraCelle(NoteContigueVisionTests.Calibra(capovolta),new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault})
                    .Leggi(f,new RettangoloTavolo(.145,.34,.15,.18),new CartoncinoRilevato[0]);
                Assert.That(r.QrDecodificati,Is.GreaterThanOrEqualTo(1));
                Assert.That(r.Catalogo.All(n=>n.AreaQr.HasValue),Is.True);
                Assert.That(r.Catalogo.First().Colore,Is.Not.Null);
                Assert.That(r.Catalogo.First().FineEstensioneX,Is.EqualTo(.2575).Within(.008));
            }
        }
        [Test] public void NuovoQrDellaStessaNotaConservaDueAttacchiETroncaLaPrecedente()
        {
            using(var f=NoteContigueVisionTests.Scena(false))
            {
                var precedente=AggancioQrTests.Nota(2,3,14,76);
                var r=new LettoreFinestraCelle(NoteContigueVisionTests.Calibra(false),new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault})
                    .Leggi(f,new RettangoloTavolo(.39,.347,.14,.15),new[]{precedente});
                var note=AggancioQrTests.Interprete().Interpreta(r.Catalogo).Note.OrderBy(n=>n.StepIniziale).ToArray();
                Assert.That(note.Select(n=>n.StepIniziale),Is.EqualTo(new[]{3,6}));
                Assert.That(note[0].DurataStep,Is.EqualTo(3));
            }
        }
        [Test] public void MisuraCostoSuFinestraVuotaRispettoAllaLetturaZona()
        {
            using(var f=new Mat(600,1000,MatType.CV_8UC3,Scalar.White))
            {
                var zona=new RettangoloTavolo(.375,.17,.1875,1.0/6);var l=Lettore();var vecchio=new LettoreZone(NoteContigueVisionTests.Calibra(false,1000,600),Griglia());
                l.Leggi(f,zona,new CartoncinoRilevato[0]);var sw=Stopwatch.StartNew();
                for(int i=0;i<5;i++)l.Leggi(f,zona,new CartoncinoRilevato[0]);sw.Stop();double locale=sw.Elapsed.TotalMilliseconds;
                sw.Restart();for(int i=0;i<5;i++)vecchio.Leggi(f,zona,new CartoncinoRilevato[0]);sw.Stop();
                TestContext.WriteLine($"5 finestre: locale={locale:F1}ms; precedente={sw.Elapsed.TotalMilliseconds:F1}ms");
                Assert.That(l.Leggi(f,zona,new CartoncinoRilevato[0]).Completa,Is.True);
            }
        }
    }
}




