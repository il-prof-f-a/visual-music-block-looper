using System;
using System.Collections.Generic;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.App
{
    /// <summary>
    /// Compone la catena di elaborazione: rilevamento, interpretazione del tavolo, stabilizzazione
    /// e aggiornamento del motore musicale. Non contiene UI né thread propri.
    /// </summary>
    public class Compositore
    {
        /// <summary>Zona comandi di default (coordinate normalizzate del tavolo).</summary>
        public static readonly RettangoloTavolo ZonaComandiDefault = new RettangoloTavolo(0.84, 0.04, 0.14, 0.92);

        private readonly IRilevatoreCartoncini rilevatore;
        private readonly InterpreteTavolo interprete;
        private readonly StabilizzatoreTavolo stabilizzatore = new StabilizzatoreTavolo();
        private readonly object bloccoStato = new object();
        private StatoTavolo ultimoStato = new StatoTavolo(null, null, null);

        /// <summary>Crea il compositore e il relativo motore musicale.</summary>
        /// <param name="ingresso">Ingresso MIDI; può essere null.</param>
        /// <param name="layout">Zone speciali del tavolo; se null si usa solo la zona comandi di default (<see cref="ZonaComandiDefault"/>).</param>
        /// <param name="orologioMs">Orologio in millisecondi per il motore; se null un cronometro interno.</param>
        /// <exception cref="ArgumentNullException">Lanciata se rilevatore, uscita, mappatura o config sono null.</exception>
        public Compositore(
            IRilevatoreCartoncini rilevatore,
            IUscitaMidi uscita,
            IIngressoMidi ingresso,
            IMappaturaMidi mappatura,
            ConfigGriglia config,
            LayoutTavolo layout = null,
            Func<long> orologioMs = null)
        {
            this.rilevatore = rilevatore ?? throw new ArgumentNullException(nameof(rilevatore));
            if (uscita == null)
            {
                throw new ArgumentNullException(nameof(uscita));
            }

            if (mappatura == null)
            {
                throw new ArgumentNullException(nameof(mappatura));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            Layout = layout ?? LayoutTavolo.ConSoloComandi(ZonaComandiDefault);
            interprete = new InterpreteTavolo(new Griglia(config), Layout);
            Motore = new MotoreMusicale(new Sequencer(config), uscita, ingresso, mappatura, 120, orologioMs);
        }

        /// <summary>Zone speciali del tavolo usate per interpretare i cartoncini.</summary>
        public LayoutTavolo Layout { get; }

        /// <summary>Motore musicale pilotato dal compositore.</summary>
        public MotoreMusicale Motore { get; }

        /// <summary>Ultimo stato stabilizzato del tavolo (thread-safe); mai null.</summary>
        public StatoTavolo UltimoStato
        {
            get
            {
                lock (bloccoStato)
                {
                    return ultimoStato;
                }
            }
        }

        /// <summary>
        /// Esegue un passo: rilevamento, interpretazione, stabilizzazione e aggiornamento del motore.
        /// Se il rilevatore lancia un'eccezione il passo viene saltato e lo stato resta invariato.
        /// </summary>
        public void Passo()
        {
            IReadOnlyList<CartoncinoRilevato> rilevati;
            try
            {
                rilevati = rilevatore.Rileva();
            }
            catch (Exception)
            {
                // Webcam o visione momentaneamente non disponibili: si riprova al passo successivo.
                return;
            }

            StatoTavolo stabile = stabilizzatore.Aggiorna(interprete.Interpreta(rilevati));
            lock (bloccoStato)
            {
                ultimoStato = stabile;
            }

            Motore.AggiornaTavolo(stabile);
        }
    }
}
