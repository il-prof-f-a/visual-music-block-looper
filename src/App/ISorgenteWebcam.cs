using System;
using System.Drawing;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Webcam aperta: sorgente di frame con risoluzione nota e anteprima per l'interfaccia.</summary>
    public interface ISorgenteWebcam : IFonteImmagini, IDisposable
    {
        /// <summary>Larghezza reale in pixel dei frame (0 se non determinabile).</summary>
        int Larghezza { get; }

        /// <summary>Altezza reale in pixel dei frame (0 se non determinabile).</summary>
        int Altezza { get; }

        /// <summary>Bitmap dell'ultimo frame letto, o null; il chiamante la rilascia.</summary>
        Bitmap Anteprima();
    }
}
