using System;
using System.Collections.Generic;
using System.Linq;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>Esito della risoluzione della struttura: struttura effettiva ed errori dei cartoncini scartati.</summary>
    public sealed class RisoluzioneStruttura
    {
        public RisoluzioneStruttura(StrutturaTavolo struttura, IReadOnlyList<ErroreTavolo> errori)
        {
            Struttura = struttura;
            Errori = errori ?? new List<ErroreTavolo>();
        }

        /// <summary>Struttura risultante, sempre valida.</summary>
        public StrutturaTavolo Struttura { get; }

        /// <summary>Cartoncini scartati, mai null.</summary>
        public IReadOnlyList<ErroreTavolo> Errori { get; }
    }

    /// <summary>
    /// Ricava la struttura del tavolo dai cartoncini posti nelle zone Misura, Collegamento e Ab.
    /// Per ogni zona vale il cartoncino più in alto (poi più a sinistra); gli altri sono sovrapposti.
    /// </summary>
    public static class RisolutoreStruttura
    {
        private static readonly int[] BattitiAmmessi = { 2, 3, 4 };
        private static readonly int[] RigheCollegateAmmesse = { 1, 2, 3, 6 };

        public static RisoluzioneStruttura Risolvi(IEnumerable<(PayloadCartoncino Payload, RettangoloTavolo Area, ZonaSpeciale Zona)> impostazioni)
            => Risolvi(impostazioni, null);

        public static RisoluzioneStruttura Risolvi(IEnumerable<(PayloadCartoncino Payload, RettangoloTavolo Area, ZonaSpeciale Zona)> impostazioni,
            TracciaCartoncini traccia)
        {
            var errori = new List<ErroreTavolo>();
            if (impostazioni == null)
            {
                return new RisoluzioneStruttura(StrutturaTavolo.Default, errori);
            }

            var perZona = new Dictionary<ZonaSpeciale, List<(PayloadCartoncino Payload, RettangoloTavolo Area)>>();
            foreach (var (payload, area, zona) in impostazioni)
            {
                if (zona != ZonaSpeciale.Misura && zona != ZonaSpeciale.Collegamento && zona != ZonaSpeciale.Ab)
                {
                    continue;
                }

                if (!perZona.TryGetValue(zona, out var lista))
                {
                    lista = new List<(PayloadCartoncino Payload, RettangoloTavolo Area)>();
                    perZona[zona] = lista;
                }

                lista.Add((payload, area));
            }

            var misura = Candidato(perZona, ZonaSpeciale.Misura, errori, traccia);
            var collegamento = Candidato(perZona, ZonaSpeciale.Collegamento, errori, traccia);
            var ab = Candidato(perZona, ZonaSpeciale.Ab, errori, traccia);

            // Prima si risolvono misura e collegamento: l'A-B va validato contro le righe effettive.
            int battiti = ValoreMisura(misura, errori, traccia);
            int righe = ValoreCollegamento(collegamento, errori, traccia);
            int aInizio = 1;
            int aFine = righe * battiti;

            if (ab.HasValue)
            {
                var (payload, area) = ab.Value;
                bool abValido = IsImpostazione(payload, TipoImpostazione.IntervalloAb)
                    && StrutturaTavolo.TryCrea(battiti, righe, payload.Valore, payload.ValoreB, out _);
                if (abValido)
                {
                    aInizio = payload.Valore;
                    aFine = payload.ValoreB;
                    traccia?.RegistraImpostazione(payload, area, ZonaSpeciale.Ab, true, "AbAccettato",
                        $"A-B {aInizio}-{aFine}; MISURA {battiti}/4, LINK {righe}, limite {righe * battiti} battute.");
                }
                else
                {
                    errori.Add(new ErroreTavolo { Tipo = TipoErrore.ImpostazioneNonValida, Area = area });
                    traccia?.RegistraImpostazione(payload, area, ZonaSpeciale.Ab, false,
                        IsImpostazione(payload, TipoImpostazione.IntervalloAb) ? "IntervalloAbNonValido" : "ImpostazioneInZonaErrata",
                        $"Richiesti A-B {payload?.Valore}-{payload?.ValoreB}; MISURA {battiti}/4, LINK {righe}: deve valere 1 <= A < B <= {righe * battiti}. Nella zona Ab serve un payload AB.");
                }
            }

            StrutturaTavolo.TryCrea(battiti, righe, aInizio, aFine, out var struttura);
            return new RisoluzioneStruttura(struttura, errori);
        }

        /// <summary>Restituisce il primo cartoncino della zona (per Y, poi X) e segnala gli altri come sovrapposti.</summary>
        private static (PayloadCartoncino Payload, RettangoloTavolo Area)? Candidato(
            Dictionary<ZonaSpeciale, List<(PayloadCartoncino Payload, RettangoloTavolo Area)>> perZona,
            ZonaSpeciale zona,
            List<ErroreTavolo> errori, TracciaCartoncini traccia)
        {
            if (!perZona.TryGetValue(zona, out var lista) || lista.Count == 0)
            {
                return null;
            }

            var ordinati = lista.OrderBy(c => c.Area.Y).ThenBy(c => c.Area.X).ToList();
            for (int i = 1; i < ordinati.Count; i++)
            {
                errori.Add(new ErroreTavolo { Tipo = TipoErrore.Sovrapposto, Area = ordinati[i].Area });
                traccia?.RegistraImpostazione(ordinati[i].Payload, ordinati[i].Area, zona, false,
                    "Sovrapposto", "Più impostazioni nella stessa zona: prevale quella più in alto, poi più a sinistra.");
            }

            return ordinati[0];
        }

        private static int ValoreMisura((PayloadCartoncino Payload, RettangoloTavolo Area)? candidato, List<ErroreTavolo> errori, TracciaCartoncini traccia)
        {
            if (!candidato.HasValue)
            {
                return StrutturaTavolo.Default.BattitiPerMisura;
            }

            var (payload, area) = candidato.Value;
            if (IsImpostazione(payload, TipoImpostazione.Misura) && BattitiAmmessi.Contains(payload.Valore))
            {
                traccia?.RegistraImpostazione(payload, area, ZonaSpeciale.Misura, true, "MisuraAccettata",
                    $"MISURA {payload.Valore}/4 accettata.");
                return payload.Valore;
            }

            errori.Add(new ErroreTavolo { Tipo = TipoErrore.ImpostazioneNonValida, Area = area });
            traccia?.RegistraImpostazione(payload, area, ZonaSpeciale.Misura, false,
                IsImpostazione(payload, TipoImpostazione.Misura) ? "MisuraNonValida" : "ImpostazioneInZonaErrata",
                "La zona Misura richiede MISURA 2, 3 o 4.");
            return StrutturaTavolo.Default.BattitiPerMisura;
        }

        private static int ValoreCollegamento((PayloadCartoncino Payload, RettangoloTavolo Area)? candidato, List<ErroreTavolo> errori, TracciaCartoncini traccia)
        {
            if (!candidato.HasValue)
            {
                return StrutturaTavolo.Default.RigheCollegate;
            }

            var (payload, area) = candidato.Value;
            if (IsImpostazione(payload, TipoImpostazione.Collegamento) && RigheCollegateAmmesse.Contains(payload.Valore))
            {
                traccia?.RegistraImpostazione(payload, area, ZonaSpeciale.Collegamento, true, "CollegamentoAccettato",
                    $"LINK {payload.Valore} accettato.");
                return payload.Valore;
            }

            errori.Add(new ErroreTavolo { Tipo = TipoErrore.ImpostazioneNonValida, Area = area });
            traccia?.RegistraImpostazione(payload, area, ZonaSpeciale.Collegamento, false,
                IsImpostazione(payload, TipoImpostazione.Collegamento) ? "CollegamentoNonValido" : "ImpostazioneInZonaErrata",
                "La zona Collegamento richiede LINK 1, 2, 3 o 6.");
            return StrutturaTavolo.Default.RigheCollegate;
        }

        private static bool IsImpostazione(PayloadCartoncino payload, TipoImpostazione tipo)
        {
            return payload != null
                && payload.Tipo == TipoCartoncino.Impostazione
                && payload.Impostazione == tipo;
        }
    }
}
