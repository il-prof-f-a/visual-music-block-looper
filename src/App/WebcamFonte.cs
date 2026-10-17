using System;
using System.Drawing;
using System.IO;
using OpenCvSharp;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Sorgente di immagini basata sulla prima webcam; conserva l'ultimo frame per l'anteprima.</summary>
    public sealed class WebcamFonte : IFonteImmagini, IDisposable
    {
        private readonly object bloccoCamera = new object();
        private readonly VideoCapture cattura;
        private Mat ultimoFrame;
        private bool eliminata;

        private WebcamFonte(VideoCapture cattura)
        {
            this.cattura = cattura;
        }

        /// <summary>Apre la webcam con l'indice dato; restituisce null se non è disponibile.</summary>
        public static WebcamFonte Apri(int indice = 0)
        {
            try
            {
                VideoCapture cattura = new VideoCapture(indice);
                if (!cattura.IsOpened())
                {
                    cattura.Dispose();
                    return null;
                }

                return new WebcamFonte(cattura);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Legge un nuovo frame e ne restituisce una copia (null se non disponibile).</summary>
        public Mat Frame()
        {
            lock (bloccoCamera)
            {
                if (eliminata)
                {
                    return null;
                }

                using (Mat letto = new Mat())
                {
                    if (!cattura.Read(letto) || letto.Empty())
                    {
                        return null;
                    }

                    ultimoFrame?.Dispose();
                    ultimoFrame = letto.Clone();
                    return letto.Clone();
                }
            }
        }

        /// <summary>Restituisce una Bitmap dell'ultimo frame letto, o null; il chiamante la rilascia.</summary>
        public Bitmap Anteprima()
        {
            lock (bloccoCamera)
            {
                if (eliminata || ultimoFrame == null || ultimoFrame.Empty())
                {
                    return null;
                }

                Cv2.ImEncode(".bmp", ultimoFrame, out byte[] byteImmagine);
                using (MemoryStream flusso = new MemoryStream(byteImmagine))
                using (Bitmap temporanea = new Bitmap(flusso))
                {
                    return new Bitmap(temporanea);
                }
            }
        }

        public void Dispose()
        {
            lock (bloccoCamera)
            {
                if (eliminata)
                {
                    return;
                }

                eliminata = true;
                ultimoFrame?.Dispose();
                cattura.Dispose();
            }
        }
    }
}
