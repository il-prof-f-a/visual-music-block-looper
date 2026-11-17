using System;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Un'operazione reale su un frame. Creata dal lettore, mai dal thread MIDI.</summary>
    public sealed class LetturaScansione
    {
        public LetturaScansione(IstantaneaTrasporto trasporto,long misura,int indiceMisura,long frameId,
            long istanteFrameMs,long inizioMs,long fineMs=0,double? fase=null)
        {
            Trasporto=trasporto;MisuraAssoluta=misura;IndiceMisura=indiceMisura;FrameId=frameId;
            IstanteFrameMs=istanteFrameMs;InizioMs=inizioMs;FineMs=fineMs;
            var s=trasporto.Struttura;
            double durata=s.TickFineMisura(trasporto.IndiceMisura)-s.TickInizioMisura(trasporto.IndiceMisura);
            Fase=fase??Math.Max(0,Math.Min(1,(trasporto.TickNelGiro-s.TickInizioMisura(trasporto.IndiceMisura)
                +trasporto.FrazioneTick(inizioMs))/durata));
        }
        public IstantaneaTrasporto Trasporto { get; }
        public long MisuraAssoluta { get; }
        public int IndiceMisura { get; }
        public long FrameId { get; }
        public long IstanteFrameMs { get; }
        public long InizioMs { get; }
        public long FineMs { get; }
        public double Fase { get; }
        public PosizioneIndicatore Posizione=>PosizioneAllaCoordinata(MisuraAssoluta+Fase);
        public PosizioneIndicatore PosizioneAllaCoordinata(double coordinata)
        {
            var s=Trasporto.Struttura;
            long misura=(long)Math.Floor(coordinata);
            long indice=(IndiceMisura+misura-MisuraAssoluta)%s.MisurePerGiro;
            if(indice<0)indice+=s.MisurePerGiro;
            var p=s.PosizioneMisuraGiro((int)indice);
            double step=p.StepInMisura+(coordinata-misura)*(s.TickFineMisura((int)indice)-s.TickInizioMisura((int)indice))/Sequencer.TickPerStep;
            return new PosizioneIndicatore(step,p.RigheAttive,misura);
        }
    }

    public sealed class StatoScansione
    {
        public static readonly StatoScansione Vuoto=new StatoScansione(null,null,null,"In attesa della scansione live");
        public StatoScansione(LetturaScansione inCorso,LetturaScansione ultimaCompletata,LetturaScansione ultimaLettura,string esito)
        {InCorso=inCorso;UltimaCompletata=ultimaCompletata;UltimaLettura=ultimaLettura;Esito=esito;}
        public LetturaScansione InCorso { get; }
        public LetturaScansione UltimaCompletata { get; }
        public LetturaScansione UltimaLettura { get; }
        public string Esito { get; }
    }
}
