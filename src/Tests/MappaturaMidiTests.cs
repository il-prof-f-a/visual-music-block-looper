using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using VisualMusicBlockLooper.Midi;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class MappaturaMidiTests
    {
        private string cartellaTemp;

        [SetUp]
        public void Prepara()
        {
            cartellaTemp = Path.Combine(Path.GetTempPath(), "vmbl-mappatura-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(cartellaTemp);
        }

        [TearDown]
        public void Pulisci()
        {
            if (Directory.Exists(cartellaTemp))
            {
                Directory.Delete(cartellaTemp, true);
            }
        }

        [Test]
        public void CaricaDefault()
        {
            string percorso = Path.Combine(TestContext.CurrentContext.TestDirectory, "config", "mappatura-default.json");

            MappaturaMidiJson mappatura = MappaturaMidiJson.Carica(percorso);

            Assert.That(mappatura.Velocity, Is.EqualTo(100));
            Assert.That(mappatura.Canale(2), Is.EqualTo(2));
            Assert.That(mappatura.Nota(0), Is.EqualTo(36));
            Assert.That(mappatura.Nota(1), Is.EqualTo(37));
            Assert.That(mappatura.Nota(9), Is.EqualTo(45));
        }

        [Test]
        public void NotaDefaultSePerClipMancante()
        {
            ConfigurazioneMidi configurazione = new ConfigurazioneMidi();
            configurazione.NotaPerClip = new Dictionary<int, int> { { 0, 60 } };

            MappaturaMidiJson mappatura = new MappaturaMidiJson(configurazione);

            Assert.That(mappatura.Nota(0), Is.EqualTo(60));
            Assert.That(mappatura.Nota(5), Is.EqualTo(41));
        }

        [Test]
        public void CanaleDaTraccia()
        {
            ConfigurazioneMidi configurazione = new ConfigurazioneMidi();
            configurazione.CanalePerTraccia = new[] { 9, 8 };

            MappaturaMidiJson mappatura = new MappaturaMidiJson(configurazione);

            Assert.That(mappatura.Canale(0), Is.EqualTo(9));
            Assert.That(mappatura.Canale(1), Is.EqualTo(8));
            Assert.That(mappatura.Canale(7), Is.EqualTo(7), "Traccia fuori dall'array: canale = traccia.");
        }

        [Test]
        public void ClampDeiValori()
        {
            ConfigurazioneMidi configurazione = new ConfigurazioneMidi();
            configurazione.Velocity = 500;
            configurazione.CanalePerTraccia = new[] { 20, -3 };
            configurazione.NotaPerClip = new Dictionary<int, int> { { 0, 200 }, { 1, -5 } };

            MappaturaMidiJson alta = new MappaturaMidiJson(configurazione);
            Assert.That(alta.Velocity, Is.EqualTo(127));
            Assert.That(alta.Canale(0), Is.EqualTo(15));
            Assert.That(alta.Canale(1), Is.EqualTo(0));
            Assert.That(alta.Canale(-1), Is.EqualTo(0));
            Assert.That(alta.Canale(40), Is.EqualTo(15));
            Assert.That(alta.Nota(0), Is.EqualTo(127));
            Assert.That(alta.Nota(1), Is.EqualTo(0));

            configurazione.Velocity = 0;
            MappaturaMidiJson bassa = new MappaturaMidiJson(configurazione);
            Assert.That(bassa.Velocity, Is.EqualTo(1));
        }

        [Test]
        public void PercorsoVuoto()
        {
            Assert.Throws<ArgumentException>(() => MappaturaMidiJson.Carica(null));
            Assert.Throws<ArgumentException>(() => MappaturaMidiJson.Carica(""));
        }

        [Test]
        public void FileMancanteLanciaEccezioneChiara()
        {
            string percorso = Path.Combine(cartellaTemp, "non-esiste.json");

            FileNotFoundException eccezione = Assert.Throws<FileNotFoundException>(
                () => MappaturaMidiJson.Carica(percorso));

            Assert.That(eccezione.Message, Does.Contain(percorso));
        }

        [Test]
        public void JsonMalformato()
        {
            string percorso = Path.Combine(cartellaTemp, "malformato.json");
            File.WriteAllText(percorso, "{ \"Velocity\": ");

            Assert.Throws<InvalidDataException>(() => MappaturaMidiJson.Carica(percorso));
        }
    }
}
