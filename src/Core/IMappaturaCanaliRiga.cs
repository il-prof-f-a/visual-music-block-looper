namespace VisualMusicBlockLooper.Core
{
    /// <summary>
    /// Mappatura che può sovrascrivere il canale MIDI per singola riga del tavolo.
    /// Se una riga non ha un override si continua a usare <see cref="IMappaturaMidi.Canale"/>.
    /// </summary>
    public interface IMappaturaCanaliRiga
    {
        /// <summary>Canale 0-based assegnato alla riga; false se la riga usa la mappatura normale.</summary>
        bool TryCanaleRiga(int riga, out int canale);
    }
}
