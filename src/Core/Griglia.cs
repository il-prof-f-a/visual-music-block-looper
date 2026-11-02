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

        /// <summary>Il QR decide l'attacco; il colore decide il confine finale più vicino.</summary>
        public bool TryCella(CartoncinoRilevato nota, out int traccia, out int stepIniziale, out int durataStep)
        {
            traccia = stepIniziale = durataStep = 0;
            if (nota == null) return false;
            RettangoloTavolo attacco = nota.AreaQr ?? nota.Area;
            if (nota.AreaQr.HasValue && (attacco.Larghezza <= 0 || attacco.Altezza <= 0)) return false;
            double fine = nota.FineEstensioneX ?? (nota.Area.X + nota.Area.Larghezza);
            if (!Finito(attacco.X) || !Finito(attacco.Y) || !Finito(attacco.Larghezza) || !Finito(attacco.Altezza)
                || !Finito(fine) || config.Step <= 0 || config.Tracce <= 0
                || config.Area.Larghezza <= 0 || config.Area.Altezza <= 0) return false;
            if (!nota.AreaQr.HasValue && !nota.FineEstensioneX.HasValue)
                return TryCella(nota.Area, out traccia, out stepIniziale, out durataStep);
            if (!TrovaPosizione(attacco, out traccia, out stepIniziale)) return false;
            double confine = Math.Round((fine - config.Area.X) / LarghezzaStep() + Epsilon, MidpointRounding.AwayFromZero);
            int finale = (int)Math.Min(config.Step, Math.Max(stepIniziale + 1, confine));
            durataStep = finale - stepIniziale;
            return true;
        }

        private static bool Finito(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

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
