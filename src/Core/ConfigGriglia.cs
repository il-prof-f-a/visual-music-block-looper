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
    }
}
