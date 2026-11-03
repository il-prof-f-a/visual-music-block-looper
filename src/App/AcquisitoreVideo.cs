using System;
using System.Threading;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    public sealed class AcquisitoreVideo : IDisposable
    {
        private readonly IFonteImmagini fonte;
        private readonly Func<long> orologioMs;
        private Thread thread;
        private volatile bool fermare;
        private string ultimoErrore;
        public AcquisitoreVideo(IFonteImmagini fonte, Func<long> orologioMs)
        { this.fonte = fonte ?? throw new ArgumentNullException(nameof(fonte)); this.orologioMs = orologioMs ?? throw new ArgumentNullException(nameof(orologioMs)); }
        public RegistroFotogrammi Registro { get; } = new RegistroFotogrammi();
        public string UltimoErrore => Volatile.Read(ref ultimoErrore);
        public void Avvia()
        {
            if (thread != null) return;
            fermare = false;
            thread = new Thread(Ciclo) { Name = "Acquisitore video", IsBackground = true };
            thread.Start();
        }
        private void Ciclo()
        {
            while (!fermare)
            {
                try
                {
                    using (var f = fonte.Frame())
                    {
                        if (f != null && !f.Empty()) { Registro.Pubblica(f, orologioMs()); Volatile.Write(ref ultimoErrore, null); }
                    }
                }
                catch (Exception e) { Volatile.Write(ref ultimoErrore, e.GetType().Name + ": " + e.Message); }
                Thread.Sleep(20);
            }
        }
        public void Ferma()
        {
            fermare = true;
            Thread t = thread;
            if (t != null && t != Thread.CurrentThread && !t.Join(2000))
                throw new TimeoutException("La lettura della webcam non è terminata.");
            thread = null;
        }
        public void Dispose() { Ferma(); Registro.Dispose(); }
    }
}
