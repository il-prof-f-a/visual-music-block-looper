using System;
using System.Collections.Generic;
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
        public void Rileggi()=>Interlocked.Exchange(ref forzaRilettura,1);
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
                if(f==null||f.Id==ultimoFrame)return false;
                var c=calibrazione();
                if(c==null){Volatile.Write(ref ultimoEsito,"Calibrazione assente");return false;}
                if(c.VerificaRisoluzione(f.Immagine.Cols,f.Immagine.Rows)==EsitoRisoluzione.Diversa)
                {Volatile.Write(ref ultimoEsito,"Risoluzione diversa dalla calibrazione");return false;}
                if(!ReferenceEquals(c,corrente)||f.Generazione!=generazioneVideo)
                {
                    corrente=c;generazioneVideo=f.Generazione;generazione++;
                    preparatore.ImpostaGenerazione(generazione);catalogo=new CartoncinoRilevato[0];inizializzato=false;
                    RiavviaConfronto();lettore=new LettoreZone(c,config);
                }
                if(Interlocked.Exchange(ref forzaRilettura,0)!=0){RiavviaConfronto();preparatore.Riprova();}
                if(normale||manuale)tentativoBurst=0;
                ultimoFrame=f.Id;ultimoSegnale=segnale;ultimaOsservazione=ora;retryAt=long.MaxValue;
                Interlocked.Increment(ref fotogrammi);
                CatturaInCorso cattura=registro.IniziaCattura();var traccia=cattura?.Traccia;
                try
                {
                    if(traccia!=null){traccia.Layout=layout;traccia.CalibrazionePresente=true;traccia.LarghezzaFrame=f.Immagine.Cols;traccia.AltezzaFrame=f.Immagine.Rows;}
                    using(var tavolo=c.Raddrizza(f.Immagine,640,360))
                    {
                        // La zona comandi ha un osservatore indipendente.
                        Rect cmd=new Rect((int)(layout.Comandi.X*640),(int)(layout.Comandi.Y*360),
                            (int)(layout.Comandi.Larghezza*640),(int)(layout.Comandi.Altezza*360))&new Rect(0,0,640,360);
                        if(cmd.Width>0&&cmd.Height>0)Cv2.Rectangle(tavolo,cmd,Scalar.White,-1);
                        var variazioni=differenze.Confronta(tavolo);
                        Volatile.Write(ref ultimeRegioni,variazioni.Regioni);
                        if(!variazioni.Affidabile){Descrivi(traccia,"ImmagineNonAffidabile",f);return true;}
                        if(variazioni.Regioni.Count==0){Descrivi(traccia,"FoglioInvariato",f);return true;}
                        var lavoro=catalogo;var risolte=new List<RettangoloTavolo>();bool completa=true;
                        foreach(var z in variazioni.Regioni)
                        {
                            double destra=Math.Min(z.X+z.Larghezza,layout.Comandi.X);
                            if(destra<=z.X)continue;
                            var zona=new RettangoloTavolo(z.X,z.Y,destra-z.X,z.Altezza);
                            var r=lettore.Leggi(f.Immagine,zona,lavoro,traccia);Interlocked.Increment(ref letture);
                            if(vita.InChiusura||Sospesa())return true;
                            if(r.Completa)
                            {lavoro=r.Catalogo.Where(n=>layout.ZonaDelCentro(n.AreaQr??n.Area)!=ZonaSpeciale.Comandi).ToArray();risolte.Add(z);}
                            else completa=false;
                        }
                        if(!ReferenceEquals(calibrazione(),c)||immagini.GenerazioneCorrente!=f.Generazione)
                        {Descrivi(traccia,"LetturaObsoleta",f);return true;}
                        // Un decoder può aver oltrepassato una scadenza: congela prima di fondere il nuovo contenuto.
                        preparatore.ControllaScadenza();
                        var stato=interprete.Interpreta(lavoro,traccia);
                        string firma=Firma(stato);
                        if(firma!=firmaCandidata){firmaCandidata=firma;conferme=1;}else conferme=Math.Min(2,conferme+1);
                        if(conferme>=2&&(inizializzato||completa))
                        {
                            catalogo=lavoro;inizializzato=true;Volatile.Write(ref ultimoStato,stato);
                            preparatore.Aggiorna(stato,true,generazione,f.IstanteMs);
                            differenze.Conferma(risolte);
                            Descrivi(traccia,completa?"FoglioConfermato":"ZoneDaRileggere",f);
                        }
                        else Descrivi(traccia,completa?"InAttesaDiConferma":"ZoneDaRileggere",f);
                        if(tentativoBurst==0&&(conferme<2||!completa)){tentativoBurst=1;retryAt=orologio()+80;}
                    }
                }
                catch(Exception e){Volatile.Write(ref ultimoEsito,"Analisi non applicata: "+e.Message);if(traccia!=null){traccia.Errore=e.GetType().Name+": "+e.Message;traccia.EsitoVisione="ErroreAnalisi";}}
                finally{registro.CompletaCattura(cattura);}
                return true;
            }
        }
        private void Descrivi(TracciaCartoncini t,string esito,FotogrammaAcquisito f)
        {
            Volatile.Write(ref ultimoEsito,esito);
            if(t==null)return;
            var s=UltimoStato;t.EsitoVisione=esito;t.NoteStabili=s.Note.Count;t.StrutturaStabile=s.Struttura;
            t.EsitoStabilizzazione=esito;t.ConfermeRichieste=2;t.ConfermeStruttura=conferme;
            t.Visione.Add(new EventoDiagnosticoCartoncino("Osservazione",esito,"VersioniFoglio",
                $"Frame {f.Id} a {f.IstanteMs}ms; osservata={preparatore.Foglio.Ultima?.Versione??0}, pronta={preparatore.Esecutore.Stato.VersionePronta}, attiva={preparatore.Esecutore.Stato.VersioneAttiva}; letture ROI={NumeroLettureZone}."));
        }
        private void RiavviaConfronto(){differenze.Dispose();differenze=new RilevatoreVariazioni();firmaCandidata=null;conferme=0;}
        internal static string Firma(StatoTavolo s)=>s.Struttura.BattitiPerMisura+":"+s.Struttura.RigheCollegate+":"+s.Struttura.AInizio+":"+s.Struttura.AFine+"|"+
            string.Join(";",s.Note.Select(n=>n.Chiave+":"+n.DurataStep).OrderBy(x=>x));
        public void Dispose()=>vita.Dispose();
    }
}
