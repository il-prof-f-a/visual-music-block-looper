namespace VisualMusicBlockLooper.Core
{
    /// <summary>Tipo di cartoncino riconosciuto dal payload QR.</summary>
    public enum TipoCartoncino
    {
        /// <summary>Cartoncino che associa una nota a un clip.</summary>
        Nota,

        /// <summary>Cartoncino che esegue un comando.</summary>
        Comando,

        /// <summary>Cartoncino che imposta un parametro del tavolo.</summary>
        Impostazione,

        /// <summary>Cartoncino che suona più note contemporaneamente (accordo).</summary>
        Accordo
    }

    /// <summary>Tipo di impostazione letta da un cartoncino di tipo Impostazione.</summary>
    public enum TipoImpostazione
    {
        /// <summary>Misura (parametro intero).</summary>
        Misura,

        /// <summary>Collegamento (parametro intero).</summary>
        Collegamento,

        /// <summary>Intervallo AB (due parametri interi).</summary>
        IntervalloAb,

        /// <summary>Canale MIDI di uscita della riga: valore 1..16.</summary>
        Canale,

        /// <summary>Strumento da caricare sul canale della riga (nome breve).</summary>
        Strumento
    }

    /// <summary>Comando eseguibile da un cartoncino di tipo Comando.</summary>
    public enum TipoComando
    {
        Play,
        Stop,
        Tap,
        Modo,

        /// <summary>Comando che richiede una nota MIDI come parametro (0-127).</summary>
        OneShot
    }

    /// <summary>Contenuto decodificato di un payload QR.</summary>
    public class PayloadCartoncino
    {
        /// <summary>Tipo del cartoncino.</summary>
        public TipoCartoncino Tipo { get; set; }

        /// <summary>Identificativo del clip (cartoncino di tipo Nota).</summary>
        public int IdClip { get; set; }

        /// <summary>Comando (cartoncino di tipo Comando).</summary>
        public TipoComando Comando { get; set; }

        /// <summary>Parametro del comando, ad esempio la nota MIDI per ONESHOT.</summary>
        public int Parametro { get; set; }

        /// <summary>Impostazione (cartoncino di tipo Impostazione).</summary>
        public TipoImpostazione Impostazione { get; set; }

        /// <summary>Valore dell'impostazione (primo valore per AB).</summary>
        public int Valore { get; set; }

        /// <summary>Secondo valore dell'intervallo AB (solo per IntervalloAb).</summary>
        public int ValoreB { get; set; }

        /// <summary>Fondamentale dell'accordo, classe di altezza 0..11 (Do = 0).</summary>
        public int Fondamentale { get; set; }

        /// <summary>Qualità dell'accordo (solo per Tipo = Accordo).</summary>
        public TipoAccordo Accordo { get; set; }

        /// <summary>Nome breve dello strumento (solo per Impostazione = Strumento).</summary>
        public string Strumento { get; set; }
    }
}
