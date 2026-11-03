using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    [Apartment(ApartmentState.STA)]
    public class AnteprimaAsincronaTests
    {
        private sealed class Camera : ISorgenteWebcam
        {
            public int Larghezza=>800;public int Altezza=>450;public double FpsCatturati=>0;
            public Mat Frame()=>null;
            public Bitmap Anteprima()=>throw new InvalidOperationException("La UI non deve accedere alla camera.");
            public Bitmap AnteprimaRaddrizzata(Calibrazione c,int w,int h)=>throw new InvalidOperationException("Il remap non deve girare sulla UI.");
            public void ApplicaImpostazioni(ConfigurazioneWebcam c){}public StatoImpostazioniWebcam LeggiImpostazioni()=>StatoImpostazioniWebcam.NonSupportato();public void Dispose(){}
        }
        private sealed class Fonte : IFonteImmagini {public Mat Frame()=>null;}
        [Test]
        public void FinestraPrelevaImmaginiPronteECalibrazioneUsaLaRisoluzioneOriginale()
        {
            var cfg=new ConfigGriglia();var rilevatore=new RilevatoreCommutabile();var cal=NoteContigueVisionTests.Calibra(false,800,450);
            using(var webcam=new GestoreWebcam(i=>new Camera()))
            using(var c=new Compositore(rilevatore,new UscitaMidiSilenziosa(),null,new EsecutorePartituraTests.Mappa(),cfg,
                orologioMs:()=>0,esecuzioneDeterministica:true,fonteVideo:new Fonte()))
            {
                webcam.Apri(0);rilevatore.Imposta(new RilevatoreCartoncini(webcam,cal));
                string file=Path.Combine(Path.GetTempPath(),"vmbl-cal-asinc-"+Guid.NewGuid().ToString("N")+".txt");
                using(var f=new FormControllo(c,new FormProiettore(cfg),webcam,rilevatore,file,new ConfigurazioneApplicazione(),null,
                    new List<InfoWebcam>(),()=>0,"MIDI",new string[0],new IDisposable[0],true))
                using(var frame=new Mat(450,800,MatType.CV_8UC3,new Scalar(40,80,120)))
                {
                    var tipo=typeof(FormControllo);var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                    c.Video.Registro.Pubblica(frame,0);
                    var prep=(PreparatoreAnteprima)tipo.GetField("preparatoreAnteprima",flags).GetValue(f);prep.Passo();
                    Assert.That(tipo.GetMethod("AggiornaAnteprima",flags).Invoke(f,null),Is.EqualTo(true));
                    tipo.GetField("inCalibrazione",flags).SetValue(f,true);
                    Assert.That(tipo.GetMethod("AggiornaAnteprima",flags).Invoke(f,null),Is.EqualTo(true));
                    Assert.That(f.Controls.OfType<PictureBox>().Single().Image.Size,Is.EqualTo(new System.Drawing.Size(800,450)));
                    var punti=(List<PuntoImmagine>)tipo.GetField("puntiCalibrazione",flags).GetValue(f);
                    punti.AddRange(SchemaCalibrazione.Punti.Select(p=>new PuntoImmagine(p.X*800,p.Y*450)));
                    tipo.GetField("dimensioniFrame",flags).SetValue(f,new System.Drawing.Size(800,450));
                    try
                    {
                        tipo.GetMethod("CompletaCalibrazione",flags).Invoke(f,null);
                        Assert.That(File.Exists(file),Is.True,"Anche il controllo finale deve usare i frame pubblicati, senza chiamare la camera sulla UI.");
                    }
                    finally{if(File.Exists(file))File.Delete(file);}
                }
            }
        }
        [Test]
        public void PreparazioneLentaNonBloccaIlPrelievoENonPubblicaDopoDispose()
        {
            using(var entrato=new ManualResetEventSlim())using(var libera=new ManualResetEventSlim())
            using(var registro=new RegistroFotogrammi())using(var frame=new Mat(90,160,MatType.CV_8UC3,Scalar.White))
            {
                registro.Pubblica(frame,0);
                var p=new PreparatoreAnteprima(registro,()=>{entrato.Set();libera.Wait(5000);return null;});
                var lavoro=Task.Run(()=>p.Passo());
                try{Assert.That(entrato.Wait(2000));Assert.That(p.Preleva(),Is.Null);p.Dispose();}
                finally{libera.Set();Assert.That(lavoro.Wait(2000));p.Dispose();}
                Assert.That(p.Preleva(),Is.Null);
            }
        }
    }
}
