using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.App;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class AbbinamentoWebcamTests
    {
        private static InfoWebcam Cam(int indice, string nome, string percorso = null)
        {
            return new InfoWebcam(indice, nome, percorso);
        }

        [Test]
        public void StessoPercorso_TrovaLaWebcamAnchePosizioneECambiata()
        {
            // All'avvio precedente era la webcam 0; ora l'ordine è invertito.
            ConfigurazioneWebcam salvata = new ConfigurazioneWebcam { Nome = "Logitech C920", Percorso = @"\\?\usb#a", Indice = 0 };
            IReadOnlyList<InfoWebcam> presenti = new[] { Cam(0, "Integrata", @"\\?\usb#b"), Cam(1, "Logitech C920", @"\\?\usb#a") };

            InfoWebcam trovata = AbbinamentoWebcam.Trova(presenti, salvata);

            Assert.That(trovata.Indice, Is.EqualTo(1));
        }

        [Test]
        public void SoloNome_TrovaLaWebcamQuandoIlPercorsoNonCorrisponde()
        {
            // Stessa webcam collegata a un'altra porta USB: cambia il percorso, non il nome.
            ConfigurazioneWebcam salvata = new ConfigurazioneWebcam { Nome = "Logitech C920", Percorso = @"\\?\usb#vecchio" };
            IReadOnlyList<InfoWebcam> presenti = new[] { Cam(0, "Integrata", @"\\?\usb#b"), Cam(1, "Logitech C920", @"\\?\usb#nuovo") };

            Assert.That(AbbinamentoWebcam.Trova(presenti, salvata).Indice, Is.EqualTo(1));
        }

        [Test]
        public void ILPercorsoPrevaleSulNome_TraDueWebcamIdentiche()
        {
            ConfigurazioneWebcam salvata = new ConfigurazioneWebcam { Nome = "USB Camera", Percorso = @"\\?\usb#seconda" };
            IReadOnlyList<InfoWebcam> presenti = new[] { Cam(0, "USB Camera", @"\\?\usb#prima"), Cam(1, "USB Camera", @"\\?\usb#seconda") };

            Assert.That(AbbinamentoWebcam.Trova(presenti, salvata).Indice, Is.EqualTo(1));
        }

        [Test]
        public void StessoNomePiuDispositiviSenzaPercorso_PrendeIlPrimo()
        {
            ConfigurazioneWebcam salvata = new ConfigurazioneWebcam { Nome = "USB Camera" };
            IReadOnlyList<InfoWebcam> presenti = new[] { Cam(0, "USB Camera"), Cam(1, "USB Camera") };

            Assert.That(AbbinamentoWebcam.Trova(presenti, salvata).Indice, Is.EqualTo(0));
        }

        [Test]
        public void ConfrontoDelNome_IgnoraMaiuscole()
        {
            ConfigurazioneWebcam salvata = new ConfigurazioneWebcam { Nome = "logitech c920" };

            Assert.That(AbbinamentoWebcam.Trova(new[] { Cam(0, "Logitech C920") }, salvata), Is.Not.Null);
        }

        [Test]
        public void WebcamSalvataNonPiuPresente_NullEMaiUnaltraWebcam()
        {
            ConfigurazioneWebcam salvata = new ConfigurazioneWebcam { Nome = "Logitech C920", Percorso = @"\\?\usb#a", Indice = 0 };
            IReadOnlyList<InfoWebcam> presenti = new[] { Cam(0, "Integrata", @"\\?\usb#b") };

            Assert.That(AbbinamentoWebcam.Trova(presenti, salvata), Is.Null);
        }

        [Test]
        public void ConfigurazioneVecchiaSoloIndice_UsaLIndice()
        {
            ConfigurazioneWebcam salvata = new ConfigurazioneWebcam { Indice = 1 };
            IReadOnlyList<InfoWebcam> presenti = new[] { Cam(0, "Integrata"), Cam(1, "Logitech C920") };

            Assert.That(AbbinamentoWebcam.Trova(presenti, salvata).Nome, Is.EqualTo("Logitech C920"));
        }

        [Test]
        public void ConfigurazioneVecchiaConIndiceInesistente_Null()
        {
            Assert.That(AbbinamentoWebcam.Trova(new[] { Cam(0, "Integrata") }, new ConfigurazioneWebcam { Indice = 4 }), Is.Null);
        }

        [Test]
        public void ElencoVuotoOParametriNull_Null()
        {
            Assert.That(AbbinamentoWebcam.Trova(new List<InfoWebcam>(), new ConfigurazioneWebcam { Nome = "x" }), Is.Null);
            Assert.That(AbbinamentoWebcam.Trova(null, new ConfigurazioneWebcam()), Is.Null);
            Assert.That(AbbinamentoWebcam.Trova(new[] { Cam(0, "a") }, null), Is.Null);
        }

        [Test]
        public void Corrisponde_RispettaLaStessaRegola()
        {
            ConfigurazioneWebcam salvata = new ConfigurazioneWebcam { Nome = "Logitech C920", Percorso = @"\\?\usb#a" };

            Assert.That(salvata.Corrisponde(Cam(3, "Qualunque", @"\\?\usb#a")), Is.True, "stesso percorso");
            Assert.That(salvata.Corrisponde(Cam(3, "Logitech C920", @"\\?\usb#z")), Is.True, "stesso nome");
            Assert.That(salvata.Corrisponde(Cam(0, "Integrata", @"\\?\usb#b")), Is.False);
            Assert.That(new ConfigurazioneWebcam { Indice = 2 }.Corrisponde(Cam(2, "x")), Is.True, "legacy per indice");
            Assert.That(salvata.Corrisponde(null), Is.False);
        }
    }

    [TestFixture]
    public class ElencoWebcamPerNomeTests
    {
        [Test]
        public void Descrizione_UsaIlNome_OppureWebcamN()
        {
            Assert.That(new InfoWebcam(1, "Logitech C920", "p").Descrizione, Is.EqualTo("Logitech C920"));
            Assert.That(new InfoWebcam(2).Descrizione, Is.EqualTo("Webcam 2"));
            Assert.That(new InfoWebcam(2, "  ").Descrizione, Is.EqualTo("Webcam 2"));
        }

        [Test]
        public void NomiDuplicati_RicevonoUnSuffissoNellaLista()
        {
            IReadOnlyList<InfoWebcam> elenco = ElencoWebcam.AssegnaEtichette(new[]
            {
                new InfoWebcam(0, "USB Camera", "a"), new InfoWebcam(1, "Integrata", "b"), new InfoWebcam(2, "USB Camera", "c")
            });

            Assert.That(elenco.Select(w => w.Descrizione), Is.EqualTo(new[] { "USB Camera (1)", "Integrata", "USB Camera (2)" }));
            Assert.That(elenco.Select(w => w.Indice), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(elenco[2].Nome, Is.EqualTo("USB Camera"), "il nome vero non cambia: serve al salvataggio");
        }

        [Test]
        public void Costruisci_UsaIDispositiviDiSistema()
        {
            IReadOnlyList<InfoWebcam> elenco = ElencoWebcam.Costruisci(
                () => new[] { new InfoWebcam(0, "Integrata", "a"), new InfoWebcam(1, "Logitech C920", "b") },
                i => throw new InvalidOperationException("non si deve provare ad aprire"));

            Assert.That(elenco.Select(w => w.Descrizione), Is.EqualTo(new[] { "Integrata", "Logitech C920" }));
        }

        [Test]
        public void EnumerazioneVuota_RipiegaSullaRilevazionePerIndici()
        {
            IReadOnlyList<InfoWebcam> elenco = ElencoWebcam.Costruisci(
                () => new List<InfoWebcam>(), indice => indice < 2);

            Assert.That(elenco.Select(w => w.Descrizione), Is.EqualTo(new[] { "Webcam 0", "Webcam 1" }));
        }

        [Test]
        public void EnumerazioneChiLancia_RipiegaSullaRilevazionePerIndici()
        {
            IReadOnlyList<InfoWebcam> elenco = ElencoWebcam.Costruisci(
                () => throw new InvalidOperationException("COM non disponibile"), indice => indice < 1);

            Assert.That(elenco.Select(w => w.Descrizione), Is.EqualTo(new[] { "Webcam 0" }));
        }

        [Test]
        public void EnumeratoreDirectShowReale_NonLanciaESeTrovaWebcamHaNomiNonVuoti()
        {
            IReadOnlyList<InfoWebcam> trovate = null;

            Assert.DoesNotThrow(() => trovate = DispositiviDirectShow.Enumera());

            Assert.That(trovate, Is.Not.Null);
            TestContext.WriteLine("Webcam di sistema: " + (trovate.Count == 0
                ? "(nessuna)"
                : string.Join(" | ", trovate.Select(w => w.Indice + ": " + w.Nome))));
            Assert.That(trovate.All(w => !string.IsNullOrWhiteSpace(w.Nome)), Is.True);
            Assert.That(trovate.Select(w => w.Indice), Is.EqualTo(Enumerable.Range(0, trovate.Count)));
        }
    }

    [TestFixture]
    public class ConfigurazioneWebcamPerNomeTests
    {
        private string cartella;

        [SetUp]
        public void Prepara()
        {
            cartella = Path.Combine(Path.GetTempPath(), "vmbl-nome-" + Guid.NewGuid().ToString("N"));
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
        public void SalvaECarica_ConservaNomePercorsoEIndice()
        {
            string percorso = Path.Combine(cartella, ConfigurazioneApplicazione.NomeFile);
            new ConfigurazioneApplicazione
            {
                Webcam = new ConfigurazioneWebcam { Nome = "Logitech C920", Percorso = @"\\?\usb#vid_046d", Indice = 1 }
            }.Salva(percorso);

            ConfigurazioneWebcam letta = ConfigurazioneApplicazione.Carica(percorso).Webcam;

            Assert.That(letta.Nome, Is.EqualTo("Logitech C920"));
            Assert.That(letta.Percorso, Is.EqualTo(@"\\?\usb#vid_046d"));
            Assert.That(letta.Indice, Is.EqualTo(1));
        }

        [Test]
        public void ConfigurazioneDellaVersioneVecchia_ConSoloIndice_SiLeggeAncora()
        {
            string percorso = Path.Combine(cartella, ConfigurazioneApplicazione.NomeFile);
            File.WriteAllText(percorso, "{ \"Webcam\": { \"Indice\": 2 } }");

            ConfigurazioneWebcam letta = ConfigurazioneApplicazione.Carica(percorso).Webcam;

            Assert.That(letta.Indice, Is.EqualTo(2));
            Assert.That(letta.Nome, Is.Null.Or.Empty);
        }

        [Test]
        public void Imposta_CopiaLIdentitaDellaWebcam()
        {
            ConfigurazioneWebcam configurazione = new ConfigurazioneWebcam();

            configurazione.Imposta(new InfoWebcam(3, "Logitech C920", "p"));

            Assert.That(configurazione.Nome, Is.EqualTo("Logitech C920"));
            Assert.That(configurazione.Percorso, Is.EqualTo("p"));
            Assert.That(configurazione.Indice, Is.EqualTo(3));
        }
    }

    [TestFixture]
    public class GestoreWebcamIdentitaTests
    {
        private sealed class Sorgente : ISorgenteWebcam
        {
            public int Larghezza => 1280;

            public int Altezza => 720;

            public Mat Frame() { return null; }

            public Bitmap Anteprima() { return null; }

            public void Dispose() { }
        }

        [Test]
        public void ApriConInfo_EspongaLIdentitaCorrente()
        {
            using (GestoreWebcam gestore = new GestoreWebcam(i => new Sorgente()))
            {
                InfoWebcam info = new InfoWebcam(2, "Logitech C920", "p");

                Assert.That(gestore.Apri(info), Is.True);

                Assert.That(gestore.IndiceCorrente, Is.EqualTo(2));
                Assert.That(gestore.Info.Nome, Is.EqualTo("Logitech C920"));
                Assert.That(gestore.Info.Percorso, Is.EqualTo("p"));
            }
        }

        [Test]
        public void ApriFallito_MantieneLIdentitaPrecedente()
        {
            using (GestoreWebcam gestore = new GestoreWebcam(i => i == 0 ? new Sorgente() : null))
            {
                gestore.Apri(new InfoWebcam(0, "Integrata", "a"));

                Assert.That(gestore.Apri(new InfoWebcam(1, "Logitech C920", "b")), Is.False);

                Assert.That(gestore.Info.Nome, Is.EqualTo("Integrata"));
            }
        }

        [Test]
        public void SenzaWebcam_InfoNull()
        {
            using (GestoreWebcam gestore = new GestoreWebcam(i => null))
            {
                Assert.That(gestore.Info, Is.Null);
                gestore.Apri(new InfoWebcam(0, "x"));
                Assert.That(gestore.Info, Is.Null);
            }
        }

        [Test]
        public void AperturaPerIndice_RestaCompatibile()
        {
            using (GestoreWebcam gestore = new GestoreWebcam(i => new Sorgente()))
            {
                Assert.That(gestore.Apri(1), Is.True);
                Assert.That(gestore.Info.Indice, Is.EqualTo(1));
                Assert.That(gestore.Info.Descrizione, Is.EqualTo("Webcam 1"));
            }
        }
    }
}
