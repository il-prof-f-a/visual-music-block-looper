using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Midi;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    public class SequenzaNoteDaQrTests
    {
        private sealed class Uscita : IUscitaMidi
        {
            public int Step;
            public readonly List<(int Step, string Evento)> Eventi = new List<(int, string)>();
            public void NotaOn(int c, int n, int v) => Eventi.Add((Step, $"On:{c}:{n}"));
            public void NotaOff(int c, int n) => Eventi.Add((Step, $"Off:{c}:{n}"));
            public void Clock() { }
            public void Start() { }
            public void Stop() { }
            public void Continue() { }
        }

        [TestCase(2)]
        [TestCase(6)]
        public void DieciAttacchi_DopoStabilizzazione_SuonanoAlleCelleQr(int collegamento)
        {
            var stabilizzatore = new StabilizzatoreTavolo();
            StatoTavolo stabile = null;
            using (var frame = NoteContigueVisionTests.Scena(true))
            {
                var rilevatore = new RilevatoreCartoncini(new NoteContigueVisionTests.Fonte(frame), NoteContigueVisionTests.Calibra(true));
                for (int i = 0; i < 3; i++)
                {
                    var letti = rilevatore.Rileva().ToList();
                    // Vincolo del banco simulato; non sostituisce la lettura del marker nella prova reale.
                    letti.Add(new CartoncinoRilevato { Payload = "VMBL1;S;LINK;" + collegamento, Area = LayoutTavolo.Default.Collegamento });
                    stabile = stabilizzatore.Aggiorna(AggancioQrTests.Interprete().Interpreta(letti));
                }
            }
            Assert.That(stabile.Note, Has.Count.EqualTo(10));
            Assert.That(stabile.Struttura.RigheCollegate, Is.EqualTo(collegamento));
            var uscita = new Uscita();
            var mappa = MappaturaMidiJson.Carica(Path.Combine(GeneratoreCartoncini.CartellaCartoncini(), "..", "config", "mappatura-default.json"));
            var motore = new MotoreMusicale(new Sequencer(new ConfigGriglia()), uscita, null, mappa);
            motore.AggiornaTavolo(stabile); motore.Avvia();
            int giro = stabile.Struttura.StepGiro;
            for (int tick = 0; tick <= giro * Sequencer.TickPerStep; tick++)
            {
                uscita.Step = tick / Sequencer.TickPerStep;
                motore.TickInterno();
            }
            int baseRiga = collegamento == 2 ? 0 : 32;
            int canale = collegamento == 2 ? 1 : 0;
            int[][] midi = { new[] { 64, 69, 76, 76, 76 }, new[] { 72, 74, 71, 71, 71 } };
            var attesi = new List<(int, string)>();
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 5; c++)
                    attesi.Add((baseRiga + r*16 + NoteContigueVisionTests.Celle[c], $"On:{canale}:{midi[r][c]}"));
            Assert.That(uscita.Eventi.Where(e => e.Step < giro && e.Evento.StartsWith("On:")), Is.EqualTo(attesi));
            for (int r = 0; r < 2; r++)
            {
                int passo = baseRiga + r*16 + 13, nota = r == 0 ? 76 : 71;
                Assert.That(uscita.Eventi.Where(e => e.Step == passo).Select(e => e.Evento),
                    Is.EqualTo(new[] { $"Off:{canale}:{nota}", $"On:{canale}:{nota}" }));
            }
            motore.Arresta();
        }
    }
}
