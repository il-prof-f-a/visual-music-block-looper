namespace VisualMusicBlockLooper.Core
{
    /// <summary>Configurazione base della griglia di step (estesa nei task successivi).</summary>
    public class ConfigGriglia
    {
        /// <summary>Numero di tracce della griglia.</summary>
        public int Tracce { get; set; } = 6;

        /// <summary>Numero di step per traccia.</summary>
        public int Step { get; set; } = 16;
    }
}
