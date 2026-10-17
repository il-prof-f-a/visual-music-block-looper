namespace VisualMusicBlockLooper.App
{
    /// <summary>Logica pura di pianificazione dei tick: accumulatore senza deriva e recupero limitato.</summary>
    public static class PianificatoreTick
    {
        /// <summary>Numero massimo di tick già scaduti che possono essere recuperati di seguito dopo uno stallo.</summary>
        public const int MassimoRecuperoTick = 2;

        /// <summary>
        /// Calcola la scadenza del tick successivo dopo aver eseguito quello previsto a <paramref name="scadenza"/>.
        /// La scadenza avanza di un intervallo esatto (niente deriva); se resterebbero da recuperare
        /// <see cref="MassimoRecuperoTick"/> tick o più, i tick in eccesso vengono scartati e la
        /// pianificazione riparte da <paramref name="adesso"/>.
        /// </summary>
        /// <param name="scadenza">Scadenza del tick appena eseguito, in millisecondi.</param>
        /// <param name="adesso">Istante corrente in millisecondi.</param>
        /// <param name="intervallo">Intervallo tra due tick al BPM corrente, in millisecondi.</param>
        public static double Prossima(double scadenza, double adesso, double intervallo)
        {
            double prossima = scadenza + intervallo;
            if (adesso - prossima >= MassimoRecuperoTick * intervallo)
            {
                return adesso + intervallo;
            }

            return prossima;
        }
    }
}
