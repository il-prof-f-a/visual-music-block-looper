namespace VisualMusicBlockLooper.Vision
{
    /// <summary>Punto nel sistema di coordinate dell'immagine della camera (pixel).</summary>
    public readonly struct PuntoImmagine
    {
        public PuntoImmagine(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }
    }

    /// <summary>Punto nel sistema di coordinate normalizzato del tavolo.</summary>
    public readonly struct PuntoTavolo
    {
        public PuntoTavolo(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }
    }
}
