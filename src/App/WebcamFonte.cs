using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using OpenCvSharp;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Sorgente di immagini basata su una webcam di sistema (per indice); conserva l'ultimo frame per l'anteprima.</summary>
    public sealed class WebcamFonte : ISorgenteWebcam, IProprietaCamera
    {
        /// <summary>Larghezza richiesta alla webcam.</summary>
        public const int LarghezzaRichiesta = 1280;

        /// <summary>Altezza richiesta alla webcam.</summary>
        public const int AltezzaRichiesta = 720;

        private readonly object bloccoCamera = new object();
        private readonly VideoCapture cattura;
        private Mat ultimoFrame;
        private bool eliminata;
        private readonly ImpostazioniWebcamLogica logica;
        private readonly Stopwatch orologio = Stopwatch.StartNew();
        private readonly MisuraFps misuraFps;

        private WebcamFonte(VideoCapture cattura, int larghezza, int altezza)
        {
            this.cattura = cattura;
            logica = new ImpostazioniWebcamLogica(this);
            misuraFps = new MisuraFps(() => orologio.ElapsedMilliseconds);
            Larghezza = larghezza;
            Altezza = altezza;
        }

        /// <summary>Larghezza reale in pixel dei frame (0 se non determinabile).</summary>
        public int Larghezza { get; }

        /// <summary>Altezza reale in pixel dei frame (0 se non determinabile).</summary>
        public int Altezza { get; }

        /// <summary>
        /// Apre la webcam con l'indice dato; restituisce null se non è disponibile. Si usa il backend
        /// DirectShow, il cui indice coincide con l'ordine dei nomi di <see cref="DispositiviDirectShow"/>;
        /// se non si apre si ripiega sul backend automatico. Se sono passate le
        /// impostazioni di fuoco ed esposizione, le applica subito dopo l'apertura.
        /// </summary>
        public static WebcamFonte Apri(int indice = 0, int larghezzaRichiesta = 0, int altezzaRichiesta = 0, ConfigurazioneWebcam impostazioni = null)
        {
            try
            {
                VideoCapture cattura = new VideoCapture(indice, VideoCaptureAPIs.DSHOW);
                if (!cattura.IsOpened())
                {
                    cattura.Dispose();
                    cattura = new VideoCapture(indice);
                }

                if (!cattura.IsOpened())
                {
                    cattura.Dispose();
                    return null;
                }

                // Se il driver rifiuta la risoluzione richiesta si usa quella che concede.
                cattura.Set(VideoCaptureProperties.FrameWidth, larghezzaRichiesta > 0 ? larghezzaRichiesta : LarghezzaRichiesta);
                cattura.Set(VideoCaptureProperties.FrameHeight, altezzaRichiesta > 0 ? altezzaRichiesta : AltezzaRichiesta);
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

                WebcamFonte fonte = new WebcamFonte(cattura, Math.Max(larghezza, 0), Math.Max(altezza, 0));
                if (impostazioni != null)
                {
                    fonte.ApplicaImpostazioni(impostazioni);
                }

                return fonte;
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
                    misuraFps.Registra();
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

                return ABitmap(ultimoFrame);
            }
        }

        /// <summary>Bitmap dell'ultimo frame raddrizzato sull'area calibrata del tavolo, o null; il chiamante la rilascia.</summary>
        public Bitmap AnteprimaRaddrizzata(Calibrazione calibrazione, int larghezza, int altezza)
        {
            if (calibrazione == null)
            {
                return null;
            }

            lock (bloccoCamera)
            {
                if (eliminata || ultimoFrame == null || ultimoFrame.Empty())
                {
                    return null;
                }

                using (Mat raddrizzato = calibrazione.Raddrizza(ultimoFrame, larghezza, altezza))
                {
                    return ABitmap(raddrizzato);
                }
            }
        }

        /// <summary>Frame realmente consegnati da <see cref="Frame"/> al secondo, nell'ultima finestra.</summary>
        public double FpsCatturati => misuraFps.Fps;

        /// <summary>Applica solo i campi non null di fuoco ed esposizione (vedi <see cref="ImpostazioniWebcamLogica"/>).</summary>
        public void ApplicaImpostazioni(ConfigurazioneWebcam impostazioni)
        {
            logica.Applica(impostazioni);
        }

        /// <summary>Valori effettivi riletti dalla webcam e supporto reale di fuoco ed esposizione.</summary>
        public StatoImpostazioniWebcam LeggiImpostazioni()
        {
            return logica.Leggi();
        }

        /// <summary>Imposta una proprietà OpenCV della webcam (false se eliminata o in caso di errore).</summary>
        bool IProprietaCamera.Imposta(ProprietaCamera proprieta, double valore)
        {
            lock (bloccoCamera)
            {
                if (eliminata)
                {
                    return false;
                }

                try
                {
                    return cattura.Set(Converti(proprieta), valore);
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>Legge una proprietà OpenCV della webcam (NaN se eliminata o in caso di errore).</summary>
        double IProprietaCamera.Leggi(ProprietaCamera proprieta)
        {
            lock (bloccoCamera)
            {
                if (eliminata)
                {
                    return double.NaN;
                }

                try
                {
                    return cattura.Get(Converti(proprieta));
                }
                catch (Exception)
                {
                    return double.NaN;
                }
            }
        }

        private static VideoCaptureProperties Converti(ProprietaCamera proprieta)
        {
            switch (proprieta)
            {
                case ProprietaCamera.Fuoco:
                    return VideoCaptureProperties.Focus;
                case ProprietaCamera.FuocoAutomatico:
                    return VideoCaptureProperties.AutoFocus;
                case ProprietaCamera.Esposizione:
                    return VideoCaptureProperties.Exposure;
                case ProprietaCamera.EsposizioneAutomatica:
                    return VideoCaptureProperties.AutoExposure;
                default:
                    return VideoCaptureProperties.Fps;
            }
        }

        private static Bitmap ABitmap(Mat immagine)
        {
            Cv2.ImEncode(".bmp", immagine, out byte[] byteImmagine);
            using (MemoryStream flusso = new MemoryStream(byteImmagine))
            using (Bitmap temporanea = new Bitmap(flusso))
            {
                return new Bitmap(temporanea);
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
