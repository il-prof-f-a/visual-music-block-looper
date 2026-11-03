using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Vision
{
    public sealed class LetturaIstantanea
    {
        internal LetturaIstantanea(IEnumerable<CartoncinoRilevato> cartoncini,IEnumerable<RettangoloTavolo> incerte,bool vuoto,double durata)
        {Cartoncini=Array.AsReadOnly(cartoncini.ToArray());RegioniDaRileggere=Array.AsReadOnly(incerte.ToArray());VuotoAffidabile=vuoto;DurataMs=durata;}
        public IReadOnlyList<CartoncinoRilevato> Cartoncini {get;}
        public IReadOnlyList<RettangoloTavolo> RegioniDaRileggere {get;}
        public bool VuotoAffidabile {get;}
        public double DurataMs {get;}
    }
    public sealed class LettoreIstantanea
    {
        private readonly Calibrazione calibrazione;private readonly ConfigGriglia config;
        public LettoreIstantanea(Calibrazione calibrazione,ConfigGriglia config){this.calibrazione=calibrazione;this.config=config;}
        public LetturaIstantanea Leggi(Mat frame,TracciaCartoncini traccia=null)
            =>new RilevatoreCartoncini(null,calibrazione,config).LeggiIstantanea(frame,traccia);
    }
    public sealed partial class RilevatoreCartoncini
    {
        internal LetturaIstantanea LeggiIstantanea(Mat frame,TracciaCartoncini traccia)
        {
            var tempo=Stopwatch.StartNew();
            using(var colore=new Mat())using(var grigio=new Mat())
            {
                Mat bgr=frame;
                if(frame.Channels()!=3){Cv2.CvtColor(frame,colore,frame.Channels()==4?ColorConversionCodes.BGRA2BGR:ColorConversionCodes.GRAY2BGR);bgr=colore;}
                Cv2.CvtColor(bgr,grigio,ColorConversionCodes.BGR2GRAY);
                var qr=DecodificaConZxing(grigio,null,"IstantaneaZXing").ToList();
                double tempoQr=tempo.Elapsed.TotalMilliseconds;
                using(var maschera=MascheraCorpo(bgr,calibrazione,out Rect[] corpi))
                {
                    double tempoMaschera=tempo.Elapsed.TotalMilliseconds;
                    var candidati=qr.Select(q=>Analizza(corpi,q)).Where(c=>c!=null).ToList();
                    var letti=candidati.Select(c=>new{C=c,R=Converti(c,candidati,maschera,out _)})
                        .Where(x=>x.R!=null).OrderBy(x=>RigoIstantanea(x.R)).ThenBy(x=>(x.R.AreaQr??x.R.Area).X).ToArray();
                    var incerte=new List<RettangoloTavolo>();
                    foreach(var corpo in corpi)
                    {
                        bool plausibile=Math.Min(corpo.Width,corpo.Height)>=15 &&
                            (Math.Max(corpo.Width,corpo.Height)<5*Math.Min(corpo.Width,corpo.Height)||corpo.Width*(double)corpo.Height>frame.Cols*(double)frame.Rows*.05);
                        // Un QR leggibile non prova che un corpo contiguo non ne contenga altri.
                        // La verifica locale avverrà dopo la prima pubblicazione MIDI.
                        if(!plausibile)continue;
                        var p=calibrazione.BordiDaCameraATavolo(corpo);
                        incerte.Add(new RettangoloTavolo(p.Min(x=>x.X),p.Min(x=>x.Y),p.Max(x=>x.X)-p.Min(x=>x.X),p.Max(x=>x.Y)-p.Min(x=>x.Y)));
                    }
                    if(traccia!=null)
                    {
                        foreach(var x in letti)
                            RegistraVisione(traccia,"QrIstantanea","QR letto nella singola istantanea, in ordine di rigo e X.",x.R.Payload,x.C.Riquadro,"IstantaneaZXing",x.R.AreaQr);
                        traccia.EsitoVisione="IstantaneaRapida";
                        traccia.Visione.Add(new EventoDiagnosticoCartoncino("Avvio","TempiIstantanea","TempiIstantanea",
                            $"QR={tempoQr:0.0}ms; maschera={tempoMaschera-tempoQr:0.0}ms; geometria={tempo.Elapsed.TotalMilliseconds-tempoMaschera:0.0}ms; QR letti={qr.Count}."));
                    }
                    return new LetturaIstantanea(letti.Select(x=>x.R),incerte,qr.Count==0&&incerte.Count==0&&Cv2.Mean(grigio).Val0>=80,tempo.Elapsed.TotalMilliseconds);
                }
            }
        }
        private int RigoIstantanea(CartoncinoRilevato c)
        {var a=c.AreaQr??c.Area;return (int)Math.Floor((a.Y+a.Altezza/2-griglia.Area.Y)/(griglia.Area.Altezza/griglia.Tracce));}
    }
}
