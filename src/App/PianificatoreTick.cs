using System;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Logica pura di pianificazione dei tick con riallineamento dopo ritardi significativi.</summary>
    public static class PianificatoreTick
    {
        /// <summary>
        /// Calcola la scadenza del tick successivo dopo aver eseguito quello previsto a <paramref name="scadenza"/>.
        /// Conserva la scadenza assoluta se il ritardo di invio è sotto la soglia; altrimenti riparte
        /// un intervallo dopo la fine dell'invio, senza pianificare tick arretrati.
        /// </summary>
        /// <param name="scadenza">Scadenza del tick appena eseguito, in millisecondi.</param>
        /// <param name="fineInvio">Istante in cui è terminato l'invio, in millisecondi.</param>
        /// <param name="intervallo">Intervallo tra due tick al BPM corrente, in millisecondi.</param>
        public static double Prossima(double scadenza, double fineInvio, double intervallo)
        {
            double sogliaRiallineamento = Math.Min(2.0, intervallo * 0.2);
            if (fineInvio - scadenza >= sogliaRiallineamento)
            {
                return fineInvio + intervallo;
            }

            return scadenza + intervallo;
        }
    }
}
