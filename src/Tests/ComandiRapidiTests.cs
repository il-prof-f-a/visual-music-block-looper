using System;
using System.IO;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class ComandiRapidiTests
    {
        private static Mat ScenaTap()
        {
            var f=new Mat(900,1600,MatType.CV_8UC3,Scalar.White);
            using(var card=Cv2.ImRead(Path.Combine(GeneratoreCartoncini.CartellaCartoncini(),"comando-tap.png")))
            using(var piccolo=new Mat())
            using(var destinazione=new Mat(f,new Rect(1380,120,180,120)))
            {Cv2.Resize(card,piccolo,new Size(180,120));piccolo.CopyTo(destinazione);}
            return f;
        }
        [Test]
        public void TapUsaIlPrimoFrameEIlCartoncinoFermoNonVieneRidecodificato()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
            var c=NoteContigueVisionTests.Calibra(false);
            using(var tap=ScenaTap())
            using(var vuoto=new Mat(900,1600,MatType.CV_8UC3,Scalar.White))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            using(var g=new GestoreComandiRapidi(r,()=>c,cfg,LayoutTavolo.Default,e,new RegistroCatture(),()=>ora))
            {
                r.Pubblica(tap,0);g.Passo(true);ora=100;r.Pubblica(tap,100);g.Passo(true);e.ElaboraPendenti();
                long letture=g.NumeroLetture;
                ora=150;r.Pubblica(tap,150);g.Passo(true);Assert.That(g.NumeroLetture,Is.EqualTo(letture));
                ora=200;r.Pubblica(vuoto,200);g.Passo(true);ora=300;r.Pubblica(vuoto,300);g.Passo(true);
                ora=600;r.Pubblica(tap,600);g.Passo(true);ora=700;r.Pubblica(tap,700);g.Passo(true);e.ElaboraPendenti();
                Assert.That(e.Bpm,Is.EqualTo(100).Within(.01));
            }
        }
        [Test]
        public void UnaRisorsaInUsoSiRilasciaSoltantoQuandoIlLettoreEsce()
        {
            int liberazioni=0;var vita=new RilascioDifferito(()=>liberazioni++);
            Assert.That(vita.ProvaEntra());vita.Dispose();Assert.That(liberazioni,Is.Zero);
            Assert.That(vita.ProvaEntra(),Is.False);vita.Esci();vita.Dispose();Assert.That(liberazioni,Is.EqualTo(1));
        }
    }
}
