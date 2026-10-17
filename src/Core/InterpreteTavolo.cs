using System;
using System.Collections.Generic;
using System.Linq;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>
    /// Trasforma i cartoncini rilevati sul tavolo in struttura, note, comandi ed errori.
    /// Il centro di ogni cartoncino determina la zona del layout: nelle zone Misura, Collegamento e Ab
    /// si trovano le impostazioni della struttura, nella zona Comandi i comandi, altrove le note.
    /// </summary>
    public class InterpreteTavolo
    {
        private readonly Griglia griglia;
        private readonly LayoutTavolo layout;

        /// <summary>Crea l'interprete per la griglia e il layout delle zone speciali del tavolo.</summary>
        /// <param name="griglia">Griglia di riferimento. Non può essere null (errore di programmazione).</param>
        /// <param name="layout">Zone speciali del tavolo. Non può essere null (errore di programmazione).</param>
        /// <exception cref="ArgumentNullException">Lanciata se <paramref name="griglia"/> o <paramref name="layout"/> è null.</exception>
        public InterpreteTavolo(Griglia griglia, LayoutTavolo layout)
        {
            this.griglia = griglia ?? throw new ArgumentNullException(nameof(griglia));
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
        }

        /// <summary>
        /// Crea l'interprete con la sola zona comandi: le zone speciali di struttura restano vuote.
        /// Equivale a <c>new InterpreteTavolo(griglia, LayoutTavolo.ConSoloComandi(zonaComandi))</c>.
        /// </summary>
        /// <param name="griglia">Griglia di riferimento. Non può essere null (errore di programmazione).</param>
        /// <param name="zonaComandi">Area normalizzata della zona comandi.</param>
        /// <exception cref="ArgumentNullException">Lanciata se <paramref name="griglia"/> è null.</exception>
        public InterpreteTavolo(Griglia griglia, RettangoloTavolo zonaComandi)
            : this(griglia, LayoutTavolo.ConSoloComandi(zonaComandi))
        {
        }

        /// <summary>
        /// Interpreta i cartoncini in due passaggi. Il primo classifica ogni cartoncino per zona e ne legge il payload;
        /// il secondo risolve la struttura dalle impostazioni e interpreta le note, in ordine di Area.X (poi Area.Y)
        /// così che, in caso di sovrapposizione sulla stessa riga e sullo stesso colore, il primo resti.
        /// Una lista null è trattata come vuota; gli elementi null vengono ignorati.
        /// </summary>
        public StatoTavolo Interpreta(IReadOnlyList<CartoncinoRilevato> rilevati)
        {
            var note = new List<(CartoncinoRilevato Rilevato, PayloadCartoncino Payload)>();
            var comandi = new List<ComandoRilevato>();
            var errori = new List<ErroreTavolo>();
            var impostazioni = new List<(PayloadCartoncino Payload, RettangoloTavolo Area, ZonaSpeciale Zona)>();

            IEnumerable<CartoncinoRilevato> validi = rilevati?
                .Where(r => r != null)
                ?? Enumerable.Empty<CartoncinoRilevato>();

            IEnumerable<CartoncinoRilevato> ordinati = validi
                .OrderBy(r => r.Area.X)
                .ThenBy(r => r.Area.Y);

            // Primo passaggio: classificazione per zona e lettura del payload.
            foreach (CartoncinoRilevato rilevato in ordinati)
            {
                if (!ParserPayload.TryParse(rilevato.Payload, out PayloadCartoncino payload))
                {
                    AggiungiErrore(errori, TipoErrore.PayloadNonValido, rilevato);
                    continue;
                }

                ZonaSpeciale zona = layout.ZonaDelCentro(rilevato.Area);
                switch (zona)
                {
                    case ZonaSpeciale.Misura:
                    case ZonaSpeciale.Collegamento:
                    case ZonaSpeciale.Ab:
                        if (payload.Tipo == TipoCartoncino.Nota)
                        {
                            AggiungiErrore(errori, TipoErrore.FuoriGriglia, rilevato);
                        }
                        else if (payload.Tipo == TipoCartoncino.Impostazione)
                        {
                            impostazioni.Add((payload, rilevato.Area, zona));
                        }
                        else
                        {
                            AggiungiErrore(errori, TipoErrore.PayloadNonValido, rilevato);
                        }

                        break;

                    case ZonaSpeciale.Comandi:
                        if (payload.Tipo != TipoCartoncino.Comando)
                        {
                            AggiungiErrore(errori, TipoErrore.PayloadNonValido, rilevato);
                            break;
                        }

                        comandi.Add(new ComandoRilevato { Tipo = payload.Comando, Parametro = payload.Parametro });
                        break;

                    default:
                        if (payload.Tipo != TipoCartoncino.Nota)
                        {
                            AggiungiErrore(errori, TipoErrore.PayloadNonValido, rilevato);
                            break;
                        }

                        note.Add((rilevato, payload));
                        break;
                }
            }

            // Secondo passaggio: struttura dalle impostazioni, poi note sulla griglia.
            RisoluzioneStruttura risoluzione = RisolutoreStruttura.Risolvi(impostazioni);
            errori.AddRange(risoluzione.Errori);
            StrutturaTavolo struttura = risoluzione.Struttura;

            var notaStato = new List<CartoncinoNota>();

            // Occupazione per (riga, colore): intervalli [inizio, fine) degli step già assegnati.
            var occupate = new Dictionary<(int Riga, int Traccia), List<(int Inizio, int Fine)>>();

            foreach (var (rilevato, payload) in note)
            {
                if (!griglia.TryCella(rilevato.Area, out int riga, out int stepIniziale, out int durataStep))
                {
                    AggiungiErrore(errori, TipoErrore.FuoriGriglia, rilevato);
                    continue;
                }

                if (rilevato.Colore == null)
                {
                    AggiungiErrore(errori, TipoErrore.ColoreMancante, rilevato);
                    continue;
                }

                if (stepIniziale >= struttura.StepPerMisura)
                {
                    AggiungiErrore(errori, TipoErrore.FuoriMisura, rilevato);
                    continue;
                }

                durataStep = Math.Min(durataStep, struttura.StepPerMisura - stepIniziale);

                int traccia = (int)rilevato.Colore.Value;
                if (SovrapponeGiaOccupato(occupate, (riga, traccia), stepIniziale, durataStep))
                {
                    AggiungiErrore(errori, TipoErrore.Sovrapposto, rilevato);
                    continue;
                }

                OccupaStep(occupate, (riga, traccia), stepIniziale, durataStep);
                notaStato.Add(new CartoncinoNota
                {
                    Riga = riga,
                    Traccia = traccia,
                    StepIniziale = stepIniziale,
                    DurataStep = durataStep,
                    IdClip = payload.IdClip,
                    Colore = rilevato.Colore.Value
                });
            }

            return new StatoTavolo(notaStato, comandi, errori, struttura);
        }

        private static void AggiungiErrore(List<ErroreTavolo> errori, TipoErrore tipo, CartoncinoRilevato rilevato)
        {
            errori.Add(new ErroreTavolo { Tipo = tipo, Area = rilevato.Area });
        }

        private static bool SovrapponeGiaOccupato(
            Dictionary<(int Riga, int Traccia), List<(int Inizio, int Fine)>> occupate,
            (int Riga, int Traccia) chiave,
            int stepIniziale,
            int durataStep)
        {
            if (!occupate.TryGetValue(chiave, out List<(int Inizio, int Fine)> intervalli))
            {
                return false;
            }

            int fine = stepIniziale + durataStep;
            return intervalli.Any(i => stepIniziale < i.Fine && i.Inizio < fine);
        }

        private static void OccupaStep(
            Dictionary<(int Riga, int Traccia), List<(int Inizio, int Fine)>> occupate,
            (int Riga, int Traccia) chiave,
            int stepIniziale,
            int durataStep)
        {
            if (!occupate.TryGetValue(chiave, out List<(int Inizio, int Fine)> intervalli))
            {
                intervalli = new List<(int Inizio, int Fine)>();
                occupate[chiave] = intervalli;
            }

            intervalli.Add((stepIniziale, stepIniziale + durataStep));
        }
    }
}
