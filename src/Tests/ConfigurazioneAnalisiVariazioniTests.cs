using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.App;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class ConfigurazioneAnalisiVariazioniTests
    {
        private string cartella;

        [SetUp]
        public void Prepara()
        {
            cartella=Path.Combine(Path.GetTempPath(),"vmbl-variazioni-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(cartella);
        }

        [TearDown]
        public void Pulisci()
        {
            if(Directory.Exists(cartella))Directory.Delete(cartella,true);
        }

        [Test]
        public void ConfigurazionePrecedenteMantieneLetturaIncrementaleEScriveITuttiIDefault()
        {
            string percorso=Path.Combine(cartella,ConfigurazioneApplicazione.NomeFile);
            File.WriteAllText(percorso,"{\"PulsazioniPerOsservazione\":2,\"Webcam\":{\"Indice\":1}}");

            var configurazione=ConfigurazioneApplicazione.Carica(percorso);
            configurazione.Salva(percorso);
            var json=JObject.Parse(File.ReadAllText(percorso));

            Assert.That(json["PulsazioniPerOsservazione"].Value<int>(),Is.EqualTo(2));
            Assert.That(json["Webcam"]["Indice"].Value<int>(),Is.EqualTo(1));
            Assert.That(json["AnalizzaSoloSeCambia"]?.Value<bool>(),Is.True);
            Assert.That(json["AnalisiVariazioni"]?["LarghezzaAnalisi"]?.Value<int>(),Is.EqualTo(640));
            Assert.That(json["AnalisiVariazioni"]?["AltezzaAnalisi"]?.Value<int>(),Is.EqualTo(360));
            Assert.That(json["AnalisiVariazioni"]?["PercentileSfondo"]?.Value<double>(),Is.EqualTo(.85));
            Assert.That(json["AnalisiVariazioni"]?["LuminositaMinimaSfondo"]?.Value<int>(),Is.EqualTo(80));
            Assert.That(json["AnalisiVariazioni"]?["SogliaGrigioRelativa"]?.Value<double>(),Is.EqualTo(.55));
            Assert.That(json["AnalisiVariazioni"]?["SogliaNeroRelativa"]?.Value<double>(),Is.EqualTo(.30));
            Assert.That(json["AnalisiVariazioni"]?["SogliaColoreRelativa"]?.Value<double>(),Is.EqualTo(.25));
            Assert.That(json["AnalisiVariazioni"]?["SogliaDominanzaColore"]?.Value<double>(),Is.EqualTo(.60));
            Assert.That(json["AnalisiVariazioni"]?["SogliaInstabilitaGlobale"]?.Value<double>(),Is.EqualTo(.45));
            Assert.That(json["AnalisiVariazioni"]?["SogliaVariazioneFrame"]?.Value<double>(),Is.EqualTo(.02));
            Assert.That(json["AnalisiVariazioni"]?["DimensioneDilatazione"]?.Value<int>(),Is.EqualTo(9));
            Assert.That(json["AnalisiVariazioni"]?["AreaMinimaContorno"]?.Value<int>(),Is.EqualTo(30));
            Assert.That(json["AnalisiVariazioni"]?["MargineRegione"]?.Value<int>(),Is.EqualTo(12));
            Assert.That(json["AnalisiVariazioni"]?["FrazioneQrVicinoAlFondo"]?.Value<double>(),Is.EqualTo(.72));
        }

        [Test]
        public void ModalitaCompletaESogliePersonalizzateSopravvivonoAlRoundTrip()
        {
            string percorso=Path.Combine(cartella,ConfigurazioneApplicazione.NomeFile);
            File.WriteAllText(percorso,"""
                {
                  "AnalizzaSoloSeCambia": false,
                  "PulsazioniPerOsservazione": 4,
                  "AnalisiVariazioni": {
                    "LarghezzaAnalisi": 320,
                    "AltezzaAnalisi": 180,
                    "PercentileSfondo": 0.80,
                    "LuminositaMinimaSfondo": 65,
                    "SogliaGrigioRelativa": 0.70,
                    "MargineRegione": 18
                  }
                }
                """);

            var letta=ConfigurazioneApplicazione.Carica(percorso);
            letta.Salva(percorso);
            var ricaricata=ConfigurazioneApplicazione.Carica(percorso);
            var json=JObject.Parse(File.ReadAllText(percorso));

            Assert.That(json["AnalizzaSoloSeCambia"]?.Value<bool>(),Is.False);
            Assert.That(ricaricata.PulsazioniPerOsservazione,Is.EqualTo(4));
            Assert.That(json["AnalisiVariazioni"]?["LarghezzaAnalisi"]?.Value<int>(),Is.EqualTo(320));
            Assert.That(json["AnalisiVariazioni"]?["AltezzaAnalisi"]?.Value<int>(),Is.EqualTo(180));
            Assert.That(json["AnalisiVariazioni"]?["PercentileSfondo"]?.Value<double>(),Is.EqualTo(.80));
            Assert.That(json["AnalisiVariazioni"]?["LuminositaMinimaSfondo"]?.Value<int>(),Is.EqualTo(65));
            Assert.That(json["AnalisiVariazioni"]?["SogliaGrigioRelativa"]?.Value<double>(),Is.EqualTo(.70));
            Assert.That(json["AnalisiVariazioni"]?["MargineRegione"]?.Value<int>(),Is.EqualTo(18));
        }
    }
}
