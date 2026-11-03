using System;
using System.Collections.Generic;
using System.Threading;

namespace VisualMusicBlockLooper.App
{
    public readonly struct CampioneTempoMidi
    {
        public CampioneTempoMidi(long tick, long previsto, long inizioInvio, long fineInvio)
        { Tick = tick; Previsto = previsto; InizioInvio = inizioInvio; FineInvio = fineInvio; }
        public long Tick { get; }
        public long Previsto { get; }
        public long InizioInvio { get; }
        public long FineInvio { get; }
    }

    /// <summary>Ring a singolo produttore MIDI. Il lettore scarta uno slot sovrascritto, senza fermare l'invio.</summary>
    public sealed class RegistroTempiMidi
    {
        private sealed class Slot
        {
            public long Sequenza;
            public CampioneTempoMidi Campione;
        }
        private readonly Slot[] slot;
        private long totale;
        public RegistroTempiMidi(int capacita = 8192)
        {
            if (capacita < 1) throw new ArgumentOutOfRangeException(nameof(capacita));
            slot = new Slot[capacita];
            for (int i = 0; i < capacita; i++) slot[i] = new Slot();
        }
        public long TotaleCampioni => Volatile.Read(ref totale);
        public void Registra(long tick, long previsto, long inizioInvio, long fineInvio)
        {
            long sequenza = totale + 1;
            Slot s = slot[(int)((sequenza - 1) % slot.Length)];
            Interlocked.Exchange(ref s.Sequenza, 0);
            s.Campione = new CampioneTempoMidi(tick, previsto, inizioInvio, fineInvio);
            Volatile.Write(ref s.Sequenza, sequenza);
            Volatile.Write(ref totale, sequenza);
        }
        public IReadOnlyList<CampioneTempoMidi> Istantanea()
        {
            long fine = TotaleCampioni;
            var risultato = new List<CampioneTempoMidi>(slot.Length);
            for (long n = Math.Max(1, fine - slot.Length + 1); n <= fine; n++)
            {
                Slot s = slot[(int)((n - 1) % slot.Length)];
                if (Volatile.Read(ref s.Sequenza) != n) continue;
                CampioneTempoMidi campione = s.Campione;
                Thread.MemoryBarrier();
                if (Volatile.Read(ref s.Sequenza) == n) risultato.Add(campione);
            }
            return risultato.AsReadOnly();
        }

        /// <summary>Copia in memoria del lettore, riutilizzabile; non alloca né blocca il produttore.</summary>
        public int CopiaIn(CampioneTempoMidi[] destinazione)
        {
            if(destinazione==null)throw new ArgumentNullException(nameof(destinazione));
            long fine=TotaleCampioni;int copiati=0;
            for(long n=Math.Max(1,fine-Math.Min(slot.Length,destinazione.Length)+1);n<=fine;n++)
            {
                Slot s=slot[(int)((n-1)%slot.Length)];
                if(Volatile.Read(ref s.Sequenza)!=n)continue;
                var c=s.Campione;Thread.MemoryBarrier();
                if(Volatile.Read(ref s.Sequenza)==n)destinazione[copiati++]=c;
            }
            return copiati;
        }
    }
}
