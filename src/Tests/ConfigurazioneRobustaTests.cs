using System.IO;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class ConfigurazioneRobustaTests
    {
        [Test]
        public void DefaultUsaQuattroMisureDiAnticipoETolleranzaQrConfigurata()
        {
            var configurazione = new ConfigurazioneApplicazione();

            Assert.That(new ConfigGriglia().TolleranzaQrCelle, Is.EqualTo(0.35).Within(1e-9));
            Assert.That(configurazione.BpmIniziale, Is.EqualTo(100));
            Assert.That(configurazione.MisureAnticipoScansione, Is.EqualTo(4));
            Assert.That(configurazione.TolleranzaQrCelle, Is.EqualTo(0.35).Within(1e-9));
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

                Assert.That(configurazione.MisureAnticipoScansione, Is.EqualTo(4));
                Assert.That(configurazione.TolleranzaQrCelle, Is.EqualTo(0.35).Within(1e-9));
                Assert.That(configurazione.BpmIniziale, Is.EqualTo(300));
            }
            finally
            {
                if (File.Exists(percorso)) File.Delete(percorso);
            }
        }

        [Test]
        public void CorrezioneBordoHaIDefaultAttesi()
        {
            var configurazione = new ConfigurazioneApplicazione();

            Assert.That(configurazione.CorrezioneBordo.Pieno, Is.EqualTo(0.04).Within(1e-9));
            Assert.That(configurazione.CorrezioneBordo.Esterno, Is.EqualTo(0.22).Within(1e-9));
            Assert.That(configurazione.CorrezioneBordo.TipoSferico, Is.EqualTo("Equisolido"));
            Assert.That(configurazione.CorrezioneBordo.Focale, Is.EqualTo(0).Within(1e-9));
        }

        [Test]
        public void CorrezioneBordoNonValidaRipristinaIDefault()
        {
            string percorso = Path.GetTempFileName();
            try
            {
                File.WriteAllText(percorso,
                    "{\"CorrezioneBordo\":{\"Pieno\":0.20,\"Esterno\":0.22}}");
                var configurazione = ConfigurazioneApplicazione.Carica(percorso);

                Assert.That(configurazione.CorrezioneBordo.Pieno, Is.EqualTo(0.04).Within(1e-9));
                Assert.That(configurazione.CorrezioneBordo.Esterno, Is.EqualTo(0.22).Within(1e-9));
            }
            finally
            {
                if (File.Exists(percorso)) File.Delete(percorso);
            }
        }

        [Test]
        public void CorrezioneBordoValidaVieneConservata()
        {
            string percorso = Path.GetTempFileName();
            try
            {
                File.WriteAllText(percorso,
                    "{\"CorrezioneBordo\":{\"Pieno\":0.05,\"Esterno\":0.26}}");
                var configurazione = ConfigurazioneApplicazione.Carica(percorso);

                Assert.That(configurazione.CorrezioneBordo.Pieno, Is.EqualTo(0.05).Within(1e-9));
                Assert.That(configurazione.CorrezioneBordo.Esterno, Is.EqualTo(0.26).Within(1e-9));
            }
            finally
            {
                if (File.Exists(percorso)) File.Delete(percorso);
            }
        }

        [Test]
        public void ModelloSmussatoVieneConservato()
        {
            string percorso = Path.GetTempFileName();
            try
            {
                File.WriteAllText(percorso,
                    "{\"CorrezioneBordo\":{\"Modello\":\"BordoSmussato\",\"Pieno\":0.05,\"Esterno\":0.26,"
                    + "\"FattoreX\":0.8,\"FattoreY\":1.2,\"Smussamento\":20}}");
                var parametri = ConfigurazioneApplicazione.Carica(percorso).CorrezioneBordo.Converti();

                Assert.That(parametri.Modello, Is.EqualTo(VisualMusicBlockLooper.Vision.ModelloCorrezioneBordo.BordoSmussato));
                Assert.That(parametri.Pieno, Is.EqualTo(0.05).Within(1e-9));
                Assert.That(parametri.Esterno, Is.EqualTo(0.26).Within(1e-9));
                Assert.That(parametri.FattoreX, Is.EqualTo(0.8).Within(1e-9));
                Assert.That(parametri.FattoreY, Is.EqualTo(1.2).Within(1e-9));
                Assert.That(parametri.Smussamento, Is.EqualTo(20).Within(1e-9));
            }
            finally
            {
                if (File.Exists(percorso)) File.Delete(percorso);
            }
        }

        [Test]
        public void ModelloSfericoVieneConservato()
        {
            string percorso = Path.GetTempFileName();
            try
            {
                File.WriteAllText(percorso,
                    "{\"CorrezioneBordo\":{\"Modello\":\"Sferico\",\"TipoSferico\":\"Equisolido\","
                    + "\"Focale\":737,\"CentroX\":672,\"CentroY\":494}}");
                var parametri = ConfigurazioneApplicazione.Carica(percorso).CorrezioneBordo.Converti();

                Assert.That(parametri.Modello, Is.EqualTo(VisualMusicBlockLooper.Vision.ModelloCorrezioneBordo.Sferico));
                Assert.That(parametri.TipoSferico, Is.EqualTo(VisualMusicBlockLooper.Vision.TipoProiezioneSferica.Equisolido));
                Assert.That(parametri.Focale, Is.EqualTo(737).Within(1e-9));
                Assert.That(parametri.CentroX, Is.EqualTo(672).Within(1e-9));
                Assert.That(parametri.CentroY, Is.EqualTo(494).Within(1e-9));
            }
            finally
            {
                if (File.Exists(percorso)) File.Delete(percorso);
            }
        }

        [Test]
        public void ModalitaLetturaCompletaVieneConservata()
        {
            string percorso = Path.GetTempFileName();
            try
            {
                File.WriteAllText(percorso,
                    "{\"ModalitaLettura\":\"CompletaPeriodica\",\"PeriodoLetturaCompleta\":8}");
                var configurazione = ConfigurazioneApplicazione.Carica(percorso);

                Assert.That(configurazione.ModalitaLettura, Is.EqualTo("CompletaPeriodica"));
                Assert.That(configurazione.PeriodoLetturaCompleta, Is.EqualTo(8));
            }
            finally
            {
                if (File.Exists(percorso)) File.Delete(percorso);
            }
        }

        [Test]
        public void ModalitaLetturaNonValidaRipristinaProgressivaEQuattroMisure()
        {
            string percorso = Path.GetTempFileName();
            try
            {
                File.WriteAllText(percorso,
                    "{\"ModalitaLettura\":\"Sconosciuta\",\"PeriodoLetturaCompleta\":3}");
                var configurazione = ConfigurazioneApplicazione.Carica(percorso);

                Assert.That(configurazione.ModalitaLettura, Is.EqualTo("Progressiva"));
                Assert.That(configurazione.PeriodoLetturaCompleta, Is.EqualTo(4));
            }
            finally
            {
                if (File.Exists(percorso)) File.Delete(percorso);
            }
        }

        [Test]
        public void CanaliRigaNonValidiVengonoNormalizzati()
        {
            string percorso = Path.GetTempFileName();
            try
            {
                File.WriteAllText(percorso,
                    "{\"CanaliRiga\":[3,-2,20]," +
                    "\"StrumentiRiga\":[\" Piano \",null,\"\",\"Basso\"]}");
                var configurazione = ConfigurazioneApplicazione.Carica(percorso);

                Assert.That(configurazione.CanaliRiga, Has.Length.EqualTo(StrutturaTavolo.RigheTotali));
                Assert.That(configurazione.CanaliRiga[0], Is.EqualTo(3));
                Assert.That(configurazione.CanaliRiga[1], Is.EqualTo(-1));
                Assert.That(configurazione.CanaliRiga[2], Is.EqualTo(-1), "Fuori 0..15 viene ignorato");
                Assert.That(configurazione.CanaliRiga[3], Is.EqualTo(-1));

                Assert.That(configurazione.StrumentiRiga[0], Is.EqualTo("Piano"));
                Assert.That(configurazione.StrumentiRiga[1], Is.Null);
                Assert.That(configurazione.StrumentiRiga[2], Is.Null);
                Assert.That(configurazione.StrumentiRiga[3], Is.EqualTo("Basso"));
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
