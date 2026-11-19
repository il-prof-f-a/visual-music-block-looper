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
    /// <summary>Un solo produttore di osservazioni: richieste musicali coalescenti, una conferma breve e nessuna coda di foto.</summary>
    public sealed partial class AnalizzatoreFoglio : IDisposable
    {
        private readonly RegistroFotogrammi immagini;
        private readonly Func<Calibrazione> calibrazione;
        private readonly ConfigGriglia config;
        private readonly LayoutTavolo layout;
        private readonly PreparatoreFoglio preparatore;
        private readonly RegistroCatture registro;
        private readonly Func<long> orologio;
        private readonly Griglia griglia;
        private readonly InterpreteTavolo interprete;
        private readonly RilascioDifferito vita;
        private readonly bool analizzaSoloSeCambia;
        private readonly bool scansioneABlocchiAnticipata;
        private int misureAnticipoScansione;
        private int ultimoAnticipoScansione;
        private int letturaCompletaPeriodica, periodoLetturaCompleta = 4;
        private long ultimaMisuraLetturaCompleta = long.MinValue, esecuzioneLetturaCompleta = -1;
        private long misuraLetturaCompleta = -1;
        private bool letturaCompletaInCorso;
        private int tentativiLetturaCompleta;
        private int modalitaLetturaPrecedente = -1;
        private readonly ParametriRilevatoreVariazioni parametriVariazioni;
        private RilevatoreVariazioni differenze;
        private Calibrazione corrente;
        private LettoreZone lettore;
        private IReadOnlyList<CartoncinoRilevato> catalogo=new CartoncinoRilevato[0];
        private StatoTavolo ultimoStato=new StatoTavolo(null,null,null);
        private IReadOnlyList<RettangoloTavolo> ultimeRegioni=new RettangoloTavolo[0];
        private long generazione, generazioneVideo, ultimoFrame, ultimoFrameScansioneCompleta=long.MinValue,
            ultimoSegnale=-1, ultimaOsservazione=-1000, letture, fotogrammi, retryAt=long.MaxValue;
        private string firmaCandidata, ultimoEsito="Preparazione del foglio";
        private int conferme, tentativoBurst, forzaRilettura;
        private bool inizializzato;
        private readonly Dictionary<int, ConfermaBlocco> confermeBlocchi=new Dictionary<int, ConfermaBlocco>();
        private readonly HashSet<ChiaveBloccoPianificato> blocchiScansionati=new HashSet<ChiaveBloccoPianificato>();
        private readonly HashSet<int> blocchiIncompleti=new HashSet<int>();
        private readonly HashSet<int> blocchiDaConfermare=new HashSet<int>();
        private readonly Dictionary<int,ChiaveBloccoPianificato> destinazioniIncomplete=new Dictionary<int,ChiaveBloccoPianificato>();
        private StatoScansione scansione=StatoScansione.Vuoto;
        private long esecuzioneScansione=-1;
        private StrutturaTavolo strutturaScansione;
        private bool esecuzionePrecedente;
        private IReadOnlyList<RettangoloTavolo> regioniDaVerificare=new RettangoloTavolo[0];
        private double primaPubblicazioneMs;
        private int prossimoBloccoFermo;
        public double PrimaPubblicazioneMs=>Volatile.Read(ref primaPubblicazioneMs);
        private sealed class ConfermaBlocco
        {
            public string Firma;
            public long UltimoFrame;
            public long Giro;
            public int Misura;
            public long MisuraAssoluta;
            public int Numero;
            public IReadOnlyList<CartoncinoRilevato> Catalogo;
        }
        private sealed class AggiornamentoBloccoConfermato
        {
            public BloccoScansione Blocco;
            public long MisuraAssoluta;
            public IReadOnlyList<CartoncinoRilevato> Catalogo;
        }
        private sealed class BloccoScansione
        {
            public BloccoScansione(int id,RettangoloTavolo zona,ZonaSpeciale speciale=ZonaSpeciale.Nessuna)
            {Id=id;Zona=zona;Speciale=speciale;}
            public int Id;
            public RettangoloTavolo Zona;
            public ZonaSpeciale Speciale;
        }
        private sealed class BloccoPianificato
        {
            public BloccoScansione Blocco;
            public readonly List<ChiaveBloccoPianificato> Chiavi=new List<ChiaveBloccoPianificato>();
        }
        private readonly struct ChiaveBloccoPianificato : IEquatable<ChiaveBloccoPianificato>
        {
            public ChiaveBloccoPianificato(long giro,int misura,int id,long misuraAssoluta=-1)
            {Giro=giro;Misura=misura;Id=id;MisuraAssoluta=misuraAssoluta;}
            public long Giro { get; }
            public int Misura { get; }
            public int Id { get; }
            public long MisuraAssoluta { get; }
            public bool Equals(ChiaveBloccoPianificato altra)=>Giro==altra.Giro&&Misura==altra.Misura&&Id==altra.Id&&MisuraAssoluta==altra.MisuraAssoluta;
            public override bool Equals(object obj)=>obj is ChiaveBloccoPianificato altra&&Equals(altra);
            public override int GetHashCode(){unchecked{return (((Giro.GetHashCode()*397)^Misura)*397^Id)*397^MisuraAssoluta.GetHashCode();}}
        }
        public AnalizzatoreFoglio(RegistroFotogrammi immagini,Func<Calibrazione> calibrazione,ConfigGriglia config,
            LayoutTavolo layout,PreparatoreFoglio preparatore,RegistroCatture registro,Func<long> orologio,
            bool analizzaSoloSeCambia=true,ParametriRilevatoreVariazioni parametriVariazioni=null,
            bool scansioneABlocchiAnticipata=false,int misureAnticipoScansione=1,bool scansioneCelle=false)
        {
            this.immagini=immagini;this.calibrazione=calibrazione;this.config=config;this.layout=layout;
            this.preparatore=preparatore;this.registro=registro;this.orologio=orologio;
            this.analizzaSoloSeCambia=analizzaSoloSeCambia;
            this.scansioneABlocchiAnticipata=scansioneABlocchiAnticipata;
            this.scansioneCelle=scansioneCelle;
            this.misureAnticipoScansione=ConfigurazioneApplicazione.AnticipoScansioneValido(misureAnticipoScansione)?misureAnticipoScansione:4;
            ultimoAnticipoScansione=this.misureAnticipoScansione;
            this.parametriVariazioni=(parametriVariazioni??new ParametriRilevatoreVariazioni()).Normalizzati();
            differenze=new RilevatoreVariazioni(this.parametriVariazioni);
            griglia=new Griglia(config);
            interprete=new InterpreteTavolo(griglia,layout);
            vita=new RilascioDifferito(()=>differenze.Dispose());
        }
        public StatoTavolo UltimoStato=>Volatile.Read(ref ultimoStato);
        public IReadOnlyList<RettangoloTavolo> UltimeRegioni=>Volatile.Read(ref ultimeRegioni);
        public long NumeroLetture=>Volatile.Read(ref letture);
        public long NumeroLettureZone=>NumeroLetture;
        public long NumeroFotogrammi=>Volatile.Read(ref fotogrammi);
        public string UltimoEsito=>Volatile.Read(ref ultimoEsito);
        public StatoScansione Scansione=>Volatile.Read(ref scansione);
        public int MisureAnticipoScansione
        {
            get=>Volatile.Read(ref misureAnticipoScansione);
            set
            {
                if(!ConfigurazioneApplicazione.AnticipoScansioneValido(value))throw new ArgumentOutOfRangeException(nameof(value));
                Volatile.Write(ref misureAnticipoScansione,value);
            }
        }
        public bool IndicatoriScansioneAttivi=>!LetturaCompletaPeriodica&&analizzaSoloSeCambia&&scansioneABlocchiAnticipata;

        /// <summary>
        /// True: a ogni multiplo di <see cref="PeriodoLetturaCompleta"/> misure esegue una lettura
        /// completa del foglio come «Rileggi foglio». False: usa le finestre progressive di celle.
        /// </summary>
        public bool LetturaCompletaPeriodica
        {
            get=>Volatile.Read(ref letturaCompletaPeriodica)!=0;
            set=>Volatile.Write(ref letturaCompletaPeriodica,value?1:0);
        }

        /// <summary>Periodo in misure della lettura completa (1, 2, 4 o 8).</summary>
        public int PeriodoLetturaCompleta
        {
            get=>Volatile.Read(ref periodoLetturaCompleta);
            set
            {
                if(value!=1&&value!=2&&value!=4&&value!=8)throw new ArgumentOutOfRangeException(nameof(value));
                Volatile.Write(ref periodoLetturaCompleta,value);
            }
        }

        public static bool DeveRileggereCompletamente(bool attiva,long misura,int periodo,long ultima)
            =>attiva&&misura>=0&&periodo>0&&misura%periodo==0&&misura!=ultima;

        public int PulsazioniPerOsservazione { get; set; }
        public Func<bool> Sospesa { get; set; }=()=>false;
        public void Rileggi()
        {
            Interlocked.Exchange(ref forzaRilettura,1);
            Volatile.Write(ref ultimoEsito,"Rilettura completa in attesa del prossimo frame");
        }
        public bool Passo(bool forza=false)
        {
            if(!vita.ProvaEntra())return false;
            try{return PassoInterno(forza);}finally{vita.Esci();}
        }
        private bool PassoInterno(bool forza)
        {
            if(Sospesa())return false;
            preparatore.ControllaScadenza();
            var motore=preparatore.Esecutore.Stato;
            long ora=orologio();
            long segnale=scansioneCelle&&motore.InEsecuzione?motore.TickTempo/6:PulsazioniPerOsservazione>0?motore.TickTempo/(24L*PulsazioniPerOsservazione):motore.SegnaleOsservazione;
            bool completaPeriodica=LetturaCompletaPeriodica;
            if(completaPeriodica!=(modalitaLetturaPrecedente!=0))
            {
                modalitaLetturaPrecedente=completaPeriodica?1:0;
                ultimaMisuraLetturaCompleta=long.MinValue;esecuzioneLetturaCompleta=-1;
                letturaCompletaInCorso=false;tentativiLetturaCompleta=0;misuraLetturaCompleta=-1;
                confermeBlocchi.Clear();blocchiIncompleti.Clear();blocchiDaConfermare.Clear();
                destinazioniIncomplete.Clear();blocchiScansionati.Clear();candidataCelle=null;
                Volatile.Write(ref scansione,StatoScansione.Vuoto);
            }
            if(completaPeriodica&&motore.Trasporto.Esecuzione!=esecuzioneLetturaCompleta)
            {
                esecuzioneLetturaCompleta=motore.Trasporto.Esecuzione;
                ultimaMisuraLetturaCompleta=long.MinValue;
                letturaCompletaInCorso=false;tentativiLetturaCompleta=0;misuraLetturaCompleta=-1;
            }
            if(DeveRileggereCompletamente(completaPeriodica,motore.Trasporto.MisuraAssoluta,
                PeriodoLetturaCompleta,ultimaMisuraLetturaCompleta)&&!letturaCompletaInCorso)
            {
                ultimaMisuraLetturaCompleta=motore.Trasporto.MisuraAssoluta;
                letturaCompletaInCorso=true;tentativiLetturaCompleta=0;
                misuraLetturaCompleta=motore.Trasporto.MisuraAssoluta;
                conferme=0;firmaCandidata=null;
                Volatile.Write(ref ultimoEsito,"Lettura completa periodica in corso");
            }
            bool manuale=Volatile.Read(ref forzaRilettura)!=0;
            int anticipo=MisureAnticipoScansione;
            bool cambioAnticipo=anticipo!=ultimoAnticipoScansione;
            bool normale=letturaCompletaInCorso||(!completaPeriodica&&((scansioneCelle&&motore.InEsecuzione&&motore.Trasporto.Esecuzione!=esecuzioneCelle)||cambioAnticipo||(motore.InEsecuzione?segnale!=ultimoSegnale:ora-ultimaOsservazione>=250)));
            bool riprovaBlocchi=!completaPeriodica&&motore.InEsecuzione&&analizzaSoloSeCambia&&scansioneABlocchiAnticipata
                &&(confermeBlocchi.Count>0||blocchiIncompleti.Count>0||candidataCelle!=null);
            if(!forza&&!manuale&&!normale&&!riprovaBlocchi&&ora<retryAt)return false;
            using(var f=immagini.Copia())
            {
                if(f==null)
                {if(motore.InPreparazione&&preparatore.Esecutore.AttesaAvvioMs>=1000)FallimentoAvvio("Nessun frame disponibile dalla webcam.");return false;}
                if(f.Id==ultimoFrame&&!manuale&&!motore.InPreparazione)return false;
                var c=calibrazione();
                if(c==null){FallimentoAvvio("Calibrazione assente.");return false;}
                if(c.VerificaRisoluzione(f.Immagine.Cols,f.Immagine.Rows)==EsitoRisoluzione.Diversa)
                {FallimentoAvvio("Risoluzione diversa dalla calibrazione.");return false;}
                if(!ReferenceEquals(c,corrente)||f.Generazione!=generazioneVideo)
                {
                    corrente=c;generazioneVideo=f.Generazione;generazione++;
                    candidataCelle=null;calibrazioneCelle=null;
                    preparatore.ImpostaGenerazione(generazione);catalogo=new CartoncinoRilevato[0];inizializzato=false;
                    confermeBlocchi.Clear();
                    blocchiScansionati.Clear();blocchiIncompleti.Clear();strutturaScansione=null;esecuzionePrecedente=false;
                    destinazioniIncomplete.Clear();blocchiDaConfermare.Clear();Volatile.Write(ref scansione,StatoScansione.Vuoto);
                    ultimoFrameScansioneCompleta=long.MinValue;
                    RiavviaConfronto();lettore=new LettoreZone(c,config);
                }
                if(Interlocked.Exchange(ref forzaRilettura,0)!=0)
                {
                    // La richiesta manuale deve ripartire da una nuova istantanea: il confronto
                    // incrementale può rifiutare il frame dopo una variazione globale di esposizione.
                    RiavviaConfronto();catalogo=new CartoncinoRilevato[0];inizializzato=false;
                    confermeBlocchi.Clear();
                    blocchiScansionati.Clear();blocchiIncompleti.Clear();strutturaScansione=null;
                    destinazioniIncomplete.Clear();blocchiDaConfermare.Clear();Volatile.Write(ref scansione,StatoScansione.Vuoto);
                    candidataCelle=null;calibrazioneCelle=null;
                    firmaCandidata=null;conferme=0;regioniDaVerificare=new RettangoloTavolo[0];
                    Volatile.Write(ref ultimeRegioni,regioniDaVerificare);
                    Volatile.Write(ref ultimoStato,new StatoTavolo(null,null,null));
                    Volatile.Write(ref ultimoEsito,"Rilettura completa in corso");
                }
                if(!analizzaSoloSeCambia&&inizializzato&&f.Id==ultimoFrameScansioneCompleta)return false;
                if(normale||manuale)tentativoBurst=0;
                if(cambioAnticipo)
                {
                    // Il worker applica il salto sul primo frame nuovo, senza scandire le misure intermedie.
                    blocchiScansionati.Clear();
                    Volatile.Write(ref scansione,StatoScansione.Vuoto);
                    ultimoAnticipoScansione=anticipo;
                }
                ultimoFrame=f.Id;ultimoSegnale=segnale;ultimaOsservazione=ora;retryAt=long.MaxValue;
                if(analizzaSoloSeCambia&&scansioneABlocchiAnticipata&&inizializzato
                    &&!motore.InEsecuzione&&!motore.InPreparazione&&!manuale
                    &&Volatile.Read(ref forzaRilettura)==0)
                {
                    // I blocchi si aggiornano quando la riproduzione li avvicina; da fermo
                    // decodificarli a rotazione spreca CPU e non può influire sul MIDI.
                    esecuzionePrecedente=false;
                    Volatile.Write(ref ultimeRegioni,new RettangoloTavolo[0]);
                    Volatile.Write(ref ultimoEsito,"ScansioneBlocchiInPausa");
                    return false;
                }
                Interlocked.Increment(ref fotogrammi);
                CatturaInCorso cattura=registro.IniziaCattura();var traccia=cattura?.Traccia;
                try
                {
                    if(traccia!=null){traccia.Layout=layout;traccia.CalibrazionePresente=true;traccia.LarghezzaFrame=f.Immagine.Cols;traccia.AltezzaFrame=f.Immagine.Rows;}
                    if(!inizializzato)return PrimaIstantanea(f,c,traccia);
                    // In modalità completa le finestre progressive non vengono eseguite:
                    // il prossimo aggiornamento arriva dalla lettura completa periodica.
                    if(completaPeriodica)
                    {
                        if(letturaCompletaInCorso)
                        {
                            AnalizzaScenaCompleta(f,c,traccia,true);
                            tentativiLetturaCompleta++;
                            if(conferme>=2||tentativiLetturaCompleta>=3)letturaCompletaInCorso=false;
                        }
                        return true;
                    }
                    if(!analizzaSoloSeCambia)
                    {
                        AnalizzaScenaCompleta(f,c,traccia);
                        return true;
                    }
                    if(scansioneABlocchiAnticipata)
                    {
                        if(scansioneCelle&&motore.InEsecuzione)AnalizzaFinestreCelle(f,c,traccia,motore,anticipo);
                        else AnalizzaBlocchiAnticipati(f,c,traccia,motore,anticipo,!normale&&!forza&&!manuale);
                        return true;
                    }
                    using(var tavolo=ImmagineTavolo(f.Immagine,c))
                    {
                        var variazioni=differenze.Confronta(tavolo);
                        var regioni=variazioni.Regioni.Concat(regioniDaVerificare).Distinct().ToArray();
                        Volatile.Write(ref ultimeRegioni,regioni);
                        if(!variazioni.Affidabile){firmaCandidata=null;conferme=0;Descrivi(traccia,"ImmagineNonAffidabile",f);return true;}
                        if(regioni.Length==0)
                        {firmaCandidata=null;conferme=0;Descrivi(traccia,"FoglioInvariato",f);return true;}
                        var lavoro=catalogo;var richiesteLette=new List<RettangoloTavolo>();var areeLette=new List<RettangoloTavolo>();
                        var rimozioniConfermate=new List<CartoncinoRilevato>();bool completa=true;
                        foreach(var z in regioni)
                        {
                            double destra=Math.Min(z.X+z.Larghezza,layout.Comandi.X);
                            if(destra<=z.X)continue;
                            var zona=new RettangoloTavolo(z.X,z.Y,destra-z.X,z.Altezza);
                            var primaLettura=lavoro;
                            var r=lettore.Leggi(f.Immagine,zona,primaLettura,traccia);Interlocked.Increment(ref letture);
                            if(vita.InChiusura||Sospesa())return true;
                            // Una ROI incompleta puo' aver perso un QR per occlusione: applica
                            // rimozioni solo quando il lettore dichiara affidabile quella ROI.
                            if(r.Completa)rimozioniConfermate.AddRange(TrovaRimozioniConfermate(zona,primaLettura,r.Catalogo,tavolo,variazioni.LivelloSfondo));
                            // Una ROI parziale conserva i cartoncini noti e integra ogni QR valido.
                            // La completezza governa l'esito diagnostico, non annulla i risultati positivi.
                            var uniti=r.Completa?r.Catalogo:UnisciCatalogoParziale(lavoro,r.Catalogo);
                            lavoro=uniti.Where(n=>layout.ZonaDelCentro(n.AreaQr??n.Area)!=ZonaSpeciale.Comandi).ToArray();
                            richiesteLette.Add(z);areeLette.Add(zona);
                            if(!r.Completa)completa=false;
                        }
                        if(!ReferenceEquals(calibrazione(),c)||immagini.GenerazioneCorrente!=f.Generazione)
                        {Descrivi(traccia,"LetturaObsoleta",f);return true;}
                        // Un decoder può aver oltrepassato una scadenza: congela prima di fondere il nuovo contenuto.
                        preparatore.ControllaScadenza();
                        // Una sola ROI incerta rende provvisoria anche l'assenza di ogni cartoncino noto:
                        // recupera i positivi e conserva le identità non verificate. Una ROI può però
                        // confermare localmente l'assenza di un QR anche se un'altra resta incerta.
                        if(!completa)
                            lavoro=UnisciCatalogoParziale(catalogo.Where(n=>
                                    !rimozioniConfermate.Any(x=>StessaPosizioneQr(n,x))).ToArray(),lavoro)
                                .Where(n=>layout.ZonaDelCentro(n.AreaQr??n.Area)!=ZonaSpeciale.Comandi).ToArray();
                        var stato=interprete.Interpreta(lavoro,traccia);
                        string firma=Firma(stato);
                        if(firma!=firmaCandidata){firmaCandidata=firma;conferme=1;}else conferme=Math.Min(2,conferme+1);
                        if(conferme>=2&&(inizializzato||completa))
                        {
                            catalogo=lavoro;inizializzato=true;Volatile.Write(ref ultimoStato,stato);
                            preparatore.Aggiorna(stato,true,generazione,f.IstanteMs);
                            // Dopo due frame coerenti si aggiorna il riferimento anche nelle zone
                            // incomplete: l'incertezza resta registrata, ma non forza scansioni
                            // identiche. Un cambiamento successivo o Rileggi riapre quelle zone.
                            differenze.Conferma(areeLette);
                            regioniDaVerificare=regioniDaVerificare.Where(z=>!richiesteLette.Contains(z)).ToArray();
                            Descrivi(traccia,completa?"FoglioConfermato":"ZoneIncerteConfermate",f);
                        }
                        else Descrivi(traccia,completa?"InAttesaDiConferma":"ZoneDaRileggere",f);
                        if(tentativoBurst==0&&conferme<2){tentativoBurst=1;retryAt=orologio()+80;}
                    }
                }
                catch(Exception e){FallimentoAvvio("Analisi non applicata: "+e.Message);if(traccia!=null){traccia.Errore=e.GetType().Name+": "+e.Message;traccia.EsitoVisione="ErroreAnalisi";}}
                finally{registro.CompletaCattura(cattura);}
                return true;
            }
        }
        private bool PrimaIstantanea(FotogrammaAcquisito f,Calibrazione c,TracciaCartoncini traccia)
        {
            var tempo=Stopwatch.StartNew();var letta=new LettoreIstantanea(c,config).Leggi(f.Immagine,traccia);
            Interlocked.Increment(ref letture);
            if(vita.InChiusura||Sospesa()||!ReferenceEquals(calibrazione(),c)||immagini.GenerazioneCorrente!=f.Generazione)return true;
            var nuovi=letta.Cartoncini.Where(n=>layout.ZonaDelCentro(n.AreaQr??n.Area)!=ZonaSpeciale.Comandi).ToArray();
            var stato=interprete.Interpreta(nuovi,traccia,true);
            if(stato.Note.Count==0&&!letta.VuotoAffidabile&&(nuovi.Length==0||stato.Errori.Count!=0))
            {
                FallimentoAvvio(nuovi.Length==0?"Nessun QR leggibile nell'istantanea.":"Nessuna nota applicabile: controlla i vincoli nel debug.");
                if(traccia!=null){traccia.EsitoVisione="AvvioRifiutato";traccia.Errore=UltimoEsito;}
                return true;
            }
            Volatile.Write(ref ultimoStato,stato);
            if(!preparatore.PreparaAvvio(stato,generazione,f.IstanteMs,()=>Volatile.Write(ref primaPubblicazioneMs,tempo.Elapsed.TotalMilliseconds)))return true;
            catalogo=nuovi;inizializzato=true;conferme=1;
            regioniDaVerificare=letta.RegioniDaRileggere.Where(z=>z.X<layout.Comandi.X).ToArray();
            Volatile.Write(ref ultimeRegioni,regioniDaVerificare);
            // La base visiva viene preparata dopo la prima pubblicazione, senza ritardare il MIDI.
            using(var tavolo=ImmagineTavolo(f.Immagine,c))
            {var v=differenze.Confronta(tavolo);if(v.Affidabile)differenze.Conferma(new[]{new RettangoloTavolo(0,0,1,1)});}
            Descrivi(traccia,"AvvioDaIstantanea",f,1);
            if(traccia!=null)traccia.Visione.Add(new EventoDiagnosticoCartoncino("Avvio","PrimoGruppoPronto","PrimoGruppoPronto",
                $"Frame {f.Id}; lettura={letta.DurataMs:0.0}ms; prima pubblicazione={PrimaPubblicazioneMs:0.0}ms; regioni da verificare={regioniDaVerificare.Count}."));
            return true;
        }
        private Mat ImmagineTavolo(Mat frame,Calibrazione c)
        {
            var tavolo=c.Raddrizza(frame,640,360);
            Rect cmd=new Rect((int)(layout.Comandi.X*640),(int)(layout.Comandi.Y*360),
                (int)(layout.Comandi.Larghezza*640),(int)(layout.Comandi.Altezza*360))&new Rect(0,0,640,360);
            if(cmd.Width>0&&cmd.Height>0)Cv2.Rectangle(tavolo,cmd,Scalar.White,-1);
            return tavolo;
        }
        private void FallimentoAvvio(string motivo)
        {Volatile.Write(ref ultimoEsito,motivo);if(preparatore.Esecutore.Stato.InPreparazione)preparatore.Esecutore.RifiutaAvvio("Avvio impossibile: "+motivo);}
        private void Descrivi(TracciaCartoncini t,string esito,FotogrammaAcquisito f,int richieste=2)
        {
            Volatile.Write(ref ultimoEsito,esito);
            if(t==null)return;
            var s=UltimoStato;t.EsitoVisione=esito;t.NoteStabili=s.Note.Count;t.StrutturaStabile=s.Struttura;
            t.EsitoStabilizzazione=esito;t.ConfermeRichieste=richieste;t.ConfermeStruttura=conferme;
            t.Visione.Add(new EventoDiagnosticoCartoncino("Osservazione",esito,"VersioniFoglio",
                $"Frame {f.Id} a {f.IstanteMs}ms; osservata={preparatore.Foglio.Ultima?.Versione??0}, pronta={preparatore.Esecutore.Stato.VersionePronta}, attiva={preparatore.Esecutore.Stato.VersioneAttiva}; "+
                $"ultima misura destinazione={preparatore.Esecutore.Stato.UltimaMisuraDestinazione}, applicata={preparatore.Esecutore.Stato.UltimaMisuraApplicazione}, "+
                $"rinviata={preparatore.Esecutore.Stato.UltimoAggiornamentoRinviato}; letture scena/ROI={NumeroLetture}."));
        }
        private void AnalizzaScenaCompleta(FotogrammaAcquisito f,Calibrazione c,TracciaCartoncini traccia,bool programmata=false)
        {
            ultimoFrameScansioneCompleta=f.Id;
            var letta=new LettoreIstantanea(c,config).Leggi(f.Immagine,traccia);
            Interlocked.Increment(ref letture);
            if(vita.InChiusura||Sospesa()||!ReferenceEquals(calibrazione(),c)||immagini.GenerazioneCorrente!=f.Generazione)
            {Descrivi(traccia,"LetturaObsoleta",f);return;}

            var incerte=letta.RegioniDaRileggere.Where(z=>z.X<layout.Comandi.X).ToArray();
            // Una scena buia senza QR né regioni riconoscibili non conferma la rimozione di tutto.
            // Si conserva il catalogo e si attende una cattura che il lettore possa valutare.
            if(letta.Cartoncini.Count==0&&!letta.VuotoAffidabile&&incerte.Length==0)
            {
                firmaCandidata=null;
                conferme=0;
                Descrivi(traccia,"ScansioneCompletaNonAffidabile",f);
                return;
            }

            preparatore.ControllaScadenza();
            var nuovi=letta.Cartoncini
                .Where(n=>layout.ZonaDelCentro(n.AreaQr??n.Area)!=ZonaSpeciale.Comandi).ToArray();
            var daConservare=incerte.Length==0?new CartoncinoRilevato[0]:catalogo.Where(n=>
            {
                var area=n.AreaQr??n.Area;
                return incerte.Any(z=>GeometriaRegioni.ContieneCentro(z,area));
            }).ToArray();
            var lavoro=UnisciCatalogoParziale(daConservare,nuovi)
                .Where(n=>layout.ZonaDelCentro(n.AreaQr??n.Area)!=ZonaSpeciale.Comandi).ToArray();
            bool completa=incerte.Length==0;
            var stato=interprete.Interpreta(lavoro,traccia);
            string firma=Firma(stato);
            if(firma!=firmaCandidata){firmaCandidata=firma;conferme=1;}
            else conferme=Math.Min(2,conferme+1);

            if(conferme>=2)
            {
                catalogo=lavoro;
                inizializzato=true;
                regioniDaVerificare=incerte;
                Volatile.Write(ref ultimeRegioni,regioniDaVerificare);
                Volatile.Write(ref ultimoStato,stato);
                var trasporto=preparatore.Esecutore.Stato.Trasporto;
                bool strutturaCambiata=trasporto.Struttura==null||!stato.Struttura.Equals(trasporto.Struttura);
                if(programmata&&!strutturaCambiata&&preparatore.Esecutore.Stato.InEsecuzione)
                    preparatore.AggiornaPerMisura(stato,true,generazione,f.IstanteMs,
                        preparatore.Esecutore.Stato.MisuraAssoluta+1);
                else
                    preparatore.Aggiorna(stato,true,generazione,f.IstanteMs);
                Descrivi(traccia,completa?"ScansioneCompletaConfermata":"ScansioneCompletaConIncertezze",f);
            }
            else Descrivi(traccia,completa?"ScansioneCompletaInAttesaDiConferma":"ScansioneCompletaDaConfermare",f);
        }

        public static int[] RigheAnticipate(StrutturaTavolo struttura,PosizioneGiro posizione,int misure)
        {
            if(struttura==null)throw new ArgumentNullException(nameof(struttura));
            if(posizione==null)throw new ArgumentNullException(nameof(posizione));
            if(misure<0)throw new ArgumentOutOfRangeException(nameof(misure));
            long inizio=(struttura.AInizio-1L)*StrutturaTavolo.StepPerBattuta;
            long passoCorrente=(long)posizione.MisuraNelGiro*struttura.StepPerMisura+posizione.StepInMisura-inizio;
            int indiceCorrente=0;
            for(int i=0;i<struttura.MisurePerGiro;i++)
            {
                var frontiera=struttura.PosizioneMisuraGiro(i);
                long passoFrontiera=(long)frontiera.MisuraNelGiro*struttura.StepPerMisura+frontiera.StepInMisura-inizio;
                if(passoFrontiera<=passoCorrente)indiceCorrente=i;else break;
            }
            int indiceFuturo=(indiceCorrente+misure)%struttura.MisurePerGiro;
            return struttura.PosizioneMisuraGiro(indiceFuturo).RigheAttive.OrderBy(r=>r).ToArray();
        }

        private void AnalizzaBlocchiAnticipati(FotogrammaAcquisito f,Calibrazione c,TracciaCartoncini traccia,
            MotorePartitura motore,int anticipo,bool soloPendenti=false)
        {
            var trasporto=motore.Trasporto;
            var struttura=trasporto.Struttura??motore.Struttura;var posizione=motore.Posizione;long giro=motore.Giro;
            bool inEsecuzione=trasporto.InEsecuzione;long misuraCorrente=trasporto.MisuraAssoluta;
            long misuraDestinazione=-1;PosizioneGiro posizioneDestinazione=null;
            var tuttiBlocchi=Enumerable.Range(0,config.Tracce).Select(r=>new BloccoScansione(r,ZonaRiga(r)))
                .Concat(new[]
                {
                    new BloccoScansione(-1,layout.Misura,ZonaSpeciale.Misura),
                    new BloccoScansione(-2,layout.Collegamento,ZonaSpeciale.Collegamento),
                    new BloccoScansione(-3,layout.Ab,ZonaSpeciale.Ab)
                }).Where(b=>b.Zona.Larghezza>0&&b.Zona.Altezza>0).ToDictionary(b=>b.Id);
            var pianificati=new Dictionary<int,BloccoPianificato>();
            void Aggiungi(BloccoScansione blocco,long giroDestinazione,int misuraDestinazione,long misuraAssoluta=-1)
            {
                if(blocco==null)return;
                if(!pianificati.TryGetValue(blocco.Id,out var pianificato))
                {pianificato=new BloccoPianificato{Blocco=blocco};pianificati.Add(blocco.Id,pianificato);}
                var chiave=new ChiaveBloccoPianificato(giroDestinazione,misuraDestinazione,blocco.Id,misuraAssoluta);
                if(!pianificato.Chiavi.Contains(chiave))pianificato.Chiavi.Add(chiave);
            }

            if(inEsecuzione)
            {
                if(!esecuzionePrecedente||esecuzioneScansione!=trasporto.Esecuzione
                    ||strutturaScansione==null||!strutturaScansione.Equals(struttura))
                {
                    blocchiScansionati.Clear();confermeBlocchi.Clear();blocchiIncompleti.Clear();destinazioniIncomplete.Clear();blocchiDaConfermare.Clear();
                    Volatile.Write(ref scansione,StatoScansione.Vuoto);
                }
                esecuzioneScansione=trasporto.Esecuzione;
                esecuzionePrecedente=true;strutturaScansione=struttura;
                blocchiScansionati.RemoveWhere(k=>k.Id<0?k.Giro<giro:k.MisuraAssoluta>=0&&k.MisuraAssoluta<misuraCorrente);

                misuraDestinazione=trasporto.MisuraAssoluta+anticipo;
                posizioneDestinazione=struttura.PosizioneMisuraGiro((trasporto.IndiceMisura+anticipo)%struttura.MisurePerGiro);
                foreach(int riga in soloPendenti?new int[0]:posizioneDestinazione.RigheAttive??new int[0])
                    if(tuttiBlocchi.TryGetValue(riga,out var bloccoFuturo))
                        Aggiungi(bloccoFuturo,0,posizioneDestinazione.MisuraNelGiro,misuraDestinazione);

                for(int riga=0;riga<config.Tracce;riga++)
                    if(!soloPendenti&&regioniDaVerificare.Any(z=>GeometriaRegioni.Interseca(z,ZonaRiga(riga)))
                        &&tuttiBlocchi.TryGetValue(riga,out var bloccoDaVerificare))
                        Aggiungi(bloccoDaVerificare,0,posizioneDestinazione.MisuraNelGiro,misuraDestinazione);

                foreach(int id in new[]{-1,-2,-3})
                    if(!soloPendenti&&tuttiBlocchi.TryGetValue(id,out var vincolo))Aggiungi(vincolo,giro,-1);

                foreach(int id in blocchiIncompleti)
                    if(tuttiBlocchi.TryGetValue(id,out var bloccoIncompleto))
                    {
                        var origine=destinazioniIncomplete[id];
                        Aggiungi(bloccoIncompleto,origine.Giro,origine.Misura,origine.MisuraAssoluta);
                    }

                foreach(var conferma in confermeBlocchi)
                    if(tuttiBlocchi.TryGetValue(conferma.Key,out var bloccoInConferma))
                        Aggiungi(bloccoInConferma,conferma.Value.Giro,conferma.Value.Misura,
                            conferma.Key<0?-1:conferma.Value.MisuraAssoluta);
            }
            else
            {
                esecuzionePrecedente=false;
                if(prossimoBloccoFermo>=tuttiBlocchi.Count)prossimoBloccoFermo=0;
                var bloccoFermo=tuttiBlocchi.Values.OrderBy(b=>b.Id>=0?b.Id:config.Tracce-b.Id-1)
                    .ElementAt(prossimoBloccoFermo++);
                Aggiungi(bloccoFermo,giro,posizione.MisuraNelGiro,-1);
            }

            var blocchi=pianificati.Values.Where(p=>!inEsecuzione
                    ||p.Chiavi.Any(k=>!blocchiScansionati.Contains(k))
                    ||confermeBlocchi.ContainsKey(p.Blocco.Id)
                    ||regioniDaVerificare.Any(z=>GeometriaRegioni.Interseca(z,p.Blocco.Zona)))
                .OrderBy(p=>p.Blocco.Id>=0?p.Blocco.Id:config.Tracce-p.Blocco.Id-1).ToArray();
            int blocchiSaltati=inEsecuzione?pianificati.Count-blocchi.Length:0;
            Volatile.Write(ref ultimeRegioni,blocchi.Select(p=>p.Blocco.Zona).ToArray());

            var catalogoPrima=catalogo;
            var lavoro=catalogo;
            var modificheConfermate=new List<AggiornamentoBloccoConfermato>();
            var lettureConfermate=new List<LetturaScansione>();
            bool catalogoAggiornato=false;
            int blocchiIncerti=0;
            foreach(var pianificato in blocchi)
            {
                var blocco=pianificato.Blocco;
                var chiaveLettura=confermeBlocchi.TryGetValue(blocco.Id,out var pendente)
                    ?new ChiaveBloccoPianificato(pendente.Giro,pendente.Misura,blocco.Id,pendente.MisuraAssoluta)
                    :destinazioniIncomplete.TryGetValue(blocco.Id,out var incompleta)?incompleta:pianificato.Chiavi[0];
                LetturaScansione operazione=null;
                if(inEsecuzione&&blocco.Id>=0&&chiaveLettura.MisuraAssoluta>=0)
                {
                    int indice=0;
                    while(indice+1<struttura.MisurePerGiro&&struttura.PosizioneMisuraGiro(indice).MisuraNelGiro!=chiaveLettura.Misura)indice++;
                    if(struttura.PosizioneMisuraGiro(indice).RigheAttive.Contains(blocco.Id))
                    {
                        operazione=new LetturaScansione(trasporto,chiaveLettura.MisuraAssoluta,indice,f.Id,f.IstanteMs,orologio());
                        Volatile.Write(ref scansione,new StatoScansione(operazione,Scansione.UltimaCompletata,Scansione.UltimaLettura,"Lettura in corso"));
                    }
                }
                LetturaZona lettura;bool fallita=false;
                try {lettura=lettore.Leggi(f.Immagine,blocco.Zona,lavoro,traccia,"blocco:"+blocco.Id);}
                catch
                {
                    fallita=true;
                    if(operazione!=null)Volatile.Write(ref scansione,new StatoScansione(null,
                        Scansione.UltimaCompletata,operazione,"Errore di lettura"));
                    throw;
                }
                finally
                {
                    if(operazione!=null&&!fallita)Volatile.Write(ref scansione,new StatoScansione(null,
                        Scansione.UltimaCompletata,operazione,"Lettura terminata, verifica in corso"));
                }
                Interlocked.Increment(ref letture);
                if(vita.InChiusura||Sospesa())return;
                var adesso=motore.Trasporto;
                if(inEsecuzione&&(!adesso.InEsecuzione||adesso.Esecuzione!=trasporto.Esecuzione
                    ||!adesso.Struttura.Equals(struttura)||!ReferenceEquals(calibrazione(),c)
                    ||immagini.GenerazioneCorrente!=f.Generazione))
                {Volatile.Write(ref scansione,StatoScansione.Vuoto);return;}
                var correnti=LocaliDelBlocco(lavoro,blocco);
                var letti=LocaliDelBlocco(lettura.Catalogo,blocco);
                string firmaLettura=FirmaCatalogoBlocco(letti);
                string firmaCorrente=FirmaCatalogoBlocco(correnti);
                bool completa=lettura.Completa&&IlluminazioneAffidabile(f.Immagine,lettura.RegioneMaschera);
                if(!completa)
                {
                    blocchiIncerti++;
                    blocchiIncompleti.Add(blocco.Id);
                    if(!destinazioniIncomplete.ContainsKey(blocco.Id))destinazioniIncomplete[blocco.Id]=chiaveLettura;
                    if(operazione!=null)Volatile.Write(ref scansione,new StatoScansione(null,
                        Scansione.UltimaCompletata,operazione,"Lettura incompleta o illuminazione insufficiente"));
                    confermeBlocchi.Remove(blocco.Id);
                    if(inEsecuzione)blocchiScansionati.ExceptWith(pianificato.Chiavi);
                    var integrati=UnisciCatalogoParziale(correnti,letti);
                    if(FirmaCatalogoBlocco(integrati)!=firmaCorrente)
                    {
                        blocchiDaConfermare.Add(blocco.Id);
                        lavoro=SostituisciBlocco(lavoro,blocco,integrati);
                        catalogoAggiornato=true;
                    }
                    continue;
                }

                blocchiIncompleti.Remove(blocco.Id);
                destinazioniIncomplete.Remove(blocco.Id);
                regioniDaVerificare=regioniDaVerificare.Where(z=>!GeometriaRegioni.Interseca(z,blocco.Zona)).ToArray();
                if(firmaLettura==firmaCorrente&&!blocchiDaConfermare.Contains(blocco.Id))
                {
                    confermeBlocchi.Remove(blocco.Id);
                    if(inEsecuzione)blocchiScansionati.UnionWith(pianificato.Chiavi);
                    if(operazione!=null)lettureConfermate.Add(operazione);
                    continue;
                }

                if(!confermeBlocchi.TryGetValue(blocco.Id,out var candidata)||candidata.Firma!=firmaLettura)
                {
                    var chiave=chiaveLettura;
                    candidata=new ConfermaBlocco{Firma=firmaLettura,UltimoFrame=f.Id,Giro=chiave.Giro,
                        Misura=chiave.Misura,MisuraAssoluta=chiave.MisuraAssoluta,Numero=1,Catalogo=letti};
                    confermeBlocchi[blocco.Id]=candidata;
                }
                else if(candidata.UltimoFrame!=f.Id)
                {
                    candidata.UltimoFrame=f.Id;
                    candidata.Numero=Math.Min(2,candidata.Numero+1);
                    candidata.Catalogo=letti;
                }
                if(candidata.Numero>=2)
                {
                    blocchiDaConfermare.Remove(blocco.Id);
                    lavoro=SostituisciBlocco(lavoro,blocco,candidata.Catalogo);
                    catalogoAggiornato=true;
                    modificheConfermate.Add(new AggiornamentoBloccoConfermato
                    {Blocco=blocco,MisuraAssoluta=candidata.MisuraAssoluta,Catalogo=candidata.Catalogo});
                    confermeBlocchi.Remove(blocco.Id);
                    if(inEsecuzione)blocchiScansionati.UnionWith(pianificato.Chiavi);
                    if(operazione!=null)lettureConfermate.Add(operazione);
                }
                else if(inEsecuzione)
                {
                    blocchiScansionati.ExceptWith(pianificato.Chiavi);
                    if(operazione!=null)Volatile.Write(ref scansione,new StatoScansione(null,
                        Scansione.UltimaCompletata,operazione,"Conferma 1/2 in attesa del prossimo frame"));
                }
            }

            Volatile.Write(ref ultimeRegioni,blocchi.Select(p=>p.Blocco.Zona).ToArray());
            if(catalogoAggiornato)
            {
                if(!ReferenceEquals(calibrazione(),c)||immagini.GenerazioneCorrente!=f.Generazione)
                {Descrivi(traccia,"LetturaObsoleta",f);return;}
                preparatore.ControllaScadenza();
                catalogo=lavoro;
                var stato=interprete.Interpreta(catalogo,traccia);
                bool strutturaCambiata=!stato.Struttura.Equals(motore.Struttura);
                if(inEsecuzione&&!strutturaCambiata&&modificheConfermate.Any(x=>x.Blocco.Id>=0))
                {
                    var timeline=catalogoPrima;
                    var baseConfermata=preparatore.Foglio.Ultima?.CopiaStato()??motore.PartituraAttiva.CopiaStato();
                    var noteConfermate=baseConfermata.Note;
                    string firmaPrecedente=Firma(baseConfermata);
                    foreach(var gruppo in modificheConfermate.Where(x=>x.Blocco.Id>=0)
                        .GroupBy(x=>x.MisuraAssoluta).OrderBy(x=>x.Key))
                    {
                        foreach(var modifica in gruppo.OrderBy(x=>x.Blocco.Id))
                            timeline=SostituisciBlocco(timeline,modifica.Blocco,modifica.Catalogo);
                        var statoMisura=interprete.Interpreta(timeline,traccia);
                        // Il catalogo osservato può includere QR positivi di altre ROI ancora parziali.
                        // Al MIDI trasferiamo soltanto le righe confermate di questo gruppo.
                        var righeConfermate=new HashSet<int>(gruppo.Select(x=>x.Blocco.Id));
                        noteConfermate=noteConfermate.Where(n=>!righeConfermate.Contains(n.Riga))
                            .Concat(statoMisura.Note.Where(n=>righeConfermate.Contains(n.Riga))).ToArray();
                        statoMisura=new StatoTavolo(noteConfermate,null,statoMisura.Errori,statoMisura.Struttura);
                        string firma=Firma(statoMisura);
                        if(firma!=firmaPrecedente)
                        {
                            bool programmata=preparatore.AggiornaPerMisura(statoMisura,true,generazione,f.IstanteMs,gruppo.Key);
                            if(traccia!=null)traccia.Visione.Add(new EventoDiagnosticoCartoncino("MIDI",
                                programmata?"AggiornamentoProgrammata":"AggiornamentoNonProgrammato","MisuraAssoluta",
                                $"Destinazione={gruppo.Key}; offset={anticipo}; pronta={preparatore.Esecutore.Stato.VersionePronta}; "+
                                $"attiva={preparatore.Esecutore.Stato.VersioneAttiva}; errore={preparatore.UltimoErrore??"nessuno"}."));
                        }
                        firmaPrecedente=firma;
                    }
                    Volatile.Write(ref ultimoStato,stato);
                }
                else if(Firma(stato)!=Firma(UltimoStato))
                {
                    Volatile.Write(ref ultimoStato,stato);
                    if(!inEsecuzione||strutturaCambiata||modificheConfermate.Any(x=>x.Blocco.Id<0))
                        preparatore.Aggiorna(stato,true,generazione,f.IstanteMs);
                }
            }
            foreach(var operazione in lettureConfermate.OrderBy(x=>x.MisuraAssoluta))
            {
                var attuale=motore.Trasporto;
                if(preparatore.UltimoErrore!=null||!attuale.InEsecuzione||attuale.Esecuzione!=trasporto.Esecuzione
                    ||!struttura.Equals(attuale.Struttura))continue;
                var righe=struttura.PosizioneMisuraGiro(operazione.IndiceMisura).RigheAttive;
                if(!righe.All(r=>blocchiScansionati.Any(k=>k.Id==r&&k.MisuraAssoluta==operazione.MisuraAssoluta)))continue;
                var precedente=Scansione.UltimaCompletata;
                if(precedente!=null&&precedente.MisuraAssoluta>operazione.MisuraAssoluta)continue;
                var completa=new LetturaScansione(operazione.Trasporto,operazione.MisuraAssoluta,operazione.IndiceMisura,
                    operazione.FrameId,operazione.IstanteFrameMs,operazione.InizioMs,orologio());
                Volatile.Write(ref scansione,new StatoScansione(null,completa,completa,"Scansione confermata"));
                if(traccia!=null)traccia.Visione.Add(new EventoDiagnosticoCartoncino("Scansione","Completata","MisuraAssoluta",
                    $"Destinazione={completa.MisuraAssoluta}; frame={completa.FrameId}; cattura={completa.IstanteFrameMs}ms; "+
                    $"inizio={completa.InizioMs}ms; fine={completa.FineMs}ms; righe={string.Join(",",completa.Posizione.RigheAttive)}."));
            }
            conferme=confermeBlocchi.Count==0?0:confermeBlocchi.Values.Max(x=>x.Numero);
            string esito=inEsecuzione?"ScansioneBlocchiAnticipata":"ScansioneBlocchiFermi";
            Descrivi(traccia,esito,f);
            if(traccia!=null)traccia.Visione.Add(new EventoDiagnosticoCartoncino("Osservazione",
                inEsecuzione?"BlocchiAnticipati":"BloccoFermo","RigheMusicali",
                $"Blocchi={string.Join(",",blocchi.Select(p=>p.Blocco.Id>=0?"riga"+p.Blocco.Id:p.Blocco.Speciale.ToString()))}; " +
                $"vincoli MIDI={blocchi.Count(p=>p.Blocco.Id<0)}; offset={anticipo} misure; " +
                $"misura destinazione={misuraDestinazione}; " +
                $"blocchi già confermati saltati={blocchiSaltati}; incerti={blocchiIncerti}; " +
                $"conferme in attesa={confermeBlocchi.Count}; giro={giro}; frame={f.Id}."));
        }

        private bool IlluminazioneAffidabile(Mat frame,Rect regione)
        {
            regione&=new Rect(0,0,frame.Cols,frame.Rows);
            if(regione.Width<8||regione.Height<8)return false;
            using(var ritaglio=new Mat(frame,regione))
            using(var grigio=new Mat())
            using(var ridotto=new Mat())
            {
                Mat sorgente=ritaglio;
                if(ritaglio.Channels()!=1)
                {
                    Cv2.CvtColor(ritaglio,grigio,ritaglio.Channels()==4?ColorConversionCodes.BGRA2GRAY:ColorConversionCodes.BGR2GRAY);
                    sorgente=grigio;
                }
                Cv2.Resize(sorgente,ridotto,new Size(96,64),0,0,InterpolationFlags.Area);
                return Cv2.Mean(ridotto).Val0>=parametriVariazioni.LuminositaMinimaSfondo;
            }
        }

        private RettangoloTavolo ZonaRiga(int riga)
        {
            double altezza=config.Area.Altezza/config.Tracce;
            double tolleranza=config.TolleranzaQrCelle;
            if(double.IsNaN(tolleranza)||double.IsInfinity(tolleranza)||tolleranza<0)tolleranza=0;
            tolleranza=Math.Min(0.49,tolleranza);
            double margineX=config.Step>0?config.Area.Larghezza/config.Step*tolleranza:0;
            // La ROI verticale coincide con la corsia: espanderla sovrappone i blocchi e
            // fa ridigitalizzare i QR delle righe adiacenti a ogni scansione anticipata.
            double margineY=0;
            double x=Math.Max(0,config.Area.X-margineX);
            double y=Math.Max(config.Area.Y,config.Area.Y+riga*altezza-margineY);
            double destra=Math.Min(Math.Min(config.Area.X+config.Area.Larghezza,layout.Comandi.X),
                config.Area.X+config.Area.Larghezza+margineX);
            double basso=Math.Min(config.Area.Y+config.Area.Altezza,config.Area.Y+(riga+1)*altezza+margineY);
            return new RettangoloTavolo(x,y,Math.Max(0,destra-x),Math.Max(0,basso-y));
        }

        private IReadOnlyList<CartoncinoRilevato> LocaliDelBlocco(IReadOnlyList<CartoncinoRilevato> valori,BloccoScansione blocco)
            =>(valori??new CartoncinoRilevato[0]).Where(n=>AppartieneAlBlocco(n,blocco)).ToArray();

        private bool AppartieneAlBlocco(CartoncinoRilevato nota,BloccoScansione blocco)
        {
            var area=nota.AreaQr??nota.Area;
            if(blocco.Id>=0)
                return griglia.TryCella(nota,out int riga,out _,out _) ? riga==blocco.Id
                    : GeometriaRegioni.ContieneCentro(blocco.Zona,area);
            return layout.ZonaDelCentro(area)==blocco.Speciale;
        }

        private IReadOnlyList<CartoncinoRilevato> SostituisciBlocco(IReadOnlyList<CartoncinoRilevato> valori,
            BloccoScansione blocco,IReadOnlyList<CartoncinoRilevato> nuovi)
            =>valori.Where(n=>!AppartieneAlBlocco(n,blocco)).Select(GeometriaRegioni.Copia)
                .Concat((nuovi??new CartoncinoRilevato[0]).Select(GeometriaRegioni.Copia)).ToArray();

        private static string FirmaCatalogoBlocco(IEnumerable<CartoncinoRilevato> cartoncini)
            =>string.Join(";",(cartoncini??new CartoncinoRilevato[0]).Select(n=>
            {
                var qr=n.AreaQr??n.Area;
                return (n.Payload??"")+":"+n.Colore+":"+Quantizza(qr.X)+":"+Quantizza(qr.Y)+":"+
                    Quantizza(qr.Larghezza)+":"+Quantizza(qr.Altezza)+":"+Quantizza(n.FineEstensioneX??double.NaN);
            }).OrderBy(x=>x,StringComparer.Ordinal));

        private static long Quantizza(double valore)=>double.IsNaN(valore)||double.IsInfinity(valore)?0:(long)Math.Round(valore*200,MidpointRounding.AwayFromZero);

        private void RiavviaConfronto(){differenze.Dispose();differenze=new RilevatoreVariazioni(parametriVariazioni);firmaCandidata=null;conferme=0;}
        internal static string Firma(StatoTavolo s)=>s.Struttura.BattitiPerMisura+":"+s.Struttura.RigheCollegate+":"+s.Struttura.AInizio+":"+s.Struttura.AFine+"|"+
            string.Join(";",s.Note.Select(n=>n.Chiave+":"+n.DurataStep).OrderBy(x=>x));

        private IReadOnlyList<CartoncinoRilevato> TrovaRimozioniConfermate(RettangoloTavolo zona,
            IReadOnlyList<CartoncinoRilevato> noti,IReadOnlyList<CartoncinoRilevato> catalogoLetto,Mat tavolo,int livelloSfondo)
        {
            var rimossi=new List<CartoncinoRilevato>();var associati=new HashSet<int>();
            foreach(var noto in noti??new CartoncinoRilevato[0])
            {
                var area=noto.AreaQr??noto.Area;
                if(!GeometriaRegioni.ContieneCentro(zona,area))continue;
                int migliore=-1;double distanza=double.MaxValue;
                var letti=catalogoLetto??new CartoncinoRilevato[0];
                for(int i=0;i<letti.Count;i++)
                {
                    if(associati.Contains(i))continue;
                    var altro=letti[i].AreaQr??letti[i].Area;
                    if(!GeometriaRegioni.StessaPosizioneQr(area,altro))continue;
                    double dx=area.X+area.Larghezza/2-altro.X-altro.Larghezza/2;
                    double dy=area.Y+area.Altezza/2-altro.Y-altro.Altezza/2;
                    double d=Math.Sqrt(dx*dx+dy*dy);
                    if(d<distanza){migliore=i;distanza=d;}
                }
                if(migliore>=0)associati.Add(migliore);
                else if(QrSostituitoDaSfondo(tavolo,noto,livelloSfondo))rimossi.Add(GeometriaRegioni.Copia(noto));
            }
            return rimossi.AsReadOnly();
        }

        private bool QrSostituitoDaSfondo(Mat tavolo,CartoncinoRilevato cartoncino,int livelloSfondo)
        {
            if(tavolo==null||tavolo.Empty()||livelloSfondo<parametriVariazioni.LuminositaMinimaSfondo)return false;
            var area=cartoncino.AreaQr??cartoncino.Area;
            int x=(int)Math.Floor(area.X*tavolo.Cols),y=(int)Math.Floor(area.Y*tavolo.Rows);
            int x1=(int)Math.Ceiling((area.X+area.Larghezza)*tavolo.Cols);
            int y1=(int)Math.Ceiling((area.Y+area.Altezza)*tavolo.Rows);
            if(x<0||y<0||x1>tavolo.Cols||y1>tavolo.Rows||x1-x<4||y1-y<4)return false;
            using(var qr=new Mat(tavolo,new Rect(x,y,x1-x,y1-y)))
            {
                Scalar media=Cv2.Mean(qr);
                double luminanza=qr.Channels()==1?media.Val0:(media.Val0*29+media.Val1*150+media.Val2*77)/256.0;
                // QR illeggibile o occluso non equivale a tavolo vuoto: il box rimosso
                // deve essere tornato vicino alla luminosita' di fondo della scena.
                return luminanza>=livelloSfondo*parametriVariazioni.FrazioneQrVicinoAlFondo;
            }
        }

        private static bool StessaPosizioneQr(CartoncinoRilevato a,CartoncinoRilevato b)
        {
            var x=a.AreaQr??a.Area;var y=b.AreaQr??b.Area;
            return GeometriaRegioni.StessaPosizioneQr(x,y);
        }

        private static IReadOnlyList<CartoncinoRilevato> UnisciCatalogoParziale(
            IReadOnlyList<CartoncinoRilevato> noti,IReadOnlyList<CartoncinoRilevato> letti)
        {
            var risultato=(noti??new CartoncinoRilevato[0]).Select(GeometriaRegioni.Copia).ToList();
            var associati=new HashSet<int>();
            foreach(var letto in letti??new CartoncinoRilevato[0])
            {
                int migliore=-1;double distanza=double.MaxValue;
                for(int i=0;i<risultato.Count;i++)
                {
                    if(associati.Contains(i))continue;
                    var a=risultato[i].AreaQr??risultato[i].Area;var b=letto.AreaQr??letto.Area;
                    if(!GeometriaRegioni.StessaPosizioneQr(a,b))continue;
                    double dx=a.X+a.Larghezza/2-b.X-b.Larghezza/2;
                    double dy=a.Y+a.Altezza/2-b.Y-b.Altezza/2;
                    double d=Math.Sqrt(dx*dx+dy*dy);
                    if(d<distanza){migliore=i;distanza=d;}
                }
                if(migliore>=0){risultato[migliore]=GeometriaRegioni.Copia(letto);associati.Add(migliore);}
                else{risultato.Add(GeometriaRegioni.Copia(letto));associati.Add(risultato.Count-1);}
            }
            return risultato.AsReadOnly();
        }
        public void Dispose()=>vita.Dispose();
    }
}
