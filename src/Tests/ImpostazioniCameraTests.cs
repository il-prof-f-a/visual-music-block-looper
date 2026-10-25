using NUnit.Framework;
using VisualMusicBlockLooper.App;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class ImpostazioniCameraTests
    {
        [Test]
        public void Costanti_HannoIValoriDelloSpec()
        {
            Assert.That(ImpostazioniCamera.FuocoMin, Is.EqualTo(0));
            Assert.That(ImpostazioniCamera.FuocoMax, Is.EqualTo(255));
            Assert.That(ImpostazioniCamera.FuocoPasso, Is.EqualTo(5));
            Assert.That(ImpostazioniCamera.EsposizioneMin, Is.EqualTo(-13));
            Assert.That(ImpostazioniCamera.EsposizioneMax, Is.EqualTo(0));
        }

        [TestCase(-5, 0)]
        [TestCase(0, 0)]
        [TestCase(120, 120)]
        [TestCase(255, 255)]
        [TestCase(300, 255)]
        public void LimitaFuoco_RestringeNelRange(int valore, int atteso)
        {
            Assert.That(ImpostazioniCamera.LimitaFuoco(valore), Is.EqualTo(atteso));
        }

        [TestCase(-20.0, -13.0)]
        [TestCase(-6.5, -6.5)]
        [TestCase(0.0, 0.0)]
        [TestCase(3.0, 0.0)]
        public void LimitaEsposizione_RestringeNelRange(double valore, double atteso)
        {
            Assert.That(ImpostazioniCamera.LimitaEsposizione(valore), Is.EqualTo(atteso));
        }

        [Test]
        public void EtichettaEsposizione_ConvertiteInMillisecondi()
        {
            Assert.That(ImpostazioniCamera.EtichettaEsposizione(0), Is.EqualTo("1000 ms"));
            Assert.That(ImpostazioniCamera.EtichettaEsposizione(-3), Is.EqualTo("125 ms"));
        }

        [Test]
        public void EtichettaEsposizione_SottoLeDieciMillisecondi_UnDecimale()
        {
            // 1000 * 2^-7 = 7,8125 ms
            Assert.That(ImpostazioniCamera.EtichettaEsposizione(-7), Is.EqualTo("7,8 ms"));
        }

        [Test]
        public void EtichettaFuoco_E_IlValoreNumerico()
        {
            Assert.That(ImpostazioniCamera.EtichettaFuoco(120), Is.EqualTo("120"));
            Assert.That(ImpostazioniCamera.EtichettaFuoco(0), Is.EqualTo("0"));
        }
    }
}
