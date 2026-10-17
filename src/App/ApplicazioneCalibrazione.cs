using System;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Applica una calibrazione calcolata: attiva il rilevatore e poi tenta di salvarla.</summary>
    public static class ApplicazioneCalibrazione
    {
        /// <summary>Avviso per una calibrazione calcolata per una risoluzione diversa da quella della camera.</summary>
        public const string AvvisoRisoluzioneDiversa = "Calibrazione per una risoluzione diversa: rifai la calibrazione";

        /// <summary>Avviso per una calibrazione salvata senza risoluzione (vecchio formato).</summary>
        public const string AvvisoRisoluzioneSconosciuta = "Calibrazione senza risoluzione: ripetila per sicurezza";

        /// <summary>
        /// Decide se una calibrazione caricata può attivare il rilevatore, confrontando la sua risoluzione
        /// con quella attuale della camera.
        /// </summary>
        /// <param name="avviso">Messaggio da mostrare all'operatore, o null se non serve.</param>
        /// <returns>False se la risoluzione differisce (rilevatore da non attivare), altrimenti true.</returns>
        public static bool ValutaRisoluzione(
            Calibrazione calibrazione, int larghezzaCamera, int altezzaCamera, out string avviso)
        {
            switch (calibrazione.VerificaRisoluzione(larghezzaCamera, altezzaCamera))
            {
                case EsitoRisoluzione.Diversa:
                    avviso = AvvisoRisoluzioneDiversa;
                    return false;
                case EsitoRisoluzione.Sconosciuta:
                    avviso = AvvisoRisoluzioneSconosciuta;
                    return true;
                default:
                    avviso = null;
                    return true;
            }
        }

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
