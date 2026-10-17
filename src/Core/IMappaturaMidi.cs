namespace VisualMusicBlockLooper.Core
{
    /// <summary>Traduce tracce e clip della griglia in canali e note MIDI.</summary>
    public interface IMappaturaMidi
    {
        /// <summary>Canale MIDI associato a una traccia.</summary>
        /// <param name="traccia">Traccia della griglia (0-based).</param>
        int Canale(int traccia);

        /// <summary>Numero di nota MIDI associato a un clip.</summary>
        /// <param name="idClip">Identificativo del clip.</param>
        int Nota(int idClip);

        /// <summary>Velocity usata per le note-on.</summary>
        int Velocity { get; }
    }
}
