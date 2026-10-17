namespace VisualMusicBlockLooper.Core
{
    /// <summary>Rettangolo in coordinate normalizzate (0..1) del tavolo o del proiettore.</summary>
    public struct RettangoloTavolo
    {
        public RettangoloTavolo(double x, double y, double larghezza, double altezza)
        {
            X = x;
            Y = y;
            Larghezza = larghezza;
            Altezza = altezza;
        }

        /// <summary>Coordinata X del bordo sinistro.</summary>
        public double X { get; }

        /// <summary>Coordinata Y del bordo superiore.</summary>
        public double Y { get; }

        /// <summary>Larghezza normalizzata.</summary>
        public double Larghezza { get; }

        /// <summary>Altezza normalizzata.</summary>
        public double Altezza { get; }
    }
}
