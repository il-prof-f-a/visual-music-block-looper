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

            Assert.That(configurazione.BpmIniziale, Is.EqualTo(100));
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
                    "{\"MisureAnticipoScansione\":0,\"TolleranzaQrCelle\":0.8,\"BpmIniziale\":999}");
                var configurazione = ConfigurazioneApplicazione.Carica(percorso);

                Assert.That(configurazione.MisureAnticipoScansione, Is.EqualTo(2));
                Assert.That(configurazione.TolleranzaQrCelle, Is.EqualTo(0.15).Within(1e-9));
                Assert.That(configurazione.BpmIniziale, Is.EqualTo(300));
            }
            finally
            {
                if (File.Exists(percorso)) File.Delete(percorso);
            }
        }

        [Test]
        public void BpmNonFinitoRipristinaIlDefaultApplicativo()
        {
            string percorso = Path.GetTempFileName();
            try
            {
                File.WriteAllText(percorso, "{\"BpmIniziale\":NaN}");
                Assert.That(ConfigurazioneApplicazione.Carica(percorso).BpmIniziale, Is.EqualTo(100));
            }
            finally
            {
                if (File.Exists(percorso)) File.Delete(percorso);
            }
        }
    }
}
