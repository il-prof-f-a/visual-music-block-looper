using System;
using System.Threading;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>Uno slot versionato. La pubblicazione non aspetta il lettore e non accumula candidati obsoleti.</summary>
    public sealed class ScambioPartiture
    {
        private PartituraMidiPreparata ultima;
        private long generazione = 1;
        public long Generazione => Volatile.Read(ref generazione);
        public PartituraMidiPreparata Ultima => Volatile.Read(ref ultima);
        public bool Pubblica(PartituraMidiPreparata nuova)
        {
            if (nuova == null) throw new ArgumentNullException(nameof(nuova));
            while (nuova.Dati.Generazione == Generazione)
            {
                var prima = Ultima;
                if (prima != null && prima.Dati.Generazione == nuova.Dati.Generazione && prima.Dati.VersioneFoglio >= nuova.Dati.VersioneFoglio) return false;
                if (Interlocked.CompareExchange(ref ultima, nuova, prima) == prima) return nuova.Dati.Generazione == Generazione;
            }
            return false;
        }
        public void ImpostaGenerazione(long nuova)
        {
            if (nuova < Generazione) throw new ArgumentOutOfRangeException(nameof(nuova));
            Interlocked.Exchange(ref generazione, nuova);
            while (true)
            {
                var prima = Ultima;
                if (prima == null || prima.Dati.Generazione == nuova) return;
                if (Interlocked.CompareExchange(ref ultima, null, prima) == prima) return;
            }
        }
        public PartituraMidiPreparata Leggi(long frontiera)
        {
            var p = Ultima;
            return p != null && p.Dati.Generazione == Generazione && p.Dati.PrimaFrontiera <= frontiera ? p : null;
        }
    }
}
