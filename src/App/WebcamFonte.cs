using System;
using System.Drawing;
using System.IO;
using OpenCvSharp;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Sorgente di immagini basata su una webcam di sistema (per indice); conserva l'ultimo frame per l'anteprima.</summary>
    public sealed class WebcamFonte : ISorgenteWebcam
    {
        /// <summary>Larghezza richiesta alla webcam.</summary>
        public const int LarghezzaRichiesta = 1280;

        /// <summary>Altezza richiesta alla webcam.</summary>
        public const int AltezzaRichiesta = 720;

        private readonly object bloccoCamera = new object();
        private readonly VideoCapture cattura;
        private Mat ultimoFrame;
        private bool eliminata;

        private WebcamFonte(VideoCapture cattura, int larghezza, int altezza)
        {
            this.cattura = cattura;
            Larghezza = larghezza;
            Altezza = altezza;
        }

        /// <summary>Larghezza reale in pixel dei frame (0 se non determinabile).</summary>
        public int Larghezza { get; }

        /// <summary>Altezza reale in pixel dei frame (0 se non determinabile).</summary>
        public int Altezza { get; }

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

                // Se il driver rifiuta la risoluzione richiesta si usa quella che concede.
                cattura.Set(VideoCaptureProperties.FrameWidth, LarghezzaRichiesta);
                cattura.Set(VideoCaptureProperties.FrameHeight, AltezzaRichiesta);
                int larghezza = (int)cattura.Get(VideoCaptureProperties.FrameWidth);
                int altezza = (int)cattura.Get(VideoCaptureProperties.FrameHeight);
                if (larghezza <= 0 || altezza <= 0)
                {
                    using (Mat prova = new Mat())
                    {
                        if (cattura.Read(prova) && !prova.Empty())
                        {
                            larghezza = prova.Width;
                            altezza = prova.Height;
                        }
                    }
                }

                return new WebcamFonte(cattura, Math.Max(larghezza, 0), Math.Max(altezza, 0));
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
