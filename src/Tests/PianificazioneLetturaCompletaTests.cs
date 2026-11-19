using NUnit.Framework;
using VisualMusicBlockLooper.App;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class PianificazioneLetturaCompletaTests
    {
        [TestCase(true, 0, 4, -1, true)]
        [TestCase(true, 4, 4, -1, true)]
        [TestCase(true, 5, 4, -1, false)]
        [TestCase(true, 8, 4, 4, true)]
        [TestCase(true, 8, 4, 8, false)]
        [TestCase(true, 8, 8, -1, true)]
        [TestCase(false, 4, 4, -1, false)]
        [TestCase(true, -1, 4, -1, false)]
        public void DecideSoloAiMultipliNonAncoraLetti(bool attiva, long misura, int periodo, long ultima, bool atteso)
        {
            Assert.That(AnalizzatoreFoglio.DeveRileggereCompletamente(attiva, misura, periodo, ultima),
                Is.EqualTo(atteso));
        }
    }
}
