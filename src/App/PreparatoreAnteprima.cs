using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using OpenCvSharp;
using VisualMusicBlockLooper.Vision;
using Size=System.Drawing.Size;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Il consumatore UI possiede la coppia dopo Preleva; il produttore non la modifica più.</summary>
    public sealed class AnteprimaPronta : IDisposable
    {
        public AnteprimaPronta(Bitmap grezza,Bitmap raddrizzata,Calibrazione calibrazione)
        {Grezza=grezza;Raddrizzata=raddrizzata;Calibrazione=calibrazione;}
        public Bitmap Grezza {get;}
        public Bitmap Raddrizzata {get;}
        public Calibrazione Calibrazione {get;}
        public void Dispose(){Grezza?.Dispose();Raddrizzata?.Dispose();}
    }

    /// <summary>Ultima coppia raw/raddrizzata pronta. Nessuna camera, conversione o attesa sulla UI.</summary>
    public sealed class PreparatoreAnteprima : IDisposable
    {
        private sealed class Richiesta {public Size Dimensioni;}
        private readonly RegistroFotogrammi immagini;
        private readonly Func<Calibrazione> calibrazione;
        private readonly CicloCattura ciclo;
        private readonly RilascioDifferito vita;
        private Richiesta richiesta=new Richiesta{Dimensioni=new Size(520,293)};
        private Richiesta ultimaRichiesta;
        private Calibrazione ultimaCalibrazione;
        private long ultimoFrame;
        private AnteprimaPronta pronta;
        private string ultimoErrore;

        public PreparatoreAnteprima(RegistroFotogrammi immagini,Func<Calibrazione> calibrazione)
        {
            this.immagini=immagini??throw new ArgumentNullException(nameof(immagini));
            this.calibrazione=calibrazione??throw new ArgumentNullException(nameof(calibrazione));
            vita=new RilascioDifferito(()=>Interlocked.Exchange(ref pronta,null)?.Dispose());
            ciclo=new CicloCattura(Passo,100);
        }
        public string UltimoErrore=>Volatile.Read(ref ultimoErrore);
        public void Avvia()=>ciclo.Avvia();
        public void Richiedi(Size dimensioni)
        {
            if(dimensioni.Width<=0||dimensioni.Height<=0)return;
            if(Volatile.Read(ref richiesta).Dimensioni==dimensioni)return;
            Volatile.Write(ref richiesta,new Richiesta{Dimensioni=dimensioni});
        }
        public AnteprimaPronta Preleva()=>vita.InChiusura?null:Interlocked.Exchange(ref pronta,null);
        public void Passo()
        {
            if(!vita.ProvaEntra())return;
            try
            {
                var r=Volatile.Read(ref richiesta);
                using(var f=immagini.Copia())
                {
                    if(f==null)return;
                    var c=calibrazione();
                    if(f.Id==ultimoFrame&&ReferenceEquals(r,ultimaRichiesta)&&ReferenceEquals(c,ultimaCalibrazione))return;
                    Bitmap grezza=null,raddrizzata=null;
                    try
                    {
                        grezza=ABitmap(f.Immagine);
                        if(c!=null&&c.VerificaRisoluzione(f.Immagine.Cols,f.Immagine.Rows)!=EsitoRisoluzione.Diversa)
                            using(var rect=c.Raddrizza(f.Immagine,r.Dimensioni.Width,r.Dimensioni.Height))raddrizzata=ABitmap(rect);
                        if(vita.InChiusura||!ReferenceEquals(c,calibrazione())||immagini.GenerazioneCorrente!=f.Generazione)return;
                        var nuova=new AnteprimaPronta(grezza,raddrizzata,c);grezza=null;raddrizzata=null;
                        Interlocked.Exchange(ref pronta,nuova)?.Dispose();
                        ultimoFrame=f.Id;ultimaRichiesta=r;ultimaCalibrazione=c;
                        Volatile.Write(ref ultimoErrore,null);
                    }
                    finally{grezza?.Dispose();raddrizzata?.Dispose();}
                }
            }
            catch(Exception e){Volatile.Write(ref ultimoErrore,"Anteprima: "+e.Message);}
            finally{vita.Esci();}
        }
        private static Bitmap ABitmap(Mat immagine)
        {
            using(var convertita=new Mat())
            {
                Mat bgra=immagine;
                if(immagine.Channels()!=4)
                {
                    Cv2.CvtColor(immagine,convertita,immagine.Channels()==1?ColorConversionCodes.GRAY2BGRA:ColorConversionCodes.BGR2BGRA);
                    bgra=convertita;
                }
                using(var vista=new Bitmap(bgra.Cols,bgra.Rows,(int)bgra.Step(),PixelFormat.Format32bppArgb,bgra.Data))
                    return vista.Clone(new Rectangle(0,0,vista.Width,vista.Height),PixelFormat.Format32bppArgb);
            }
        }
        public void Dispose(){ciclo.Ferma();vita.Dispose();}
    }
}
