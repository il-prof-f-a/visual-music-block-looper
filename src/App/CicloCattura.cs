using System;
using System.Diagnostics;
using System.Threading;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Thread in background che esegue un'azione a frequenza fissa (circa 15 fps di default).</summary>
    public sealed class CicloCattura : IDisposable
    {
        private readonly Action passo;
        private readonly int periodoMs;
        private readonly Stopwatch orologio = Stopwatch.StartNew();
        private readonly MisuraFps misura;
        private Thread thread;
        private volatile bool fermare;

        public CicloCattura(Action passo, int periodoMs = 66)
        {
            this.passo = passo ?? throw new ArgumentNullException(nameof(passo));
            this.periodoMs = Math.Max(1, periodoMs);
            misura = new MisuraFps(() => orologio.ElapsedMilliseconds);
        }

        /// <summary>Passi completati al secondo nell'ultimo secondo (0 finché non ce ne sono almeno due); thread-safe.</summary>
        public double PassiAlSecondo => misura.Fps;

        /// <summary>Avvia il ciclo; non fa nulla se è già avviato.</summary>
        public void Avvia()
        {
            if (thread != null)
            {
                return;
            }

            fermare = false;
            thread = new Thread(Ciclo) { Name = "CicloCattura", IsBackground = true };
            thread.Start();
        }

        /// <summary>Ferma il ciclo e attende la fine del passo in corso.</summary>
        public void Ferma()
        {
            fermare = true;
            Thread corrente = thread;
            thread = null;
            if (corrente != null && corrente != Thread.CurrentThread)
            {
                corrente.Join(2000);
            }
        }

        public void Dispose()
        {
            Ferma();
        }

        private void Ciclo()
        {
            Stopwatch cronometro = new Stopwatch();
            while (!fermare)
            {
                cronometro.Restart();
                try
                {
                    passo();
                }
                catch (Exception)
                {
                    // Il ciclo di visione non deve mai terminare per un errore di un singolo frame.
                }

                // Il passo conta come completato anche se ha lanciato: misura il ritmo del ciclo, non gli errori.
                misura.Registra();

                int resto = periodoMs - (int)cronometro.ElapsedMilliseconds;
                if (resto > 0)
                {
                    Thread.Sleep(resto);
                }
            }
        }
    }
}
