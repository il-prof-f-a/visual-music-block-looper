using System;
using System.Threading;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Coda MPSC preallocata. Il singolo consumatore non aspetta un produttore sospeso.</summary>
    public sealed class CodaLimitata<T> where T : struct
    {
        private sealed class Cella { public long Sequenza; public T Valore; }
        private readonly Cella[] celle;
        private readonly int maschera;
        private long scrittura, lettura;
        public CodaLimitata(int capacita)
        {
            if (capacita < 2 || (capacita & (capacita - 1)) != 0) throw new ArgumentOutOfRangeException(nameof(capacita));
            celle = new Cella[capacita]; maschera = capacita - 1;
            for (int i = 0; i < capacita; i++) celle[i] = new Cella { Sequenza = i };
        }
        public bool ProvaInserisci(T valore)
        {
            while (true)
            {
                long p = Volatile.Read(ref scrittura); Cella c = celle[(int)(p & maschera)];
                long differenza = Volatile.Read(ref c.Sequenza) - p;
                if (differenza < 0) return false;
                if (differenza != 0 || Interlocked.CompareExchange(ref scrittura, p + 1, p) != p) continue;
                c.Valore = valore; Volatile.Write(ref c.Sequenza, p + 1); return true;
            }
        }
        public bool ProvaEstrai(out T valore)
        {
            long p = lettura; Cella c = celle[(int)(p & maschera)];
            if (Volatile.Read(ref c.Sequenza) != p + 1) { valore = default(T); return false; }
            valore = c.Valore; Volatile.Write(ref c.Sequenza, p + celle.Length); lettura = p + 1; return true;
        }
    }
}
