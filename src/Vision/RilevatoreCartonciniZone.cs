using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Vision
{
    public sealed partial class RilevatoreCartoncini
    {
        internal LetturaZona LeggiZona(Mat frame,RettangoloTavolo zona,IReadOnlyList<CartoncinoRilevato> noti,TracciaCartoncini traccia)
        {
            var catalogo=(noti??new CartoncinoRilevato[0]).Select(GeometriaRegioni.Copia).ToList();
            Rect regione=GeometriaRegioni.InCamera(calibrazione,zona,frame.Cols,frame.Rows,48);
            if(regione.Width<8||regione.Height<8) return new LetturaZona(catalogo,false,0,regione);
            double rigaH=griglia.Area.Altezza/griglia.Tracce;
            int prima=Math.Max(0,(int)Math.Floor((zona.Y-griglia.Area.Y)/rigaH));
            int ultima=Math.Min(griglia.Tracce-1,(int)Math.Floor((zona.Y+zona.Altezza-griglia.Area.Y)/rigaH));
            double y0=Math.Min(zona.Y,griglia.Area.Y+prima*rigaH),y1=Math.Max(zona.Y+zona.Altezza,griglia.Area.Y+(ultima+1)*rigaH);
            var fascia=new RettangoloTavolo(0,Math.Max(0,y0),1,Math.Min(1,y1)-Math.Max(0,y0));
            if(!GeometriaRegioni.Interseca(zona,griglia.Area))fascia=zona;
            Rect areaMaschera=GeometriaRegioni.InCamera(calibrazione,fascia,frame.Cols,frame.Rows,55);
            using(var convertita=new Mat())
            using(var maschera=new Mat())
            {
                Mat bgr=frame;
                if(frame.Channels()!=3)
                { Cv2.CvtColor(frame,convertita,frame.Channels()==4?ColorConversionCodes.BGRA2BGR:ColorConversionCodes.GRAY2BGR);bgr=convertita; }
                Rect[] corpi;
                using(var taglioMaschera=new Mat(bgr,areaMaschera))
                using(var locale=MascheraCorpoInterna(taglioMaschera,calibrazione,out Rect[] _,false,frame.Cols*(double)frame.Rows))
                using(var clipTavolo=calibrazione.MascheraTavolo(areaMaschera,frame.Cols,frame.Rows,RestringiTavolo))
                {
                    Cv2.BitwiseAnd(locale,clipTavolo,locale);locale.CopyTo(maschera);
                    using(var copia=locale.Clone())
                    {
                        Cv2.FindContours(copia,out Point[][] contorni,out HierarchyIndex[] _,RetrievalModes.External,ContourApproximationModes.ApproxSimple);
                        corpi=contorni.Select(Cv2.BoundingRect).Where(r=>r.Width*r.Height>=AreaMinimaCorpo)
                            .Select(r=>new Rect(r.X+areaMaschera.X,r.Y+areaMaschera.Y,r.Width,r.Height)).ToArray();
                    }
                }
                List<QrTrovato> qr,localizzati;
                using(var taglio=new Mat(bgr,regione))
                {
                    qr=DecodificaQr(taglio,null,out localizzati);
                    var corpiLocali=corpi.Select(r=>r&regione).Where(r=>r.Width>5&&r.Height>5)
                        .Select(r=>new Rect(r.X-regione.X,r.Y-regione.Y,r.Width,r.Height)).ToArray();
                    qr.AddRange(RecuperaQrMancanti(taglio,qr,localizzati,corpiLocali,null));
                    foreach(var q in qr.Concat(localizzati).Distinct())
                        q.Angoli=q.Angoli.Select(p=>new Point2f(p.X+regione.X,p.Y+regione.Y)).ToArray();
                }
                foreach(var q in qr) { q.Origine=q.Origine=="Memoria"?"Memoria":"Zona/"+q.Origine;if(traccia!=null)RegistraQr(traccia,q,"QrDecodificato"); }
                var candidati=qr.Select(q=>Analizza(corpi,q)).Where(c=>c!=null).ToList();
                var letti=new List<CartoncinoRilevato>();
                foreach(var c in candidati)
                {
                    var letto=Converti(c,candidati,maschera,out Rect _,areaMaschera.TopLeft,frame.Size());
                    if(letto!=null&&GeometriaRegioni.Interseca(zona,letto.AreaQr??letto.Area)) letti.Add(letto);
                }
                bool completa=true;
                // Prima risolvi i match quasi esatti; un QR uguale in un'altra posizione resta distinto.
                var associati=new HashSet<int>();
                foreach(var letto in letti.OrderBy(n=>catalogo.Count==0?double.MaxValue:catalogo.Min(o=>DistanzaQr(o,n))))
                {
                    int migliore=-1;double distanza=double.MaxValue;
                    for(int i=0;i<catalogo.Count;i++)
                    {
                        if(associati.Contains(i))continue;
                        var a=catalogo[i].AreaQr??catalogo[i].Area;var b=letto.AreaQr??letto.Area;
                        double limite=Math.Max(a.Larghezza,b.Larghezza)*(catalogo[i].Payload==letto.Payload?1.4:.45);
                        double d=DistanzaQr(catalogo[i],letto);
                        if(d<limite&&d<distanza){distanza=d;migliore=i;}
                    }
                    if(migliore>=0){catalogo[migliore]=letto;associati.Add(migliore);}
                    else {catalogo.Add(letto);associati.Add(catalogo.Count-1);}
                }
                var rimossi=new List<int>();
                for(int i=0;i<catalogo.Count;i++)
                {
                    if(associati.Contains(i))continue;
                    var c=catalogo[i];var a=c.AreaQr??c.Area;
                    if(GeometriaRegioni.ContieneCentro(zona,a))
                    {
                        Rect box=GeometriaRegioni.InCamera(calibrazione,a,frame.Cols,frame.Rows);
                        Rect limitato=box&areaMaschera;
                        if(limitato!=box){completa=false;continue;}
                        if(box.Width>0&&box.Height>0)
                            using(var contenuto=new Mat(maschera,new Rect(box.X-areaMaschera.X,box.Y-areaMaschera.Y,box.Width,box.Height)))
                            {
                                if(Cv2.CountNonZero(contenuto)<box.Width*box.Height*.015){rimossi.Add(i);continue;}
                                completa=false; // non letto non equivale a rimosso
                            }
                    }
                    else if(c.AreaQr.HasValue&&GeometriaRegioni.Interseca(fascia,a))
                        c.FineEstensioneX=MisuratoreEstensioneNota.Misura(maschera,calibrazione,a,griglia,areaMaschera.TopLeft,frame.Size());
                }
                foreach(int i in rimossi.OrderByDescending(i=>i))catalogo.RemoveAt(i);
                foreach(Rect corpo in corpi)
                {
                    bool plausibile=Math.Min(corpo.Width,corpo.Height)>=Math.Max(12,Math.Min(frame.Cols,frame.Rows)*.015)
                        && (Math.Max(corpo.Width,corpo.Height)<Math.Min(corpo.Width,corpo.Height)*5||corpo.Width*(double)corpo.Height>frame.Cols*(double)frame.Rows*.05);
                    if(!plausibile||qr.Any(q=>(corpo&Gonfia(BoundingBox(q.Angoli),5)).Width>0&&(corpo&Gonfia(BoundingBox(q.Angoli),5)).Height>0)
                        ||catalogo.Any(c=>{var r=GeometriaRegioni.InCamera(calibrazione,c.AreaQr??c.Area,frame.Cols,frame.Rows);var a=corpo&r;return a.Width>0&&a.Height>0;}))continue;
                    var punti=calibrazione.BordiDaCameraATavolo(corpo);
                    var a=new RettangoloTavolo(punti.Min(p=>p.X),punti.Min(p=>p.Y),punti.Max(p=>p.X)-punti.Min(p=>p.X),punti.Max(p=>p.Y)-punti.Min(p=>p.Y));
                    if(GeometriaRegioni.ContieneCentro(zona,a))completa=false;
                }
                if(traccia!=null)
                {
                    traccia.LarghezzaFrame=frame.Cols;traccia.AltezzaFrame=frame.Rows;traccia.CalibrazionePresente=true;
                    traccia.EsitoVisione=completa?"ZoneConfermate":"ZoneDaRileggere";
                    traccia.Visione.Add(new EventoDiagnosticoCartoncino("Visione","LetturaZona","LetturaZona",
                        $"ROI effettiva {regione}; {qr.Count} QR; completa={completa}.",null,
                        new RettangoloTavolo(regione.X,regione.Y,regione.Width,regione.Height),zona));
                }
                return new LetturaZona(catalogo.AsReadOnly(),completa,qr.Count,regione){RegioneMaschera=areaMaschera};
            }
        }
        private static double DistanzaQr(CartoncinoRilevato a,CartoncinoRilevato b)
        {
            var x=a.AreaQr??a.Area;var y=b.AreaQr??b.Area;
            double dx=x.X+x.Larghezza/2-y.X-y.Larghezza/2,dy=x.Y+x.Altezza/2-y.Y-y.Altezza/2;
            return Math.Sqrt(dx*dx+dy*dy);
        }
    }
}
