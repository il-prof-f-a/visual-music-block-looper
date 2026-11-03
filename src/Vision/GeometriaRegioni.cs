using System;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Vision
{
    public static class GeometriaRegioni
    {
        public static bool Interseca(RettangoloTavolo a, RettangoloTavolo b) =>
            a.X < b.X+b.Larghezza && b.X < a.X+a.Larghezza && a.Y < b.Y+b.Altezza && b.Y < a.Y+a.Altezza;
        public static bool ContieneCentro(RettangoloTavolo a, RettangoloTavolo b)
        {
            double x=b.X+b.Larghezza/2,y=b.Y+b.Altezza/2;
            return x>=a.X && x<=a.X+a.Larghezza && y>=a.Y && y<=a.Y+a.Altezza;
        }
        public static Rect InCamera(Calibrazione calibrazione, RettangoloTavolo r, int w, int h, int margine = 0)
        {
            double minX=double.PositiveInfinity,minY=minX,maxX=double.NegativeInfinity,maxY=maxX;
            // Il bordo CAL3 può avere estremi fra i vertici.
            for(int i=0;i<=16;i++) for(int lato=0;lato<4;lato++)
            {
                double t=i/16.0;
                double x=lato==0 ? r.X+r.Larghezza*t : lato==1 ? r.X+r.Larghezza : lato==2 ? r.X+r.Larghezza*(1-t) : r.X;
                double y=lato==0 ? r.Y : lato==1 ? r.Y+r.Altezza*t : lato==2 ? r.Y+r.Altezza : r.Y+r.Altezza*(1-t);
                var p=calibrazione.DaTavoloACamera(new PuntoTavolo(x,y));
                if(double.IsNaN(p.X)||double.IsNaN(p.Y)||double.IsInfinity(p.X)||double.IsInfinity(p.Y)) continue;
                minX=Math.Min(minX,p.X);maxX=Math.Max(maxX,p.X);minY=Math.Min(minY,p.Y);maxY=Math.Max(maxY,p.Y);
            }
            if(double.IsInfinity(minX)) return new Rect();
            int x0=Math.Max(0,(int)Math.Floor(minX)-margine),y0=Math.Max(0,(int)Math.Floor(minY)-margine);
            int x1=Math.Min(w,(int)Math.Ceiling(maxX)+margine),y1=Math.Min(h,(int)Math.Ceiling(maxY)+margine);
            return new Rect(x0,y0,Math.Max(0,x1-x0),Math.Max(0,y1-y0));
        }
        public static CartoncinoRilevato Copia(CartoncinoRilevato c) => new CartoncinoRilevato
        { Payload=c.Payload,Colore=c.Colore,Area=c.Area,AreaQr=c.AreaQr,FineEstensioneX=c.FineEstensioneX };
    }
}
