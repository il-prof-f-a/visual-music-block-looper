using System;
using System.Collections.Generic;
using System.Globalization;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Risoluzioni predefinite della webcam, dalla più piccola alla più grande.</summary>
    public static class PresetRisoluzione
    {
        /// <summary>Indice del preset 1280x720, usato come riferimento per valori non validi.</summary>
        private const int IndiceRiferimento = 2;

        /// <summary>Preset disponibili (larghezza, altezza), in ordine crescente di pixel.</summary>
        public static IReadOnlyList<(int Larghezza, int Altezza)> Elenco { get; } = new[]
        {
            (640, 480),
            (800, 600),
            (1280, 720),
            (1600, 900),
            (1920, 1080),
        };

        /// <summary>
        /// Restituisce l'indice del preset con numero di pixel più vicino a quello richiesto.
        /// A parità vince il preset più piccolo; valori non positivi danno l'indice di 1280x720.
        /// </summary>
        public static int IndiceVicino(int larghezza, int altezza)
        {
            if (larghezza <= 0 || altezza <= 0)
            {
                return IndiceRiferimento;
            }

            long pixel = (long)larghezza * altezza;
            int migliore = 0;
            long distanzaMigliore = long.MaxValue;

            for (int i = 0; i < Elenco.Count; i++)
            {
                long pixelPreset = (long)Elenco[i].Larghezza * Elenco[i].Altezza;
                long distanza = Math.Abs(pixel - pixelPreset);
                if (distanza < distanzaMigliore)
                {
                    migliore = i;
                    distanzaMigliore = distanza;
                }
            }

            return migliore;
        }

        /// <summary>Etichetta del preset in forma «larghezzaxaltezza», es. «1280x720».</summary>
        /// <exception cref="ArgumentOutOfRangeException">L'indice non corrisponde a nessun preset.</exception>
        public static string Etichetta(int indice)
        {
            if (indice < 0 || indice >= Elenco.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(indice), indice, "Indice di preset non valido.");
            }

            (int larghezza, int altezza) = Elenco[indice];
            return string.Format(CultureInfo.InvariantCulture, "{0}x{1}", larghezza, altezza);
        }
    }
}
