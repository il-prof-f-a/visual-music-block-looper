using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Una webcam di sistema selezionabile dall'operatore.</summary>
    public sealed class InfoWebcam
    {
        public InfoWebcam(int indice)
        {
            Indice = indice;
        }

        /// <summary>Indice di sistema della webcam (0 = la prima).</summary>
        public int Indice { get; }

        /// <summary>Testo mostrato nella lista.</summary>
        public string Descrizione => "Webcam " + Indice;

        public override string ToString()
        {
            return Descrizione;
        }
    }

    /// <summary>Rileva le webcam disponibili provando gli indici di sistema in sequenza.</summary>
    public static class ElencoWebcam
    {
        /// <summary>Numero massimo di indici provati.</summary>
        public const int MassimoIndici = 8;

        /// <summary>
        /// Prova gli indici 0, 1, 2... e si ferma al primo non apribile (gli indici delle webcam sono
        /// contigui). L'indice già in uso non viene riaperto (il dispositivo risulterebbe occupato) ed è
        /// sempre incluso.
        /// </summary>
        /// <param name="apribile">Restituisce true se l'indice si apre; un'eccezione vale "non apribile".</param>
        /// <param name="indiceInUso">Indice della webcam già aperta, oppure -1.</param>
        /// <param name="massimo">Numero massimo di indici da provare.</param>
        public static IReadOnlyList<InfoWebcam> Rileva(
            Func<int, bool> apribile, int indiceInUso = -1, int massimo = MassimoIndici)
        {
            if (apribile == null)
            {
                throw new ArgumentNullException(nameof(apribile));
            }

            List<InfoWebcam> trovate = new List<InfoWebcam>();
            for (int indice = 0; indice < massimo; indice++)
            {
                if (indice != indiceInUso && !Prova(apribile, indice))
                {
                    break;
                }

                trovate.Add(new InfoWebcam(indice));
            }

            // L'indice in uso oltre il limite provato resta comunque selezionabile.
            if (indiceInUso >= massimo)
            {
                trovate.Add(new InfoWebcam(indiceInUso));
            }

            return trovate;
        }

        /// <summary>Rileva le webcam reali del sistema aprendole e rilasciandole subito.</summary>
        public static IReadOnlyList<InfoWebcam> RilevaReali(int indiceInUso = -1)
        {
            return Rileva(ApriEChiudi, indiceInUso);
        }

        private static bool Prova(Func<int, bool> apribile, int indice)
        {
            try
            {
                return apribile(indice);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool ApriEChiudi(int indice)
        {
            using (VideoCapture cattura = new VideoCapture(indice))
            {
                return cattura.IsOpened();
            }
        }
    }
}
