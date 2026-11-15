namespace VisualMusicBlockLooper.Core
{
    /// <summary>Configurazione base della griglia di step.</summary>
    public class ConfigGriglia
    {
        /// <summary>Numero di tracce della griglia.</summary>
        public int Tracce { get; set; } = 6;

        /// <summary>Numero di step per traccia.</summary>
        public int Step { get; set; } = 16;

        /// <summary>Area normalizzata del tavolo coperta dalla griglia.</summary>
        public RettangoloTavolo Area { get; set; } = new RettangoloTavolo(0, 0, 1, 1);

        /// <summary>Scarto massimo dal bordo di cella, in frazioni di cella, corretto per l'aggancio dei QR.</summary>
        public double TolleranzaQrCelle { get; set; } = 0.15;
    }
}
