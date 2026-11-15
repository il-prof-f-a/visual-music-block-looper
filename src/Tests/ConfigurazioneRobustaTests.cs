using System.IO;
using NUnit.Framework;
using VisualMusicBlockLooper.App;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class ConfigurazioneRobustaTests
    {
        [Test]
        public void DefaultUsaDueMisureDiAnticipoETolleranzaQrConfigurata()
        {
            var configurazione = new ConfigurazioneApplicazione();

            Assert.That(configurazione.MisureAnticipoScansione, Is.EqualTo(2));
            Assert.That(configurazione.TolleranzaQrCelle, Is.EqualTo(0.15).Within(1e-9));
        }

        [Test]
        public void CaricamentoRipristinaValoriDiDefaultPerTolleranzeNonValide()
        {
            string percorso = Path.GetTempFileName();
            try
            {
                File.WriteAllText(percorso,
                    "{\"MisureAnticipoScansione\":0,\"TolleranzaQrCelle\":0.8}");
                var configurazione = ConfigurazioneApplicazione.Carica(percorso);

                Assert.That(configurazione.MisureAnticipoScansione, Is.EqualTo(2));
                Assert.That(configurazione.TolleranzaQrCelle, Is.EqualTo(0.15).Within(1e-9));
            }
            finally
            {
                if (File.Exists(percorso)) File.Delete(percorso);
            }
        }
    }
}
