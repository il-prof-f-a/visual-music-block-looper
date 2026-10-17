namespace VisualMusicBlockLooper.Core
{
    /// <summary>Tipo di cartoncino riconosciuto dal payload QR.</summary>
    public enum TipoCartoncino
    {
        /// <summary>Cartoncino che associa una nota a un clip.</summary>
        Nota,

        /// <summary>Cartoncino che esegue un comando.</summary>
        Comando
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
    }
}
