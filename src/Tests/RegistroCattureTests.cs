using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class RegistroCattureTests
    {
        private static void Aggiungi(RegistroCatture registro, int numero)
        {
            for (int i = 0; i < numero; i++) registro.CompletaCattura(registro.IniziaCattura());
        }

        [Test]
        public void Spento_NonCostruisceContestiENonRegistra()
        {
            var registro = new RegistroCatture();
            Assert.That(registro.Attivo, Is.False);
            Assert.That(registro.IniziaCattura(), Is.Null);
            registro.CompletaCattura(null);
            Assert.That(registro.Istantanea().Catture, Is.Empty);
        }

        [TestCase(1)] [TestCase(50)] [TestCase(51)] [TestCase(157)]
        public void ConservaSoloLeUltime50InOrdine(int numero)
        {
            var registro = new RegistroCatture { Attivo = true };
            Aggiungi(registro, numero);
            var rapporto = registro.Istantanea();
            Assert.That(rapporto.Catture.Select(c => c.Sequenza),
                Is.EqualTo(Enumerable.Range(Math.Max(1, numero - 49), Math.Min(50, numero)).Select(n => (long)n)));
            Assert.That(rapporto.TotaleCatture, Is.EqualTo(numero));
            Assert.That(rapporto.CattureOmesse, Is.EqualTo(Math.Max(0, numero - 50)));
        }

        [Test]
        public void DisattivazioneCongelaERiattivazioneScartaLeCattureInVolo()
        {
            var registro = new RegistroCatture { Attivo = true };
            Aggiungi(registro, 3);
            var inVolo = registro.IniziaCattura();
            registro.Attivo = false;
            registro.CompletaCattura(inVolo);
            Assert.That(registro.NumeroCatture, Is.EqualTo(3));
            registro.Attivo = true;
            registro.CompletaCattura(inVolo);
            Assert.That(registro.NumeroCatture, Is.Zero);
            Aggiungi(registro, 1);
            Assert.That(registro.Istantanea().Catture.Single().Sequenza, Is.EqualTo(1));
        }

        [Test]
        public void StessaAttivazioneNonAzzeraEContestiNonSiDuplicano()
        {
            var registro = new RegistroCatture { Attivo = true };
            var cattura = registro.IniziaCattura();
            registro.CompletaCattura(cattura);
            registro.Attivo = true;
            registro.CompletaCattura(cattura);
            var altro = new RegistroCatture { Attivo = true };
            altro.CompletaCattura(cattura);
            Assert.That(registro.NumeroCatture, Is.EqualTo(1));
            Assert.That(altro.NumeroCatture, Is.Zero);
        }

        [Test]
        public void IstantaneaIndipendenteDaBufferEContesto()
        {
            var registro = new RegistroCatture { Attivo = true };
            var cattura = registro.IniziaCattura();
            cattura.Traccia.Errore = "prima";
            registro.CompletaCattura(cattura);
            var rapporto = registro.Istantanea();
            cattura.Traccia.Errore = "dopo";
            Aggiungi(registro, 60);
            Assert.That(rapporto.Catture.Count, Is.EqualTo(1));
            Assert.That(rapporto.Catture[0].Errore, Is.EqualTo("prima"));
        }

        [Test]
        public void LetturaConcorrenteNonVedeArrayParziali()
        {
            var registro = new RegistroCatture { Attivo = true };
            Task scrittore = Task.Run(() => Aggiungi(registro, 500));
            for (int i = 0; i < 300; i++)
            {
                var rapporto = registro.Istantanea();
                Assert.That(rapporto.Catture.Count, Is.LessThanOrEqualTo(50));
                Assert.That(rapporto.Catture.Select(c => c.Sequenza), Is.Ordered.Ascending);
                Assert.That(rapporto.Catture.Any(c => c == null), Is.False);
            }
            Assert.That(scrittore.Wait(5000), Is.True);
        }

        [Test]
        public void EsportaJsonUtf8ConOrdineEConteggiESenzaCollisioni()
        {
            string dir = Path.Combine(Path.GetTempPath(), "vmbl-debug-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var registro = new RegistroCatture { Attivo = true };
                Aggiungi(registro, 51);
                var cattura = registro.IniziaCattura();
                cattura.Traccia.Errore = "QR non leggibile: è sfocato";
                cattura.Traccia.Visione.Add(new EventoDiagnosticoCartoncino("Visione", "QrDecodificato",
                    "QrDecodificato", "Testo letto", "VMBL1;S;AB;1;2"));
                registro.CompletaCattura(cattura);
                string file = EsportatoreDiagnosticaCartoncini.Salva(registro.Istantanea(), dir);
                string secondo = EsportatoreDiagnosticaCartoncini.Salva(registro.Istantanea(), dir);
                var json = JObject.Parse(File.ReadAllText(file));
                Assert.That(json["VersioneFormato"].Value<int>(), Is.EqualTo(1));
                Assert.That(json["Catture"].Count(), Is.EqualTo(50));
                Assert.That(json["TotaleCatture"].Value<int>(), Is.EqualTo(52));
                Assert.That(json["Catture"][0]["Sequenza"].Value<int>(), Is.EqualTo(3));
                Assert.That(json["Catture"].Last["Errore"].Value<string>(), Does.Contain("è sfocato"));
                Assert.That(json["Catture"].Last["Visione"][0]["Accettato"], Is.Null,
                    "Una lettura Vision non è un giudizio di applicabilità.");
                Assert.That(secondo, Is.Not.EqualTo(file));
                Assert.That(Directory.GetFiles(dir, "*.tmp"), Is.Empty);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void ExportFallitoNonConsumaIlBuffer()
        {
            var registro = new RegistroCatture { Attivo = true };
            Aggiungi(registro, 1);
            string assente = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "assente");
            Assert.Throws<DirectoryNotFoundException>(() => EsportatoreDiagnosticaCartoncini.Salva(registro.Istantanea(), assente));
            Assert.That(registro.NumeroCatture, Is.EqualTo(1));
            Assert.That(Directory.Exists(assente), Is.False);
        }
    }
}
