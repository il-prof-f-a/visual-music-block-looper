using NUnit.Framework;
using VisualMusicBlockLooper.Midi;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class ConversioneMidiTests
    {
        [TestCase(0, 0)]
        [TestCase(7, 7)]
        [TestCase(15, 15)]
        [TestCase(16, 15)]
        [TestCase(-1, 0)]
        public void Canale_LimitaAlRange0_15(int ingresso, int atteso)
        {
            Assert.That(ConversioneMidi.Canale(ingresso), Is.EqualTo(atteso));
        }

        [TestCase(0, 0)]
        [TestCase(60, 60)]
        [TestCase(127, 127)]
        [TestCase(128, 127)]
        [TestCase(-3, 0)]
        public void Nota_LimitaAlRange0_127(int ingresso, int atteso)
        {
            Assert.That(ConversioneMidi.Nota(ingresso), Is.EqualTo(atteso));
        }

        [TestCase(100, 100)]
        [TestCase(127, 127)]
        [TestCase(200, 127)]
        [TestCase(-5, 0)]
        public void Velocity_LimitaAlRange0_127(int ingresso, int atteso)
        {
            Assert.That(ConversioneMidi.Velocity(ingresso), Is.EqualTo(atteso));
        }
    }
}
