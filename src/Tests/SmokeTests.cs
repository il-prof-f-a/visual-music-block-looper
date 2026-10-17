using NUnit.Framework;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class SmokeTests
    {
        [Test]
        public void Soluzione_SiCompila_ECaricaIlNucleo()
        {
            Assert.That(typeof(VisualMusicBlockLooper.Core.ConfigGriglia).Assembly, Is.Not.Null);
        }
    }
}
