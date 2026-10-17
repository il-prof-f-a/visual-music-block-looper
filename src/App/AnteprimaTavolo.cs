using System;
using System.Drawing;

namespace VisualMusicBlockLooper.App
{
    /// <summary>
    /// Regole dell'anteprima nella finestra di controllo: dopo la calibrazione si mostra l'area del tavolo
    /// raddrizzata (stirata) invece dell'immagine grezza della webcam, con un avviso a schermo.
    /// </summary>
    public static class AnteprimaTavolo
    {
        /// <summary>Rapporto larghezza/altezza usato se quello dello schermo del proiettore non è noto.</summary>
        public const double AspettoPredefinito = 16.0 / 9.0;

        /// <summary>Avviso mostrato sotto l'anteprima raddrizzata.</summary>
        public const string TestoRaddrizzata =
            "Anteprima RADDRIZZATA: l'immagine è stirata sull'area del tavolo secondo i punti di calibrazione. "
            + "Se vedi parti che non sono il tavolo, rifai la calibrazione con il pulsante «Calibra».";

        /// <summary>Testo mostrato sotto l'anteprima dell'immagine originale.</summary>
        public const string TestoGrezza = "Anteprima: immagine originale della webcam (non calibrata).";

        /// <summary>True se l'anteprima deve mostrare l'area raddrizzata: serve una calibrazione attiva e non si deve stare calibrando.</summary>
        public static bool Raddrizzata(bool calibrazioneAttiva, bool inCalibrazione)
        {
            return calibrazioneAttiva && !inCalibrazione;
        }

        /// <summary>
        /// Dimensioni dell'anteprima raddrizzata: massime entro il riquadro dato, con il rapporto del tavolo
        /// (quello dello schermo del proiettore). Un rapporto non valido ripiega su 16:9.
        /// </summary>
        public static Size Dimensioni(double aspetto, int larghezzaMassima, int altezzaMassima)
        {
            if (double.IsNaN(aspetto) || double.IsInfinity(aspetto) || aspetto <= 0)
            {
                aspetto = AspettoPredefinito;
            }

            int larghezza = Math.Max(1, larghezzaMassima);
            int altezza = Math.Max(1, (int)Math.Round(larghezza / aspetto));
            if (altezza > altezzaMassima)
            {
                altezza = Math.Max(1, altezzaMassima);
                larghezza = Math.Max(1, (int)Math.Round(altezza * aspetto));
            }

            return new Size(larghezza, altezza);
        }
    }
}
