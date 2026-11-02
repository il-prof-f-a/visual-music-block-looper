using System;
using System.Collections.Generic;
using System.Linq;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Punti da ripetere senza perdere le altre osservazioni della raccolta.</summary>
    public sealed class PuntiCalibrazioneIncoerentiException : ArgumentException
    {
        public IReadOnlyList<int> Numeri { get; }
        public PuntiCalibrazioneIncoerentiException(int[] numeri)
            : base("Punti incoerenti: " + string.Join(", ", numeri) + ". Ripetere i bersagli segnalati.", "punti")
        {
            Numeri = Array.AsReadOnly((int[])numeri.Clone());
        }
    }

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

        /// <summary>Controlla errori isolati rispetto alla prospettiva, poi interpola la curva sui dodici riferimenti.</summary>
        public static Calibrazione Calcola(PuntoImmagine[] punti, int larghezza, int altezza, out double scartoMedio)
        {
            scartoMedio = 0;
            if (punti == null) throw new ArgumentNullException(nameof(punti));
            if (punti.Length != Punti.Count) throw new ArgumentException("Servono esattamente 12 punti.", nameof(punti));
            Calibrazione calibrazione = Calibrazione.Calcola(punti, Punti.ToArray(), larghezza, altezza);
            Calibrazione media = Calibrazione.Calcola(punti, Punti.ToArray(), larghezza, altezza, scartaAnomalie: false);
            var incoerenti = new List<int>();
            var scarti = new double[punti.Length];
            var scartiDistribuiti = new double[punti.Length];
            for (int i = 0; i < punti.Length; i++)
            {
                PuntoTavolo letto = calibrazione.DaCameraATavolo(punti[i]);
                double dx = letto.X - Punti[i].X, dy = letto.Y - Punti[i].Y;
                double scarto = Math.Sqrt(dx * dx + dy * dy);
                scarti[i] = scarto;
                PuntoTavolo globale = media.DaCameraATavolo(punti[i]);
                double gx = globale.X - Punti[i].X, gy = globale.Y - Punti[i].Y;
                scartiDistribuiti[i] = Math.Sqrt(gx * gx + gy * gy);
                scartoMedio += scartiDistribuiti[i] / punti.Length;
            }
            // La curvatura distribuita non è un errore di click. Si conservano il controllo degli
            // errori isolati e la ripetizione del riferimento, senza pretendere una sola omografia.
            var ordinati = scartiDistribuiti.OrderBy(s => s).ToArray();
            double mediana = (ordinati[5] + ordinati[6]) / 2;
            double soglia = Math.Max(ScartoMassimo, 4 * mediana);
            for (int i = 0; i < scarti.Length; i++)
                if (double.IsNaN(scarti[i]) || double.IsInfinity(scarti[i]) || scarti[i] > soglia)
                    incoerenti.Add(i + 1);
            if (incoerenti.Count > 0)
                throw new PuntiCalibrazioneIncoerentiException(incoerenti.ToArray());
            return Calibrazione.CalcolaCurva(punti, Punti.ToArray(), larghezza, altezza);
        }
    }
}
