using System;
using System.Globalization;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Limiti e conversioni delle impostazioni di messa a fuoco ed esposizione della webcam (funzioni pure).</summary>
    public static class ImpostazioniCamera
    {
        /// <summary>Valore minimo della messa a fuoco manuale.</summary>
        public const int FuocoMin = 0;

        /// <summary>Valore massimo della messa a fuoco manuale.</summary>
        public const int FuocoMax = 255;

        /// <summary>Passo del regolo di messa a fuoco.</summary>
        public const int FuocoPasso = 5;

        /// <summary>Esposizione minima, in log2 dei secondi (scala DirectShow).</summary>
        public const int EsposizioneMin = -13;

        /// <summary>Esposizione massima, in log2 dei secondi (scala DirectShow).</summary>
        public const int EsposizioneMax = 0;

        /// <summary>Cultura italiana per le etichette, fissata per rendere deterministico il formato.</summary>
        private static readonly CultureInfo Italiano = CultureInfo.GetCultureInfo("it-IT");

        /// <summary>Limita la messa a fuoco all'intervallo consentito.</summary>
        public static int LimitaFuoco(int valore)
        {
            return Math.Min(FuocoMax, Math.Max(FuocoMin, valore));
        }

        /// <summary>Limita l'esposizione (log2 dei secondi) all'intervallo consentito.</summary>
        public static double LimitaEsposizione(double valore)
        {
            return Math.Min(EsposizioneMax, Math.Max(EsposizioneMin, valore));
        }

        /// <summary>
        /// Etichetta del tempo di esposizione in millisecondi (1000 * 2^valore), con cultura it-IT:
        /// senza decimali da 10 ms in su, con un decimale sotto i 10 ms (es. «125 ms», «7,8 ms»).
        /// </summary>
        public static string EtichettaEsposizione(double log2Secondi)
        {
            double millisecondi = 1000.0 * Math.Pow(2, log2Secondi);
            string formato = millisecondi >= 10 ? "0" : "0.0";
            return millisecondi.ToString(formato, Italiano) + " ms";
        }

        /// <summary>Etichetta della messa a fuoco (il valore numerico).</summary>
        public static string EtichettaFuoco(int valore)
        {
            return valore.ToString(CultureInfo.InvariantCulture);
        }
    }
}
