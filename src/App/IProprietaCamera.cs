namespace VisualMusicBlockLooper.App
{
    /// <summary>Proprietà della webcam che l'applicazione sa impostare e rileggere.</summary>
    public enum ProprietaCamera
    {
        /// <summary>Posizione del fuoco manuale (0..255).</summary>
        Fuoco,

        /// <summary>Messa a fuoco automatica (valore booleano 0/1).</summary>
        FuocoAutomatico,

        /// <summary>Esposizione manuale (log2 dei secondi).</summary>
        Esposizione,

        /// <summary>Modalità di esposizione automatica (codifica che dipende dal driver).</summary>
        EsposizioneAutomatica,

        /// <summary>Frame al secondo dichiarati dal driver.</summary>
        Fps
    }

    /// <summary>Accesso minimo alle proprietà di una webcam; permette di provare la logica senza hardware.</summary>
    public interface IProprietaCamera
    {
        /// <summary>Imposta la proprietà; il valore restituito dai driver non è affidabile, quindi va riletto.</summary>
        bool Imposta(ProprietaCamera proprieta, double valore);

        /// <summary>Legge il valore corrente della proprietà (0 o NaN se non disponibile).</summary>
        double Leggi(ProprietaCamera proprieta);
    }
}
