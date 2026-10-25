using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.App;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class PresetRisoluzioneTests
    {
        [Test]
        public void Elenco_HaITrePresetInOrdine()
        {
            IReadOnlyList<(int Larghezza, int Altezza)> elenco = PresetRisoluzione.Elenco;

            Assert.That(elenco.Count, Is.EqualTo(5));
            Assert.That(elenco.Select(p => (p.Larghezza, p.Altezza)).ToArray(), Is.EqualTo(new[]
            {
                (640, 480), (800, 600), (1280, 720), (1600, 900), (1920, 1080),
            }));
        }

        [Test]
        public void IndiceVicino_RisoluzioniDelPreset_RestituisceLoStessoIndice()
        {
            Assert.That(PresetRisoluzione.IndiceVicino(640, 480), Is.EqualTo(0));
            Assert.That(PresetRisoluzione.IndiceVicino(800, 600), Is.EqualTo(1));
            Assert.That(PresetRisoluzione.IndiceVicino(1280, 720), Is.EqualTo(2));
            Assert.That(PresetRisoluzione.IndiceVicino(1600, 900), Is.EqualTo(3));
            Assert.That(PresetRisoluzione.IndiceVicino(1920, 1080), Is.EqualTo(4));
        }

        [Test]
        public void IndiceVicino_RisoluzioneNonPreset_SceglieIlNumeroDiPixelPiuVicino()
        {
            // 1024x768 = 786432 px: distanza da 1280x720 (921600) 135168, da 800x600 (480000) 306432.
            Assert.That(PresetRisoluzione.IndiceVicino(1024, 768), Is.EqualTo(2));
        }

        [Test]
        public void IndiceVicino_Parita_SceglieIlPresetPiuPiccolo()
        {
            // 640x615 = 393600 px, esattamente a meta' tra 640x480 (307200) e 800x600 (480000).
            Assert.That(PresetRisoluzione.IndiceVicino(640, 615), Is.EqualTo(0));
        }

        [Test]
        public void IndiceVicino_ValoriNonPositivi_RestituisceIl1280x720()
        {
            Assert.That(PresetRisoluzione.IndiceVicino(0, 0), Is.EqualTo(2));
            Assert.That(PresetRisoluzione.IndiceVicino(-1, 480), Is.EqualTo(2));
            Assert.That(PresetRisoluzione.IndiceVicino(640, -5), Is.EqualTo(2));
        }

        [Test]
        public void Etichetta_RestituisceLarghezzaxAltezza()
        {
            Assert.That(PresetRisoluzione.Etichetta(2), Is.EqualTo("1280x720"));
            Assert.That(PresetRisoluzione.Etichetta(4), Is.EqualTo("1920x1080"));
        }

        [Test]
        public void Etichetta_IndiceFuoriRange_LanciaArgumentOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PresetRisoluzione.Etichetta(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => PresetRisoluzione.Etichetta(5));
        }
    }
}
