using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    public class AvvioIstantaneaTests
    {
        [TestCase(false)] [TestCase(true)]
        public void UnSoloFrameConNoteValideAvviaAncheConUnCorpoNonDecifrabile(bool capovolta)
        {
            var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};var cal=NoteContigueVisionTests.Calibra(capovolta);
            using(var frame=NoteContigueVisionTests.Scena(false))
            using(var registro=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>0))
            {
                Cv2.Rectangle(frame,new Rect(650,60,180,110),new Scalar(35,35,35),-1);
                if(capovolta)Cv2.Rotate(frame,frame,RotateFlags.Rotate180);
                var p=new PreparatoreFoglio(e);
                using(var a=new AnalizzatoreFoglio(registro,()=>cal,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>0))
                {
                    e.Avvia();e.ElaboraPendenti();registro.Pubblica(frame,0);
                    var tempo=Stopwatch.StartNew();a.Passo(true);e.TickInterno();
                    TestContext.WriteLine("Istantanea→avvio: "+tempo.Elapsed.TotalMilliseconds+"ms");
                    Assert.That(e.InEsecuzione,Is.True,"La prima lettura valida non deve aspettare due frame o tutti i corpi.");
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10));
                }
            }
        }
        [Test]
        public void LetturaRapidaOrdinaPerRigoPoiPerXEMantieneLeDurate()
        {
            var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
            using(var frame=NoteContigueVisionTests.Scena(false))
            {
                var r=new LettoreIstantanea(NoteContigueVisionTests.Calibra(false),cfg).Leggi(frame);
                Assert.That(r.Cartoncini,Has.Count.EqualTo(10));
                Assert.That(r.RegioniDaRileggere.Any(z=>r.Cartoncini.Any(c=>
                    GeometriaRegioni.Interseca(z,c.AreaQr??c.Area))),Is.True,
                    "Un corpo con QR già letti può contenere altri QR: va verificato in background.");
                var s=AggancioQrTests.Interprete().Interpreta(r.Cartoncini,null,true);
                Assert.That(s.Note.Select(n=>n.Riga),Is.EqualTo(new[]{2,2,2,2,2,3,3,3,3,3}));
                foreach(int riga in new[]{2,3})
                    Assert.That(s.Note.Where(n=>n.Riga==riga).Select(n=>n.DurataStep),Is.EqualTo(new[]{3,3,3,2,3}));
            }
        }
        [Test]
        public void MancanzaFrameScadeENonRestaInPreparazione()
        {
            long ora=0;var cfg=new ConfigGriglia();
            using(var registro=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            using(var a=new AnalizzatoreFoglio(registro,()=>null,cfg,LayoutTavolo.Default,new PreparatoreFoglio(e),new RegistroCatture(),()=>ora))
            {
                e.Avvia();e.ElaboraPendenti();ora=1001;a.Passo();e.ElaboraPendenti();
                Assert.That(e.Stato.InPreparazione,Is.False);Assert.That(e.UltimoErrore,Does.Contain("frame"));
            }
        }
        [TestCase(true)] [TestCase(false)]
        public void CalibrazioneAssenteOImmagineBuiaRifiutanoAvvioConMotivo(bool mancaCalibrazione)
        {
            var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
            var cal=mancaCalibrazione?null:NoteContigueVisionTests.Calibra(false);
            using(var frame=new Mat(900,1600,MatType.CV_8UC3,Scalar.Black))
            using(var registro=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>0))
            using(var a=new AnalizzatoreFoglio(registro,()=>cal,cfg,LayoutTavolo.Default,new PreparatoreFoglio(e),new RegistroCatture(),()=>0))
            {
                registro.Pubblica(frame,0);e.Avvia();e.ElaboraPendenti();a.Passo(true);e.ElaboraPendenti();
                Assert.That(e.Stato.InPreparazione,Is.False);Assert.That(e.InEsecuzione,Is.False);
                Assert.That(e.UltimoErrore,Does.Contain(mancaCalibrazione?"Calibrazione":"QR"));
            }
        }
        [Test]
        public void UnErroreDiAnalisiTerminaLaPreparazione()
        {
            var cfg=new ConfigGriglia();var cal=NoteContigueVisionTests.Calibra(false);
            using(var frame=new Mat(900,1600,MatType.CV_8UC2,Scalar.Black))
            using(var registro=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>0))
            using(var a=new AnalizzatoreFoglio(registro,()=>cal,cfg,LayoutTavolo.Default,new PreparatoreFoglio(e),new RegistroCatture(),()=>0))
            {
                registro.Pubblica(frame,0);e.Avvia();e.ElaboraPendenti();a.Passo(true);e.ElaboraPendenti();
                Assert.That(e.Stato.InPreparazione,Is.False);Assert.That(e.UltimoErrore,Does.Contain("Analisi non applicata"));
            }
        }
    }
}
