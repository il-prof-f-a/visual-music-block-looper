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
        private readonly InterpreteTavolo interprete;
        private readonly StabilizzatoreTavolo stabilizzatore=new StabilizzatoreTavolo(2,2);
        private readonly RilascioDifferito vita;
        private RilevatoreVariazioni differenze=new RilevatoreVariazioni();
        private Calibrazione corrente;
        private LettoreZone lettore;
        private IReadOnlyList<CartoncinoRilevato> catalogo=new CartoncinoRilevato[0];
        private long ultimoFrame,video,ultimaOsservazione=-1000,letture,istanteCandidato;
        private int conferme;
        private string candidata,ultimoEsito;
        public GestoreComandiRapidi(RegistroFotogrammi immagini,Func<Calibrazione> calibrazione,ConfigGriglia config,
            LayoutTavolo layout,EsecutoreMidi esecutore,RegistroCatture registro,Func<long> orologio)
        {
            this.immagini=immagini;this.calibrazione=calibrazione;this.config=config;this.layout=layout;
            this.esecutore=esecutore;this.registro=registro;this.orologio=orologio;
            interprete=new InterpreteTavolo(new Griglia(config),layout);vita=new RilascioDifferito(()=>differenze.Dispose());
        }
        public long NumeroLetture=>Volatile.Read(ref letture);
        public string UltimoEsito=>Volatile.Read(ref ultimoEsito);
        public Func<bool> Sospesa {get;set;}=()=>false;
        public bool Passo(bool forza=false)
        {
            if(!vita.ProvaEntra())return false;
            try{return PassoInterno(forza);}finally{vita.Esci();}
        }
        private bool PassoInterno(bool forza)
        {
            if(Sospesa())return false;
            long ora=orologio();if(!forza&&ora-ultimaOsservazione<100)return false;
            using(var f=immagini.Copia())
            {
                if(f==null||f.Id==ultimoFrame)return false;
                var c=calibrazione();if(c==null||c.VerificaRisoluzione(f.Immagine.Cols,f.Immagine.Rows)==EsitoRisoluzione.Diversa)return false;
                if(!ReferenceEquals(c,corrente)||video!=f.Generazione)
                {
                    corrente=c;video=f.Generazione;differenze.Dispose();differenze=new RilevatoreVariazioni();
                    catalogo=new CartoncinoRilevato[0];lettore=new LettoreZone(c,config);candidata=null;conferme=0;
                }
                ultimoFrame=f.Id;ultimaOsservazione=ora;
                using(var tavolo=c.Raddrizza(f.Immagine,640,360))
                {
                    var a=layout.Comandi;
                    Rect area=new Rect((int)(a.X*640),(int)(a.Y*360),(int)(a.Larghezza*640),(int)(a.Altezza*360))&new Rect(0,0,640,360);
                    if(area.Width<2||area.Height<2)return false;
                    using(var zona=new Mat(tavolo,area))
                    {
                        var v=differenze.Confronta(zona);
                        if(!v.Affidabile||v.Regioni.Count==0)return true;
                        var cattura=registro.IniziaCattura();var traccia=cattura?.Traccia;
                        try
                        {
                            if(traccia!=null)traccia.Layout=layout;
                            var lavoro=catalogo;bool completa=true;
                            foreach(var r in v.Regioni)
                            {
                                var globale=new RettangoloTavolo(a.X+r.X*a.Larghezza,a.Y+r.Y*a.Altezza,r.Larghezza*a.Larghezza,r.Altezza*a.Altezza);
                                var letta=lettore.Leggi(f.Immagine,globale,lavoro,traccia);Interlocked.Increment(ref letture);
                                if(vita.InChiusura||Sospesa())return true;
                                if(letta.Completa)lavoro=letta.Catalogo.Where(n=>layout.ZonaDelCentro(n.AreaQr??n.Area)==ZonaSpeciale.Comandi).ToArray();
                                else completa=false;
                            }
                            if(!completa||!ReferenceEquals(calibrazione(),c)||immagini.GenerazioneCorrente!=f.Generazione)
                            {Volatile.Write(ref ultimoEsito,"Comandi da rileggere");return true;}
                            catalogo=lavoro;var grezzo=interprete.Interpreta(catalogo,traccia);
                            string firma=string.Join(";",grezzo.Comandi.Select(k=>k.Tipo+":"+k.Parametro).OrderBy(k=>k));
                            if(firma!=candidata){candidata=firma;conferme=1;istanteCandidato=f.IstanteMs;}else conferme=Math.Min(2,conferme+1);
                            var stabile=stabilizzatore.Aggiorna(grezzo,traccia);
                            if(conferme>=2)differenze.Conferma(v.Regioni);
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
                }
                return true;
            }
        }
        public void Dispose()=>vita.Dispose();
    }
}
