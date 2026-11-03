using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class CompilatorePartituraTests
    {
        private sealed class Mappa : IMappaturaMidi
        { public int Canale(int t) => t; public int Nota(int clip) => clip + 36; public int Velocity => 100; }
        private static CartoncinoNota Nota(int riga, int step, int durata, int clip = 40) =>
            new CartoncinoNota { Riga = riga, StepIniziale = step, DurataStep = durata, IdClip = clip, Traccia = 0 };
        private static StrutturaTavolo Struttura(int misura = 4, int link = 1, int a = 1, int b = 4)
        { StrutturaTavolo s; Assert.That(StrutturaTavolo.TryCrea(misura, link, a, b, out s)); return s; }
        private static PartituraMidi Compila(IEnumerable<CartoncinoNota> note, StrutturaTavolo s) =>
            new CompilatorePartitura(new ConfigGriglia(), new Mappa()).Compila(new StatoTavolo(note, null, null, s), 8, 3, 12);

        [Test]
        public void EventiSonoProntiInTickConReleasePrimaDelNuovoAttacco()
        {
            var p = Compila(new[] { Nota(0, 0, 3), Nota(0, 3, 2) }, Struttura());
            Assert.That(p.VersioneFoglio, Is.EqualTo(8)); Assert.That(p.Generazione, Is.EqualTo(3));
            Assert.That(p.PrimaFrontiera, Is.EqualTo(12)); Assert.That(p.TickGiro, Is.EqualTo(96));
            Assert.That(p.Eventi.Select(e => $"{e.Tick}:{e.NoteOn}:{e.Nota}:{e.Canale}"),
                Is.EqualTo(new[] { "0:True:76:0", "18:False:76:0", "18:True:76:0", "30:False:76:0" }));
        }

        [Test]
        public void MutareNoteOriginaliNonCambiaPartituraOAnteprima()
        {
            var n = Nota(0, 0, 1); var lista = new List<CartoncinoNota> { n };
            var p = Compila(lista, Struttura());
            n.StepIniziale = 10; n.IdClip = 0; lista.Clear();
            Assert.That(p.Eventi.First().Tick, Is.EqualTo(0)); Assert.That(p.Eventi.First().Nota, Is.EqualTo(76));
            var prima = p.CopiaStato(); prima.Note[0].IdClip = 5;
            Assert.That(p.CopiaStato().Note[0].IdClip, Is.EqualTo(40));
        }

        [Test]
        public void GiroAbParzialeParteDalPuntoGiustoEChiudeAlConfine()
        {
            var p = Compila(new[] { Nota(0, 4, 4), Nota(0, 8, 4), Nota(0, 0, 2) }, Struttura(4, 1, 2, 3));
            Assert.That(p.TickGiro, Is.EqualTo(48));
            Assert.That(p.Eventi.Select(e => $"{e.Tick}:{e.NoteOn}"),
                Is.EqualTo(new[] { "0:True", "24:False", "24:True", "48:False" }));
            Assert.That(p.PosizioneAlTick(0).StepInMisura, Is.EqualTo(4));
        }

        [TestCase(2, 1, 2, 48)]
        [TestCase(3, 2, 6, 144)]
        [TestCase(4, 6, 24, 576)]
        public void MisureECollegamentiHannoLunghezzaAttesa(int misura, int link, int b, int tick)
        { Assert.That(Compila(new CartoncinoNota[0], Struttura(misura, link, 1, b)).TickGiro, Is.EqualTo(tick)); }

        [Test]
        public void DieciNoteProduconoDieciAttacchiConGruppiContemporanei()
        {
            int[] celle = { 0, 3, 6, 11, 13 }; var note = new List<CartoncinoNota>();
            foreach (int r in new[] { 2, 3 }) foreach (int c in celle)
            { var n = Nota(r, c, 1); n.Traccia = r; note.Add(n); }
            var p = Compila(note, Struttura());
            Assert.That(p.Eventi.Count(e => e.NoteOn), Is.EqualTo(10));
            Assert.That(p.Eventi.Where(e => e.NoteOn).Select(e => e.Tick),
                Is.EqualTo(new[] { 0, 0, 18, 18, 36, 36, 66, 66, 78, 78 }));
        }
    }
}
