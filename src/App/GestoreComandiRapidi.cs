using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Corsia ottica indipendente dalle note. Due frame confermano l'azione; il tempo del primo frame alimenta Tap.</summary>
    public sealed class GestoreComandiRapidi : IDisposable
    {
        private readonly RegistroFotogrammi immagini;
        private readonly Func<Calibrazione> calibrazione;
        private readonly ConfigGriglia config;
        private readonly LayoutTavolo layout;
        private readonly EsecutoreMidi esecutore;
        private readonly RegistroCatture registro;
        private readonly Func<long> orologio;
        private readonly ParametriRilevatoreVariazioni parametriVariazioni;
        private readonly InterpreteTavolo interprete;
        private readonly StabilizzatoreTavolo stabilizzatore=new StabilizzatoreTavolo(2,2);
        private readonly RilascioDifferito vita;
        private RilevatoreVariazioni differenze;
        private Calibrazione corrente;
        private LettoreZone lettore;
        private IReadOnlyList<CartoncinoRilevato> catalogo=new CartoncinoRilevato[0];
        private long ultimoFrame,video,ultimaOsservazione=-1000,letture,istanteCandidato;
        private long? istanteFramePrecedente;
        private int conferme,forzaRilettura;
        private string candidata,ultimoEsito,firmaAssegnazioni;
        public GestoreComandiRapidi(RegistroFotogrammi immagini,Func<Calibrazione> calibrazione,ConfigGriglia config,
            LayoutTavolo layout,EsecutoreMidi esecutore,RegistroCatture registro,Func<long> orologio,
            ParametriRilevatoreVariazioni parametriVariazioni=null)
        {
            this.immagini=immagini;this.calibrazione=calibrazione;this.config=config;this.layout=layout;
            this.esecutore=esecutore;this.registro=registro;this.orologio=orologio;
            this.parametriVariazioni=(parametriVariazioni??new ParametriRilevatoreVariazioni()).Normalizzati();
            differenze=new RilevatoreVariazioni(this.parametriVariazioni);
            interprete=new InterpreteTavolo(new Griglia(config),layout);vita=new RilascioDifferito(()=>differenze.Dispose());
        }
        public long NumeroLetture=>Volatile.Read(ref letture);
        public string UltimoEsito=>Volatile.Read(ref ultimoEsito);
        public Func<bool> Sospesa {get;set;}=()=>false;

        /// <summary>Notifica i canali/strumenti letti nella fascia destra, solo a sistema fermo.</summary>
        public Action<IReadOnlyList<AssegnazioneRiga>> AssegnazioniRilevate { get; set; }
        public void RichiediRilettura()
        {
            Interlocked.Exchange(ref forzaRilettura,1);
            Volatile.Write(ref ultimoEsito,"Rilettura corsia comandi in attesa del prossimo frame");
        }
        public bool Passo(bool forza=false)
        {
            if(!vita.ProvaEntra())return false;
            try{return PassoInterno(forza);}finally{vita.Esci();}
        }
        private bool PassoInterno(bool forza)
        {
            if(Sospesa())return false;
            bool rileggi=Volatile.Read(ref forzaRilettura)!=0;
            long ora=orologio();if(!forza&&!rileggi&&ora-ultimaOsservazione<100)return false;
            using(var f=immagini.Copia())
            {
                if(f==null||f.Id==ultimoFrame)return false;
                var c=calibrazione();if(c==null||c.VerificaRisoluzione(f.Immagine.Cols,f.Immagine.Rows)==EsitoRisoluzione.Diversa)return false;
                if(rileggi)rileggi=Interlocked.Exchange(ref forzaRilettura,0)!=0;
                if(!ReferenceEquals(c,corrente)||video!=f.Generazione)
                {
                    corrente=c;video=f.Generazione;differenze.Dispose();differenze=new RilevatoreVariazioni(parametriVariazioni);
                    catalogo=new CartoncinoRilevato[0];lettore=new LettoreZone(c,config);candidata=null;conferme=0;istanteFramePrecedente=null;
                }
                if(rileggi)
                {
                    differenze.Dispose();differenze=new RilevatoreVariazioni(parametriVariazioni);
                    catalogo=new CartoncinoRilevato[0];candidata=null;conferme=0;istanteFramePrecedente=null;
                    stabilizzatore.InterrompiConfermeComandi();
                }
                ultimoFrame=f.Id;ultimaOsservazione=ora;
                var a=layout.Comandi;
                Rect area=new Rect((int)(a.X*640),(int)(a.Y*360),(int)(a.Larghezza*640),(int)(a.Altezza*360))&new Rect(0,0,640,360);
                if(area.Width<2||area.Height<2)return false;
                using(var zona=c.RaddrizzaZona(f.Immagine,640,360,area))
                {
                    var v=differenze.ConfrontaStabile(zona);
                    if(!v.Affidabile)
                    {candidata=null;conferme=0;istanteFramePrecedente=null;stabilizzatore.InterrompiConfermeComandi();return true;}
                    // La maschera stabile conserva firme del frame precedente: il loro onset è quel frame.
                    long inizioVariazione=istanteFramePrecedente??f.IstanteMs;
                    istanteFramePrecedente=f.IstanteMs;
                    if(v.Regioni.Count==0)
                    {
                        // Nessuna differenza stabile: interrompe le presenze candidate senza nuova decodifica.
                        candidata=null;conferme=0;
                        stabilizzatore.Aggiorna(interprete.Interpreta(catalogo));
                        return true;
                    }
                    var cattura=registro.IniziaCattura();var traccia=cattura?.Traccia;
                    try
                    {
                        if(traccia!=null)traccia.Layout=layout;
                        var lavoro=catalogo;
                        // Molte variazioni locali (per esempio flicker o moiré della webcam)
                        // possono produrre decine di ROI che tagliano il QR in moduli separati.
                        // La corsia comandi è piccola: una lettura unica conserva il codice intero
                        // e limita il lavoro del decoder a un solo passaggio per frame.
                        var letta=lettore.Leggi(f.Immagine,a,lavoro,traccia,"corsia-comandi");
                        Interlocked.Increment(ref letture);
                        if(vita.InChiusura||Sospesa())return true;
                        if(!ReferenceEquals(calibrazione(),c)||immagini.GenerazioneCorrente!=f.Generazione)
                        {candidata=null;conferme=0;stabilizzatore.InterrompiConfermeComandi();Volatile.Write(ref ultimoEsito,"Frame cambiato durante la lettura dei comandi");return true;}
                        if(!letta.Completa)
                        {
                            // Un QR non leggibile resta una modifica visiva valida: memorizzala,
                            // così la scena ferma non rilancia la scansione completa ogni 100 ms.
                            differenze.Conferma(v.Regioni);
                            candidata=null;conferme=0;stabilizzatore.InterrompiConfermeComandi();
                            Volatile.Write(ref ultimoEsito,$"Corsia comandi incompleta ({letta.QrDecodificati} QR); attendo una variazione o Rileggi foglio");
                            if(traccia!=null)
                            {
                                traccia.EsitoVisione="CorsiaComandiIncompleta";
                                traccia.Visione.Add(new EventoDiagnosticoCartoncino("Elaborazione","LetturaSospesaPerScenaStabile",
                                    "LetturaSospesaPerScenaStabile","QR non completamente leggibili; la scena è memorizzata e sarà riletta dopo una variazione o una richiesta manuale."));
                            }
                            return true;
                        }
                        lavoro=letta.Catalogo
                            .Where(n=>layout.ZonaDelCentro(n.AreaQr??n.Area)==ZonaSpeciale.Comandi).ToArray();
                        var grezzo=interprete.Interpreta(lavoro,traccia);
                        string firma=string.Join(";",grezzo.Comandi.Select(k=>k.Tipo+":"+k.Parametro).OrderBy(k=>k));
                        if(firma!=candidata){candidata=firma;conferme=1;istanteCandidato=inizioVariazione;}else conferme=Math.Min(2,conferme+1);
                        var stabile=stabilizzatore.Aggiorna(grezzo,traccia);
                        if(conferme>=2){catalogo=lavoro;differenze.Conferma(v.Regioni);}
                        if(conferme>=2)AggiornaAssegnazioni(stabile);
                        esecutore.InviaComandi(stabile.Comandi,istanteCandidato);
                        Volatile.Write(ref ultimoEsito,"Comandi letti: "+stabile.Comandi.Count);
                        if(traccia!=null)
                        {
                            traccia.EsitoVisione="CorsiaComandi";
                            traccia.Visione.Add(new EventoDiagnosticoCartoncino("Osservazione","CorsiaComandi","CorsiaComandi",
                                $"Frame {f.Id}; timestamp iniziale={istanteCandidato}ms; comandi emessi={stabile.Comandi.Count}."));
                        }
                    }
                    catch(Exception e){Volatile.Write(ref ultimoEsito,e.Message);if(traccia!=null)traccia.Errore=e.GetType().Name+": "+e.Message;}
                    finally{registro.CompletaCattura(cattura);}
                }
                return true;
            }
        }
        public void Dispose()=>vita.Dispose();

        private void AggiornaAssegnazioni(StatoTavolo stato)
        {
            // Solo a sistema fermo: durante Play i cartoncini restano visibili ma non cambiano nulla.
            if(esecutore.InEsecuzione||stato?.AssegnazioniRiga==null||stato.AssegnazioniRiga.Count==0)
            {
                firmaAssegnazioni=null;
                return;
            }

            string firma=string.Join(";",stato.AssegnazioniRiga
                .Select(a=>a.Riga+":"+a.Canale+":"+(a.Strumento??""))
                .OrderBy(x=>x));
            if(firma==firmaAssegnazioni)return;
            firmaAssegnazioni=firma;
            AssegnazioniRilevate?.Invoke(stato.AssegnazioniRiga);
        }
    }
}
