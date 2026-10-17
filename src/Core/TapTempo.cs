using System.Collections.Generic;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>
    /// Stima il BPM dai tap dell'utente: media degli ultimi intervalli tra tap consecutivi.
    /// Una pausa superiore a 2000 ms, o un timestamp non crescente, riparte da zero.
    /// </summary>
    public class TapTempo
    {
        /// <summary>Intervallo massimo tra due tap perché restino nella stessa misura.</summary>
        private const long PausaMassimaMs = 2000;

        /// <summary>Numero massimo di intervalli usati per la media.</summary>
        private const int IntervalliMassimi = 4;

        private readonly Queue<long> intervalli = new Queue<long>();
        private long? ultimoTap;

        /// <summary>
        /// Registra un tap e restituisce il BPM stimato, oppure <c>null</c> se non è ancora calcolabile
        /// (primo tap, oppure primo tap dopo una pausa o un timestamp non valido).
        /// </summary>
        /// <param name="millisecondi">Istante del tap in millisecondi.</param>
        /// <returns>BPM limitato con <see cref="Tempo.Limita"/>, oppure <c>null</c>.</returns>
        public double? Tap(long millisecondi)
        {
            if (ultimoTap == null)
            {
                ultimoTap = millisecondi;
                return null;
            }

            long intervallo = millisecondi - ultimoTap.Value;
            ultimoTap = millisecondi;

            if (intervallo > PausaMassimaMs || intervallo <= 0)
            {
                Azzera();
                ultimoTap = millisecondi;
                return null;
            }

            intervalli.Enqueue(intervallo);
            if (intervalli.Count > IntervalliMassimi)
            {
                intervalli.Dequeue();
            }

            double media = 0;
            foreach (long valore in intervalli)
            {
                media += valore;
            }

            media /= intervalli.Count;
            return Tempo.Limita(60000 / media);
        }

        /// <summary>Cancella la storia dei tap: il prossimo tap sarà il primo.</summary>
        public void Azzera()
        {
            intervalli.Clear();
            ultimoTap = null;
        }
    }
}
