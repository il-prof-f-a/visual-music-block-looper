namespace VisualMusicBlockLooper.Core
{
    /// <summary>Evento MIDI prodotto dal sequencer per una nota: note-on o note-off.</summary>
    public class EventoNota
    {
        /// <summary>Crea l'evento per la traccia e il clip indicati.</summary>
        /// <param name="traccia">Traccia della griglia (indice 0-based).</param>
        /// <param name="idClip">Identificativo del clip associato alla nota.</param>
        /// <param name="noteOn">True per note-on, false per note-off.</param>
        public EventoNota(int traccia, int idClip, bool noteOn,int durataStep=0,string identita=null)
        {
            Traccia = traccia;
            IdClip = idClip;
            NoteOn = noteOn;
            DurataStep = durataStep;
            Identita = identita;
        }

        /// <summary>Traccia della nota.</summary>
        public int Traccia { get; }

        /// <summary>Identificativo del clip della nota.</summary>
        public int IdClip { get; }

        /// <summary>True se l'evento è un note-on, false se è un note-off.</summary>
        public bool NoteOn { get; }
        public int DurataStep { get; }
        public string Identita { get; }
    }
}
