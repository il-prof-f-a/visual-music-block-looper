using System;
using System.Diagnostics;
using System.Threading;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Clock a scadenze assolute. Camera, compilazione e notifiche UI non girano su questo thread.</summary>
    public sealed class TickMaster : IDisposable
    {
        private readonly IMotoreMusicale motore;
        private readonly Func<long> orologioMs;
        private readonly Action dopoTick;
        private Thread thread;
        private Timer notifiche;
        private volatile bool fermare;
        private int notificaInCorso;
        private long tickEmessi, ultimoNotificato;
        private string ultimoErrore;
        public TickMaster(IMotoreMusicale motore, Func<long> orologioMs, Action dopoTick = null)
        {
            this.motore = motore ?? throw new ArgumentNullException(nameof(motore));
            this.orologioMs = orologioMs ?? throw new ArgumentNullException(nameof(orologioMs));
            this.dopoTick = dopoTick;
        }
        public RegistroTempiMidi RegistroTempi { get; } = new RegistroTempiMidi();
        public bool MmcssAttiva { get; private set; }
        public string UltimoErrore => Volatile.Read(ref ultimoErrore);
        public void Avvia()
        {
            if (thread != null) return;
            fermare = false;
            if (dopoTick != null) notifiche = new Timer(Notifica, null, 15, 15);
            thread = new Thread(Ciclo) { Name = "Esecutore MIDI", IsBackground = true, Priority = ThreadPriority.Highest };
            thread.Start();
        }
        public void Ferma()
        {
            fermare = true; notifiche?.Dispose(); notifiche = null;
            Thread corrente = thread;
            if (corrente == null || corrente == Thread.CurrentThread) return;
            if (!corrente.Join(2000)) throw new TimeoutException("L'uscita MIDI non ha terminato l'invio in corso.");
            thread = null;
        }
        public void Dispose() => Ferma();
        private void Notifica(object _)
        {
            if (fermare || Interlocked.Exchange(ref notificaInCorso, 1) != 0) return;
            try
            {
                long n = Volatile.Read(ref tickEmessi);
                if (n != ultimoNotificato) { ultimoNotificato = n; dopoTick(); }
            }
            catch (Exception) { }
            finally { Volatile.Write(ref notificaInCorso, 0); }
        }
        private void Ciclo()
        {
            bool timerRichiesto = RisoluzioneTimer.Richiedi();
            var proprietario = motore as EsecutoreMidi;
            using (var priorita = new PrioritaMultimediale())
            {
                MmcssAttiva = priorita.Attiva;
                try { CicloInterno(proprietario); }
                catch (Exception e) { Volatile.Write(ref ultimoErrore, e.GetType().Name + ": " + e.Message); }
                finally
                {
                    try { proprietario?.CompletaArresto(); }
                    catch (Exception e) { Volatile.Write(ref ultimoErrore, "Arresto MIDI: " + e.Message); }
                    if (timerRichiesto) RisoluzioneTimer.Rilascia();
                }
            }
        }
        private void CicloInterno(EsecutoreMidi proprietario)
        {
            double frequenzaMs = Stopwatch.Frequency / 1000.0;
            double prossimoTick = Stopwatch.GetTimestamp(), prossimoTimeout = prossimoTick + 100 * frequenzaMs;
            ModoSincronia modoPrima = motore.Modo;
            while (!fermare)
            {
                proprietario?.ElaboraPendenti();
                long adesso = Stopwatch.GetTimestamp();
                if (modoPrima != motore.Modo) { modoPrima = motore.Modo; prossimoTick = adesso; }
                if (adesso >= prossimoTimeout)
                { motore.ControllaTimeoutClock(orologioMs()); prossimoTimeout = adesso + 100 * frequenzaMs; }
                if (motore.Modo == ModoSincronia.Slave) { Thread.Sleep(1); continue; }
                if (adesso >= prossimoTick)
                {
                    long previsto = (long)prossimoTick;
                    double periodo = Tempo.IntervalloTickMs(motore.Bpm) * frequenzaMs;
                    if (adesso - prossimoTick >= periodo)
                    {
                        if (proprietario != null && motore.InEsecuzione)
                            proprietario.PerditaSincronia("Clock in ritardo: arresto controllato, premi Avvia per ripartire.");
                        prossimoTick = adesso;
                    }
                    long inizio = Stopwatch.GetTimestamp(); motore.TickInterno(); long fine = Stopwatch.GetTimestamp();
                    long n = tickEmessi + 1;
                    RegistroTempi.Registra(n, previsto, inizio, fine); Volatile.Write(ref tickEmessi, n);
                    prossimoTick += Tempo.IntervalloTickMs(motore.Bpm) * frequenzaMs;
                }
                if (prossimoTick - Stopwatch.GetTimestamp() > 2 * frequenzaMs) Thread.Sleep(1);
                else Thread.SpinWait(50);
            }
        }
    }
}
