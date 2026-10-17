using System;
using System.Collections.Generic;
using System.Linq;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>
    /// Trasforma i cartoncini rilevati sul tavolo in note sulla griglia, comandi ed errori.
    /// Un cartoncino è un comando se il centro della sua area ricade nella zona comandi, altrimenti una nota.
    /// </summary>
    public class InterpreteTavolo
    {
        private readonly Griglia griglia;
        private readonly RettangoloTavolo zonaComandi;

        public InterpreteTavolo(Griglia griglia, RettangoloTavolo zonaComandi)
        {
            this.griglia = griglia ?? throw new ArgumentNullException(nameof(griglia));
            this.zonaComandi = zonaComandi;
        }

        /// <summary>
        /// Interpreta i cartoncini. Vengono elaborati in ordine di Area.X (poi Area.Y) così che,
        /// in caso di sovrapposizione sulla stessa traccia, il primo resti e il successivo sia scartato.
        /// </summary>
        public StatoTavolo Interpreta(IReadOnlyList<CartoncinoRilevato> rilevati)
        {
            if (rilevati == null)
            {
                throw new ArgumentNullException(nameof(rilevati));
            }

            var note = new List<CartoncinoNota>();
            var comandi = new List<ComandoRilevato>();
            var errori = new List<ErroreTavolo>();

            // Occupazione per traccia: intervalli [inizio, fine) degli step già assegnati.
            var occupate = new Dictionary<int, List<(int Inizio, int Fine)>>();

            IEnumerable<CartoncinoRilevato> ordinati = rilevati
                .OrderBy(r => r.Area.X)
                .ThenBy(r => r.Area.Y);

            foreach (CartoncinoRilevato rilevato in ordinati)
            {
                if (!ParserPayload.TryParse(rilevato.Payload, out PayloadCartoncino payload))
                {
                    AggiungiErrore(errori, TipoErrore.PayloadNonValido, rilevato);
                    continue;
                }

                if (CentroInZonaComandi(rilevato.Area))
                {
                    if (payload.Tipo != TipoCartoncino.Comando)
                    {
                        AggiungiErrore(errori, TipoErrore.PayloadNonValido, rilevato);
                        continue;
                    }

                    comandi.Add(new ComandoRilevato { Tipo = payload.Comando, Parametro = payload.Parametro });
                    continue;
                }

                if (payload.Tipo != TipoCartoncino.Nota)
                {
                    AggiungiErrore(errori, TipoErrore.PayloadNonValido, rilevato);
                    continue;
                }

                if (!griglia.TryCella(rilevato.Area, out int traccia, out int stepIniziale, out int durataStep))
                {
                    AggiungiErrore(errori, TipoErrore.FuoriGriglia, rilevato);
                    continue;
                }

                if (rilevato.Colore == null)
                {
                    AggiungiErrore(errori, TipoErrore.ColoreMancante, rilevato);
                    continue;
                }

                if ((int)rilevato.Colore.Value != traccia)
                {
                    AggiungiErrore(errori, TipoErrore.ColoreIncoerente, rilevato);
                    continue;
                }

                if (SovrapponeGiaOccupato(occupate, traccia, stepIniziale, durataStep))
                {
                    AggiungiErrore(errori, TipoErrore.Sovrapposto, rilevato);
                    continue;
                }

                OccupaStep(occupate, traccia, stepIniziale, durataStep);
                note.Add(new CartoncinoNota
                {
                    Traccia = traccia,
                    StepIniziale = stepIniziale,
                    DurataStep = durataStep,
                    IdClip = payload.IdClip,
                    Colore = rilevato.Colore.Value
                });
            }

            return new StatoTavolo(note, comandi, errori);
        }

        private bool CentroInZonaComandi(RettangoloTavolo area)
        {
            double centroX = area.X + area.Larghezza / 2.0;
            double centroY = area.Y + area.Altezza / 2.0;

            return centroX >= zonaComandi.X
                && centroX <= zonaComandi.X + zonaComandi.Larghezza
                && centroY >= zonaComandi.Y
                && centroY <= zonaComandi.Y + zonaComandi.Altezza;
        }

        private static void AggiungiErrore(List<ErroreTavolo> errori, TipoErrore tipo, CartoncinoRilevato rilevato)
        {
            errori.Add(new ErroreTavolo { Tipo = tipo, Area = rilevato.Area });
        }

        private static bool SovrapponeGiaOccupato(
            Dictionary<int, List<(int Inizio, int Fine)>> occupate, int traccia, int stepIniziale, int durataStep)
        {
            if (!occupate.TryGetValue(traccia, out List<(int Inizio, int Fine)> intervalli))
            {
                return false;
            }

            int fine = stepIniziale + durataStep;
            return intervalli.Any(i => stepIniziale < i.Fine && i.Inizio < fine);
        }

        private static void OccupaStep(
            Dictionary<int, List<(int Inizio, int Fine)>> occupate, int traccia, int stepIniziale, int durataStep)
        {
            if (!occupate.TryGetValue(traccia, out List<(int Inizio, int Fine)> intervalli))
            {
                intervalli = new List<(int Inizio, int Fine)>();
                occupate[traccia] = intervalli;
            }

            intervalli.Add((stepIniziale, stepIniziale + durataStep));
        }
    }
}
