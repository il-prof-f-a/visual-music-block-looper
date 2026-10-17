namespace VisualMusicBlockLooper.Core
{
    /// <summary>Uscita MIDI verso il DAW: note, clock e messaggi di trasporto.</summary>
    public interface IUscitaMidi
    {
        /// <summary>Invia una nota-on.</summary>
        /// <param name="canale">Canale MIDI (0-based).</param>
        /// <param name="nota">Numero di nota MIDI.</param>
        /// <param name="velocity">Velocity della nota.</param>
        void NotaOn(int canale, int nota, int velocity);

        /// <summary>Invia una nota-off.</summary>
        /// <param name="canale">Canale MIDI (0-based).</param>
        /// <param name="nota">Numero di nota MIDI.</param>
        void NotaOff(int canale, int nota);

        /// <summary>Invia un messaggio di clock MIDI.</summary>
        void Clock();

        /// <summary>Invia il messaggio di start.</summary>
        void Start();

        /// <summary>Invia il messaggio di stop.</summary>
        void Stop();

        /// <summary>Invia il messaggio di continue.</summary>
        void Continue();
    }
}
