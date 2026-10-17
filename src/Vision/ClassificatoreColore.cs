using System;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Vision
{
    /// <summary>Classifica un colore RGB in una delle sei tracce tramite HSV.</summary>
    public static class ClassificatoreColore
    {
        /// <summary>Saturazione minima (0..1) sotto cui il colore e' considerato neutro.</summary>
        public const double SaturazioneMinima = 0.35;

        /// <summary>Valore minimo (0..1) sotto cui il colore e' considerato troppo scuro.</summary>
        public const double ValoreMinimo = 0.25;

        // Intervalli di tonalita' in gradi [da, a) per colore; il rosso attraversa 0.
        private static readonly double[][] Intervalli =
        {
            new[] { 345.0, 360.0, 0.0, 15.0 }, // Rosso
            new[] { 15.0, 40.0 },              // Arancione
            new[] { 40.0, 70.0 },              // Giallo
            new[] { 70.0, 170.0 },             // Verde
            new[] { 170.0, 260.0 },            // Blu
            new[] { 260.0, 345.0 },            // Viola
        };

        /// <summary>Restituisce il colore della traccia o null se il colore non e' sufficientemente saturo/luminoso.</summary>
        public static ColoreTraccia? Classifica(byte r, byte g, byte b)
        {
            double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
            double max = Math.Max(rf, Math.Max(gf, bf));
            double min = Math.Min(rf, Math.Min(gf, bf));
            double delta = max - min;
            double v = max;
            double s = max <= 0 ? 0 : delta / max;
            if (s < SaturazioneMinima || v < ValoreMinimo)
            {
                return null;
            }

            double h;
            if (max == rf)
            {
                h = 60.0 * (((gf - bf) / delta) % 6.0);
            }
            else if (max == gf)
            {
                h = 60.0 * (((bf - rf) / delta) + 2.0);
            }
            else
            {
                h = 60.0 * (((rf - gf) / delta) + 4.0);
            }

            if (h < 0)
            {
                h += 360.0;
            }

            for (int i = 0; i < Intervalli.Length; i++)
            {
                double[] t = Intervalli[i];
                for (int k = 0; k < t.Length; k += 2)
                {
                    if (h >= t[k] && h < t[k + 1])
                    {
                        return (ColoreTraccia)i;
                    }
                }
            }

            return ColoreTraccia.Rosso;
        }

        /// <summary>Intervalli di tonalita' in gradi (coppie da/a) del colore, per costruire le maschere.</summary>
        internal static double[] IntervalliGradi(ColoreTraccia colore)
        {
            return Intervalli[(int)colore];
        }
    }
}
