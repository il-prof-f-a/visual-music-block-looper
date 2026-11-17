using System;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.App
{
    public readonly struct IndicatoriScansione
    {
        public IndicatoriScansione(bool visibili,PosizioneIndicatore esecuzione,PosizioneIndicatore prevista,
            PosizioneIndicatore? completata,bool inLettura,int anticipoCelle=0)
        {Visibili=visibili;Esecuzione=esecuzione;Prevista=prevista;Completata=completata;InLettura=inLettura;AnticipoCelle=anticipoCelle;}
        public bool Visibili { get; }
        public PosizioneIndicatore Esecuzione { get; }
        public PosizioneIndicatore Prevista { get; }
        public PosizioneIndicatore? Completata { get; }
        public bool InLettura { get; }
        public int AnticipoCelle { get; }
    }

    /// <summary>Solo UI: transizione conservativa fra due risultati acquisiti, senza prevedere il lavoro del decoder.</summary>
    public sealed class PresentatoreScansione
    {
        private LetturaScansione ultima;
        private double partenza,destinazione,durata;
        private long inizio;
        private int ultimoAnticipo;
        private double Coordinata(long ora)=>partenza+(destinazione-partenza)
            *(durata>0?Math.Max(0,Math.Min(1,(ora-inizio)/durata)):1);
        private static bool Compatibile(LetturaScansione l,IstantaneaTrasporto t)=>l!=null
            &&l.Trasporto.Esecuzione==t.Esecuzione&&l.Trasporto.Generazione==t.Generazione
            &&l.Trasporto.Struttura.Equals(t.Struttura);
        public IndicatoriScansione Leggi(IstantaneaTrasporto t,StatoScansione scansione,int anticipo,long ora,bool attivi=true,bool anticipoInCelle=false)
        {
            if(!attivi||!t.InEsecuzione||t.Struttura==null)
            {ultima=null;return default;}
            if(anticipo!=ultimoAnticipo){ultima=null;ultimoAnticipo=anticipo;}
            scansione=scansione??StatoScansione.Vuoto;
            if(!Compatibile(ultima,t)||scansione.UltimaCompletata==null)ultima=null;
            var nuova=scansione.UltimaCompletata;
            if(Compatibile(nuova,t)&&!ReferenceEquals(nuova,ultima)
                &&(ultima==null||nuova.MisuraAssoluta+nuova.Fase>=destinazione))
            {
                double arrivo=nuova.MisuraAssoluta+nuova.Fase;
                partenza=ultima==null?arrivo:Coordinata(nuova.FineMs);
                destinazione=arrivo;inizio=nuova.FineMs;
                // Durata misurata e tetto di 120 ms: il cursore non supera mai un risultato acquisito.
                durata=Math.Max(1,Math.Min(120,nuova.FineMs-nuova.InizioMs));
                ultima=nuova;
            }
            PosizioneIndicatore? completata=ultima==null?(PosizioneIndicatore?)null:
                ultima.PosizioneAllaCoordinata(Coordinata(ora));
            return new IndicatoriScansione(true,t.PosizioneLineare(ora),anticipoInCelle?t.PosizioneLineareCelle(ora,anticipo):t.PosizioneLineare(ora,anticipo),
                completata,Compatibile(scansione.InCorso,t),anticipoInCelle?anticipo:0);
        }
    }
}
