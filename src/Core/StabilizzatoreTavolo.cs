using System;
using System.Collections.Generic;
using System.Linq;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>
    /// Filtra nel tempo lo stato grezzo del tavolo per eliminare lo sfarfallio della visione.
    /// La struttura del tavolo cambia solo dopo <c>framePresenza</c> frame consecutivi con lo stesso valore.
    /// Una nota o un comando diventa stabile dopo <c>framePresenza</c> frame consecutivi di presenza;
    /// una nota stabile resta in output finché non è assente per <c>frameAssenza</c> frame consecutivi.
    /// I comandi vengono esposti solo nel frame in cui diventano stabili, per evitare doppi PLAY.
    /// Gli errori passano senza filtro.
    /// </summary>
    public class StabilizzatoreTavolo
    {
        private readonly int framePresenza;
        private readonly int frameAssenza;

        // Note non ancora stabili: conteggio di presenze consecutive per chiave.
        private readonly Dictionary<string, int> presenzeNote = new Dictionary<string, int>();

        // Note stabili: ultimo dato visto e conteggio di assenze consecutive.
        private readonly Dictionary<string, NotaStabile> noteStabili = new Dictionary<string, NotaStabile>();

        // Ordine di primo ingresso in stabilità, usato per l'ordine di output.
        private readonly List<string> ordineNoteStabili = new List<string>();

        // Comandi non ancora stabili: conteggio di presenze consecutive per chiave.
        private readonly Dictionary<string, int> presenzeComandi = new Dictionary<string, int>();

        // Comandi stabili (tenuti, non esposti): conteggio di assenze consecutive per chiave.
        private readonly Dictionary<string, int> comandiTenuti = new Dictionary<string, int>();

        // Struttura stabile del tavolo, con la candidata che si sta confermando e il suo conteggio di frame.
        private StrutturaTavolo strutturaStabile = StrutturaTavolo.Default;
        private StrutturaTavolo candidata;
        private int contatoreCandidata;

        /// <summary>Crea lo stabilizzatore con le soglie di presenza e di assenza in frame.</summary>
        /// <param name="framePresenza">Frame consecutivi di presenza necessari per diventare stabile (minimo 1).</param>
        /// <param name="frameAssenza">Frame consecutivi di assenza necessari per la rimozione (minimo 1).</param>
        /// <exception cref="ArgumentOutOfRangeException">Lanciata se una delle soglie è minore di 1.</exception>
        public StabilizzatoreTavolo(int framePresenza = 3, int frameAssenza = 5)
        {
            if (framePresenza < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(framePresenza), "Deve essere almeno 1.");
            }

            if (frameAssenza < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(frameAssenza), "Deve essere almeno 1.");
            }

            this.framePresenza = framePresenza;
            this.frameAssenza = frameAssenza;
        }

        /// <summary>
        /// Elabora un fotogramma e restituisce la struttura stabile, le note stabili, i comandi appena stabilizzati e gli errori invariati.
        /// La struttura stabile cambia solo dopo <c>framePresenza</c> frame consecutivi con lo stesso valore diverso.
        /// Uno stato null viene trattato come vuoto: tutti i candidati risultano assenti (struttura compresa: Default).
        /// </summary>
        public StatoTavolo Aggiorna(StatoTavolo grezzo)
        {
            IReadOnlyList<CartoncinoNota> noteGrezze = grezzo?.Note ?? new List<CartoncinoNota>();
            IReadOnlyList<ComandoRilevato> comandiGrezzi = grezzo?.Comandi ?? new List<ComandoRilevato>();
            IReadOnlyList<ErroreTavolo> errori = grezzo?.Errori ?? new List<ErroreTavolo>();

            AggiornaNote(noteGrezze);
            List<ComandoRilevato> comandiEsposti = AggiornaComandi(comandiGrezzi);
            AggiornaStruttura(grezzo?.Struttura ?? StrutturaTavolo.Default);

            return new StatoTavolo(
                OutputNote(),
                comandiEsposti,
                errori,
                strutturaStabile);
        }

        // La struttura cambia solo dopo framePresenza frame consecutivi con lo stesso valore diverso dallo stabile.
        private void AggiornaStruttura(StrutturaTavolo struttura)
        {
            if (struttura.Equals(strutturaStabile))
            {
                candidata = null;
                contatoreCandidata = 0;
                return;
            }

            if (struttura.Equals(candidata))
            {
                contatoreCandidata++;
            }
            else
            {
                candidata = struttura;
                contatoreCandidata = 1;
            }

            if (contatoreCandidata >= framePresenza)
            {
                strutturaStabile = candidata;
                candidata = null;
                contatoreCandidata = 0;
            }
        }

        private void AggiornaNote(IReadOnlyList<CartoncinoNota> noteGrezze)
        {
            var viste = new Dictionary<string, CartoncinoNota>();
            foreach (CartoncinoNota nota in noteGrezze.Where(n => n != null))
            {
                if (!viste.ContainsKey(nota.Chiave))
                {
                    viste[nota.Chiave] = nota;
                }
            }

            foreach (KeyValuePair<string, CartoncinoNota> coppia in viste)
            {
                if (noteStabili.TryGetValue(coppia.Key, out NotaStabile stabile))
                {
                    stabile.Ultima = coppia.Value;
                    stabile.Assenze = 0;
                    continue;
                }

                presenzeNote.TryGetValue(coppia.Key, out int presenze);
                presenze++;
                if (presenze >= framePresenza)
                {
                    presenzeNote.Remove(coppia.Key);
                    noteStabili[coppia.Key] = new NotaStabile { Ultima = coppia.Value, Assenze = 0 };
                    ordineNoteStabili.Add(coppia.Key);
                }
                else
                {
                    presenzeNote[coppia.Key] = presenze;
                }
            }

            // I candidati non stabili assenti perdono il conteggio di presenza.
            foreach (string chiave in presenzeNote.Keys.Where(k => !viste.ContainsKey(k)).ToList())
            {
                presenzeNote.Remove(chiave);
            }

            // Le note stabili assenti accumulano assenze e vengono dimenticate alla soglia.
            foreach (string chiave in noteStabili.Keys.Where(k => !viste.ContainsKey(k)).ToList())
            {
                NotaStabile stabile = noteStabili[chiave];
                stabile.Assenze++;
                if (stabile.Assenze >= frameAssenza)
                {
                    noteStabili.Remove(chiave);
                    ordineNoteStabili.Remove(chiave);
                }
            }
        }

        private List<ComandoRilevato> AggiornaComandi(IReadOnlyList<ComandoRilevato> comandiGrezzi)
        {
            var viste = new Dictionary<string, ComandoRilevato>();
            foreach (ComandoRilevato comando in comandiGrezzi.Where(c => c != null))
            {
                string chiave = ChiaveComando(comando);
                if (!viste.ContainsKey(chiave))
                {
                    viste[chiave] = comando;
                }
            }

            var esposti = new List<ComandoRilevato>();

            foreach (KeyValuePair<string, ComandoRilevato> coppia in viste)
            {
                if (comandiTenuti.ContainsKey(coppia.Key))
                {
                    comandiTenuti[coppia.Key] = 0;
                    continue;
                }

                presenzeComandi.TryGetValue(coppia.Key, out int presenze);
                presenze++;
                if (presenze >= framePresenza)
                {
                    presenzeComandi.Remove(coppia.Key);
                    comandiTenuti[coppia.Key] = 0;
                    esposti.Add(coppia.Value);
                }
                else
                {
                    presenzeComandi[coppia.Key] = presenze;
                }
            }

            foreach (string chiave in presenzeComandi.Keys.Where(k => !viste.ContainsKey(k)).ToList())
            {
                presenzeComandi.Remove(chiave);
            }

            foreach (string chiave in comandiTenuti.Keys.Where(k => !viste.ContainsKey(k)).ToList())
            {
                comandiTenuti[chiave]++;
                if (comandiTenuti[chiave] >= frameAssenza)
                {
                    comandiTenuti.Remove(chiave);
                }
            }

            return esposti;
        }

        private List<CartoncinoNota> OutputNote()
        {
            return ordineNoteStabili
                .Select(chiave => noteStabili[chiave].Ultima)
                .ToList();
        }

        private static string ChiaveComando(ComandoRilevato comando)
        {
            return $"{comando.Tipo}:{comando.Parametro}";
        }

        private class NotaStabile
        {
            public CartoncinoNota Ultima { get; set; }

            public int Assenze { get; set; }
        }
    }
}
