using System;
using System.Collections.Generic;
using System.IO;
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
        /// Carica la calibrazione salvata e, se compatibile con la webcam corrente, attiva il rilevatore.
        /// Senza webcam, senza file o con risoluzione diversa il rilevatore resta spento.
        /// </summary>
        /// <param name="avvisi">Riceve i messaggi da mostrare all'operatore.</param>
        /// <returns>True se il rilevatore è stato attivato.</returns>
        public static bool TentaCaricamento(
            string percorso, GestoreWebcam webcam, RilevatoreCommutabile rilevatore, ICollection<string> avvisi)
        {
            if (webcam == null || !webcam.Disponibile || !File.Exists(percorso))
            {
                return false;
            }

            try
            {
                Calibrazione calibrazione = Calibrazione.Carica(percorso);
                bool attivabile = ValutaRisoluzione(calibrazione, webcam.Larghezza, webcam.Altezza, out string avviso);
                if (avviso != null)
                {
                    avvisi.Add(avviso);
                }

                if (!attivabile)
                {
                    return false;
                }

                rilevatore.Imposta(new RilevatoreCartoncini(webcam, calibrazione));
                return true;
            }
            catch (Exception eccezione)
            {
                avvisi.Add("Calibrazione salvata non valida (" + eccezione.Message + "): ripetere la calibrazione.");
                return false;
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
