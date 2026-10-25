using System;
using NUnit.Framework;
using VisualMusicBlockLooper.App;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class PresentatoreRegoliWebcamTests
    {
        [Test]
        public void LimitiDeiRegoli_SonoQuelliDelloSpec()
        {
            Assert.That(PresentatoreRegoliWebcam.RegoloFuocoMax, Is.EqualTo(51));
            Assert.That(PresentatoreRegoliWebcam.RegoloEsposizioneMin, Is.EqualTo(-26));
            Assert.That(PresentatoreRegoliWebcam.RegoloEsposizioneMax, Is.EqualTo(0));
        }

        [TestCase(0, 640, 480)]
        [TestCase(2, 1280, 720)]
        [TestCase(4, 1920, 1080)]
        [TestCase(-3, 640, 480)]
        [TestCase(99, 1920, 1080)]
        public void RisoluzioneDaRegolo_DaIlPresetLimitato(int indice, int larghezza, int altezza)
        {
            Assert.That(PresentatoreRegoliWebcam.RisoluzioneDaRegolo(indice), Is.EqualTo((larghezza, altezza)));
        }

        [Test]
        public void RegoloRisoluzione_PreferisceIlConfig()
        {
            Assert.That(PresentatoreRegoliWebcam.RegoloRisoluzione(1920, 1080, 640, 480), Is.EqualTo(4));
        }

        [Test]
        public void RegoloRisoluzione_SenzaConfigUsaLaWebcamReale()
        {
            Assert.That(PresentatoreRegoliWebcam.RegoloRisoluzione(0, 0, 800, 600), Is.EqualTo(1));
        }

        [Test]
        public void RegoloRisoluzione_SenzaNulla_E1280x720()
        {
            Assert.That(PresentatoreRegoliWebcam.RegoloRisoluzione(0, 0, 0, 0), Is.EqualTo(2));
        }

        [Test]
        public void RegoloRisoluzione_ValoreNonStandard_PresetPiuVicino()
        {
            Assert.That(PresentatoreRegoliWebcam.RegoloRisoluzione(1300, 730, 0, 0), Is.EqualTo(2));
        }

        [TestCase(2, "Risoluzione: 1280x720")]
        [TestCase(0, "Risoluzione: 640x480")]
        [TestCase(7, "Risoluzione: 1920x1080")]
        public void EtichettaRisoluzione_MostraLaDimensione(int indice, string atteso)
        {
            Assert.That(PresentatoreRegoliWebcam.EtichettaRisoluzione(indice), Is.EqualTo(atteso));
        }

        [TestCase(0, 0)]
        [TestCase(1, 5)]
        [TestCase(24, 120)]
        [TestCase(51, 255)]
        [TestCase(80, 255)]
        [TestCase(-4, 0)]
        public void FuocoDaRegolo_PassiDiCinque(int regolo, int atteso)
        {
            Assert.That(PresentatoreRegoliWebcam.FuocoDaRegolo(regolo), Is.EqualTo(atteso));
        }

        [TestCase(0, 0)]
        [TestCase(120, 24)]
        [TestCase(255, 51)]
        [TestCase(122, 24)]
        [TestCase(123, 25)]
        [TestCase(400, 51)]
        [TestCase(-10, 0)]
        public void RegoloDaFuoco_ArrotondaAlPassoPiuVicino(int fuoco, int atteso)
        {
            Assert.That(PresentatoreRegoliWebcam.RegoloDaFuoco(fuoco), Is.EqualTo(atteso));
        }

        [Test]
        public void Fuoco_AndataERitorno_ConservaIMultipliDelPasso()
        {
            for (int regolo = 0; regolo <= PresentatoreRegoliWebcam.RegoloFuocoMax; regolo++)
            {
                Assert.That(
                    PresentatoreRegoliWebcam.RegoloDaFuoco(PresentatoreRegoliWebcam.FuocoDaRegolo(regolo)),
                    Is.EqualTo(regolo));
            }
        }

        [TestCase(0, 0.0)]
        [TestCase(-1, -0.5)]
        [TestCase(-14, -7.0)]
        [TestCase(-26, -13.0)]
        [TestCase(-40, -13.0)]
        [TestCase(5, 0.0)]
        public void EsposizioneDaRegolo_MezziPassi(int regolo, double atteso)
        {
            Assert.That(PresentatoreRegoliWebcam.EsposizioneDaRegolo(regolo), Is.EqualTo(atteso));
        }

        [TestCase(0.0, 0)]
        [TestCase(-7.0, -14)]
        [TestCase(-6.5, -13)]
        [TestCase(-13.0, -26)]
        [TestCase(-20.0, -26)]
        [TestCase(2.0, 0)]
        [TestCase(-6.8, -14)]
        public void RegoloDaEsposizione_ArrotondaAlMezzoPasso(double esposizione, int atteso)
        {
            Assert.That(PresentatoreRegoliWebcam.RegoloDaEsposizione(esposizione), Is.EqualTo(atteso));
        }

        [Test]
        public void Esposizione_AndataERitorno_ConservaOgniPosizione()
        {
            for (int regolo = PresentatoreRegoliWebcam.RegoloEsposizioneMin; regolo <= PresentatoreRegoliWebcam.RegoloEsposizioneMax; regolo++)
            {
                Assert.That(
                    PresentatoreRegoliWebcam.RegoloDaEsposizione(PresentatoreRegoliWebcam.EsposizioneDaRegolo(regolo)),
                    Is.EqualTo(regolo));
            }
        }

        [Test]
        public void PosizioniIniziali_ConfigHaLaPrecedenzaSullaWebcam()
        {
            var config = new ConfigurazioneWebcam
            {
                FuocoAutomatico = false, Fuoco = 120, EsposizioneAutomatica = false, Esposizione = -7
            };
            var stato = new StatoImpostazioniWebcam
            {
                FuocoAutomatico = true, Fuoco = 30, EsposizioneAutomatica = true, Esposizione = -3
            };

            Assert.That(PresentatoreRegoliWebcam.FuocoAutomaticoIniziale(config, stato), Is.False);
            Assert.That(PresentatoreRegoliWebcam.RegoloFuocoIniziale(config, stato), Is.EqualTo(24));
            Assert.That(PresentatoreRegoliWebcam.EsposizioneAutomaticaIniziale(config, stato), Is.False);
            Assert.That(PresentatoreRegoliWebcam.RegoloEsposizioneIniziale(config, stato), Is.EqualTo(-14));
        }

        [Test]
        public void PosizioniIniziali_SenzaConfigUsanoLaWebcam()
        {
            var config = new ConfigurazioneWebcam();
            var stato = new StatoImpostazioniWebcam
            {
                FuocoAutomatico = false, Fuoco = 30, EsposizioneAutomatica = false, Esposizione = -3
            };

            Assert.That(PresentatoreRegoliWebcam.FuocoAutomaticoIniziale(config, stato), Is.False);
            Assert.That(PresentatoreRegoliWebcam.RegoloFuocoIniziale(config, stato), Is.EqualTo(6));
            Assert.That(PresentatoreRegoliWebcam.EsposizioneAutomaticaIniziale(config, stato), Is.False);
            Assert.That(PresentatoreRegoliWebcam.RegoloEsposizioneIniziale(config, stato), Is.EqualTo(-6));
        }

        [Test]
        public void PosizioniIniziali_SenzaNulla_Automatico_EValoriNeutri()
        {
            var config = new ConfigurazioneWebcam();
            var stato = StatoImpostazioniWebcam.NonSupportato();

            Assert.That(PresentatoreRegoliWebcam.FuocoAutomaticoIniziale(config, stato), Is.True);
            Assert.That(PresentatoreRegoliWebcam.RegoloFuocoIniziale(config, stato), Is.EqualTo(0));
            Assert.That(PresentatoreRegoliWebcam.EsposizioneAutomaticaIniziale(config, stato), Is.True);
            Assert.That(PresentatoreRegoliWebcam.RegoloEsposizioneIniziale(config, stato), Is.EqualTo(-12));
        }

        [Test]
        public void ParzialeFuoco_Manuale_HaFlagEValore()
        {
            ConfigurazioneWebcam p = PresentatoreRegoliWebcam.ParzialeFuoco(false, 24);

            Assert.That(p.FuocoAutomatico, Is.False);
            Assert.That(p.Fuoco, Is.EqualTo(120));
            Assert.That(p.EsposizioneAutomatica, Is.Null);
            Assert.That(p.Esposizione, Is.Null);
            Assert.That(p.Larghezza, Is.EqualTo(0));
        }

        [Test]
        public void ParzialeFuoco_Auto_HaSoloIlFlag()
        {
            ConfigurazioneWebcam p = PresentatoreRegoliWebcam.ParzialeFuoco(true, 24);

            Assert.That(p.FuocoAutomatico, Is.True);
            Assert.That(p.Fuoco, Is.Null);
        }

        [Test]
        public void ParzialeEsposizione_Manuale_HaFlagEValore()
        {
            ConfigurazioneWebcam p = PresentatoreRegoliWebcam.ParzialeEsposizione(false, -14);

            Assert.That(p.EsposizioneAutomatica, Is.False);
            Assert.That(p.Esposizione, Is.EqualTo(-7.0));
            Assert.That(p.FuocoAutomatico, Is.Null);
            Assert.That(p.Fuoco, Is.Null);
        }

        [Test]
        public void ParzialeEsposizione_Auto_HaSoloIlFlag()
        {
            ConfigurazioneWebcam p = PresentatoreRegoliWebcam.ParzialeEsposizione(true, -14);

            Assert.That(p.EsposizioneAutomatica, Is.True);
            Assert.That(p.Esposizione, Is.Null);
        }

        [Test]
        public void ParzialeRisoluzione_HaSoloLeDimensioni()
        {
            ConfigurazioneWebcam p = PresentatoreRegoliWebcam.ParzialeRisoluzione(3);

            Assert.That(p.Larghezza, Is.EqualTo(1600));
            Assert.That(p.Altezza, Is.EqualTo(900));
            Assert.That(p.FuocoAutomatico, Is.Null);
            Assert.That(p.Fuoco, Is.Null);
            Assert.That(p.EsposizioneAutomatica, Is.Null);
            Assert.That(p.Esposizione, Is.Null);
        }

        [Test]
        public void Unisci_CopiaSoloICampiPresenti()
        {
            var destinazione = new ConfigurazioneWebcam
            {
                Nome = "Cam", Percorso = "P", Indice = 2, Larghezza = 1280, Altezza = 720,
                FuocoAutomatico = false, Fuoco = 100, EsposizioneAutomatica = true, Esposizione = -5
            };

            PresentatoreRegoliWebcam.Unisci(destinazione, PresentatoreRegoliWebcam.ParzialeEsposizione(false, -14));

            Assert.That(destinazione.EsposizioneAutomatica, Is.False);
            Assert.That(destinazione.Esposizione, Is.EqualTo(-7.0));
            Assert.That(destinazione.FuocoAutomatico, Is.False);
            Assert.That(destinazione.Fuoco, Is.EqualTo(100));
            Assert.That(destinazione.Larghezza, Is.EqualTo(1280));
            Assert.That(destinazione.Altezza, Is.EqualTo(720));
            Assert.That(destinazione.Nome, Is.EqualTo("Cam"));
            Assert.That(destinazione.Percorso, Is.EqualTo("P"));
            Assert.That(destinazione.Indice, Is.EqualTo(2));
        }

        [Test]
        public void Unisci_AutoConservaIlValoreManualeSalvato()
        {
            var destinazione = new ConfigurazioneWebcam { FuocoAutomatico = false, Fuoco = 100 };

            PresentatoreRegoliWebcam.Unisci(destinazione, PresentatoreRegoliWebcam.ParzialeFuoco(true, 0));

            Assert.That(destinazione.FuocoAutomatico, Is.True);
            Assert.That(destinazione.Fuoco, Is.EqualTo(100));
        }

        [Test]
        public void Unisci_Risoluzione_SostituisceLeDimensioni()
        {
            var destinazione = new ConfigurazioneWebcam { Larghezza = 640, Altezza = 480 };

            PresentatoreRegoliWebcam.Unisci(destinazione, PresentatoreRegoliWebcam.ParzialeRisoluzione(4));

            Assert.That(destinazione.Larghezza, Is.EqualTo(1920));
            Assert.That(destinazione.Altezza, Is.EqualTo(1080));
        }

        [Test]
        public void Unisci_ParzialeVuoto_NonCambiaNulla()
        {
            var destinazione = new ConfigurazioneWebcam { Larghezza = 640, Altezza = 480, Fuoco = 5 };

            PresentatoreRegoliWebcam.Unisci(destinazione, new ConfigurazioneWebcam());
            PresentatoreRegoliWebcam.Unisci(destinazione, null);

            Assert.That(destinazione.Larghezza, Is.EqualTo(640));
            Assert.That(destinazione.Altezza, Is.EqualTo(480));
            Assert.That(destinazione.Fuoco, Is.EqualTo(5));
        }

        [Test]
        public void Unisci_DestinazioneNulla_Lancia()
        {
            Assert.Throws<ArgumentNullException>(
                () => PresentatoreRegoliWebcam.Unisci(null, new ConfigurazioneWebcam()));
        }

        [TestCase(true, false, 24, "Fuoco: 120")]
        [TestCase(true, true, 24, "Fuoco: automatico")]
        [TestCase(false, false, 24, "Fuoco: non supportato")]
        [TestCase(false, true, 24, "Fuoco: non supportato")]
        public void EtichettaFuoco_Testi(bool supportato, bool automatico, int regolo, string atteso)
        {
            Assert.That(PresentatoreRegoliWebcam.EtichettaFuoco(supportato, automatico, regolo), Is.EqualTo(atteso));
        }

        [TestCase(true, false, -14, "Esposizione: 7,8 ms")]
        [TestCase(true, false, -6, "Esposizione: 125 ms")]
        [TestCase(true, true, -14, "Esposizione: automatica")]
        [TestCase(false, false, -14, "Esposizione: non supportata")]
        [TestCase(false, true, -14, "Esposizione: non supportata")]
        public void EtichettaEsposizione_Testi(bool supportata, bool automatica, int regolo, string atteso)
        {
            Assert.That(PresentatoreRegoliWebcam.EtichettaEsposizione(supportata, automatica, regolo), Is.EqualTo(atteso));
        }

        [Test]
        public void TestoFps_Completo()
        {
            Assert.That(
                PresentatoreRegoliWebcam.TestoFps(29.8, 30, 14.9, 1280, 720),
                Is.EqualTo("Webcam: 29,8 fps (dichiarati 30) | Elaborazione: 14,9 fps | 1280x720"));
        }

        [Test]
        public void TestoFps_DichiaratiIgnoti_NonDisponibile()
        {
            Assert.That(
                PresentatoreRegoliWebcam.TestoFps(10, 0, 5, 640, 480),
                Is.EqualTo("Webcam: 10,0 fps (dichiarati n/d) | Elaborazione: 5,0 fps | 640x480"));
        }

        [Test]
        public void TestoFps_DichiaratiConDecimali()
        {
            Assert.That(
                PresentatoreRegoliWebcam.TestoFps(29.97, 29.97, 15, 1920, 1080),
                Is.EqualTo("Webcam: 30,0 fps (dichiarati 29,97) | Elaborazione: 15,0 fps | 1920x1080"));
        }

        [TestCase(0, 0)]
        [TestCase(0, 480)]
        [TestCase(640, 0)]
        public void TestoFps_SenzaWebcam_NonAttiva(int larghezza, int altezza)
        {
            Assert.That(
                PresentatoreRegoliWebcam.TestoFps(0, 0, 0, larghezza, altezza),
                Is.EqualTo("Webcam non attiva"));
        }
    }
}
