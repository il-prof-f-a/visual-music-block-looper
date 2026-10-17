using System;
using System.Diagnostics;
using System.Threading;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.App
{
    /// <summary>
    /// Thread ad alta priorità che genera i tick di clock interno a <see cref="Tempo.IntervalloTickMs"/>
    /// e controlla periodicamente il timeout del clock esterno.
    /// </summary>
    public sealed class TickMaster : IDisposable
    {
        private const double PeriodoTimeoutMs = 100;
        private const double SogliaRitardoInIntervalli = 4;

        private readonly MotoreMusicale motore;
        private readonly Func<long> orologioMs;
        private Thread thread;
        private volatile bool fermare;

        /// <param name="orologioMs">Stesso orologio del motore, usato per il controllo del timeout del clock.</param>
        public TickMaster(MotoreMusicale motore, Func<long> orologioMs)
        {
            this.motore = motore ?? throw new ArgumentNullException(nameof(motore));
            this.orologioMs = orologioMs ?? throw new ArgumentNullException(nameof(orologioMs));
        }

        /// <summary>Avvia il thread; non fa nulla se è già avviato.</summary>
        public void Avvia()
        {
            if (thread != null)
            {
                return;
            }

            fermare = false;
            thread = new Thread(Ciclo)
            {
                Name = "TickMaster",
                IsBackground = true,
                Priority = ThreadPriority.Highest
            };
            thread.Start();
        }

        /// <summary>Ferma il thread e attende la sua terminazione.</summary>
        public void Ferma()
        {
            fermare = true;
            Thread corrente = thread;
            thread = null;
            if (corrente != null && corrente != Thread.CurrentThread)
            {
                corrente.Join(1000);
            }
        }

        public void Dispose()
        {
            Ferma();
        }

        private void Ciclo()
        {
            Stopwatch cronometro = Stopwatch.StartNew();
            double prossimoTick = 0;
            double prossimoTimeout = PeriodoTimeoutMs;

            while (!fermare)
            {
                double adesso = cronometro.Elapsed.TotalMilliseconds;

                if (adesso >= prossimoTick)
                {
                    double intervallo = Tempo.IntervalloTickMs(motore.Bpm);
                    try
                    {
                        motore.TickInterno();
                    }
                    catch (Exception)
                    {
                        // Un errore MIDI momentaneo non deve fermare il clock.
                    }

                    // Accumulatore: la scadenza successiva non dipende dal ritardo effettivo (niente deriva).
                    prossimoTick += intervallo;
                    if (adesso - prossimoTick > SogliaRitardoInIntervalli * intervallo)
                    {
                        prossimoTick = adesso + intervallo;
                    }
                }

                if (adesso >= prossimoTimeout)
                {
                    try
                    {
                        motore.ControllaTimeoutClock(orologioMs());
                    }
                    catch (Exception)
                    {
                        // Come sopra: il controllo verrà ripetuto al giro successivo.
                    }

                    prossimoTimeout = adesso + PeriodoTimeoutMs;
                }

                if (prossimoTick - cronometro.Elapsed.TotalMilliseconds > 2)
                {
                    Thread.Sleep(1);
                }
                else
                {
                    Thread.SpinWait(50);
                }
            }
        }
    }
}
