using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class ColoriNoteTests
    {
        [TestCase(0, 184, 0, 0)]     // MIDI 36, La0: rosso
        [TestCase(7, 184, 74, 0)]    // MIDI 43: arancio
        [TestCase(24, 153, 184, 0)]  // MIDI 60, Do4: giallo-verde
        [TestCase(47, 0, 172, 184)]  // MIDI 83, Si5: ciano
        public void ColoreDerivaDallaNota(int idClip, int r, int g, int b)
        {
            var colore = ColoriNote.DaIdClip(idClip);
            Assert.That(colore.R, Is.EqualTo(r).Within(1));
            Assert.That(colore.G, Is.EqualTo(g).Within(1));
            Assert.That(colore.B, Is.EqualTo(b).Within(1));
        }

        [Test]
        public void RipetizioneAbEViola()
        {
            var colore = ColoriNote.DaSpeciale(TipoImpostazione.IntervalloAb);
            Assert.That(colore, Is.EqualTo(System.Drawing.Color.FromArgb(130, 50, 170)));
        }
    }
}
