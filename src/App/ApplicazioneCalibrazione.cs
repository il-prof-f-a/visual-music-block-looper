using System;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Applica una calibrazione calcolata: attiva il rilevatore e poi tenta di salvarla.</summary>
    public static class ApplicazioneCalibrazione
    {
        /// <summary>
        /// Imposta il rilevatore con la calibrazione PRIMA del salvataggio, così un errore di scrittura
        /// non perde la calibrazione valida in memoria.
        /// </summary>
        /// <param name="salva">Azione che persiste la calibrazione.</param>
        /// <returns>Messaggio di avviso se il salvataggio è fallito, altrimenti null.</returns>
        public static string Applica(
            RilevatoreCommutabile rilevatore, IFonteImmagini fonte, Calibrazione calibrazione, Action<Calibrazione> salva)
        {
            rilevatore.Imposta(new RilevatoreCartoncini(fonte, calibrazione));
            try
            {
                salva(calibrazione);
                return null;
            }
            catch (Exception eccezione)
            {
                return "Calibrazione attiva ma non salvata su disco: " + eccezione.Message;
            }
        }
    }
}
