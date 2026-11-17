using System;
using System.Collections.Generic;
using System.Linq;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>Usa le regole del sequencer soltanto durante la preparazione, mai sul thread dei tick.</summary>
    public sealed class CompilatorePartitura
    {
        private readonly ConfigGriglia config;
        private readonly IMappaturaMidi mappatura;
        public CompilatorePartitura(ConfigGriglia config, IMappaturaMidi mappatura)
        { this.config = config ?? throw new ArgumentNullException(nameof(config)); this.mappatura = mappatura ?? throw new ArgumentNullException(nameof(mappatura)); }
        public PartituraMidi Compila(StatoTavolo stato, long versione, long generazione, long primaFrontiera = 0,
            long primaMisuraAssoluta = -1)
        {
            if (stato == null) throw new ArgumentNullException(nameof(stato));
            var copia = new StatoTavolo(stato.Note.Where(n => n != null).Select(PartituraMidi.CopiaNota), null, null, stato.Struttura);
            var sequencer = new Sequencer(config);
            var eventi = new List<EventoMidiProgrammato>();
            int lunghezza = copia.Struttura.StepGiro * Sequencer.TickPerStep;
            for (int tick = 0; tick < lunghezza; tick++)
            {
                foreach (var e in sequencer.Tick(copia.Note, copia.Struttura))
                {
                    int fine=e.NoteOn?Math.Min(lunghezza,tick+e.DurataStep*Sequencer.TickPerStep):-1;
                    eventi.Add(Converti(tick,e,fine));
                }
            }
            sequencer.Ferma(out IReadOnlyList<EventoNota> chiusura);
            foreach (var e in chiusura) eventi.Add(Converti(lunghezza, e));
            return new PartituraMidi(copia, versione, generazione, primaFrontiera, primaMisuraAssoluta, eventi);
        }
        private EventoMidiProgrammato Converti(int tick, EventoNota evento,int fineTick=-1)
        {
            int canale = mappatura.Canale(evento.Traccia), nota = mappatura.Nota(evento.IdClip), velocity = mappatura.Velocity;
            if (canale < 0 || canale > 15 || nota < 0 || nota > 127 || velocity < 0 || velocity > 127)
                throw new ArgumentOutOfRangeException(nameof(evento), "Mappatura MIDI fuori intervallo.");
            return new EventoMidiProgrammato(tick, canale, nota, velocity, evento.NoteOn,fineTick,evento.Identita);
        }
    }
}
