using System;
using System.Linq;
using System.Threading;

namespace VisualMusicBlockLooper.Core
{
    public sealed class IstantaneaFoglio
    {
        private readonly StatoTavolo stato;
        internal IstantaneaFoglio(StatoTavolo origine, long versione, long generazione, long istante)
        { stato = Copia(origine); Versione = versione; Generazione = generazione; Istante = istante; }
        public long Versione { get; }
        public long Generazione { get; }
        public long Istante { get; }
        public StatoTavolo CopiaStato() => Copia(stato);
        internal bool StessoContenuto(StatoTavolo altro)
        {
            if (!stato.Struttura.Equals(altro.Struttura) || stato.Note.Count != altro.Note.Count) return false;
            return stato.Note.Select(Chiave).OrderBy(x => x).SequenceEqual(altro.Note.Select(Chiave).OrderBy(x => x));
        }
        private static string Chiave(CartoncinoNota n) => n == null ? "null" : n.Chiave + ":" + n.DurataStep + ":" + (int)n.Colore;
        private static StatoTavolo Copia(StatoTavolo s) => new StatoTavolo(
            s.Note.Where(n => n != null).Select(PartituraMidi.CopiaNota), null,
            s.Errori.Where(e => e != null).Select(e => new ErroreTavolo { Tipo = e.Tipo, Area = e.Area }), s.Struttura);
    }

    /// <summary>Un produttore di osservazioni confermate; istantanee immutabili leggibili dalla UI.</summary>
    public sealed class FoglioOsservato
    {
        private IstantaneaFoglio ultima;
        private long versione;
        public IstantaneaFoglio Ultima => Volatile.Read(ref ultima);
        public bool Aggiorna(StatoTavolo stato, bool confermata, long generazione, long istante)
        {
            if (!confermata || stato == null) return false;
            var prima = Ultima;
            if (prima != null && prima.Generazione == generazione && prima.StessoContenuto(stato)) return false;
            Volatile.Write(ref ultima, new IstantaneaFoglio(stato, ++versione, generazione, istante));
            return true;
        }
    }
}
