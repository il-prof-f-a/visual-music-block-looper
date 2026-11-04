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
        private readonly InterpreteTavolo interprete;
        private readonly RilascioDifferito vita;
        private RilevatoreVariazioni differenze=new RilevatoreVariazioni();
        private Calibrazione corrente;
        private LettoreZone lettore;
        private IReadOnlyList<CartoncinoRilevato> catalogo=new CartoncinoRilevato[0];
        private StatoTavolo ultimoStato=new StatoTavolo(null,null,null);
        private IReadOnlyList<RettangoloTavolo> ultimeRegioni=new RettangoloTavolo[0];
        private long generazione, generazioneVideo, ultimoFrame, ultimoSegnale=-1, ultimaOsservazione=-1000, letture, fotogrammi, retryAt=long.MaxValue;
        private string firmaCandidata, ultimoEsito="Preparazione del foglio";
        private int conferme, tentativoBurst, forzaRilettura;
        private bool inizializzato;
        private IReadOnlyList<RettangoloTavolo> regioniDaVerificare=new RettangoloTavolo[0];
        private double primaPubblicazioneMs;
        public double PrimaPubblicazioneMs=>Volatile.Read(ref primaPubblicazioneMs);
        public AnalizzatoreFoglio(RegistroFotogrammi immagini,Func<Calibrazione> calibrazione,ConfigGriglia config,
            LayoutTavolo layout,PreparatoreFoglio preparatore,RegistroCatture registro,Func<long> orologio)
        {
            this.immagini=immagini;this.calibrazione=calibrazione;this.config=config;this.layout=layout;
            this.preparatore=preparatore;this.registro=registro;this.orologio=orologio;
            interprete=new InterpreteTavolo(new Griglia(config),layout);
            vita=new RilascioDifferito(()=>differenze.Dispose());
        }
        public StatoTavolo UltimoStato=>Volatile.Read(ref ultimoStato);
        public IReadOnlyList<RettangoloTavolo> UltimeRegioni=>Volatile.Read(ref ultimeRegioni);
        public long NumeroLettureZone=>Volatile.Read(ref letture);
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
                    RiavviaConfronto();lettore=new LettoreZone(c,config);
                }
                if(Interlocked.Exchange(ref forzaRilettura,0)!=0)
                {
                    // La richiesta manuale deve ripartire da una nuova istantanea: il confronto
                    // incrementale può rifiutare il frame dopo una variazione globale di esposizione.
                    RiavviaConfronto();catalogo=new CartoncinoRilevato[0];inizializzato=false;
                    firmaCandidata=null;conferme=0;regioniDaVerificare=new RettangoloTavolo[0];
                    Volatile.Write(ref ultimeRegioni,regioniDaVerificare);
                    Volatile.Write(ref ultimoStato,new StatoTavolo(null,null,null));
                    Volatile.Write(ref ultimoEsito,"Rilettura completa in corso");
                }
                if(normale||manuale)tentativoBurst=0;
                ultimoFrame=f.Id;ultimoSegnale=segnale;ultimaOsservazione=ora;retryAt=long.MaxValue;
                Interlocked.Increment(ref fotogrammi);
                CatturaInCorso cattura=registro.IniziaCattura();var traccia=cattura?.Traccia;
                try
                {
                    if(traccia!=null){traccia.Layout=layout;traccia.CalibrazionePresente=true;traccia.LarghezzaFrame=f.Immagine.Cols;traccia.AltezzaFrame=f.Immagine.Rows;}
                    if(!inizializzato)return PrimaIstantanea(f,c,traccia);
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
                $"Frame {f.Id} a {f.IstanteMs}ms; osservata={preparatore.Foglio.Ultima?.Versione??0}, pronta={preparatore.Esecutore.Stato.VersionePronta}, attiva={preparatore.Esecutore.Stato.VersioneAttiva}; letture ROI={NumeroLettureZone}."));
        }
        private void RiavviaConfronto(){differenze.Dispose();differenze=new RilevatoreVariazioni();firmaCandidata=null;conferme=0;}
        internal static string Firma(StatoTavolo s)=>s.Struttura.BattitiPerMisura+":"+s.Struttura.RigheCollegate+":"+s.Struttura.AInizio+":"+s.Struttura.AFine+"|"+
            string.Join(";",s.Note.Select(n=>n.Chiave+":"+n.DurataStep).OrderBy(x=>x));

        private static IReadOnlyList<CartoncinoRilevato> TrovaRimozioniConfermate(RettangoloTavolo zona,
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
                    double limite=Math.Max(area.Larghezza,altro.Larghezza)*.15;
                    double dx=area.X+area.Larghezza/2-altro.X-altro.Larghezza/2;
                    double dy=area.Y+area.Altezza/2-altro.Y-altro.Altezza/2;
                    double d=Math.Sqrt(dx*dx+dy*dy);
                    if(d<limite&&d<distanza){migliore=i;distanza=d;}
                }
                if(migliore>=0)associati.Add(migliore);
                else if(QrSostituitoDaSfondo(tavolo,noto,livelloSfondo))rimossi.Add(GeometriaRegioni.Copia(noto));
            }
            return rimossi.AsReadOnly();
        }

        private static bool QrSostituitoDaSfondo(Mat tavolo,CartoncinoRilevato cartoncino,int livelloSfondo)
        {
            if(tavolo==null||tavolo.Empty()||livelloSfondo<80)return false;
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
                return luminanza>=livelloSfondo*.72;
            }
        }

        private static bool StessaPosizioneQr(CartoncinoRilevato a,CartoncinoRilevato b)
        {
            var x=a.AreaQr??a.Area;var y=b.AreaQr??b.Area;
            double limite=Math.Max(x.Larghezza,y.Larghezza)*.15;
            double dx=x.X+x.Larghezza/2-y.X-y.Larghezza/2;
            double dy=x.Y+x.Altezza/2-y.Y-y.Altezza/2;
            return Math.Sqrt(dx*dx+dy*dy)<limite;
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
                    double limite=Math.Max(a.Larghezza,b.Larghezza)*
                        (string.Equals(risultato[i].Payload,letto.Payload,StringComparison.Ordinal)?1.4:.15);
                    double dx=a.X+a.Larghezza/2-b.X-b.Larghezza/2;
                    double dy=a.Y+a.Altezza/2-b.Y-b.Altezza/2;
                    double d=Math.Sqrt(dx*dx+dy*dy);
                    if(d<limite&&d<distanza){migliore=i;distanza=d;}
                }
                if(migliore>=0){risultato[migliore]=GeometriaRegioni.Copia(letto);associati.Add(migliore);}
                else{risultato.Add(GeometriaRegioni.Copia(letto));associati.Add(risultato.Count-1);}
            }
            return risultato.AsReadOnly();
        }
        public void Dispose()=>vita.Dispose();
    }
}
