using System.Collections.Generic;
using System.Linq;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>Nota piazzata sulla griglia, derivata da un cartoncino valido.</summary>
    public class CartoncinoNota
    {
        /// <summary>Riga della griglia (indice 0-based): la riga-misura in cui sta la nota.</summary>
        public int Riga { get; set; }

        /// <summary>Traccia = indice (0-based) del gruppo di righe collegate: determina lo strumento (canale MIDI).</summary>
        public int Traccia { get; set; }

        /// <summary>Step iniziale della nota.</summary>
        public int StepIniziale { get; set; }

        /// <summary>Durata in step (minimo 1).</summary>
        public int DurataStep { get; set; }

        /// <summary>Identificativo del clip associato.</summary>
        public int IdClip { get; set; }

        /// <summary>Colore associato alla riga, solo per la visualizzazione.</summary>
        public ColoreTraccia Colore { get; set; }

        /// <summary>Note MIDI dell'accordo; null per un cartoncino di nota singola.</summary>
        public int[] NoteMidi { get; set; }

        /// <summary>Chiave univoca della nota: clip, traccia, riga e step iniziale.</summary>
        public string Chiave => $"{IdClip}:{Traccia}:{Riga}:{StepIniziale}";

        /// <summary>
        /// Identità completa, accordo compreso: due carte nella stessa cella ma con note diverse
        /// (per esempio Do+ e Do-) sono eventi distinti per lo stabilizzatore.
        /// </summary>
        public string Identita => NoteMidi == null || NoteMidi.Length == 0
            ? Chiave
            : Chiave + ":" + string.Join(",", NoteMidi);
    }

    /// <summary>Cartoncino di struttura valido, con la sua area sul tavolo (solo per la guida).</summary>
    public class CartoncinoSpeciale
    {
        public TipoImpostazione Tipo { get; set; }
        public RettangoloTavolo Area { get; set; }
    }

    /// <summary>Canale e/o strumento osservati nella fascia destra per una riga.</summary>
    public class AssegnazioneRiga
    {
        /// <summary>Riga del tavolo (0-based) a cui si riferisce la sezione.</summary>
        public int Riga { get; set; }

        /// <summary>Canale MIDI 0-based, oppure -1 se il cartoncino non imposta il canale.</summary>
        public int Canale { get; set; } = -1;

        /// <summary>Nome breve dello strumento, oppure null.</summary>
        public string Strumento { get; set; }
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
        ColoreMancante,
        Sovrapposto,
        FuoriGriglia,
        ImpostazioneNonValida,
        FuoriMisura
    }

    /// <summary>Cartoncino scartato, con l'area che lo identifica sul tavolo.</summary>
    public class ErroreTavolo
    {
        /// <summary>Motivo dello scarto.</summary>
        public TipoErrore Tipo { get; set; }

        /// <summary>Area del cartoncino scartato.</summary>
        public RettangoloTavolo Area { get; set; }
    }

    /// <summary>Risultato dell'interpretazione del tavolo: struttura, note, comandi ed errori.</summary>
    public class StatoTavolo
    {
        /// <summary>
        /// Crea lo stato; le liste null diventano liste vuote, una struttura null diventa <see cref="StrutturaTavolo.Default"/>.
        /// </summary>
        public StatoTavolo(
            IEnumerable<CartoncinoNota> note,
            IEnumerable<ComandoRilevato> comandi,
            IEnumerable<ErroreTavolo> errori,
            StrutturaTavolo struttura = null,
            IEnumerable<CartoncinoSpeciale> speciali = null,
            IEnumerable<AssegnazioneRiga> assegnazioniRiga = null)
        {
            Note = note?.ToList() ?? new List<CartoncinoNota>();
            Comandi = comandi?.ToList() ?? new List<ComandoRilevato>();
            Errori = errori?.ToList() ?? new List<ErroreTavolo>();
            Speciali = speciali?.ToList() ?? new List<CartoncinoSpeciale>();
            AssegnazioniRiga = assegnazioniRiga?.ToList() ?? new List<AssegnazioneRiga>();
            Struttura = struttura ?? StrutturaTavolo.Default;
        }

        /// <summary>Struttura del tavolo (misura, collegamento, A-B), mai null.</summary>
        public StrutturaTavolo Struttura { get; }

        /// <summary>Note valide, mai null.</summary>
        public IReadOnlyList<CartoncinoNota> Note { get; }

        /// <summary>Comandi validi, mai null.</summary>
        public IReadOnlyList<ComandoRilevato> Comandi { get; }

        /// <summary>Cartoncini scartati, mai null.</summary>
        public IReadOnlyList<ErroreTavolo> Errori { get; }

        /// <summary>Cartoncini di struttura validi, con area per la guida; mai null.</summary>
        public IReadOnlyList<CartoncinoSpeciale> Speciali { get; }

        /// <summary>Canali e strumenti osservati nella fascia destra; mai null.</summary>
        public IReadOnlyList<AssegnazioneRiga> AssegnazioniRiga { get; }
    }
}
