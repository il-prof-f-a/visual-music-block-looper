using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>
    /// Motore musicale: applica al sequencer i comandi del tavolo e pilota il trasporto in modalità
    /// Master (clock interno) o Slave (clock esterno del DAW). Tutti i metodi sono sincronizzati
    /// perché gli eventi MIDI arrivano da altri thread.
    /// </summary>
    public class MotoreMusicale
    {
        /// <summary>Tick di clock dopo i quali un one-shot viene spento.</summary>
        private const int TickOneShot = 6;

        /// <summary>Intervallo senza clock esterno oltre il quale lo Slave ferma la riproduzione.</summary>
        private const long TimeoutClockMs = 500;

        private readonly object bloccoLock = new object();
        private readonly Sequencer sequencer;
        private readonly IUscitaMidi uscita;
        private readonly IMappaturaMidi mappatura;
        private readonly Func<long> orologioMs;
        private readonly TapTempo tapTempo = new TapTempo();
        private readonly List<OneShotPendente> oneShotPendenti = new List<OneShotPendente>();
        private List<CartoncinoNota> noteCorrenti = new List<CartoncinoNota>();
        private long ultimoClock;

        /// <summary>
        /// Crea il motore. <paramref name="ingresso"/> può essere null: in tal caso non riceve eventi esterni.
        /// </summary>
        /// <param name="orologioMs">Sorgente del tempo in millisecondi; di default un cronometro interno.</param>
        /// <exception cref="ArgumentNullException">Lanciata se sequencer, uscita o mappatura sono null.</exception>
        public MotoreMusicale(
            Sequencer sequencer,
            IUscitaMidi uscita,
            IIngressoMidi ingresso,
            IMappaturaMidi mappatura,
            double bpmIniziale = 120,
            Func<long> orologioMs = null)
        {
            this.sequencer = sequencer ?? throw new ArgumentNullException(nameof(sequencer));
            this.uscita = uscita ?? throw new ArgumentNullException(nameof(uscita));
            this.mappatura = mappatura ?? throw new ArgumentNullException(nameof(mappatura));
            if (orologioMs == null)
            {
                Stopwatch cronometro = Stopwatch.StartNew();
                orologioMs = () => cronometro.ElapsedMilliseconds;
            }

            this.orologioMs = orologioMs;
            Bpm = Tempo.Limita(bpmIniziale);

            if (ingresso != null)
            {
                ingresso.ClockRicevuto += OnClockRicevuto;
                ingresso.StartRicevuto += OnStartRicevuto;
                ingresso.StopRicevuto += OnStopRicevuto;
                ingresso.ContinueRicevuto += OnContinueRicevuto;
            }
        }

        /// <summary>Modalità di sincronia corrente.</summary>
        public ModoSincronia Modo { get; private set; } = ModoSincronia.Master;

        /// <summary>True se il sequencer sta avanzando.</summary>
        public bool InEsecuzione { get; private set; }

        /// <summary>BPM corrente, sempre limitato a [<see cref="Tempo.MinBpm"/>, <see cref="Tempo.MaxBpm"/>].</summary>
        public double Bpm { get; private set; }

        /// <summary>Aggiorna le note e applica i comandi dello stato del tavolo. Uno stato null equivale a tavolo vuoto.</summary>
        public void AggiornaTavolo(StatoTavolo stato)
        {
            lock (bloccoLock)
            {
                noteCorrenti = stato == null ? new List<CartoncinoNota>() : new List<CartoncinoNota>(stato.Note);
                if (stato == null)
                {
                    return;
                }

                foreach (ComandoRilevato comando in stato.Comandi)
                {
                    if (comando == null)
                    {
                        continue;
                    }

                    switch (comando.Tipo)
                    {
                        case TipoComando.Play:
                            Avvia();
                            break;
                        case TipoComando.Stop:
                            Arresta();
                            break;
                        case TipoComando.Tap:
                            Tap(orologioMs());
                            break;
                        case TipoComando.Modo:
                            CambiaModo();
                            break;
                        case TipoComando.OneShot:
                            SuonaOneShot(comando.Parametro);
                            break;
                    }
                }
            }
        }

        /// <summary>Avanza di un tick di clock interno (solo in Master): invia il clock e, se in esecuzione, il sequencer.</summary>
        public void TickInterno()
        {
            lock (bloccoLock)
            {
                if (Modo != ModoSincronia.Master)
                {
                    return;
                }

                uscita.Clock();
                if (InEsecuzione)
                {
                    AvanzaSequencer();
                }

                AvanzaOneShot();
            }
        }

        /// <summary>Registra un tap per il calcolo del BPM (solo in Master).</summary>
        /// <param name="millisecondi">Istante del tap in millisecondi.</param>
        public void Tap(long millisecondi)
        {
            lock (bloccoLock)
            {
                if (Modo != ModoSincronia.Master)
                {
                    return;
                }

                double? bpm = tapTempo.Tap(millisecondi);
                if (bpm.HasValue)
                {
                    Bpm = bpm.Value;
                }
            }
        }

        /// <summary>
        /// Controlla che il clock esterno sia ancora presente: in Slave, se in esecuzione e
        /// il clock manca da più di 500 ms, ferma la riproduzione e spegne le note.
        /// </summary>
        /// <param name="adessoMs">Istante corrente in millisecondi.</param>
        public void ControllaTimeoutClock(long adessoMs)
        {
            lock (bloccoLock)
            {
                if (Modo == ModoSincronia.Slave && InEsecuzione && adessoMs - ultimoClock > TimeoutClockMs)
                {
                    InEsecuzione = false;
                    FermaESpegni();
                }
            }
        }

        private void Avvia()
        {
            if (Modo != ModoSincronia.Master || InEsecuzione)
            {
                return;
            }

            InEsecuzione = true;
            uscita.Start();
        }

        private void Arresta()
        {
            if (Modo != ModoSincronia.Master || !InEsecuzione)
            {
                return;
            }

            InEsecuzione = false;
            FermaESpegni();
            uscita.Stop();
        }

        private void CambiaModo()
        {
            bool eraMaster = Modo == ModoSincronia.Master;
            Modo = eraMaster ? ModoSincronia.Slave : ModoSincronia.Master;
            ultimoClock = orologioMs();

            if (InEsecuzione)
            {
                InEsecuzione = false;
                FermaESpegni();
                if (eraMaster)
                {
                    uscita.Stop();
                }
            }

            SpegniOneShotPendenti();
        }

        private void SuonaOneShot(int nota)
        {
            int canale = mappatura.Canale(0);
            uscita.NotaOn(canale, nota, mappatura.Velocity);

            OneShotPendente pendente = oneShotPendenti.Find(p => p.Nota == nota);
            if (pendente != null)
            {
                pendente.TickRimanenti = TickOneShot;
                return;
            }

            oneShotPendenti.Add(new OneShotPendente(canale, nota, TickOneShot));
        }

        private void AvanzaOneShot()
        {
            List<OneShotPendente> scaduti = new List<OneShotPendente>();
            foreach (OneShotPendente pendente in oneShotPendenti)
            {
                pendente.TickRimanenti--;
                if (pendente.TickRimanenti <= 0)
                {
                    scaduti.Add(pendente);
                }
            }

            foreach (OneShotPendente scaduto in scaduti)
            {
                uscita.NotaOff(scaduto.Canale, scaduto.Nota);
                oneShotPendenti.Remove(scaduto);
            }
        }

        private void SpegniOneShotPendenti()
        {
            foreach (OneShotPendente pendente in oneShotPendenti)
            {
                uscita.NotaOff(pendente.Canale, pendente.Nota);
            }

            oneShotPendenti.Clear();
        }

        private void AvanzaSequencer()
        {
            foreach (EventoNota evento in sequencer.Tick(noteCorrenti))
            {
                int canale = mappatura.Canale(evento.Traccia);
                int nota = mappatura.Nota(evento.IdClip);
                if (evento.NoteOn)
                {
                    uscita.NotaOn(canale, nota, mappatura.Velocity);
                }
                else
                {
                    uscita.NotaOff(canale, nota);
                }
            }
        }

        /// <summary>Ferma il sequencer e invia i note-off delle note che stavano suonando.</summary>
        private void FermaESpegni()
        {
            sequencer.Ferma(out IReadOnlyList<EventoNota> noteOff);
            foreach (EventoNota evento in noteOff)
            {
                uscita.NotaOff(mappatura.Canale(evento.Traccia), mappatura.Nota(evento.IdClip));
            }
        }

        private void OnClockRicevuto()
        {
            lock (bloccoLock)
            {
                if (Modo != ModoSincronia.Slave)
                {
                    return;
                }

                ultimoClock = orologioMs();
                if (InEsecuzione)
                {
                    AvanzaSequencer();
                }

                AvanzaOneShot();
            }
        }

        private void OnStartRicevuto()
        {
            lock (bloccoLock)
            {
                if (Modo != ModoSincronia.Slave)
                {
                    return;
                }

                FermaESpegni();
                InEsecuzione = true;
                ultimoClock = orologioMs();
            }
        }

        private void OnContinueRicevuto()
        {
            lock (bloccoLock)
            {
                if (Modo != ModoSincronia.Slave)
                {
                    return;
                }

                InEsecuzione = true;
                ultimoClock = orologioMs();
            }
        }

        private void OnStopRicevuto()
        {
            lock (bloccoLock)
            {
                if (Modo != ModoSincronia.Slave)
                {
                    return;
                }

                InEsecuzione = false;
                FermaESpegni();
            }
        }

        /// <summary>Nota one-shot in attesa del proprio note-off.</summary>
        private class OneShotPendente
        {
            public OneShotPendente(int canale, int nota, int tickRimanenti)
            {
                Canale = canale;
                Nota = nota;
                TickRimanenti = tickRimanenti;
            }

            public int Canale { get; }

            public int Nota { get; }

            public int TickRimanenti { get; set; }
        }
    }
}
