using System.Collections.Generic;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>Nota piazzata sulla griglia, derivata da un cartoncino valido.</summary>
    public class CartoncinoNota
    {
        /// <summary>Traccia della griglia (indice 0-based).</summary>
        public int Traccia { get; set; }

        /// <summary>Step iniziale della nota.</summary>
        public int StepIniziale { get; set; }

        /// <summary>Durata in step (minimo 1).</summary>
        public int DurataStep { get; set; }

        /// <summary>Identificativo del clip associato.</summary>
        public int IdClip { get; set; }

        /// <summary>Colore del cartoncino.</summary>
        public ColoreTraccia Colore { get; set; }

        /// <summary>Chiave univoca della nota: clip, traccia e step iniziale.</summary>
        public string Chiave => $"{IdClip}:{Traccia}:{StepIniziale}";
    }

    /// <summary>Comando richiesto da un cartoncino posto nella zona comandi.</summary>
    public class ComandoRilevato
    {
        /// <summary>Tipo del comando.</summary>
        public TipoComando Tipo { get; set; }

        /// <summary>Parametro del comando, ad esempio la nota MIDI per ONESHOT.</summary>
        public int Parametro { get; set; }
    }

    /// <summary>Motivo per cui un cartoncino non è stato interpretato.</summary>
    public enum TipoErrore
    {
        PayloadNonValido,
        ColoreIncoerente,
        ColoreMancante,
        Sovrapposto,
        FuoriGriglia
    }

    /// <summary>Cartoncino scartato, con l'area che lo identifica sul tavolo.</summary>
    public class ErroreTavolo
    {
        /// <summary>Motivo dello scarto.</summary>
        public TipoErrore Tipo { get; set; }

        /// <summary>Area del cartoncino scartato.</summary>
        public RettangoloTavolo Area { get; set; }
    }

    /// <summary>Risultato dell'interpretazione del tavolo: note, comandi ed errori.</summary>
    public class StatoTavolo
    {
        /// <summary>Crea lo stato; le liste null diventano liste vuote.</summary>
        public StatoTavolo(
            IReadOnlyList<CartoncinoNota> note,
            IReadOnlyList<ComandoRilevato> comandi,
            IReadOnlyList<ErroreTavolo> errori)
        {
            Note = note ?? new List<CartoncinoNota>();
            Comandi = comandi ?? new List<ComandoRilevato>();
            Errori = errori ?? new List<ErroreTavolo>();
        }

        /// <summary>Note valide, mai null.</summary>
        public IReadOnlyList<CartoncinoNota> Note { get; }

        /// <summary>Comandi validi, mai null.</summary>
        public IReadOnlyList<ComandoRilevato> Comandi { get; }

        /// <summary>Cartoncini scartati, mai null.</summary>
        public IReadOnlyList<ErroreTavolo> Errori { get; }
    }
}
