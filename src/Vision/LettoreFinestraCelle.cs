using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;
using ZXing;
using ZXing.Common;
using ZXing.Multi.QrCode;

namespace VisualMusicBlockLooper.Vision
{
    /// <summary>Legge solo la finestra corrente: nessuna maschera o ricerca QR su righe intere.</summary>
    public sealed class LettoreFinestraCelle
    {
        private readonly Calibrazione calibrazione;
        private readonly ConfigGriglia griglia;
        public LettoreFinestraCelle(Calibrazione calibrazione, ConfigGriglia griglia)
        {
            this.calibrazione=calibrazione??throw new ArgumentNullException(nameof(calibrazione));
            this.griglia=griglia??throw new ArgumentNullException(nameof(griglia));
        }
        public LetturaZona Leggi(Mat frame,RettangoloTavolo zona,IReadOnlyList<CartoncinoRilevato> noti,TracciaCartoncini traccia=null)
        {
            if(frame==null)throw new ArgumentNullException(nameof(frame));
            var catalogo=(noti??Array.Empty<CartoncinoRilevato>()).Select(GeometriaRegioni.Copia).ToList();
            var regione=GeometriaRegioni.InCamera(calibrazione,zona,frame.Cols,frame.Rows,6);
            if(regione.Width<4||regione.Height<4)return new LetturaZona(catalogo,false,0,regione){RegioneMaschera=regione};
            using(var crop=new Mat(frame,regione))
            using(var bgr=new Mat())
            using(var gray=new Mat())
            {
                if(crop.Channels()==3)crop.CopyTo(bgr);
                else Cv2.CvtColor(crop,bgr,crop.Channels()==4?ColorConversionCodes.BGRA2BGR:ColorConversionCodes.GRAY2BGR);
                Cv2.CvtColor(bgr,gray,ColorConversionCodes.BGR2GRAY);
                var colori=new sbyte[regione.Height,regione.Width];int colorati=0,scuri=0;
                for(int y=0;y<bgr.Rows;y++)for(int x=0;x<bgr.Cols;x++)
                {
                    var p=bgr.At<Vec3b>(y,x);var c=ClassificatoreColore.Classifica(p.Item2,p.Item1,p.Item0);
                    colori[y,x]=c.HasValue?(sbyte)c.Value:(sbyte)-1;if(c.HasValue)colorati++;
                    if(gray.At<byte>(y,x)<100)scuri++;
                }
                var letti=new List<CartoncinoRilevato>();
                // Sfondo chiaro uniforme: nessun finder QR, nessun decoder da invocare.
                if(scuri>30)
                {
                    letti=Decodifica(gray,regione,1);
                    // Un solo recupero locale, limitato a finestre piccole con corpo presente.
                    if(letti.Count==0&&colorati>50&&gray.Width<600&&gray.Height<400)
                        using(var grande=new Mat())
                        {Cv2.Resize(gray,grande,new Size(),2,2,InterpolationFlags.Linear);letti=Decodifica(grande,regione,2);}
                }
                bool completa=true;bool codaConosciuta=false;var associati=new HashSet<CartoncinoRilevato>();
                foreach(var letto in letti)
                {
                    if(!GeometriaRegioni.ContieneCentro(zona,letto.AreaQr.Value))continue;
                    var precedente=catalogo.FirstOrDefault(c=>GeometriaRegioni.StessaPosizioneQr(c.AreaQr??c.Area,letto.AreaQr.Value));
                    letto.Colore=ColoreQr(colori,regione,letto.AreaQr.Value,frame.Size())??precedente?.Colore;
                    if(letto.Colore.HasValue)letto.FineEstensioneX=Segui(colori,regione,letto,Math.Max(zona.X,letto.AreaQr.Value.X+1.15*letto.AreaQr.Value.Larghezza),zona.X+zona.Larghezza,frame.Size(),true);
                    if(precedente!=null)catalogo.Remove(precedente);
                    catalogo.Add(letto);associati.Add(letto);
                }
                foreach(var c in catalogo.ToArray())
                {
                    if(associati.Contains(c))continue;var qr=c.AreaQr??c.Area;
                    if(GeometriaRegioni.ContieneCentro(zona,qr))
                    {
                        var box=GeometriaRegioni.InCamera(calibrazione,qr,frame.Cols,frame.Rows);
                        if((box&regione)!=box||box.Width==0||box.Height==0){completa=false;continue;}
                        int chiari=0;
                        for(int y=box.Top;y<box.Bottom;y++)for(int x=box.Left;x<box.Right;x++)
                            if(gray.At<byte>(y-regione.Y,x-regione.X)>=190&&colori[y-regione.Y,x-regione.X]<0)chiari++;
                        if(chiari>=box.Width*box.Height*.985)catalogo.Remove(c);else completa=false;
                    }
                    else if(qr.X+qr.Larghezza<zona.X&&c.Colore.HasValue&&c.FineEstensioneX.HasValue
                        &&c.FineEstensioneX.Value>=zona.X-2.0/Math.Max(frame.Width,frame.Height)
                        &&qr.Y+qr.Altezza/2>=zona.Y&&qr.Y+qr.Altezza/2<=zona.Y+zona.Altezza)
                    {
                        if(scuri>regione.Width*regione.Height*.8){completa=false;continue;}
                        c.FineEstensioneX=Segui(colori,regione,c,zona.X,zona.X+zona.Larghezza,frame.Size());codaConosciuta=true;
                    }
                }
                // Colore sconosciuto non e prova di una rimozione o di un QR decodificato.
                if(colorati>50&&letti.Count==0&&!codaConosciuta&&!catalogo.Any(c=>c.Colore.HasValue&&GeometriaRegioni.Interseca(zona,c.Area)))completa=false;
                if(scuri>regione.Width*regione.Height*.8)completa=false;
                if(traccia!=null)
                {
                    traccia.LarghezzaFrame=frame.Width;traccia.AltezzaFrame=frame.Height;traccia.CalibrazionePresente=true;
                    traccia.EsitoVisione=completa?"FinestraConfermata":"FinestraDaRileggere";
                    traccia.Visione.Add(new EventoDiagnosticoCartoncino("Visione","LetturaFinestra","LetturaFinestra",$"ROI {regione}; {letti.Count} QR; completa={completa}",areaTavolo:zona));
                }
                return new LetturaZona(catalogo.AsReadOnly(),completa,letti.Count,regione){RegioneMaschera=regione};
            }
        }
        private ColoreTraccia? ColoreQr(sbyte[,] colori,Rect regione,RettangoloTavolo qr,Size frame)
        {
            var conteggi=new int[6];
            for(int i=0;i<13;i++)for(int j=0;j<9;j++)
            {
                double x=qr.X+qr.Larghezza*(-.2+i*.14),y=qr.Y+qr.Altezza*(-.2+j*.175);
                int c=Campione(colori,regione,x,y);if(c>=0)conteggi[c]++;
            }
            int m=Array.IndexOf(conteggi,conteggi.Max());return conteggi[m]>=2?(ColoreTraccia?)m:null;
        }
        private double Segui(sbyte[,] colori,Rect regione,CartoncinoRilevato nota,double inizio,double fine,Size frame,bool bordoQr=false)
        {
            var qr=nota.AreaQr??nota.Area;double passo=1.0/Math.Max(frame.Width,frame.Height),ultimo=inizio;
            double rigaH=griglia.Area.Altezza/Math.Max(1,griglia.Tracce);
            int riga=(int)Math.Floor((qr.Y+qr.Altezza/2-griglia.Area.Y)/rigaH);
            double y0=Math.Max(qr.Y-.2*qr.Altezza,griglia.Area.Y+riga*rigaH+passo);
            double y1=Math.Min(qr.Y+1.2*qr.Altezza,griglia.Area.Y+(riga+1)*rigaH-passo);
            for(double x=inizio;x<=fine;x+=passo)
            {
                int pieni=0;for(int i=0;i<17;i++)if(Campione(colori,regione,x,y0+(y1-y0)*i/16)==(int)nota.Colore.Value)pieni++;
                if(pieni<2)
                {if(bordoQr&&ultimo==inizio&&x-inizio<qr.Larghezza*.35)continue;break;}ultimo=x;
            }
            return ultimo;
        }
        private int Campione(sbyte[,] colori,Rect regione,double x,double y)
        {
            var p=calibrazione.DaTavoloACamera(new PuntoTavolo(x,y));int cx=(int)Math.Round(p.X)-regione.X,cy=(int)Math.Round(p.Y)-regione.Y;
            return cx>=0&&cy>=0&&cx<regione.Width&&cy<regione.Height?colori[cy,cx]:-1;
        }
        private List<CartoncinoRilevato> Decodifica(Mat gray,Rect regione,double scala)
        {
            var bytes=new byte[gray.Width*gray.Height];Marshal.Copy(gray.Data,bytes,0,bytes.Length);
            var bitmap=new BinaryBitmap(new HybridBinarizer(new RGBLuminanceSource(bytes,gray.Width,gray.Height,RGBLuminanceSource.BitmapFormat.Gray8)));
            var risultati=new QRCodeMultiReader().decodeMultiple(bitmap,new Dictionary<DecodeHintType,object>{{DecodeHintType.POSSIBLE_FORMATS,new[]{BarcodeFormat.QR_CODE}}});
            var letti=new List<CartoncinoRilevato>();
            foreach(var r in risultati??Array.Empty<Result>())
            {
                var p=r.ResultPoints;if(p==null||p.Length<3||string.IsNullOrWhiteSpace(r.Text))continue;
                double ux=p[2].X-p[1].X,uy=p[2].Y-p[1].Y,vx=p[0].X-p[1].X,vy=p[0].Y-p[1].Y;
                double m=3.5/(p.Length>=4?18:14);
                var angoli=new[]{new[]{p[1].X-m*(ux+vx),p[1].Y-m*(uy+vy)},new[]{p[2].X+m*(ux-vx),p[2].Y+m*(uy-vy)},new[]{p[2].X+vx+m*(ux+vx),p[2].Y+vy+m*(uy+vy)},new[]{p[0].X+m*(-ux+vx),p[0].Y+m*(-uy+vy)}};
                var tavolo=angoli.Select(a=>calibrazione.DaCameraATavolo(new PuntoImmagine(regione.X+a[0]/scala,regione.Y+a[1]/scala))).ToArray();
                double x=tavolo.Min(a=>a.X),y=tavolo.Min(a=>a.Y);
                var qr=new RettangoloTavolo(x,y,tavolo.Max(a=>a.X)-x,tavolo.Max(a=>a.Y)-y);
                letti.Add(new CartoncinoRilevato{Payload=r.Text,Area=qr,AreaQr=qr,FineEstensioneX=qr.X});
            }
            return letti;
        }
    }
}



