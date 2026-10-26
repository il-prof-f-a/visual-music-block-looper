using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class DiagnosticaTavoloTests
    {
        private static InterpreteTavolo Interprete() => new InterpreteTavolo(
            new Griglia(new ConfigGriglia { Area = LayoutTavolo.AreaGrigliaDefault }), LayoutTavolo.Default);

        private static CartoncinoRilevato Carta(string payload, RettangoloTavolo zona) => new CartoncinoRilevato
        {
            Payload = payload, Area = new RettangoloTavolo(zona.X + 0.01, zona.Y + 0.01, 0.05, 0.05)
        };

        private static List<CartoncinoRilevato> Carte(int misura = 4, int link = 6, string ab = "VMBL1;S;AB;1;2")
            => new List<CartoncinoRilevato>
            {
                Carta("VMBL1;S;MISURA;" + misura, LayoutTavolo.Default.Misura),
                Carta("VMBL1;S;LINK;" + link, LayoutTavolo.Default.Collegamento),
                Carta(ab, LayoutTavolo.Default.Ab)
            };

        public static IEnumerable<TestCaseData> Combinazioni()
        {
            foreach (int misura in new[] { 2, 3, 4 })
            foreach (int link in new[] { 1, 2, 3, 6 }) yield return new TestCaseData(misura, link);
        }

        [TestCaseSource(nameof(Combinazioni))]
        public void Ab12_ValidoPerTutteLeMisureECollegamenti(int misura, int link)
        {
            var traccia = new TracciaCartoncini();
            var stato = Interprete().Interpreta(Carte(misura, link), traccia);
            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Struttura.AFine, Is.EqualTo(2));
            Assert.That(traccia.Interpretazione.Single(e => e.Payload == "VMBL1;S;AB;1;2").Accettato, Is.True);
            Assert.That(traccia.StrutturaGrezza, Is.EqualTo(stato.Struttura));
        }

        [TestCase("VMBL1;S;AB;2;25", "IntervalloAbNonValido")]
        [TestCase("VMBL1;S;AB;2;1", "IntervalloAbNonValido")]
        [TestCase("VMBL1;S;AB;0;2", "IntervalloAbNonValido")]
        [TestCase("VMBL1;S;AB;x;2", "PayloadNonValido")]
        public void ScartoEsponePayloadZonaECausa(string payload, string causa)
        {
            var traccia = new TracciaCartoncini();
            var stato = Interprete().Interpreta(Carte(ab: payload), traccia);
            var evento = traccia.Interpretazione.Single(e => e.Payload == payload);
            Assert.That(evento.Accettato, Is.False);
            Assert.That(evento.Codice, Is.EqualTo(causa));
            Assert.That(evento.Zona, Is.EqualTo("Ab"));
            Assert.That(evento.Dettaglio, Is.Not.Empty);
            Assert.That(stato.Struttura.AFine, Is.EqualTo(24));
        }

        [Test]
        public void AbFuoriLimite_MostraLimiteEffettivo()
        {
            var traccia = new TracciaCartoncini();
            Interprete().Interpreta(Carte(2, 1, "VMBL1;S;AB;1;3"), traccia);
            Assert.That(traccia.Interpretazione.Single(e => e.Payload == "VMBL1;S;AB;1;3").Dettaglio,
                Does.Contain("2").And.Contain("LINK 1"));
        }

        [Test]
        public void AbNellaZonaLink_DistingueLaZonaDallaLetturaQr()
        {
            var traccia = new TracciaCartoncini();
            Interprete().Interpreta(new[] { Carta("VMBL1;S;AB;1;2", LayoutTavolo.Default.Collegamento) }, traccia);
            var evento = traccia.Interpretazione.Single();
            Assert.That(evento.Codice, Is.EqualTo("ImpostazioneInZonaErrata"));
            Assert.That(evento.Zona, Is.EqualTo("Collegamento"));
        }

        [Test]
        public void StessaAreaEPayload_DiagnosticaUnAccettatoEUnSovrapposto()
        {
            var traccia = new TracciaCartoncini();
            Interprete().Interpreta(new[] { Carta("VMBL1;S;AB;1;2", LayoutTavolo.Default.Ab),
                Carta("VMBL1;S;AB;1;2", LayoutTavolo.Default.Ab) }, traccia);
            Assert.That(traccia.Interpretazione.Count(e => e.Accettato == true), Is.EqualTo(1));
            Assert.That(traccia.Interpretazione.Count(e => e.Codice == "Sovrapposto"), Is.EqualTo(1));
        }

        [Test]
        public void NoteEComandi_DiagnosticaNonCambiaLoStato()
        {
            var carte = Carte();
            carte.Add(Carta("VMBL1;N;3", LayoutTavolo.AreaGrigliaDefault));
            carte.Add(Carta("VMBL1;C;PLAY", LayoutTavolo.Default.Comandi));
            carte.Add(Carta("VMBL1;C;STOP", LayoutTavolo.AreaGrigliaDefault));
            var traccia = new TracciaCartoncini();
            var senza = Interprete().Interpreta(carte);
            var con = Interprete().Interpreta(carte, traccia);
            Assert.That(JToken.DeepEquals(JToken.FromObject(senza), JToken.FromObject(con)), Is.True);
            Assert.That(traccia.Interpretazione.Count, Is.EqualTo(carte.Count));
            Assert.That(traccia.ComandiGrezzi, Is.EqualTo(1));
            Assert.That(traccia.NoteGrezze, Is.EqualTo(1));
        }

        [Test]
        public void Stabilizzazione_DistingueAttesaDaApplicazioneEResettaConIntermittenza()
        {
            var stabilizzatore = new StabilizzatoreTavolo();
            for (int i = 1; i <= 3; i++)
            {
                var traccia = new TracciaCartoncini();
                var grezzo = Interprete().Interpreta(Carte(), traccia);
                var stabile = stabilizzatore.Aggiorna(grezzo, traccia);
                Assert.That(traccia.ConfermeStruttura, Is.EqualTo(i));
                Assert.That(stabile.Struttura.AFine, Is.EqualTo(i < 3 ? 24 : 2));
                Assert.That(traccia.EsitoStabilizzazione, Is.EqualTo(i < 3 ? "InAttesa" : "Applicata"));
            }
            var nuovo = new StabilizzatoreTavolo();
            nuovo.Aggiorna(Interprete().Interpreta(Carte()), new TracciaCartoncini());
            nuovo.Aggiorna(new StatoTavolo(null, null, null));
            var ritorno = new TracciaCartoncini();
            nuovo.Aggiorna(Interprete().Interpreta(Carte()), ritorno);
            Assert.That(ritorno.ConfermeStruttura, Is.EqualTo(1));
        }
    }
}
