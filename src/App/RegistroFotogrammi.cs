using System;
using OpenCvSharp;

namespace VisualMusicBlockLooper.App
{
    public sealed class FotogrammaAcquisito : IDisposable
    {
        internal FotogrammaAcquisito(Mat immagine, long id, long istante, long generazione)
        { Immagine = immagine; Id = id; IstanteMs = istante; Generazione = generazione; }
        public Mat Immagine { get; }
        public long Id { get; }
        public long IstanteMs { get; }
        public long Generazione { get; }
        public void Dispose() => Immagine.Dispose();
    }
    /// <summary>Un solo fotogramma pubblicato; ogni lettore riceve una copia che possiede e rilascia.</summary>
    public sealed class RegistroFotogrammi : IDisposable
    {
        private readonly object blocco = new object();
        private Mat immagine;
        private long id, istante, generazione;
        private bool eliminato;
        public long NumeroFotogrammi { get { lock (blocco) return id; } }
        public long GenerazioneCorrente { get { lock (blocco) return generazione; } }
        public Size DimensioniCorrenti {get {lock(blocco)return eliminato||immagine==null?new Size():immagine.Size();}}
        public void Pubblica(Mat frame, long istanteMs)
        {
            if (frame == null || frame.Empty()) return;
            Mat copia = frame.Clone(), vecchia;
            lock (blocco)
            {
                if (eliminato) { copia.Dispose(); return; }
                if (immagine == null || immagine.Width != copia.Width || immagine.Height != copia.Height) generazione++;
                vecchia = immagine; immagine = copia; istante = istanteMs; id++;
            }
            vecchia?.Dispose();
        }
        public FotogrammaAcquisito Copia()
        {
            lock (blocco) return eliminato || immagine == null ? null : new FotogrammaAcquisito(immagine.Clone(), id, istante, generazione);
        }
        public void Dispose()
        {
            Mat vecchia;
            lock (blocco) { eliminato = true; vecchia = immagine; immagine = null; }
            vecchia?.Dispose();
        }
    }
}
