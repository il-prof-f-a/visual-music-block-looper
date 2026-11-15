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
        private static Mat ScenaTap(string nome="tap")
        {
            var f=new Mat(900,1600,MatType.CV_8UC3,Scalar.White);
            using(var card=Cv2.ImRead(Path.Combine(GeneratoreCartoncini.CartellaCartoncini(),"comando-"+nome+".png")))
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
                ora=200;r.Pubblica(vuoto,200);g.Passo(true);ora=300;r.Pubblica(vuoto,300);g.Passo(true);ora=400;r.Pubblica(vuoto,400);g.Passo(true);
                ora=500;r.Pubblica(tap,500);g.Passo(true);ora=550;r.Pubblica(vuoto,550);g.Passo(true);
                ora=600;r.Pubblica(tap,600);g.Passo(true);ora=700;r.Pubblica(tap,700);g.Passo(true);ora=800;r.Pubblica(tap,800);g.Passo(true);e.ElaboraPendenti();
                Assert.That(e.Bpm,Is.EqualTo(100).Within(.01),"Tap usa l’apparizione a 600 ms, scartando quella transitoria a 500 ms e la latenza del decoder.");
            }
        }

        [Test]
        public void VariazioneDiUnSoloFrameNonAvviaLaLetturaDellaCorsia()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
            var c=NoteContigueVisionTests.Calibra(false);
            using(var vuoto=new Mat(900,1600,MatType.CV_8UC3,Scalar.White))
            using(var cambiato=vuoto.Clone())
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            using(var g=new GestoreComandiRapidi(r,()=>c,cfg,LayoutTavolo.Default,e,new RegistroCatture(),()=>ora))
            {
                r.Pubblica(vuoto,ora);g.Passo(true);
                ora=100;r.Pubblica(vuoto,ora);g.Passo(true);
                long letturePrima=g.NumeroLetture;
                Cv2.Rectangle(cambiato,new Rect(1390,160,80,80),Scalar.Black,-1);
                ora=200;r.Pubblica(cambiato,ora);g.Passo(true);
                ora=300;r.Pubblica(vuoto,ora);g.Passo(true);
                Assert.That(g.NumeroLetture,Is.EqualTo(letturePrima),
                    "Una variazione rimossa al frame successivo non deve avviare il decoder QR.");
            }
        }

        [Test]
        public void MolteVariazioniSparseNellaCorsiaComandiProduconoUnaLetturaPerFrame()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
            var c=NoteContigueVisionTests.Calibra(false);
            using(var vuoto=new Mat(900,1600,MatType.CV_8UC3,Scalar.White))
            using(var cambiato=vuoto.Clone())
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            using(var g=new GestoreComandiRapidi(r,()=>c,cfg,LayoutTavolo.Default,e,new RegistroCatture(),()=>ora))
            {
                r.Pubblica(vuoto,ora);g.Passo(true);
                ora=100;r.Pubblica(vuoto,ora);g.Passo(true);
                long letturePrima=g.NumeroLetture;

                foreach(int y in new[]{160,300,440,580,720})
                {
                    Cv2.Rectangle(cambiato,new Rect(1390,y,24,24),Scalar.Black,-1);
                    Cv2.Rectangle(cambiato,new Rect(1510,y,24,24),Scalar.Black,-1);
                }
                ora=200;r.Pubblica(cambiato,ora);g.Passo(true);
                Assert.That(g.NumeroLetture,Is.EqualTo(letturePrima));
                ora=300;r.Pubblica(cambiato,ora);g.Passo(true);

                Assert.That(g.NumeroLetture-letturePrima,Is.EqualTo(1),
                    "Più regioni cambiate nella corsia vanno riunite in una sola lettura QR del blocco.");
            }
        }

        [Test]
        public void DueApparizioniIsolateDiPlayNonAvvianoIlTrasporto()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};var c=NoteContigueVisionTests.Calibra(false);
            using(var play=ScenaTap("play"))
            using(var vuoto=new Mat(900,1600,MatType.CV_8UC3,Scalar.White))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            using(var g=new GestoreComandiRapidi(r,()=>c,cfg,LayoutTavolo.Default,e,new RegistroCatture(),()=>ora))
            {
                e.Pubblica(e.Prepara(new StatoTavolo(null,null,null),1,1,0));
                foreach(var f in new[]{vuoto,vuoto,play,vuoto,play}){ora+=100;r.Pubblica(f,ora);g.Passo(true);e.ElaboraPendenti();}
                Assert.That(e.InEsecuzione,Is.False,"Un ritorno affidabile al riferimento deve interrompere la candidata.");
                ora+=100;r.Pubblica(play,ora);g.Passo(true);e.ElaboraPendenti();Assert.That(e.InEsecuzione,Is.False);
                ora+=100;r.Pubblica(play,ora);g.Passo(true);e.ElaboraPendenti();Assert.That(e.InEsecuzione,Is.True);
            }
        }
        [Test]
        public void FrameInaffidabiliNonRiarmanoUnComandoGiaEmesso()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};var c=NoteContigueVisionTests.Calibra(false);
            using(var modo=ScenaTap("modo"))
            using(var due=modo.Clone())
            using(var tap=ScenaTap())
            using(var vuoto=new Mat(900,1600,MatType.CV_8UC3,Scalar.White))
            using(var scuro=new Mat(900,1600,MatType.CV_8UC3,new Scalar(10,10,10)))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            using(var g=new GestoreComandiRapidi(r,()=>c,cfg,LayoutTavolo.Default,e,new RegistroCatture(),()=>ora))
            {
                using(var fonte=new Mat(tap,new Rect(1380,120,180,120)))
                using(var destinazione=new Mat(due,new Rect(1380,380,180,120)))fonte.CopyTo(destinazione);
                foreach(var f in new[]{vuoto,vuoto,modo,modo,due})
                {ora+=100;r.Pubblica(f,ora);g.Passo(true);e.ElaboraPendenti();}
                Assert.That(e.Modo,Is.EqualTo(ModoSincronia.Slave),"Il comando deve essere già emesso prima dell’occlusione.");
                foreach(var f in new[]{scuro,scuro,modo,modo})
                {ora+=100;r.Pubblica(f,ora);g.Passo(true);e.ElaboraPendenti();}
                Assert.That(e.Modo,Is.EqualTo(ModoSincronia.Slave),"L'occlusione non equivale a due rimozioni affidabili.");
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
