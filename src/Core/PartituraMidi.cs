using System;
using System.Collections.Generic;
using System.Linq;

namespace VisualMusicBlockLooper.Core
{
    public readonly struct EventoMidiProgrammato
    {
        public EventoMidiProgrammato(int tick, int canale, int nota, int velocity, bool noteOn)
        { Tick = tick; Canale = canale; Nota = nota; Velocity = velocity; NoteOn = noteOn; }
        public int Tick { get; }
        public int Canale { get; }
        public int Nota { get; }
        public int Velocity { get; }
        public bool NoteOn { get; }
    }

    /// <summary>Valori musicali e posizioni precompilati. Nessun oggetto della camera o del driver MIDI.</summary>
    public sealed class PartituraMidi
    {
        private readonly CartoncinoNota[] note;
        private readonly PosizioneGiro[] posizioni;
        internal PartituraMidi(StatoTavolo stato, long versione, long generazione, long primaFrontiera,
            IEnumerable<EventoMidiProgrammato> eventi)
        {
            VersioneFoglio = versione; Generazione = generazione; PrimaFrontiera = primaFrontiera;
            Struttura = stato.Struttura;
            note = stato.Note.Where(n => n != null).Select(CopiaNota).ToArray();
            Eventi = Array.AsReadOnly(eventi.ToArray());
            posizioni = new PosizioneGiro[Struttura.StepGiro];
            for (int i = 0; i < posizioni.Length; i++)
            {
                var p = Struttura.Posizione(i);
                posizioni[i] = new PosizioneGiro(p.StepInMisura, p.MisuraNelGiro, Array.AsReadOnly(p.RigheAttive.ToArray()));
            }
        }
        public long VersioneFoglio { get; }
        public long Generazione { get; }
        public long PrimaFrontiera { get; }
        public StrutturaTavolo Struttura { get; }
        public int TickGiro => Struttura.StepGiro * Sequencer.TickPerStep;
        public IReadOnlyList<EventoMidiProgrammato> Eventi { get; }
        public PosizioneGiro PosizioneAlTick(long tick)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            return posizioni[(int)((tick / Sequencer.TickPerStep) % posizioni.Length)];
        }
        public StatoTavolo CopiaStato() => new StatoTavolo(note.Select(CopiaNota), null, null, Struttura);
        internal static CartoncinoNota CopiaNota(CartoncinoNota n) => new CartoncinoNota
        { Riga = n.Riga, Traccia = n.Traccia, StepIniziale = n.StepIniziale, DurataStep = n.DurataStep, IdClip = n.IdClip, Colore = n.Colore };
    }
}
