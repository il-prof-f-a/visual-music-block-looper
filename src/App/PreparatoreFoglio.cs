using System;
using System.Threading;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Preparazione seriale in background. Controllare la scadenza prima di fondere una lettura appena terminata.</summary>
    public sealed class PreparatoreFoglio
    {
        private readonly EsecutoreMidi esecutore;
        private long generazione = 1, ultimoSegnale, ultimaTentata, numeroCompilazioni;
        private string ultimoErrore;
        public PreparatoreFoglio(EsecutoreMidi esecutore) { this.esecutore = esecutore ?? throw new ArgumentNullException(nameof(esecutore)); }
        public FoglioOsservato Foglio { get; } = new FoglioOsservato();
        public EsecutoreMidi Esecutore=>esecutore;
        public long NumeroCompilazioni => Volatile.Read(ref numeroCompilazioni);
        public string UltimoErrore => Volatile.Read(ref ultimoErrore);
        public long Generazione => Volatile.Read(ref generazione);
        public void ImpostaGenerazione(long nuova)
        {
            esecutore.Stato.ImpostaGenerazione(nuova); Volatile.Write(ref generazione, nuova);
            ultimaTentata = 0; ultimoSegnale = esecutore.Stato.SegnalePreparazione;
        }
        public bool Aggiorna(StatoTavolo stato, bool confermata, long generazione = 1, long istante = 0)
        {
            ControllaScadenza();
            if (generazione != Generazione) return false;
            bool cambiato = Foglio.Aggiorna(stato, confermata, generazione, istante);
            if (!esecutore.InEsecuzione) PreparaUltima(0);
            return cambiato;
        }
        public void ControllaScadenza()
        {
            long segnale = esecutore.Stato.SegnalePreparazione;
            if (!esecutore.InEsecuzione) { ultimoSegnale = segnale; PreparaUltima(0); return; }
            if (segnale == ultimoSegnale) return;
            ultimoSegnale = segnale;
            PreparaUltima(esecutore.Stato.Giro + 1);
        }
        public void Riprova()
        {
            var f=Foglio.Ultima;
            if(UltimoErrore!=null&&f!=null)Foglio.Aggiorna(f.CopiaStato(),true,f.Generazione,f.Istante,true);
            ultimaTentata = 0;
        }
        public bool PreparaAvvio(StatoTavolo stato,long generazione,long istante,Action primoGruppo=null)
        {
            if(generazione!=Generazione)return false;
            Foglio.Aggiorna(stato,true,generazione,istante,UltimoErrore!=null);var f=Foglio.Ultima;
            if(esecutore.InEsecuzione){PreparaUltima(esecutore.Stato.Giro+1);return UltimoErrore==null;}
            ultimaTentata=f.Versione;
            try
            {
                bool pubblicata=false;
                esecutore.PreparaProgressiva(f.CopiaStato(),f.Versione,f.Generazione,0,p=>{pubblicata=esecutore.Pubblica(p);if(pubblicata)primoGruppo?.Invoke();});
                if(!pubblicata||generazione!=Generazione)return false;
                Interlocked.Increment(ref numeroCompilazioni);Volatile.Write(ref ultimoErrore,null);
                return true;
            }
            catch(Exception e)
            {
                if(generazione!=Generazione)return false;
                Volatile.Write(ref ultimoErrore,"Avvio non preparato: "+e.Message);esecutore.RifiutaAvvio(UltimoErrore);return false;
            }
        }
        private void PreparaUltima(long frontiera)
        {
            var f = Foglio.Ultima;
            if (f == null || f.Generazione != Generazione || f.Versione == ultimaTentata) return;
            ultimaTentata = f.Versione;
            try
            {
                var p = esecutore.Prepara(f.CopiaStato(), f.Versione, f.Generazione, frontiera);
                Interlocked.Increment(ref numeroCompilazioni);
                esecutore.Pubblica(p); Volatile.Write(ref ultimoErrore, null);
            }
            catch (Exception e) { Volatile.Write(ref ultimoErrore, "Partitura non preparata: " + e.Message); }
        }
    }
}
