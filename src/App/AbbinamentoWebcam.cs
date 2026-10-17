using System;
using System.Collections.Generic;
using System.Linq;

namespace VisualMusicBlockLooper.App
{
    /// <summary>
    /// Ritrova tra le webcam presenti quella salvata nel config, per identità (percorso del dispositivo,
    /// poi nome) e non per posizione: se ordine o numero delle webcam cambiano si riapre la stessa.
    /// </summary>
    public static class AbbinamentoWebcam
    {
        /// <summary>
        /// Regole, in ordine: (1) stesso percorso del dispositivo; (2) stesso nome (senza distinzione di
        /// maiuscole; se più webcam hanno lo stesso nome vale la prima); (3) solo per un config vecchio che
        /// contiene soltanto l'indice, quell'indice. Se nel config c'è un'identità (nome o percorso) e nessuna
        /// webcam corrisponde il risultato è null: non si ripiega mai su un'altra webcam.
        /// </summary>
        /// <returns>La webcam trovata, o null.</returns>
        public static InfoWebcam Trova(IReadOnlyList<InfoWebcam> presenti, ConfigurazioneWebcam salvata)
        {
            if (presenti == null || salvata == null || presenti.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(salvata.Percorso))
            {
                InfoWebcam perPercorso = presenti.FirstOrDefault(
                    i => UgualiIgnorandoMaiuscole(i.Percorso, salvata.Percorso));
                if (perPercorso != null)
                {
                    return perPercorso;
                }
            }

            if (!string.IsNullOrWhiteSpace(salvata.Nome))
            {
                return presenti.FirstOrDefault(i => UgualiIgnorandoMaiuscole(i.Nome, salvata.Nome));
            }

            if (!string.IsNullOrWhiteSpace(salvata.Percorso))
            {
                return null;
            }

            return presenti.FirstOrDefault(i => i.Indice == salvata.Indice);
        }

        internal static bool UgualiIgnorandoMaiuscole(string a, string b)
        {
            return !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
                && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
