using System;
using System.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    public class AggancioQrTests
    {
        internal const double Passo = 0.67 / 16, Riga = 0.92 / 6;
        internal static CartoncinoRilevato Nota(int riga, double step, double fine, int id = 4)
        {
            double x = 0.15 + (step + 0.1) * Passo;
            return new CartoncinoRilevato
            {
                Payload = "VMBL1;N;" + id,
                AreaQr = new RettangoloTavolo(x, 0.04 + (riga + 0.3) * Riga, 0.55 * Passo, 0.4 * Riga),
                FineEstensioneX = 0.15 + fine * Passo,
                Area = new RettangoloTavolo(x - 0.5 * Passo, 0.04 + riga * Riga, (fine - step + 0.4) * Passo, 0.9 * Riga)
            };
        }
        internal static InterpreteTavolo Interprete() => new InterpreteTavolo(
            new Griglia(new ConfigGriglia { Area = LayoutTavolo.AreaGrigliaDefault }), LayoutTavolo.Default);

        [Test]
        public void QrDentroCorpoFuori_AccettatoNellaPrimaCella()
        {
            var stato = Interprete().Interpreta(new[] { Nota(2, 0, 2) });
            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Note.Single().StepIniziale, Is.EqualTo(0));
            Assert.That(stato.Note.Single().Riga, Is.EqualTo(2));
            Assert.That(stato.Note.Single().DurataStep, Is.EqualTo(2));
        }

        [Test]
        public void RigaEDurata_DerivanoDaQrEColoreNonDalRettangoloDelCorpo()
        {
            var n = Nota(3, 3, 6);
            n.Area = new RettangoloTavolo(0.2, 0.1, 0.5, 0.2);
            var stato = Interprete().Interpreta(new[] { n });
            Assert.That(stato.Errori, Is.Empty);
            var letto = stato.Note.Single();
            Assert.That(letto.Riga, Is.EqualTo(3));
            Assert.That(letto.StepIniziale, Is.EqualTo(3));
            Assert.That(letto.DurataStep, Is.EqualTo(3));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NuovoQr_TroncaLaPrecedenteEConservaEntrambiGliAttacchi(bool stessaNota)
        {
            var stato = Interprete().Interpreta(new[] { Nota(2, 3, 6, stessaNota ? 4 : 9), Nota(2, 0, 9) });
            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Note, Has.Count.EqualTo(2));
            var note = stato.Note.OrderBy(n => n.StepIniziale).ToArray();
            Assert.That(note.Select(n => n.StepIniziale), Is.EqualTo(new[] { 0, 3 }));
            Assert.That(note.Select(n => n.DurataStep), Is.EqualTo(new[] { 3, 3 }));
            Assert.That(note.Select(n => n.Chiave).Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void NuovoQr_NonRiempieLaPausaSenzaColore()
        {
            var stato = Interprete().Interpreta(new[] { Nota(2, 0, 2), Nota(2, 6, 8, 9) });
            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Note.OrderBy(n => n.StepIniziale).First().DurataStep, Is.EqualTo(2));
        }

        [Test]
        public void SoloQr_HaDurataMinimaUno()
        {
            var n = Nota(2, 3, 3.1);
            var stato = Interprete().Interpreta(new[] { n });
            Assert.That(stato.Note.Single().DurataStep, Is.EqualTo(1));
        }

        [Test]
        public void QrFuoriCorpoDentro_Rifiutato()
        {
            var n = Nota(2, -0.3, 2);
            n.Area = new RettangoloTavolo(0.16, 0.38, 0.1, 0.1);
            var stato = Interprete().Interpreta(new[] { n });
            Assert.That(stato.Note, Is.Empty);
            Assert.That(stato.Errori.Single().Tipo, Is.EqualTo(TipoErrore.FuoriGriglia));
        }

        [Test]
        public void DueQrDistintiNellaStessaCella_RestanoCollisioneEsplicita()
        {
            var stato = Interprete().Interpreta(new[] { Nota(2, 3.6, 6, 9), Nota(2, 3, 5, 4) });
            Assert.That(stato.Note, Has.Count.EqualTo(1));
            Assert.That(stato.Note[0].IdClip, Is.EqualTo(4));
            Assert.That(stato.Errori.Single().Tipo, Is.EqualTo(TipoErrore.Sovrapposto));
        }

        [Test]
        public void ColoreLungo_TroncatoAllaMisura()
        {
            var stato = Interprete().Interpreta(new[] { Nota(3, 13, 25) });
            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Note.Single().DurataStep, Is.EqualTo(3));
        }

        [Test]
        public void DatiNonFiniti_NonProduconoNoteONonLanciano()
        {
            var n = Nota(2, 3, 5); n.FineEstensioneX = double.NaN;
            StatoTavolo stato = null;
            Assert.DoesNotThrow(() => stato = Interprete().Interpreta(new[] { n }));
            Assert.That(stato.Note, Is.Empty);
        }
    }
}
