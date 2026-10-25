namespace VisualMusicBlockLooper.App
{
    /// <summary>Valori effettivi riletti dalla webcam, con l'indicazione di cosa il driver supporta davvero.</summary>
    public sealed class StatoImpostazioniWebcam
    {
        /// <summary>Messa a fuoco automatica attiva (null se non rilevabile).</summary>
        public bool? FuocoAutomatico { get; set; }

        /// <summary>Posizione del fuoco manuale (null se non rilevabile).</summary>
        public int? Fuoco { get; set; }

        /// <summary>Esposizione automatica attiva (null se non rilevabile).</summary>
        public bool? EsposizioneAutomatica { get; set; }

        /// <summary>Esposizione manuale in log2 dei secondi (null se non rilevabile).</summary>
        public double? Esposizione { get; set; }

        /// <summary>True se la webcam accetta le impostazioni di fuoco.</summary>
        public bool FuocoSupportato { get; set; }

        /// <summary>True se la webcam accetta le impostazioni di esposizione.</summary>
        public bool EsposizioneSupportata { get; set; }

        /// <summary>Frame al secondo dichiarati dal driver (0 se ignoti).</summary>
        public double FpsDichiarati { get; set; }

        /// <summary>Stato di una webcam assente: niente supportato, nessun valore.</summary>
        public static StatoImpostazioniWebcam NonSupportato()
        {
            return new StatoImpostazioniWebcam();
        }
    }
}
