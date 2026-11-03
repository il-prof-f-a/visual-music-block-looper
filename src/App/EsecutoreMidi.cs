using System;
using System.Threading;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Unico consumatore/inviatore MIDI; UI, ottica e callback sono soltanto produttori di richieste.</summary>
    public sealed class EsecutoreMidi : IMotoreMusicale, IDisposable
    {
        private enum Tipo { Play, Tap, Bpm, Modo, OneShot, ClockEsterno, StartEsterno, ContinueEsterno, StopEsterno }
        private readonly struct Richiesta
        {
            public Richiesta(Tipo tipo, int parametro, long istante, long sequenza,double bpm)
            { Tipo = tipo; Parametro = parametro; Istante = istante; Sequenza = sequenza;Bpm=bpm; }
            public readonly double Bpm;
            public readonly Tipo Tipo;
            public readonly int Parametro;
            public readonly long Istante, Sequenza;
        }
        private readonly CodaLimitata<Richiesta> coda = new CodaLimitata<Richiesta>(4096);
        private readonly IUscitaMidi uscita;
        private readonly IIngressoMidi ingresso;
        private readonly CompilatorePartitura compilatore;
        private readonly Func<long> orologioMs;
        private readonly Action clockRicevuto, startRicevuto, stopRicevuto, continueRicevuto;
        private long sequenza, ultimoStop, ultimoStopEsterno, versione;
        private long ultimoIngressoClock = -1;
        private double intervalloEsternoMs = 60000.0 / (120 * 24);
        private int proprietario, stopUrgente, stopEsternoUrgente, eliminato;
        private string ultimoErrore;
        public EsecutoreMidi(IUscitaMidi uscita, IIngressoMidi ingresso, IMappaturaMidi mappatura, ConfigGriglia config, Func<long> orologioMs, double bpmIniziale = 120)
        {
            this.uscita = uscita ?? throw new ArgumentNullException(nameof(uscita));
            this.ingresso = ingresso; this.orologioMs = orologioMs ?? throw new ArgumentNullException(nameof(orologioMs));
            Stato = new MotorePartitura(uscita, mappatura, bpmIniziale, orologioMs);
            intervalloEsternoMs = Tempo.IntervalloTickMs(Stato.Bpm);
            compilatore = new CompilatorePartitura(config, mappatura);
            clockRicevuto = () =>
            {
                long ora = orologioMs(), prima = Interlocked.Exchange(ref ultimoIngressoClock, ora);
                long delta = ora - prima;
                if (prima >= 0 && delta >= 5 && delta <= 200)
                    Volatile.Write(ref intervalloEsternoMs, Volatile.Read(ref intervalloEsternoMs) * .75 + delta * .25);
                Richiedi(Tipo.ClockEsterno, 0, ora);
            };
            startRicevuto = () => Richiedi(Tipo.StartEsterno, 0, orologioMs());
            stopRicevuto = () => RichiediStop(true);
            continueRicevuto = () => Richiedi(Tipo.ContinueEsterno, 0, orologioMs());
            if (ingresso != null)
            { ingresso.ClockRicevuto += clockRicevuto; ingresso.StartRicevuto += startRicevuto; ingresso.StopRicevuto += stopRicevuto; ingresso.ContinueRicevuto += continueRicevuto; }
        }
        public MotorePartitura Stato { get; }
        public string UltimoErrore => Volatile.Read(ref ultimoErrore);
        public ModoSincronia Modo => Stato.Modo;
        public bool InEsecuzione => Stato.InEsecuzione;
        public double Bpm => Modo == ModoSincronia.Slave ? Tempo.Limita(60000.0 / (24 * Volatile.Read(ref intervalloEsternoMs))) : Stato.Bpm;
        public PosizioneGiro Posizione => Stato.Posizione;
        public int StepCorrente => Stato.StepCorrente;
        public StrutturaTavolo Struttura => Stato.Struttura;
        public void Avvia() => Richiedi(Tipo.Play);
        public void Arresta() => RichiediStop();
        public void Silenzia() => RichiediStop();
        public void CambiaModo() => Richiedi(Tipo.Modo);
        public void Tap(long istante) => Richiedi(Tipo.Tap, 0, istante);
        public void ImpostaBpm(double bpm)=>Richiedi(Tipo.Bpm,bpm:Tempo.Limita(bpm));
        public void OneShot(int nota) => Richiedi(Tipo.OneShot, nota);
        public PartituraMidiPreparata Prepara(StatoTavolo foglio, long versioneFoglio, long generazione, long frontiera)
            => PartituraMidiPreparata.Prepara(compilatore.Compila(foglio, versioneFoglio, generazione, frontiera), uscita);
        public bool Pubblica(PartituraMidiPreparata p) => Volatile.Read(ref eliminato) == 0 && Stato.Pubblica(p);
        public void AggiornaTavolo(StatoTavolo foglio)
        {
            if (foglio == null) return;
            Pubblica(Prepara(foglio, Interlocked.Increment(ref versione), 1, InEsecuzione ? Stato.Giro + 1 : 0));
            InviaComandi(foglio.Comandi, orologioMs());
        }
        public void InviaComandi(System.Collections.Generic.IReadOnlyList<ComandoRilevato> comandi, long istante)
        {
            for (int i = 0; i < comandi.Count; i++)
            {
                var c = comandi[i]; if (c == null) continue;
                switch (c.Tipo)
                {
                    case TipoComando.Play: Avvia(); break;
                    case TipoComando.Stop: Arresta(); break;
                    case TipoComando.Modo: CambiaModo(); break;
                    case TipoComando.Tap: Tap(istante); break;
                    case TipoComando.OneShot: OneShot(c.Parametro); break;
                }
            }
        }
        private void Richiedi(Tipo tipo, int parametro = 0, long istante = 0,double bpm=0)
        {
            if (Volatile.Read(ref eliminato) != 0) return;
            var r = new Richiesta(tipo, parametro, istante, Interlocked.Increment(ref sequenza),bpm);
            if (!coda.ProvaInserisci(r)) { Volatile.Write(ref ultimoErrore, "Coda MIDI piena: arresto controllato."); RichiediStop(); }
        }
        private void RichiediStop(bool esterno = false)
        {
            long nuovo = Interlocked.Increment(ref sequenza), prima;
            if (esterno)
            {
                do { prima = Volatile.Read(ref ultimoStopEsterno); if (prima >= nuovo) break; }
                while (Interlocked.CompareExchange(ref ultimoStopEsterno, nuovo, prima) != prima);
                Interlocked.Exchange(ref stopEsternoUrgente, 1);
            }
            else
            {
                do { prima = Volatile.Read(ref ultimoStop); if (prima >= nuovo) break; }
                while (Interlocked.CompareExchange(ref ultimoStop, nuovo, prima) != prima);
                Interlocked.Exchange(ref stopUrgente, 1);
            }
        }
        private void ApplicaArrestiUrgenti()
        {
            if (Interlocked.Exchange(ref stopUrgente, 0) != 0) Stato.Arresta();
            if (Interlocked.Exchange(ref stopEsternoUrgente, 0) != 0) Stato.RiceviStop();
        }
        private void AssumiProprieta()
        {
            int id = Thread.CurrentThread.ManagedThreadId;
            int prima = Interlocked.CompareExchange(ref proprietario, id, 0);
            if (prima != 0 && prima != id) throw new InvalidOperationException("L'invio MIDI ha già un altro proprietario.");
        }
        public void ElaboraPendenti()
        {
            AssumiProprieta();
            ApplicaArrestiUrgenti();
            Richiesta r; int elaborati = 0;
            // La coda è limitata: anche uno Stop con coda piena non lascia Play/OneShot antecedenti da riavviare.
            for (int i = 0; i < 4096 && elaborati < 64 && coda.ProvaEstrai(out r); i++)
            {
                ApplicaArrestiUrgenti();
                bool controllo = r.Tipo == Tipo.Modo || r.Tipo == Tipo.Tap || r.Tipo==Tipo.Bpm;
                if (!controllo && r.Sequenza <= Volatile.Read(ref ultimoStop)) continue;
                bool ingressoEsterno = r.Tipo == Tipo.ClockEsterno || r.Tipo == Tipo.StartEsterno || r.Tipo == Tipo.ContinueEsterno;
                if (!controllo && r.Sequenza <= Volatile.Read(ref ultimoStopEsterno) && (ingressoEsterno || Modo == ModoSincronia.Slave)) continue;
                elaborati++;
                switch (r.Tipo)
                {
                    case Tipo.Play: Stato.Avvia(); break;
                    case Tipo.Tap: Stato.Tap(r.Istante); break;
                    case Tipo.Bpm: Stato.ImpostaBpm(r.Bpm);break;
                    case Tipo.Modo: Stato.CambiaModo(); break;
                    case Tipo.OneShot: Stato.OneShot(r.Parametro); break;
                    case Tipo.ClockEsterno:
                        if (Modo != ModoSincronia.Slave) break;
                        if (Stato.InEsecuzione && orologioMs() - r.Istante >= Volatile.Read(ref intervalloEsternoMs))
                        { PerditaSincronia("Clock esterno in ritardo: arresto controllato."); RichiediStop(); }
                        else Stato.RiceviClock(r.Istante);
                        break;
                    case Tipo.StartEsterno: Stato.RiceviStart(); break;
                    case Tipo.ContinueEsterno: Stato.RiceviContinue(); break;
                    case Tipo.StopEsterno: Stato.RiceviStop(); break;
                }
            }
        }
        public void TickInterno() { ElaboraPendenti(); Stato.TickInterno(); }
        public void ControllaTimeoutClock(long adesso) { AssumiProprieta(); Stato.ControllaTimeoutClock(adesso); }
        public void PerditaSincronia(string motivo)
        { AssumiProprieta(); Volatile.Write(ref ultimoErrore, motivo); Stato.Arresta(); }
        public void CompletaArresto()
        {
            AssumiProprieta();
            try { Stato.Arresta(); }
            finally { Interlocked.CompareExchange(ref proprietario, 0, Thread.CurrentThread.ManagedThreadId); }
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref eliminato, 1) != 0) return;
            if (ingresso != null)
            { ingresso.ClockRicevuto -= clockRicevuto; ingresso.StartRicevuto -= startRicevuto; ingresso.StopRicevuto -= stopRicevuto; ingresso.ContinueRicevuto -= continueRicevuto; }
            if (Volatile.Read(ref proprietario) == Thread.CurrentThread.ManagedThreadId) Stato.Arresta();
            else RichiediStop();
        }
    }
}
