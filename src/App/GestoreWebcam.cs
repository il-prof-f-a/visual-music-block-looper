using System;
using System.Drawing;
using OpenCvSharp;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    /// <summary>
    /// Possiede la webcam corrente e permette di cambiarla a caldo. Fa da sorgente di immagini stabile per
    /// il rilevatore: dopo un cambio di webcam non serve ricreare nulla.
    /// </summary>
    public sealed class GestoreWebcam : IFonteImmagini, IDisposable
    {
        private readonly Func<int, ISorgenteWebcam> apri;
        private readonly object blocco = new object();
        private volatile ISorgenteWebcam corrente;
        private int indiceCorrente = -1;
        private bool eliminato;

        /// <param name="apri">Apre la webcam con l'indice dato; restituisce null se non è disponibile.</param>
        public GestoreWebcam(Func<int, ISorgenteWebcam> apri)
        {
            this.apri = apri ?? throw new ArgumentNullException(nameof(apri));
        }

        /// <summary>True se una webcam è aperta.</summary>
        public bool Disponibile => corrente != null;

        /// <summary>Indice della webcam aperta, oppure -1.</summary>
        public int IndiceCorrente
        {
            get
            {
                lock (blocco)
                {
                    return corrente == null ? -1 : indiceCorrente;
                }
            }
        }

        /// <summary>Larghezza dei frame della webcam corrente (0 se assente).</summary>
        public int Larghezza => corrente?.Larghezza ?? 0;

        /// <summary>Altezza dei frame della webcam corrente (0 se assente).</summary>
        public int Altezza => corrente?.Altezza ?? 0;

        /// <summary>
        /// Apre la webcam con l'indice dato e sostituisce quella corrente. Se l'apertura fallisce la webcam
        /// precedente resta attiva. Riaprire l'indice già in uso non fa nulla.
        /// </summary>
        /// <returns>True se la webcam richiesta è ora quella corrente.</returns>
        public bool Apri(int indice)
        {
            lock (blocco)
            {
                if (eliminato)
                {
                    return false;
                }

                if (corrente != null && indiceCorrente == indice)
                {
                    return true;
                }

                ISorgenteWebcam nuova;
                try
                {
                    nuova = apri(indice);
                }
                catch (Exception)
                {
                    nuova = null;
                }

                if (nuova == null)
                {
                    return false;
                }

                ISorgenteWebcam vecchia = corrente;
                corrente = nuova;
                indiceCorrente = indice;
                vecchia?.Dispose();
                return true;
            }
        }

        /// <summary>Legge un frame dalla webcam corrente (null se assente o non disponibile).</summary>
        public Mat Frame()
        {
            return corrente?.Frame();
        }

        /// <summary>Bitmap dell'ultimo frame della webcam corrente, o null; il chiamante la rilascia.</summary>
        public Bitmap Anteprima()
        {
            return corrente?.Anteprima();
        }

        public void Dispose()
        {
            lock (blocco)
            {
                if (eliminato)
                {
                    return;
                }

                eliminato = true;
                ISorgenteWebcam vecchia = corrente;
                corrente = null;
                indiceCorrente = -1;
                vecchia?.Dispose();
            }
        }
    }
}
