using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class ElencoWebcamTests
    {
        [Test]
        public void IndiciContigui_SiFermaAlPrimoNonApribile()
        {
            IReadOnlyList<InfoWebcam> elenco = ElencoWebcam.Rileva(indice => indice < 2);

            Assert.That(elenco.Select(w => w.Indice), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(elenco[1].Descrizione, Is.EqualTo("Webcam 1"));
        }

        [Test]
        public void NessunaWebcam_ElencoVuoto()
        {
            Assert.That(ElencoWebcam.Rileva(indice => false), Is.Empty);
        }

        [Test]
        public void IndiceInUso_IncluSoSenzaProvarlo()
        {
            List<int> provati = new List<int>();

            IReadOnlyList<InfoWebcam> elenco = ElencoWebcam.Rileva(
                indice => { provati.Add(indice); return indice != 0 && indice < 3; }, indiceInUso: 0);

            Assert.That(elenco.Select(w => w.Indice), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(provati, Does.Not.Contain(0), "L'indice in uso non va riaperto: risulterebbe occupato.");
        }

        [Test]
        public void RispettaIlMassimo()
        {
            Assert.That(ElencoWebcam.Rileva(indice => true, massimo: 3).Count, Is.EqualTo(3));
        }

        [Test]
        public void EccezioneNellaProva_ValeNonApribile()
        {
            IReadOnlyList<InfoWebcam> elenco = ElencoWebcam.Rileva(
                indice => { if (indice == 1) { throw new InvalidOperationException(); } return true; });

            Assert.That(elenco.Select(w => w.Indice), Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void IndiceInUsoOltreIlMassimo_RestaSelezionabile()
        {
            IReadOnlyList<InfoWebcam> elenco = ElencoWebcam.Rileva(indice => false, indiceInUso: 9, massimo: 4);

            Assert.That(elenco.Select(w => w.Indice), Is.EqualTo(new[] { 9 }));
        }
    }

    [TestFixture]
    public class ConfigurazioneApplicazioneTests
    {
        private string cartella;

        [SetUp]
        public void Prepara()
        {
            cartella = Path.Combine(Path.GetTempPath(), "vmbl-config-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(cartella);
        }

        [TearDown]
        public void Pulisci()
        {
            if (Directory.Exists(cartella))
            {
                Directory.Delete(cartella, true);
            }
        }

        [Test]
        public void FileMancante_RestituisceIDefault()
        {
            ConfigurazioneApplicazione configurazione =
                ConfigurazioneApplicazione.Carica(Path.Combine(cartella, ConfigurazioneApplicazione.NomeFile));

            Assert.That(configurazione.Webcam.Indice, Is.EqualTo(0));
        }

        [Test]
        public void SalvaECarica_ConservaLIndiceDellaWebcam()
        {
            string percorso = Path.Combine(cartella, ConfigurazioneApplicazione.NomeFile);
            new ConfigurazioneApplicazione { Webcam = new ConfigurazioneWebcam { Indice = 3 } }.Salva(percorso);

            Assert.That(ConfigurazioneApplicazione.Carica(percorso).Webcam.Indice, Is.EqualTo(3));
        }

        [Test]
        public void SalvaECarica_ConservaLaRisoluzioneRichiesta()
        {
            string percorso = Path.Combine(cartella, ConfigurazioneApplicazione.NomeFile);
            new ConfigurazioneApplicazione { Webcam = new ConfigurazioneWebcam { Larghezza = 1920, Altezza = 1080 } }.Salva(percorso);

            ConfigurazioneWebcam webcam = ConfigurazioneApplicazione.Carica(percorso).Webcam;
            Assert.That(webcam.Larghezza, Is.EqualTo(1920));
            Assert.That(webcam.Altezza, Is.EqualTo(1080));
        }

        [Test]
        public void ConfigurazioneSenzaRisoluzione_UsaIlDefault()
        {
            string percorso = Path.Combine(cartella, ConfigurazioneApplicazione.NomeFile);
            File.WriteAllText(percorso, "{ \"Webcam\": { \"Nome\": \"X\", \"Indice\": 1 } }");

            ConfigurazioneWebcam webcam = ConfigurazioneApplicazione.Carica(percorso).Webcam;
            Assert.That(webcam.Larghezza, Is.EqualTo(0), "0 = risoluzione predefinita (1280x720)");
            Assert.That(webcam.Altezza, Is.EqualTo(0));
        }

        [Test]
        public void JsonLeggibileDaUmano_ContieneLaSezioneWebcam()
        {
            string percorso = Path.Combine(cartella, ConfigurazioneApplicazione.NomeFile);
            new ConfigurazioneApplicazione { Webcam = new ConfigurazioneWebcam { Indice = 2 } }.Salva(percorso);

            string testo = File.ReadAllText(percorso);

            Assert.That(testo, Does.Contain("\"Webcam\"").And.Contain("\"Indice\": 2"));
        }

        [Test]
        public void JsonMalformato_LanciaInvalidData()
        {
            string percorso = Path.Combine(cartella, ConfigurazioneApplicazione.NomeFile);
            File.WriteAllText(percorso, "{ non e' json");

            Assert.Throws<InvalidDataException>(() => ConfigurazioneApplicazione.Carica(percorso));
        }

        [Test]
        public void IndiceNegativoOWebcamNull_VieneNormalizzato()
        {
            string percorso = Path.Combine(cartella, ConfigurazioneApplicazione.NomeFile);

            File.WriteAllText(percorso, "{ \"Webcam\": { \"Indice\": -4 } }");
            Assert.That(ConfigurazioneApplicazione.Carica(percorso).Webcam.Indice, Is.EqualTo(0));

            File.WriteAllText(percorso, "{ \"Webcam\": null }");
            Assert.That(ConfigurazioneApplicazione.Carica(percorso).Webcam, Is.Not.Null);
        }

        [Test]
        public void PercorsoVuoto_LanciaArgumentException()
        {
            Assert.Throws<ArgumentException>(() => ConfigurazioneApplicazione.Carica(" "));
            Assert.Throws<ArgumentException>(() => new ConfigurazioneApplicazione().Salva(null));
        }
    }

    [TestFixture]
    public class GestoreWebcamTests
    {
        private sealed class SorgenteFinta : ISorgenteWebcam
        {
            public SorgenteFinta(int indice, int larghezza, int altezza)
            {
                Indice = indice;
                Larghezza = larghezza;
                Altezza = altezza;
            }

            public int Indice { get; }

            public int Larghezza { get; }

            public int Altezza { get; }

            public bool Rilasciata { get; private set; }

            public Mat Frame()
            {
                return Rilasciata ? null : new Mat(2, 2, MatType.CV_8UC3);
            }

            public Bitmap Anteprima()
            {
                return null;
            }

            public Bitmap AnteprimaRaddrizzata(VisualMusicBlockLooper.Vision.Calibrazione calibrazione, int larghezza, int altezza)
            {
                return null;
            }

            public void Dispose()
            {
                Rilasciata = true;
            }
        }

        private static GestoreWebcam CreaGestore(List<SorgenteFinta> aperte, params int[] indiciDisponibili)
        {
            return new GestoreWebcam(indice =>
            {
                if (!indiciDisponibili.Contains(indice))
                {
                    return null;
                }

                SorgenteFinta sorgente = new SorgenteFinta(indice, 1280, 720);
                aperte.Add(sorgente);
                return sorgente;
            });
        }

        [Test]
        public void SenzaWebcam_NonDisponibileEFrameNull()
        {
            using (GestoreWebcam gestore = CreaGestore(new List<SorgenteFinta>()))
            {
                Assert.That(gestore.Disponibile, Is.False);
                Assert.That(gestore.IndiceCorrente, Is.EqualTo(-1));
                Assert.That(gestore.Larghezza, Is.EqualTo(0));
                Assert.That(gestore.Frame(), Is.Null);
                Assert.That(gestore.Anteprima(), Is.Null);
            }
        }

        [Test]
        public void Apri_Riuscito_DiventaLaWebcamCorrente()
        {
            using (GestoreWebcam gestore = CreaGestore(new List<SorgenteFinta>(), 0, 1))
            {
                Assert.That(gestore.Apri(1), Is.True);

                Assert.That(gestore.Disponibile, Is.True);
                Assert.That(gestore.IndiceCorrente, Is.EqualTo(1));
                Assert.That(gestore.Larghezza, Is.EqualTo(1280));
                Assert.That(gestore.Altezza, Is.EqualTo(720));
                using (Mat frame = gestore.Frame())
                {
                    Assert.That(frame, Is.Not.Null);
                }
            }
        }

        [Test]
        public void CambioWebcam_RilasciaLaVecchia()
        {
            List<SorgenteFinta> aperte = new List<SorgenteFinta>();
            using (GestoreWebcam gestore = CreaGestore(aperte, 0, 1))
            {
                gestore.Apri(0);
                gestore.Apri(1);

                Assert.That(aperte[0].Rilasciata, Is.True);
                Assert.That(aperte[1].Rilasciata, Is.False);
                Assert.That(gestore.IndiceCorrente, Is.EqualTo(1));
            }
        }

        [Test]
        public void ApriFallito_MantieneLaPrecedente()
        {
            List<SorgenteFinta> aperte = new List<SorgenteFinta>();
            using (GestoreWebcam gestore = CreaGestore(aperte, 0))
            {
                gestore.Apri(0);

                Assert.That(gestore.Apri(5), Is.False);

                Assert.That(gestore.IndiceCorrente, Is.EqualTo(0));
                Assert.That(aperte[0].Rilasciata, Is.False);
            }
        }

        [Test]
        public void StessoIndice_NonRiapre()
        {
            List<SorgenteFinta> aperte = new List<SorgenteFinta>();
            using (GestoreWebcam gestore = CreaGestore(aperte, 0))
            {
                gestore.Apri(0);
                Assert.That(gestore.Apri(0), Is.True);

                Assert.That(aperte.Count, Is.EqualTo(1));
            }
        }

        [Test]
        public void ApriCheLancia_ValeFallimento()
        {
            using (GestoreWebcam gestore = new GestoreWebcam(i => throw new InvalidOperationException("driver")))
            {
                Assert.That(gestore.Apri(0), Is.False);
                Assert.That(gestore.Disponibile, Is.False);
            }
        }

        [Test]
        public void Dispose_RilasciaLaCorrenteEImpedisceNuoveAperture()
        {
            List<SorgenteFinta> aperte = new List<SorgenteFinta>();
            GestoreWebcam gestore = CreaGestore(aperte, 0);
            gestore.Apri(0);

            gestore.Dispose();
            gestore.Dispose();

            Assert.That(aperte[0].Rilasciata, Is.True);
            Assert.That(gestore.Apri(0), Is.False);
            Assert.That(gestore.Disponibile, Is.False);
        }
    }

    [TestFixture]
    public class CaricamentoCalibrazioneConGestoreTests
    {
        private string cartella;

        [SetUp]
        public void Prepara()
        {
            cartella = Path.Combine(Path.GetTempPath(), "vmbl-cal-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(cartella);
        }

        [TearDown]
        public void Pulisci()
        {
            if (Directory.Exists(cartella))
            {
                Directory.Delete(cartella, true);
            }
        }

        private static Calibrazione CalibrazioneDi(int larghezza, int altezza)
        {
            return Calibrazione.Calcola(
                new[]
                {
                    new PuntoImmagine(0, 0), new PuntoImmagine(larghezza, 0),
                    new PuntoImmagine(larghezza, altezza), new PuntoImmagine(0, altezza)
                },
                new[] { new PuntoTavolo(0, 0), new PuntoTavolo(1, 0), new PuntoTavolo(1, 1), new PuntoTavolo(0, 1) },
                larghezza,
                altezza);
        }

        private sealed class Sorgente : ISorgenteWebcam
        {
            public int Larghezza => 1280;

            public int Altezza => 720;

            public Mat Frame() { return null; }

            public Bitmap Anteprima() { return null; }

            public Bitmap AnteprimaRaddrizzata(VisualMusicBlockLooper.Vision.Calibrazione calibrazione, int larghezza, int altezza) { return null; }

            public void Dispose() { }
        }

        [Test]
        public void SenzaWebcam_NonAttiva()
        {
            string percorso = Path.Combine(cartella, "calibrazione.txt");
            CalibrazioneDi(1280, 720).Salva(percorso);
            RilevatoreCommutabile rilevatore = new RilevatoreCommutabile();

            using (GestoreWebcam gestore = new GestoreWebcam(i => null))
            {
                bool attivo = ApplicazioneCalibrazione.TentaCaricamento(percorso, gestore, rilevatore, new List<string>());

                Assert.That(attivo, Is.False);
                Assert.That(rilevatore.Attivo, Is.False);
            }
        }

        [Test]
        public void RisoluzioneUguale_AttivaIlRilevatore()
        {
            string percorso = Path.Combine(cartella, "calibrazione.txt");
            CalibrazioneDi(1280, 720).Salva(percorso);
            RilevatoreCommutabile rilevatore = new RilevatoreCommutabile();
            List<string> avvisi = new List<string>();

            using (GestoreWebcam gestore = new GestoreWebcam(i => new Sorgente()))
            {
                gestore.Apri(0);

                bool attivo = ApplicazioneCalibrazione.TentaCaricamento(percorso, gestore, rilevatore, avvisi);

                Assert.That(attivo, Is.True);
                Assert.That(rilevatore.Attivo, Is.True);
                Assert.That(avvisi, Is.Empty);
            }
        }

        [Test]
        public void RisoluzioneDiversa_NonAttivaEAvvisa()
        {
            string percorso = Path.Combine(cartella, "calibrazione.txt");
            CalibrazioneDi(640, 480).Salva(percorso);
            RilevatoreCommutabile rilevatore = new RilevatoreCommutabile();
            List<string> avvisi = new List<string>();

            using (GestoreWebcam gestore = new GestoreWebcam(i => new Sorgente()))
            {
                gestore.Apri(0);

                bool attivo = ApplicazioneCalibrazione.TentaCaricamento(percorso, gestore, rilevatore, avvisi);

                Assert.That(attivo, Is.False);
                Assert.That(rilevatore.Attivo, Is.False);
                Assert.That(avvisi, Is.EqualTo(new[] { ApplicazioneCalibrazione.AvvisoRisoluzioneDiversa }));
            }
        }

        [Test]
        public void FileMancanteOCorrotto_NonAttiva()
        {
            RilevatoreCommutabile rilevatore = new RilevatoreCommutabile();
            List<string> avvisi = new List<string>();

            using (GestoreWebcam gestore = new GestoreWebcam(i => new Sorgente()))
            {
                gestore.Apri(0);

                Assert.That(ApplicazioneCalibrazione.TentaCaricamento(
                    Path.Combine(cartella, "non-esiste.txt"), gestore, rilevatore, avvisi), Is.False);

                string corrotto = Path.Combine(cartella, "corrotta.txt");
                File.WriteAllText(corrotto, "spazzatura");
                Assert.That(ApplicazioneCalibrazione.TentaCaricamento(corrotto, gestore, rilevatore, avvisi), Is.False);
                Assert.That(avvisi.Single(), Does.Contain("Calibrazione salvata non valida"));
            }
        }
    }
}
