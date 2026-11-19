using System;

namespace VisualMusicBlockLooper.Vision
{
    /// <summary>Modello di fusione tra TPS di bordo e omografia centrale.</summary>
    public enum ModelloCorrezioneBordo
    {
        /// <summary>Distanza minima dal bordo con transizione smoothstep.</summary>
        Bordo,

        /// <summary>Distanza smussata (softmin) e fattori di anisotropia per lato.</summary>
        BordoSmussato,

        /// <summary>Proiezione sferica globale (lente assimilata a una sfera).</summary>
        Sferico
    }

    /// <summary>Proiezione sferica usata dal modello globale.</summary>
    public enum TipoProiezioneSferica
    {
        /// <summary>r = 2f·sin(θ/2): conserva l'angolo solido.</summary>
        Equisolido,

        /// <summary>r = f·θ: distanza proporzionale all'angolo.</summary>
        Equidistante,

        /// <summary>r = 2f·tan(θ/2): conserva localmente forme e angoli.</summary>
        Stereografico
    }

    /// <summary>
    /// Parametri della correzione di bordo. Sono immutabili rispetto alla calibrazione:
    /// una copia viene acquisita quando si costruisce o si clona una CAL3.
    /// </summary>
    public sealed class ParametriCorrezioneBordo
    {
        public ModelloCorrezioneBordo Modello { get; set; } = ModelloCorrezioneBordo.Bordo;

        /// <summary>Distanza dal bordo entro cui il TPS vale al 100%.</summary>
        public double Pieno { get; set; } = 0.04;

        /// <summary>Distanza dal bordo oltre cui vale la sola omografia.</summary>
        public double Esterno { get; set; } = 0.22;

        /// <summary>Fattore di scala della distanza orizzontale (modalità smussata).</summary>
        public double FattoreX { get; set; } = 1.0;

        /// <summary>Fattore di scala della distanza verticale (modalità smussata).</summary>
        public double FattoreY { get; set; } = 1.0;

        /// <summary>Intensità della softmin; 0 usa il minimo netto.</summary>
        public double Smussamento { get; set; } = 0.0;

        /// <summary>Proiezione sferica (usata solo con Modello = Sferico).</summary>
        public TipoProiezioneSferica TipoSferico { get; set; } = TipoProiezioneSferica.Equisolido;

        /// <summary>Focale in pixel; 0 = stimata dai dodici punti.</summary>
        public double Focale { get; set; } = 0.0;

        /// <summary>Centro ottico X in pixel; 0 = stimato dai dodici punti.</summary>
        public double CentroX { get; set; } = 0.0;

        /// <summary>Centro ottico Y in pixel; 0 = stimato dai dodici punti.</summary>
        public double CentroY { get; set; } = 0.0;

        public ParametriCorrezioneBordo Clona() => new ParametriCorrezioneBordo
        {
            Modello = Modello,
            Pieno = Pieno,
            Esterno = Esterno,
            FattoreX = FattoreX,
            FattoreY = FattoreY,
            Smussamento = Smussamento,
            TipoSferico = TipoSferico,
            Focale = Focale,
            CentroX = CentroX,
            CentroY = CentroY
        };

        /// <summary>Riporta i valori fuori intervallo ai default e mantiene Esterno &gt; Pieno.</summary>
        public ParametriCorrezioneBordo Normalizzati()
        {
            var p = Clona();
            if (double.IsNaN(p.Pieno) || double.IsInfinity(p.Pieno) || p.Pieno < 0.03 || p.Pieno > 0.10)
                p.Pieno = 0.04;
            if (double.IsNaN(p.Esterno) || double.IsInfinity(p.Esterno) || p.Esterno <= p.Pieno || p.Esterno > 0.50)
                p.Esterno = 0.22;
            if (double.IsNaN(p.FattoreX) || double.IsInfinity(p.FattoreX) || p.FattoreX < 0.25 || p.FattoreX > 4.0)
                p.FattoreX = 1.0;
            if (double.IsNaN(p.FattoreY) || double.IsInfinity(p.FattoreY) || p.FattoreY < 0.25 || p.FattoreY > 4.0)
                p.FattoreY = 1.0;
            if (double.IsNaN(p.Smussamento) || double.IsInfinity(p.Smussamento) || p.Smussamento < 0 || p.Smussamento > 200)
                p.Smussamento = 0.0;
            if (p.Modello == ModelloCorrezioneBordo.Bordo)
            {
                p.FattoreX = 1.0;
                p.FattoreY = 1.0;
                p.Smussamento = 0.0;
            }
            if (double.IsNaN(p.Focale) || double.IsInfinity(p.Focale) || p.Focale < 0 || p.Focale > 20000)
                p.Focale = 0.0;
            if (double.IsNaN(p.CentroX) || double.IsInfinity(p.CentroX) || p.CentroX < 0 || p.CentroX > 20000)
                p.CentroX = 0.0;
            if (double.IsNaN(p.CentroY) || double.IsInfinity(p.CentroY) || p.CentroY < 0 || p.CentroY > 20000)
                p.CentroY = 0.0;
            return p;
        }

        public bool Validi()
        {
            return Pieno >= 0.03 && Pieno <= 0.10
                && Esterno > Pieno && Esterno <= 0.50
                && FattoreX >= 0.25 && FattoreX <= 4.0
                && FattoreY >= 0.25 && FattoreY <= 4.0
                && Smussamento >= 0 && Smussamento <= 200
                && !double.IsNaN(Pieno) && !double.IsNaN(Esterno)
                && !double.IsNaN(FattoreX) && !double.IsNaN(FattoreY) && !double.IsNaN(Smussamento)
                && Focale >= 0 && Focale <= 20000
                && CentroX >= 0 && CentroX <= 20000 && CentroY >= 0 && CentroY <= 20000;
        }
    }
}
