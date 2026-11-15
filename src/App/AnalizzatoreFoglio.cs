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
    public sealed class AnalizzatoreFoglio : IDisposable
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
        private readonly int misureAnticipoScansione;
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
            public int Numero;
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
            public ChiaveBloccoPianificato(long giro,int misura,int id){Giro=giro;Misura=misura;Id=id;}
            public long Giro { get; }
            public int Misura { get; }
            public int Id { get; }
            public bool Equals(ChiaveBloccoPianificato altra)=>Giro==altra.Giro&&Misura==altra.Misura&&Id==altra.Id;
            public override bool Equals(object obj)=>obj is ChiaveBloccoPianificato altra&&Equals(altra);
            public override int GetHashCode(){unchecked{return ((Giro.GetHashCode()*397)^Misura)*397^Id;}}
        }
        public AnalizzatoreFoglio(RegistroFotogrammi immagini,Func<Calibrazione> calibrazione,ConfigGriglia config,
            LayoutTavolo layout,PreparatoreFoglio preparatore,RegistroCatture registro,Func<long> orologio,
            bool analizzaSoloSeCambia=true,ParametriRilevatoreVariazioni parametriVariazioni=null,
            bool scansioneABlocchiAnticipata=false,int misureAnticipoScansione=1)
        {
            this.immagini=immagini;this.calibrazione=calibrazione;this.config=config;this.layout=layout;
            this.preparatore=preparatore;this.registro=registro;this.orologio=orologio;
            this.analizzaSoloSeCambia=analizzaSoloSeCambia;
            this.scansioneABlocchiAnticipata=scansioneABlocchiAnticipata;
            this.misureAnticipoScansione=Math.Max(1,Math.Min(5,misureAnticipoScansione));
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
            long segnale=PulsazioniPerOsservazione>0?motore.TickTempo/(24L*PulsazioniPerOsservazione):motore.SegnaleOsservazione;
            bool manuale=Volatile.Read(ref forzaRilettura)!=0;
            bool normale=motore.InEsecuzione?segnale!=ultimoSegnale:ora-ultimaOsservazione>=250;
            if(!forza&&!manuale&&!normale&&ora<retryAt)return false;
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
                    preparatore.ImpostaGenerazione(generazione);catalogo=new CartoncinoRilevato[0];inizializzato=false;
                    confermeBlocchi.Clear();
                    blocchiScansionati.Clear();blocchiIncompleti.Clear();strutturaScansione=null;esecuzionePrecedente=false;
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
                    firmaCandidata=null;conferme=0;regioniDaVerificare=new RettangoloTavolo[0];
                    Volatile.Write(ref ultimeRegioni,regioniDaVerificare);
                    Volatile.Write(ref ultimoStato,new StatoTavolo(null,null,null));
                    Volatile.Write(ref ultimoEsito,"Rilettura completa in corso");
                }
                if(!analizzaSoloSeCambia&&inizializzato&&f.Id==ultimoFrameScansioneCompleta)return false;
                if(normale||manuale)tentativoBurst=0;
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
                    if(!analizzaSoloSeCambia)
                    {
                        AnalizzaScenaCompleta(f,c,traccia);
                        return true;
                    }
                    if(scansioneABlocchiAnticipata)
                    {
                        AnalizzaBlocchiAnticipati(f,c,traccia,motore.Struttura,motore.Posizione,motore.Giro,motore.InEsecuzione);
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
                $"Frame {f.Id} a {f.IstanteMs}ms; osservata={preparatore.Foglio.Ultima?.Versione??0}, pronta={preparatore.Esecutore.Stato.VersionePronta}, attiva={preparatore.Esecutore.Stato.VersioneAttiva}; letture scena/ROI={NumeroLetture}."));
        }
        private void AnalizzaScenaCompleta(FotogrammaAcquisito f,Calibrazione c,TracciaCartoncini traccia)
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
            var righe=new SortedSet<int>(posizione.RigheAttive??new int[0]);
            long inizio=(struttura.AInizio-1L)*StrutturaTavolo.StepPerBattuta;
            for(int anticipo=1;anticipo<=misure;anticipo++)
            {
                long passo=(long)(posizione.MisuraNelGiro+anticipo)*struttura.StepPerMisura-inizio;
                foreach(int riga in struttura.Posizione(passo).RigheAttive)righe.Add(riga);
            }
            return righe.ToArray();
        }

        private void AnalizzaBlocchiAnticipati(FotogrammaAcquisito f,Calibrazione c,TracciaCartoncini traccia,
            StrutturaTavolo struttura,PosizioneGiro posizione,long giro,bool inEsecuzione)
        {
            var tuttiBlocchi=Enumerable.Range(0,config.Tracce).Select(r=>new BloccoScansione(r,ZonaRiga(r)))
                .Concat(new[]
                {
                    new BloccoScansione(-1,layout.Misura,ZonaSpeciale.Misura),
                    new BloccoScansione(-2,layout.Collegamento,ZonaSpeciale.Collegamento),
                    new BloccoScansione(-3,layout.Ab,ZonaSpeciale.Ab)
                }).Where(b=>b.Zona.Larghezza>0&&b.Zona.Altezza>0).ToDictionary(b=>b.Id);
            var pianificati=new Dictionary<int,BloccoPianificato>();
            void Aggiungi(BloccoScansione blocco,long giroDestinazione,int misuraDestinazione)
            {
                if(blocco==null)return;
                if(!pianificati.TryGetValue(blocco.Id,out var pianificato))
                {pianificato=new BloccoPianificato{Blocco=blocco};pianificati.Add(blocco.Id,pianificato);}
                var chiave=new ChiaveBloccoPianificato(giroDestinazione,misuraDestinazione,blocco.Id);
                if(!pianificato.Chiavi.Contains(chiave))pianificato.Chiavi.Add(chiave);
            }

            if(inEsecuzione)
            {
                if(!esecuzionePrecedente||strutturaScansione==null||!strutturaScansione.Equals(struttura))
                {blocchiScansionati.Clear();confermeBlocchi.Clear();}
                esecuzionePrecedente=true;strutturaScansione=struttura;
                blocchiScansionati.RemoveWhere(k=>k.Giro<giro);

                foreach(int riga in posizione.RigheAttive??new int[0])
                    if(tuttiBlocchi.TryGetValue(riga,out var bloccoAttivo))Aggiungi(bloccoAttivo,giro,posizione.MisuraNelGiro);

                long inizio=(struttura.AInizio-1L)*StrutturaTavolo.StepPerBattuta;
                for(int anticipo=1;anticipo<=misureAnticipoScansione;anticipo++)
                {
                    long passo=(long)(posizione.MisuraNelGiro+anticipo)*struttura.StepPerMisura-inizio;
                    long giriAnticipati=passo/struttura.StepGiro;
                    var futura=struttura.Posizione(passo);
                    foreach(int riga in futura.RigheAttive??new int[0])
                        if(tuttiBlocchi.TryGetValue(riga,out var bloccoFuturo))
                            Aggiungi(bloccoFuturo,giro+giriAnticipati,futura.MisuraNelGiro);
                }

                for(int riga=0;riga<config.Tracce;riga++)
                    if(regioniDaVerificare.Any(z=>GeometriaRegioni.Interseca(z,ZonaRiga(riga)))
                        &&tuttiBlocchi.TryGetValue(riga,out var bloccoDaVerificare))
                        Aggiungi(bloccoDaVerificare,giro,posizione.MisuraNelGiro);

                foreach(int id in new[]{-1,-2,-3})
                    if(tuttiBlocchi.TryGetValue(id,out var vincolo))Aggiungi(vincolo,giro,-1);

                foreach(int id in blocchiIncompleti)
                    if(tuttiBlocchi.TryGetValue(id,out var bloccoIncompleto))
                        Aggiungi(bloccoIncompleto,giro,posizione.MisuraNelGiro);

                foreach(var conferma in confermeBlocchi)
                    if(tuttiBlocchi.TryGetValue(conferma.Key,out var bloccoInConferma))
                        Aggiungi(bloccoInConferma,conferma.Value.Giro,conferma.Value.Misura);
            }
            else
            {
                esecuzionePrecedente=false;
                if(prossimoBloccoFermo>=tuttiBlocchi.Count)prossimoBloccoFermo=0;
                var bloccoFermo=tuttiBlocchi.Values.OrderBy(b=>b.Id>=0?b.Id:config.Tracce-b.Id-1)
                    .ElementAt(prossimoBloccoFermo++);
                Aggiungi(bloccoFermo,giro,posizione.MisuraNelGiro);
            }

            var blocchi=pianificati.Values.Where(p=>!inEsecuzione
                    ||p.Chiavi.Any(k=>!blocchiScansionati.Contains(k))
                    ||confermeBlocchi.ContainsKey(p.Blocco.Id)
                    ||regioniDaVerificare.Any(z=>GeometriaRegioni.Interseca(z,p.Blocco.Zona)))
                .OrderBy(p=>p.Blocco.Id>=0?p.Blocco.Id:config.Tracce-p.Blocco.Id-1).ToArray();
            int blocchiSaltati=inEsecuzione?pianificati.Count-blocchi.Length:0;
            Volatile.Write(ref ultimeRegioni,blocchi.Select(p=>p.Blocco.Zona).ToArray());

            var lavoro=catalogo;
            bool catalogoAggiornato=false;
            int blocchiIncerti=0;
            foreach(var pianificato in blocchi)
            {
                var blocco=pianificato.Blocco;
                var lettura=lettore.Leggi(f.Immagine,blocco.Zona,lavoro,traccia,"blocco:"+blocco.Id);
                Interlocked.Increment(ref letture);
                if(vita.InChiusura||Sospesa())return;
                var correnti=LocaliDelBlocco(lavoro,blocco);
                var letti=LocaliDelBlocco(lettura.Catalogo,blocco);
                string firmaLettura=FirmaCatalogoBlocco(letti);
                string firmaCorrente=FirmaCatalogoBlocco(correnti);
                bool completa=lettura.Completa&&IlluminazioneAffidabile(f.Immagine,lettura.RegioneMaschera);
                if(!completa)
                {
                    blocchiIncerti++;
                    blocchiIncompleti.Add(blocco.Id);
                    confermeBlocchi.Remove(blocco.Id);
                    if(inEsecuzione)blocchiScansionati.ExceptWith(pianificato.Chiavi);
                    var integrati=UnisciCatalogoParziale(correnti,letti);
                    if(FirmaCatalogoBlocco(integrati)!=firmaCorrente)
                    {
                        lavoro=SostituisciBlocco(lavoro,blocco,integrati);
                        catalogoAggiornato=true;
                    }
                    continue;
                }

                blocchiIncompleti.Remove(blocco.Id);
                regioniDaVerificare=regioniDaVerificare.Where(z=>!GeometriaRegioni.Interseca(z,blocco.Zona)).ToArray();
                if(firmaLettura==firmaCorrente)
                {
                    confermeBlocchi.Remove(blocco.Id);
                    if(inEsecuzione)blocchiScansionati.UnionWith(pianificato.Chiavi);
                    continue;
                }

                if(!confermeBlocchi.TryGetValue(blocco.Id,out var candidata)||candidata.Firma!=firmaLettura)
                {
                    var chiave=pianificato.Chiavi[0];
                    candidata=new ConfermaBlocco{Firma=firmaLettura,UltimoFrame=f.Id,Giro=chiave.Giro,
                        Misura=chiave.Misura,Numero=1,Catalogo=letti};
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
                    lavoro=SostituisciBlocco(lavoro,blocco,candidata.Catalogo);
                    catalogoAggiornato=true;
                    confermeBlocchi.Remove(blocco.Id);
                    if(inEsecuzione)blocchiScansionati.UnionWith(pianificato.Chiavi);
                }
                else if(inEsecuzione)blocchiScansionati.ExceptWith(pianificato.Chiavi);
            }

            Volatile.Write(ref ultimeRegioni,blocchi.Select(p=>p.Blocco.Zona).ToArray());
            if(catalogoAggiornato)
            {
                if(!ReferenceEquals(calibrazione(),c)||immagini.GenerazioneCorrente!=f.Generazione)
                {Descrivi(traccia,"LetturaObsoleta",f);return;}
                preparatore.ControllaScadenza();
                catalogo=lavoro;
                var stato=interprete.Interpreta(catalogo,traccia);
                if(Firma(stato)!=Firma(UltimoStato))
                {
                    Volatile.Write(ref ultimoStato,stato);
                    preparatore.Aggiorna(stato,true,generazione,f.IstanteMs);
                }
            }
            conferme=confermeBlocchi.Count==0?0:confermeBlocchi.Values.Max(x=>x.Numero);
            string esito=inEsecuzione?"ScansioneBlocchiAnticipata":"ScansioneBlocchiFermi";
            Descrivi(traccia,esito,f);
            if(traccia!=null)traccia.Visione.Add(new EventoDiagnosticoCartoncino("Osservazione",
                inEsecuzione?"BlocchiAnticipati":"BloccoFermo","RigheMusicali",
                $"Blocchi={string.Join(",",blocchi.Select(p=>p.Blocco.Id>=0?"riga"+p.Blocco.Id:p.Blocco.Speciale.ToString()))}; " +
                $"vincoli MIDI={blocchi.Count(p=>p.Blocco.Id<0)}; anticipati={misureAnticipoScansione} misure; " +
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
                return (n.Payload??"")+":"+Quantizza(qr.X)+":"+Quantizza(qr.Y)+":"+
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
