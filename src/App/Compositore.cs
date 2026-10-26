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

        public RegistroCatture RegistroDiagnostica { get; } = new RegistroCatture();

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
            CatturaInCorso cattura = RegistroDiagnostica.IniziaCattura();
            TracciaCartoncini traccia = cattura?.Traccia;
            try
            {
                IReadOnlyList<CartoncinoRilevato> rilevati;
                try
                {
                    if (traccia != null) { traccia.EsitoVisione = "RilevatoreNonDiagnostico"; traccia.Layout = Layout; }
                    rilevati = traccia != null && rilevatore is IRilevatoreDiagnostico diagnostico
                        ? diagnostico.Rileva(traccia) : rilevatore.Rileva();
                }
                catch (Exception e)
                {
                    if (traccia != null)
                    {
                        if (traccia.EsitoVisione != "ErroreAcquisizione") traccia.EsitoVisione = "ErroreRilevamento";
                        traccia.Errore = e.GetType().Name + ": " + e.Message;
                        stabilizzatore.DescriviStatoInvariato(traccia);
                    }
                    // Comportamento invariato: una cattura fallita non aggiorna lo stato musicale.
                    return;
                }

                StatoTavolo grezzo = interprete.Interpreta(rilevati, traccia);
                StatoTavolo stabile = stabilizzatore.Aggiorna(grezzo, traccia);
                lock (bloccoStato) { ultimoStato = stabile; }
                Motore.AggiornaTavolo(stabile);
            }
            catch (Exception e)
            {
                if (traccia != null) traccia.Errore = e.GetType().Name + ": " + e.Message;
                throw;
            }
            finally { RegistroDiagnostica.CompletaCattura(cattura); }
        }

        /// <summary>Annota il ramo di sola anteprima senza far avanzare la pipeline musicale.</summary>
        public void RegistraSenzaCalibrazione(bool webcamDisponibile, int larghezza, int altezza, string errore = null)
            => RegistraSenzaCalibrazione(RegistroDiagnostica.IniziaCattura(), webcamDisponibile,
                webcamDisponibile, larghezza, altezza, errore);

        /// <summary>Completa un contesto aperto PRIMA dell'acquisizione, preservandone timestamp e generazione.</summary>
        public void RegistraSenzaCalibrazione(CatturaInCorso cattura, bool webcamDisponibile, bool frameDisponibile,
            int larghezza, int altezza, string errore = null)
        {
            if (cattura == null) return;
            cattura.Traccia.CalibrazionePresente = false;
            cattura.Traccia.Layout = Layout;
            cattura.Traccia.EsitoVisione = errore != null ? "ErroreAcquisizione" : !webcamDisponibile ? "WebcamAssente"
                : frameDisponibile ? "CalibrazioneAssente" : "FrameAssente";
            cattura.Traccia.LarghezzaFrame = larghezza; cattura.Traccia.AltezzaFrame = altezza;
            cattura.Traccia.Errore = errore;
            stabilizzatore.DescriviStatoInvariato(cattura.Traccia);
            RegistroDiagnostica.CompletaCattura(cattura);
        }
    }
}
