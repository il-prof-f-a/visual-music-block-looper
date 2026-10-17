using System;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>Traduce la posizione di un cartoncino nella cella (traccia, step) della griglia.</summary>
    public class Griglia
    {
        // Tolleranza per gli estremi delle celle, per evitare errori di virgola mobile sui bordi.
        private const double Epsilon = 1e-9;

        private readonly ConfigGriglia config;

        public Griglia(ConfigGriglia config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Calcola la cella occupata da un'area. Lo step iniziale deriva dal bordo sinistro,
        /// la traccia dal centro verticale; la durata è la larghezza in step (minimo 1),
        /// troncata a fine griglia.
        /// </summary>
        public bool TryCella(RettangoloTavolo area, out int traccia, out int stepIniziale, out int durataStep)
        {
            traccia = 0;
            stepIniziale = 0;
            durataStep = 0;

            if (!TrovaPosizione(area, out traccia, out stepIniziale))
            {
                return false;
            }

            double larghezzaStep = LarghezzaStep();
            long durata = (long)Math.Round(area.Larghezza / larghezzaStep + Epsilon, MidpointRounding.AwayFromZero);
            durataStep = (int)Math.Min(Math.Max(durata, 1), config.Step - stepIniziale);
            return true;
        }

        /// <summary>True quando la posizione dell'area non ricade nella griglia.</summary>
        public bool EsternoAllaGriglia(RettangoloTavolo area)
        {
            return !TrovaPosizione(area, out _, out _);
        }

        private bool TrovaPosizione(RettangoloTavolo area, out int traccia, out int stepIniziale)
        {
            RettangoloTavolo g = config.Area;
            double larghezzaStep = LarghezzaStep();
            double altezzaTraccia = g.Altezza / config.Tracce;

            double posStep = (area.X - g.X) / larghezzaStep + Epsilon;
            double centroY = area.Y + area.Altezza / 2.0;
            double posTraccia = (centroY - g.Y) / altezzaTraccia + Epsilon;

            stepIniziale = (int)Math.Floor(posStep);
            traccia = (int)Math.Floor(posTraccia);

            return posStep >= 0
                && stepIniziale < config.Step
                && traccia >= 0
                && traccia < config.Tracce;
        }

        private double LarghezzaStep()
        {
            return config.Area.Larghezza / config.Step;
        }
    }
}
