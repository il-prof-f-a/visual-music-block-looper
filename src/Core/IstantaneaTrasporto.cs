using System;
using System.Collections.Generic;

namespace VisualMusicBlockLooper.Core
{
    public readonly struct PosizioneIndicatore
    {
        public PosizioneIndicatore(double step,IReadOnlyList<int> righe,long misuraAssoluta)
        {Step=step;RigheAttive=righe;MisuraAssoluta=misuraAssoluta;}
        public double Step { get; }
        public IReadOnlyList<int> RigheAttive { get; }
        public long MisuraAssoluta { get; }
    }

    /// <summary>Tick realmente consumato e fase grafica limitata al tick successivo. Nessun evento MIDI viene interpolato.</summary>
    public readonly struct IstantaneaTrasporto
    {
        public IstantaneaTrasporto(StrutturaTavolo struttura,long esecuzione,long generazione,bool inEsecuzione,
            long misuraAssoluta,int indiceMisura,int tickNelGiro,long istanteMs,double periodoTickMs,PartituraMidi partitura=null)
        {
            Struttura=struttura;Esecuzione=esecuzione;Generazione=generazione;InEsecuzione=inEsecuzione;
            MisuraAssoluta=misuraAssoluta;IndiceMisura=indiceMisura;TickNelGiro=tickNelGiro;
            IstanteMs=istanteMs;PeriodoTickMs=periodoTickMs;
            Partitura=partitura;
        }
        public StrutturaTavolo Struttura { get; }
        public long Esecuzione { get; }
        public long Generazione { get; }
        public bool InEsecuzione { get; }
        public long MisuraAssoluta { get; }
        public int IndiceMisura { get; }
        public int TickNelGiro { get; }
        public long IstanteMs { get; }
        public double PeriodoTickMs { get; }
        public PartituraMidi Partitura { get; }
        public double FrazioneTick(long ora)=>InEsecuzione&&PeriodoTickMs>0
            ?Math.Max(0,Math.Min(1,(ora-IstanteMs)/PeriodoTickMs)):0;
        public PosizioneIndicatore PosizioneLineare(long ora,int anticipo=0)
        {
            if(anticipo<0)throw new ArgumentOutOfRangeException(nameof(anticipo));
            var s=Struttura??StrutturaTavolo.Default;
            int indice=(IndiceMisura+anticipo)%s.MisurePerGiro;
            double durata=s.TickFineMisura(IndiceMisura)-s.TickInizioMisura(IndiceMisura);
            double fase=Math.Max(0,Math.Min(1,(TickNelGiro-s.TickInizioMisura(IndiceMisura)+FrazioneTick(ora))/durata));
            var p=s.PosizioneMisuraGiro(indice);
            double step=p.StepInMisura+fase*(s.TickFineMisura(indice)-s.TickInizioMisura(indice))/Sequencer.TickPerStep;
            return new PosizioneIndicatore(step,p.RigheAttive,MisuraAssoluta+anticipo);
        }

        /// <summary>Anticipo in sedicesimi visibili, attraverso i righi e il giro A-B.</summary>
        public PosizioneIndicatore PosizioneLineareCelle(long ora,int celle=0)
        {
            if(celle<0)throw new ArgumentOutOfRangeException(nameof(celle));
            var s=Struttura??StrutturaTavolo.Default;
            long tickGiro=s.StepGiro*Sequencer.TickPerStep;
            double futuro=TickNelGiro+FrazioneTick(ora)+(long)celle*Sequencer.TickPerStep;
            long giri=(long)Math.Floor(futuro/tickGiro);
            double tick=futuro-giri*tickGiro;
            int indice=s.IndiceMisuraAlTick((int)Math.Floor(tick));
            var p=s.Posizione((long)(tick/Sequencer.TickPerStep));
            double step=p.StepInMisura+(tick%Sequencer.TickPerStep)/Sequencer.TickPerStep;
            return new PosizioneIndicatore(step,p.RigheAttive,MisuraAssoluta-IndiceMisura+giri*s.MisurePerGiro+indice);
        }
    }
}
