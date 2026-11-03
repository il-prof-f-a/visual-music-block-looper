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
        /// il secondo risolve la struttura e aggancia le note al QR (Area per le fonti precedenti).
        /// Attacchi in celle diverse troncano la precedente; nella stessa cella resta il primo da sinistra.
        /// Una lista null è trattata come vuota; gli elementi null vengono ignorati.
        /// </summary>
        public StatoTavolo Interpreta(IReadOnlyList<CartoncinoRilevato> rilevati) => Interpreta(rilevati, null);

        public StatoTavolo Interpreta(IReadOnlyList<CartoncinoRilevato> rilevati, TracciaCartoncini diagnostica,bool mantieniOrdine=false)
        {
            var note = new List<(CartoncinoRilevato Rilevato, PayloadCartoncino Payload)>();
            var comandi = new List<ComandoRilevato>();
            var errori = new List<ErroreTavolo>();
            var impostazioni = new List<(PayloadCartoncino Payload, RettangoloTavolo Area, ZonaSpeciale Zona)>();

            IEnumerable<CartoncinoRilevato> validi = rilevati?
                .Where(r => r != null)
                ?? Enumerable.Empty<CartoncinoRilevato>();

            IEnumerable<CartoncinoRilevato> ordinati = mantieniOrdine?validi:validi
                .OrderBy(r => (r.AreaQr ?? r.Area).X).ThenBy(r => (r.AreaQr ?? r.Area).Y);

            // Primo passaggio: classificazione per zona e lettura del payload.
            foreach (CartoncinoRilevato rilevato in ordinati)
            {
                ZonaSpeciale zona = layout.ZonaDelCentro(rilevato.Area);
                if (!ParserPayload.TryParse(rilevato.Payload, out PayloadCartoncino payload))
                {
                    AggiungiErrore(errori, TipoErrore.PayloadNonValido, rilevato, diagnostica, zona);
                    continue;
                }

                // Il QR determina la zona del cartoncino: il corpo colorato può sporgere
                // oltre il bordo della zona e non deve far scartare impostazioni o comandi.
                if (rilevato.AreaQr.HasValue)
                {
                    RettangoloTavolo qr = rilevato.AreaQr.Value;
                    zona = layout.ZonaDelCentro(new RettangoloTavolo(qr.X, qr.Y + qr.Altezza / 2, 0, 0));
                }

                switch (zona)
                {
                    case ZonaSpeciale.Misura:
                    case ZonaSpeciale.Collegamento:
                    case ZonaSpeciale.Ab:
                        if (payload.Tipo == TipoCartoncino.Nota)
                        {
                            AggiungiErrore(errori, TipoErrore.FuoriGriglia, rilevato, diagnostica, zona);
                        }
                        else if (payload.Tipo == TipoCartoncino.Impostazione)
                        {
                            impostazioni.Add((payload, rilevato.Area, zona));
                            diagnostica?.Associa(payload, rilevato);
                        }
                        else
                        {
                            AggiungiErrore(errori, TipoErrore.PayloadNonValido, rilevato, diagnostica, zona, "ZonaIncompatibile");
                        }

                        break;

                    case ZonaSpeciale.Comandi:
                        if (payload.Tipo != TipoCartoncino.Comando)
                        {
                            AggiungiErrore(errori, TipoErrore.PayloadNonValido, rilevato, diagnostica, zona, "ZonaIncompatibile");
                            break;
                        }

                        comandi.Add(new ComandoRilevato { Tipo = payload.Comando, Parametro = payload.Parametro });
                        diagnostica?.RegistraInterpretazione(rilevato, zona, true, "ComandoAccettato",
                            $"Comando {payload.Comando}, parametro {payload.Parametro}.");
                        break;

                    default:
                        if (payload.Tipo != TipoCartoncino.Nota)
                        {
                            AggiungiErrore(errori, TipoErrore.PayloadNonValido, rilevato, diagnostica, zona, "ZonaIncompatibile");
                            break;
                        }

                        note.Add((rilevato, payload));
                        break;
                }
            }

            // Secondo passaggio: struttura dalle impostazioni, poi note sulla griglia.
            RisoluzioneStruttura risoluzione = RisolutoreStruttura.Risolvi(impostazioni, diagnostica);
            errori.AddRange(risoluzione.Errori);
            StrutturaTavolo struttura = risoluzione.Struttura;

            var notaStato = new List<CartoncinoNota>();

            var occupate = new HashSet<(int Riga, int Step)>();
            var accettate = new List<NotaDaRisolvere>();

            foreach (var (rilevato, payload) in note)
            {
                ZonaSpeciale zona = ZonaSpeciale.Nessuna;
                if (!griglia.TryCella(rilevato, out int riga, out int stepIniziale, out int durataStep))
                {
                    AggiungiErrore(errori, TipoErrore.FuoriGriglia, rilevato, diagnostica, zona);
                    continue;
                }

                if (stepIniziale >= struttura.StepPerMisura)
                {
                    AggiungiErrore(errori, TipoErrore.FuoriMisura, rilevato, diagnostica, zona);
                    continue;
                }

                durataStep = Math.Min(durataStep, struttura.StepPerMisura - stepIniziale);

                // Il colore del cartoncino non porta informazioni: lo strumento (canale MIDI) e' il gruppo di righe
                // collegate. Righe collegate suonano lo stesso strumento; i gruppi sono strumenti simultanei.
                int traccia = riga / struttura.RigheCollegate;
                if (!occupate.Add((riga, stepIniziale)))
                {
                    AggiungiErrore(errori, TipoErrore.Sovrapposto, rilevato, diagnostica, zona);
                    continue;
                }

                accettate.Add(new NotaDaRisolvere { Rilevato = rilevato, Payload = payload,
                    Riga = riga, Step = stepIniziale, Durata = durataStep, Traccia = traccia });
            }

            foreach (var gruppo in accettate.GroupBy(n => n.Riga))
            {
                var riga = gruppo.OrderBy(n => n.Step).ToArray();
                for (int i = 0; i + 1 < riga.Length; i++)
                {
                    int prima = riga[i].Durata;
                    riga[i].Durata = Math.Min(prima, riga[i + 1].Step - riga[i].Step);
                    if (riga[i].Durata < prima)
                        riga[i].Troncamento = $" Troncata da {prima} a {riga[i].Durata} step dal QR alla cella {riga[i + 1].Step + 1}.";
                }
            }

            foreach (NotaDaRisolvere n in accettate)
            {
                notaStato.Add(new CartoncinoNota
                {
                    Riga = n.Riga,
                    Traccia = n.Traccia,
                    StepIniziale = n.Step,
                    DurataStep = n.Durata,
                    IdClip = n.Payload.IdClip,
                    Colore = (ColoreTraccia)(n.Riga % 6)
                });
                string aggancio = n.Rilevato.AreaQr.HasValue
                    ? $" Aggancio QR x={n.Rilevato.AreaQr.Value.X:0.00000}, fine colore={n.Rilevato.FineEstensioneX:0.00000}." : "";
                diagnostica?.RegistraInterpretazione(n.Rilevato, ZonaSpeciale.Nessuna, true, "NotaAccettata",
                    $"Nota {n.Payload.IdClip}, riga {n.Riga + 1}, gruppo {n.Traccia + 1}, step {n.Step}, durata {n.Durata}." + aggancio + n.Troncamento);
            }

            if (diagnostica != null)
            {
                diagnostica.Layout = layout;
                diagnostica.StrutturaGrezza = struttura;
                diagnostica.NoteGrezze = notaStato.Count;
                diagnostica.ComandiGrezzi = comandi.Count;
            }
            return new StatoTavolo(notaStato, comandi, errori, struttura);
        }

        private static void AggiungiErrore(List<ErroreTavolo> errori, TipoErrore tipo, CartoncinoRilevato rilevato,
            TracciaCartoncini diagnostica, ZonaSpeciale zona, string codice = null)
        {
            errori.Add(new ErroreTavolo { Tipo = tipo, Area = rilevato.Area });
            diagnostica?.RegistraInterpretazione(rilevato, zona, false, codice ?? tipo.ToString(),
                codice == "ZonaIncompatibile"
                    ? $"QR decodificato e payload valido, ma tipo non applicabile nella zona {zona}."
                    : $"Cartoncino scartato: {tipo}; zona {zona}.");
        }

        private sealed class NotaDaRisolvere
        {
            public CartoncinoRilevato Rilevato;
            public PayloadCartoncino Payload;
            public int Riga, Step, Durata, Traccia;
            public string Troncamento = "";
        }
    }
}
