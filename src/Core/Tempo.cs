using System;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>Conversioni e limiti del tempo in BPM.</summary>
    public static class Tempo
    {
        /// <summary>BPM minimo ammesso.</summary>
        public const double MinBpm = 30;

        /// <summary>BPM massimo ammesso.</summary>
        public const double MaxBpm = 300;

        /// <summary>BPM di default, usato quando il valore in ingresso non è finito.</summary>
        private const double BpmDefault = 120;

        /// <summary>
        /// Limita il BPM all'intervallo [<see cref="MinBpm"/>, <see cref="MaxBpm"/>].
        /// Se il valore è NaN o infinito restituisce il default di 120 BPM.
        /// </summary>
        /// <param name="bpm">BPM da limitare.</param>
        /// <returns>BPM compreso nell'intervallo ammesso.</returns>
        public static double Limita(double bpm)
        {
            if (double.IsNaN(bpm) || double.IsInfinity(bpm))
            {
                return BpmDefault;
            }

            return Math.Min(MaxBpm, Math.Max(MinBpm, bpm));
        }

        /// <summary>Durata in millisecondi di un tick MIDI (24 tick per battuta di quarto) al BPM indicato.</summary>
        /// <param name="bpm">BPM, limitato con <see cref="Limita"/> prima del calcolo.</param>
        /// <returns>Intervallo tra due tick in millisecondi.</returns>
        public static double IntervalloTickMs(double bpm)
        {
            return 60000 / (Limita(bpm) * 24);
        }
    }
}
