using System.Collections.Generic;

namespace VisualMusicBlockLooper.Midi
{
    /// <summary>Configurazione MIDI caricabile da file JSON. I canali sono 0-based (0..15).</summary>
    public class ConfigurazioneMidi
    {
        /// <summary>Nome della porta MIDI di uscita.</summary>
        public string PortaUscita { get; set; } = "VMBL";

        /// <summary>Nome della porta MIDI di ingresso.</summary>
        public string PortaIngresso { get; set; } = "VMBL";

        /// <summary>Velocity delle note-on.</summary>
        public int Velocity { get; set; } = 100;

        /// <summary>Canale MIDI per ciascuna traccia, indicizzato per traccia.</summary>
        public int[] CanalePerTraccia { get; set; } = { 0, 1, 2, 3, 4, 5 };

        /// <summary>Numero di nota MIDI per identificativo di clip.</summary>
        public Dictionary<int, int> NotaPerClip { get; set; } = new Dictionary<int, int>();
    }
}
