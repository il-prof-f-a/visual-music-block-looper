using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    public sealed partial class AnalizzatoreFoglio
    {
        private readonly bool scansioneCelle;
        private LettoreFinestraCelle lettoreCelle;
        private Calibrazione calibrazioneCelle;
        private long esecuzioneCelle=-1,ultimoVincoloCelle=-1;
        private int anticipoCelle;
        private StrutturaTavolo strutturaCelle;
        private CandidataCelle candidataCelle;
        private double durataCelleMs;
        public double DurataUltimoGruppoCelleMs=>Volatile.Read(ref durataCelleMs);
        public bool ScansioneCelleAttiva=>scansioneCelle&&IndicatoriScansioneAttivi;

        private sealed class CandidataCelle
        {
            public IstantaneaTrasporto Trasporto;
            public long Misura,StepAssoluto,Frame;
            public int Indice,Step,Tentativi;
            public double Fase;
            public RettangoloTavolo[] Zone;
            public RettangoloTavolo[] Vincoli=new RettangoloTavolo[0];
            public string Firma;
        }

        public static RettangoloTavolo[] FinestreCelle(StrutturaTavolo struttura,PosizioneIndicatore posizione,ConfigGriglia cfg)
        {
            if(struttura==null)throw new ArgumentNullException(nameof(struttura));
            if(cfg==null)throw new ArgumentNullException(nameof(cfg));
            double w=cfg.Area.Larghezza/cfg.Step,h=cfg.Area.Altezza/cfg.Tracce;
            int cella=Math.Min(struttura.StepPerMisura-1,Math.Max(0,(int)Math.Floor(posizione.Step)));
            double margine=Math.Min(.49,Math.Max(0,cfg.TolleranzaQrCelle));
            double inizio=Math.Max(0,cella-1-margine),fine=Math.Min(struttura.StepPerMisura,cella+2+margine);
            return (posizione.RigheAttive??new int[0]).Select(r=>new RettangoloTavolo(
                cfg.Area.X+inizio*w,cfg.Area.Y+r*h,(fine-inizio)*w,h)).ToArray();
        }

        private void AnalizzaFinestreCelle(FotogrammaAcquisito f,Calibrazione c,TracciaCartoncini traccia,MotorePartitura motore,int anticipo)
        {
            var t=motore.Trasporto;var s=t.Struttura;
            if(!t.InEsecuzione||s==null)return;
            if(!ReferenceEquals(c,calibrazioneCelle)||esecuzioneCelle!=t.Esecuzione||anticipoCelle!=anticipo
                ||strutturaCelle==null||!strutturaCelle.Equals(s))
            {
                lettoreCelle=new LettoreFinestraCelle(c,config);calibrazioneCelle=c;
                esecuzioneCelle=t.Esecuzione;strutturaCelle=s;anticipoCelle=anticipo;
                candidataCelle=null;ultimoVincoloCelle=-1;
                Volatile.Write(ref scansione,StatoScansione.Vuoto);
            }
            var piano=candidataCelle;
            if(piano==null)
            {
                var p=t.PosizioneLineareCelle(orologio(),anticipo);
                int indice=(int)(p.MisuraAssoluta%s.MisurePerGiro);
                int cella=Math.Min(s.StepPerMisura-1,(int)Math.Floor(p.Step));
                var inizio=s.PosizioneMisuraGiro(indice);
                double durata=s.TickFineMisura(indice)-s.TickInizioMisura(indice);
                piano=new CandidataCelle{Trasporto=t,Misura=p.MisuraAssoluta,Indice=indice,Step=cella,
                    Fase=Math.Max(0,Math.Min(1,(cella-inizio.StepInMisura)*6/durata)),
                    // Pubblicare prima della cella precedente: anche un nuovo QR nel contesto sinistro deve suonare.
                    StepAssoluto=(p.MisuraAssoluta/s.MisurePerGiro)*s.StepGiro+s.TickInizioMisura(indice)/6
                        +Math.Max(inizio.StepInMisura,cella-1)-inizio.StepInMisura,
                    Zone=FinestreCelle(s,p,config)};
                if(ultimoVincoloCelle!=t.MisuraAssoluta)
                {
                    piano.Vincoli=new[]{layout.Misura,layout.Collegamento,layout.Ab}.Where(z=>z.Larghezza>0&&z.Altezza>0).ToArray();
                    ultimoVincoloCelle=t.MisuraAssoluta;
                }
            }
            if(piano.Frame==f.Id)return;
            long inizioMs=orologio();var tempo=Stopwatch.StartNew();
            var operazione=new LetturaScansione(piano.Trasporto,piano.Misura,piano.Indice,f.Id,f.IstanteMs,inizioMs,0,piano.Fase);
            Volatile.Write(ref scansione,new StatoScansione(operazione,Scansione.UltimaCompletata,Scansione.UltimaLettura,"Lettura locale di tre celle"));
            // Ripetere gli stessi vincoli sul secondo frame della conferma, senza accumulare fotografie.
            var zone=piano.Zone.Concat(piano.Vincoli);
            Volatile.Write(ref regioniLettura,piano.Zone);
            Volatile.Write(ref letturaInCorso,1);
            var lavoro=catalogo;bool completa=true;
            try
            {
                foreach(var zona in zone)
                {
                    long prima=tempo.ElapsedMilliseconds;
                    var lettura=lettoreCelle.Leggi(f.Immagine,zona,lavoro,traccia);
                    Interlocked.Increment(ref letture);
                    completa&=lettura.Completa&&IlluminazioneAffidabile(f.Immagine,lettura.RegioneCamera);
                    lavoro=lettura.Catalogo;
                    traccia?.Visione.Add(new EventoDiagnosticoCartoncino("Scansione","TempoFinestra","TempoFinestra",
                        $"frame={f.Id}; cella={piano.Step+1}; ms={tempo.ElapsedMilliseconds-prima}; ROI={lettura.RegioneCamera}",areaTavolo:zona));
                }
            }
            finally
            {
                Volatile.Write(ref letturaInCorso,0);
                Volatile.Write(ref mostraLetturaFinoA,orologio()+150);
            }
            Volatile.Write(ref ultimeRegioni,piano.Zone);
            Volatile.Write(ref durataCelleMs,tempo.Elapsed.TotalMilliseconds);
            var adesso=motore.Trasporto;
            if(vita.InChiusura||Sospesa()||!adesso.InEsecuzione||adesso.Esecuzione!=t.Esecuzione
                ||!s.Equals(adesso.Struttura)||!ReferenceEquals(calibrazione(),c)||immagini.GenerazioneCorrente!=f.Generazione)
            {candidataCelle=null;Volatile.Write(ref scansione,StatoScansione.Vuoto);return;}
            string firma=FirmaCatalogoBlocco(lavoro),precedente=FirmaCatalogoBlocco(catalogo);
            bool cambiata=firma!=precedente;
            bool confermata=completa&&(!cambiata||(piano.Firma==firma&&piano.Frame!=f.Id));
            piano.Tentativi++;piano.Frame=f.Id;piano.Firma=completa?firma:null;
            candidataCelle=confermata||piano.Tentativi>=3?null:piano;
            string esito=!completa?"Finestra incerta: note conservate":!confermata?"Conferma celle 1/2":
                "Celle confermate";
            if(confermata&&cambiata)
            {
                var stato=interprete.Interpreta(lavoro,traccia);
                if(!stato.Struttura.Equals(s))preparatore.Aggiorna(stato,true,generazione,f.IstanteMs);
                else
                {
                    // Con +1 cella il contesto sinistro e gia trascorso: aggiornare la prossima
                    // frontiera utile senza riattaccare eventi del passato o aspettare un giro intero.
                    long pubblicazione=Math.Max(piano.StepAssoluto,motore.TickTempo/6+1);
                    preparatore.AggiornaPerCella(stato,true,generazione,f.IstanteMs,pubblicazione);
                }
                if(preparatore.UltimoErrore!=null)
                {
                    // Non dichiarare completata una modifica che il consumatore non ha ricevuto.
                    confermata=false;candidataCelle=piano;esito=preparatore.UltimoErrore;
                }
                else {catalogo=lavoro;Volatile.Write(ref ultimoStato,stato);}
            }
            var fine=new LetturaScansione(piano.Trasporto,piano.Misura,piano.Indice,f.Id,f.IstanteMs,inizioMs,orologio(),piano.Fase);
            Volatile.Write(ref scansione,new StatoScansione(null,confermata?fine:Scansione.UltimaCompletata,fine,esito));
            Volatile.Write(ref ultimoEsito,$"{esito}: cella {piano.Step+1}, misura {piano.Misura+1}; "
                +$"{piano.Zone.Length} voci; {tempo.Elapsed.TotalMilliseconds:0.0} ms; eta frame {Math.Max(0,orologio()-f.IstanteMs)} ms");
            traccia?.Visione.Add(new EventoDiagnosticoCartoncino("Scansione","GruppoCelle","GruppoCelle",
                $"frame={f.Id}; cella={piano.Step+1}; misura={piano.Misura}; step assoluto={piano.StepAssoluto}; "
                +$"voci={piano.Zone.Length}; confermata={confermata}; ms={tempo.Elapsed.TotalMilliseconds:0.0}; eta={Math.Max(0,orologio()-f.IstanteMs)}"));
        }
    }
}
