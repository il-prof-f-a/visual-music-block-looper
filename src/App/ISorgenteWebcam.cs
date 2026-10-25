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

        /// <summary>
        /// Bitmap dell'ultimo frame raddrizzato sull'area del tavolo (vedi <see cref="Calibrazione.Raddrizza"/>),
        /// o null se non c'è ancora un frame; il chiamante la rilascia.
        /// </summary>
        Bitmap AnteprimaRaddrizzata(Calibrazione calibrazione, int larghezza, int altezza);

        /// <summary>
        /// Applica le impostazioni di fuoco ed esposizione: solo i campi non null (null = non toccare il driver).
        /// La risoluzione non si cambia qui (si riapre la webcam).
        /// </summary>
        void ApplicaImpostazioni(ConfigurazioneWebcam impostazioni);

        /// <summary>Valori effettivi riletti dalla webcam e supporto reale di fuoco ed esposizione.</summary>
        StatoImpostazioniWebcam LeggiImpostazioni();

        /// <summary>Frame realmente consegnati da <see cref="IFonteImmagini.Frame"/> al secondo, nell'ultima finestra.</summary>
        double FpsCatturati { get; }
    }
}
