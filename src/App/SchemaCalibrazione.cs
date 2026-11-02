using System;
using System.Collections.Generic;
using System.Linq;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Riferimenti proiettati e controllo della calibrazione manuale a dodici punti.</summary>
    public static class SchemaCalibrazione
    {
        private const double Minimo = 0.03, Massimo = 0.97;
        private const double Terzo = Minimo + (Massimo - Minimo) / 3;
        private const double DueTerzi = Massimo - (Massimo - Minimo) / 3;
        private const double ScartoMassimo = 0.02;

        public static IReadOnlyList<PuntoTavolo> Punti { get; } = Array.AsReadOnly(new[]
        {
            new PuntoTavolo(Minimo, Minimo), new PuntoTavolo(Terzo, Minimo), new PuntoTavolo(DueTerzi, Minimo),
            new PuntoTavolo(Massimo, Minimo), new PuntoTavolo(Massimo, Terzo), new PuntoTavolo(Massimo, DueTerzi),
            new PuntoTavolo(Massimo, Massimo), new PuntoTavolo(DueTerzi, Massimo), new PuntoTavolo(Terzo, Massimo),
            new PuntoTavolo(Minimo, Massimo), new PuntoTavolo(Minimo, DueTerzi), new PuntoTavolo(Minimo, Terzo)
        });

        public static IReadOnlyList<string> Descrizioni { get; } = Array.AsReadOnly(new[]
        {
            "alto-sinistra", "lato superiore, primo terzo", "lato superiore, secondo terzo",
            "alto-destra", "lato destro, primo terzo", "lato destro, secondo terzo",
            "basso-destra", "lato inferiore, da destra, primo terzo", "lato inferiore, da destra, secondo terzo",
            "basso-sinistra", "lato sinistro, dal basso, primo terzo", "lato sinistro, dal basso, secondo terzo"
        });

        /// <summary>Calcola l'omografia e rifiuta click incoerenti anche quando RANSAC li ha scartati.</summary>
        public static Calibrazione Calcola(PuntoImmagine[] punti, int larghezza, int altezza, out double scartoMedio)
        {
            scartoMedio = 0;
            if (punti == null) throw new ArgumentNullException(nameof(punti));
            if (punti.Length != Punti.Count) throw new ArgumentException("Servono esattamente 12 punti.", nameof(punti));
            Calibrazione calibrazione = Calibrazione.Calcola(punti, Punti.ToArray(), larghezza, altezza);
            var incoerenti = new List<int>();
            for (int i = 0; i < punti.Length; i++)
            {
                PuntoTavolo letto = calibrazione.DaCameraATavolo(punti[i]);
                double dx = letto.X - Punti[i].X, dy = letto.Y - Punti[i].Y;
                double scarto = Math.Sqrt(dx * dx + dy * dy);
                if (double.IsNaN(scarto) || double.IsInfinity(scarto) || scarto > ScartoMassimo)
                    incoerenti.Add(i + 1);
                scartoMedio += scarto / punti.Length;
            }
            if (incoerenti.Count > 0)
                throw new ArgumentException("Punti incoerenti: " + string.Join(", ", incoerenti)
                    + ". Ripetere la calibrazione cliccando i bersagli numerati.", nameof(punti));
            return calibrazione;
        }
    }
}
