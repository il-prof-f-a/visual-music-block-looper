using System;
using System.Collections.Generic;
using System.Linq;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>
    /// Sequencer a step: a ogni tick di clock produce gli eventi note-on e note-off delle note sulla griglia.
    /// Il tick multiplo di <see cref="TickPerStep"/> è un confine di step; sugli altri tick si gestiscono solo le note rimosse.
    /// </summary>
    public class Sequencer
    {
        /// <summary>Numero di tick di clock per ogni step.</summary>
        public const int TickPerStep = 6;

        private readonly List<NotaInCorso> suonanti = new List<NotaInCorso>();
        private int contatoreTick;
        private StrutturaTavolo ultimaStruttura;

        /// <summary>Crea il sequencer per la griglia data.</summary>
        /// <param name="config">Configurazione della griglia. Non può essere null (errore di programmazione).</param>
        /// <exception cref="ArgumentNullException">Lanciata se <paramref name="config"/> è null.</exception>
        public Sequencer(ConfigGriglia config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
        }

        /// <summary>Posizione nel giro, aggiornata a ogni confine di step; prima del primo tick è il passo 0 della struttura di default.</summary>
        public PosizioneGiro Posizione { get; private set; } = StrutturaTavolo.Default.Posizione(0);

        /// <summary>Step corrente all'interno della misura; zero prima del primo tick.</summary>
        public int StepCorrente => Posizione.StepInMisura;

        /// <summary>
        /// Avanza di un tick e restituisce gli eventi prodotti. Se la struttura è cambiata rispetto all'ultimo tick
        /// si spengono per prime tutte le note in corso. Poi: note-off per le note rimosse,
        /// note-off per le note terminate, note-on per le note che iniziano (riga attiva e step giusto);
        /// a parità ordine per traccia, clip e riga. Un argomento null equivale a una lista vuota. Non restituisce mai null.
        /// </summary>
        /// <param name="note">Note attualmente presenti sulla griglia.</param>
        /// <param name="struttura">Struttura del giro; null equivale a <see cref="StrutturaTavolo.Default"/>.</param>
        public IReadOnlyList<EventoNota> Tick(IReadOnlyList<CartoncinoNota> note, StrutturaTavolo struttura = null)
        {
            struttura = struttura ?? StrutturaTavolo.Default;
            List<CartoncinoNota> noteAttive = (note ?? new List<CartoncinoNota>())
                .Where(n => n != null)
                .ToList();
            HashSet<string> chiaviPresenti = new HashSet<string>(noteAttive.Select(n => n.Chiave));
            List<EventoNota> eventi = new List<EventoNota>();

            if (ultimaStruttura != null && !ultimaStruttura.Equals(struttura))
            {
                eventi.AddRange(EventiDiSpegnimento(suonanti.ToList()));
                suonanti.Clear();
            }

            ultimaStruttura = struttura;

            List<NotaInCorso> rimosse = suonanti.Where(s => !chiaviPresenti.Contains(s.Chiave)).ToList();
            suonanti.RemoveAll(s => !chiaviPresenti.Contains(s.Chiave));
            eventi.AddRange(EventiDiSpegnimento(rimosse));

            int tickCorrente = contatoreTick;
            contatoreTick++;
            if (tickCorrente % TickPerStep != 0)
            {
                return eventi;
            }

            int passoAssoluto = tickCorrente / TickPerStep;
            PosizioneGiro posizione = struttura.Posizione(passoAssoluto);
            Posizione = posizione;

            List<NotaInCorso> terminate = suonanti.Where(s => s.PassoFine <= passoAssoluto).ToList();
            suonanti.RemoveAll(s => s.PassoFine <= passoAssoluto);
            eventi.AddRange(EventiDiSpegnimento(terminate));

            foreach (CartoncinoNota nota in noteAttive
                .Where(n => n.StepIniziale == posizione.StepInMisura && posizione.RigheAttive.Contains(n.Riga))
                .OrderBy(n => n.Traccia)
                .ThenBy(n => n.IdClip)
                .ThenBy(n => n.Riga))
            {
                if (suonanti.Any(s => s.Chiave == nota.Chiave))
                {
                    continue;
                }

                int durataClampata = Math.Min(Math.Max(nota.DurataStep, 1), struttura.StepPerMisura - nota.StepIniziale);
                suonanti.Add(new NotaInCorso(nota.Chiave, nota.Traccia, nota.IdClip, passoAssoluto + durataClampata));
                eventi.Add(new EventoNota(nota.Traccia, nota.IdClip, true));
            }

            return eventi;
        }

        /// <summary>
        /// Spegne tutte le note in corso e riporta il sequencer allo stato iniziale (contatore e posizione a zero).
        /// </summary>
        /// <param name="noteOffPendenti">Note-off delle note che stavano suonando, per traccia e clip. Mai null.</param>
        public void Ferma(out IReadOnlyList<EventoNota> noteOffPendenti)
        {
            noteOffPendenti = EventiDiSpegnimento(suonanti.ToList());
            suonanti.Clear();
            contatoreTick = 0;
            ultimaStruttura = null;
            Posizione = StrutturaTavolo.Default.Posizione(0);
        }

        private static List<EventoNota> EventiDiSpegnimento(IEnumerable<NotaInCorso> note)
        {
            return note
                .OrderBy(n => n.Traccia)
                .ThenBy(n => n.IdClip)
                .Select(n => new EventoNota(n.Traccia, n.IdClip, false))
                .ToList();
        }

        /// <summary>Nota attualmente in riproduzione, con il passo assoluto in cui deve terminare.</summary>
        private class NotaInCorso
        {
            public NotaInCorso(string chiave, int traccia, int idClip, int passoFine)
            {
                Chiave = chiave;
                Traccia = traccia;
                IdClip = idClip;
                PassoFine = passoFine;
            }

            public string Chiave { get; }

            public int Traccia { get; }

            public int IdClip { get; }

            /// <summary>Passo assoluto (contato dall'avvio) alla cui raggiunta la nota si spegne.</summary>
            public int PassoFine { get; }
        }
    }
}
