using OpenCvSharp;

namespace VisualMusicBlockLooper.Vision
{
    /// <summary>Fornisce i frame della camera.</summary>
    public interface IFonteImmagini
    {
        /// <summary>
        /// Restituisce il frame corrente; il chiamante ne e' proprietario e lo rilascia.
        /// Puo' essere null o vuoto se non c'e' un frame.
        /// </summary>
        Mat Frame();
    }
}
