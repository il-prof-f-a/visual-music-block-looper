using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class CalibrazioneDodiciPuntiTests
    {
        internal static PuntoImmagine Camera(PuntoTavolo p)
        {
            double w = 1 + 0.15 * p.X + 0.08 * p.Y;
            return new PuntoImmagine((80 + 950 * p.X + 120 * p.Y) / w,
                (60 + 30 * p.X + 620 * p.Y) / w);
        }

        [Test]
        public void Schema_DodiciUniciEQuattroRiferimentiPerLato()
        {
            var punti = SchemaCalibrazione.Punti;
            Assert.That(punti, Has.Count.EqualTo(12));
            Assert.That(punti.Select(p => (p.X, p.Y)).Distinct().Count(), Is.EqualTo(12));
            Assert.That(punti.Count(p => p.X == 0.03), Is.EqualTo(4));
            Assert.That(punti.Count(p => p.X == 0.97), Is.EqualTo(4));
            Assert.That(punti.Count(p => p.Y == 0.03), Is.EqualTo(4));
            Assert.That(punti.Count(p => p.Y == 0.97), Is.EqualTo(4));
            Assert.That(SchemaCalibrazione.Descrizioni, Has.Count.EqualTo(12));
            Assert.That(SchemaCalibrazione.Descrizioni.All(d => !string.IsNullOrWhiteSpace(d)), Is.True);
        }

        [Test]
        public void Schema_PercorsoOrarioSenzaInterpolareIPixelCamera()
        {
            var p = SchemaCalibrazione.Punti;
            Assert.That(p, Has.Count.EqualTo(12));
            Assert.That(p[0].X, Is.LessThan(p[1].X));
            Assert.That(p[1].X, Is.LessThan(p[2].X));
            Assert.That(p[3].Y, Is.LessThan(p[4].Y));
            Assert.That(p[4].Y, Is.LessThan(p[5].Y));
            Assert.That(p[6].X, Is.GreaterThan(p[7].X));
            Assert.That(p[7].X, Is.GreaterThan(p[8].X));
            Assert.That(p[9].Y, Is.GreaterThan(p[10].Y));
            Assert.That(p[10].Y, Is.GreaterThan(p[11].Y));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DodiciPunti_RicostruisconoLaProspettivaAncheConPiccoliErroriDiClick(bool rumore)
        {
            PuntoImmagine[] punti = SchemaCalibrazione.Punti.Select((p, i) =>
            {
                PuntoImmagine c = Camera(p);
                return new PuntoImmagine(c.X + (rumore ? (i % 3 - 1) * 0.6 : 0),
                    c.Y + (rumore ? (i % 2 == 0 ? 0.5 : -0.5) : 0));
            }).ToArray();
            Calibrazione calibrazione = SchemaCalibrazione.Calcola(punti, 1280, 720, out double scarto);
            Assert.That(scarto, Is.InRange(0, 0.003));
            foreach (double x in new[] { 0.0, 0.25, 0.5, 0.75, 1.0 })
                foreach (double y in new[] { 0.0, 0.25, 0.5, 0.75, 1.0 })
                {
                    PuntoTavolo letto = calibrazione.DaCameraATavolo(Camera(new PuntoTavolo(x, y)));
                    Assert.That(letto.X, Is.EqualTo(x).Within(0.003));
                    Assert.That(letto.Y, Is.EqualTo(y).Within(0.003));
                }
        }

        [Test]
        public void ClickIncoerente_VieneSegnalatoAncheSeRansacLoScarta()
        {
            PuntoImmagine[] punti = SchemaCalibrazione.Punti.Select(Camera).ToArray();
            Assert.That(punti, Has.Length.EqualTo(12));
            punti[5] = new PuntoImmagine(punti[5].X + 120, punti[5].Y + 60);
            var errore = Assert.Throws<PuntiCalibrazioneIncoerentiException>(() => SchemaCalibrazione.Calcola(punti, 1280, 720, out _));
            Assert.That(errore.Message, Does.Contain("6"));
            Assert.That(errore.Numeri, Is.EqualTo(new[] { 6 }));
        }

        [Test]
        public void PuntiDegeneri_SonoRifiutati() => Assert.That(() => SchemaCalibrazione.Calcola(
            Enumerable.Repeat(new PuntoImmagine(100, 100), 12).ToArray(), 1280, 720, out _),
            Throws.TypeOf<ArgumentException>());

        [Test]
        public void CameraCapovolta_ConservaLeCoordinateDeiBersagli()
        {
            PuntoImmagine[] punti = SchemaCalibrazione.Punti.Select(p =>
            {
                PuntoImmagine c = Camera(p);
                return new PuntoImmagine(1280 - c.X, 720 - c.Y);
            }).ToArray();
            Calibrazione calibrazione = SchemaCalibrazione.Calcola(punti, 1280, 720, out _);
            PuntoImmagine prova = Camera(new PuntoTavolo(0.2, 0.7));
            PuntoTavolo letto = calibrazione.DaCameraATavolo(new PuntoImmagine(1280 - prova.X, 720 - prova.Y));
            Assert.That(letto.X, Is.EqualTo(0.2).Within(0.001));
            Assert.That(letto.Y, Is.EqualTo(0.7).Within(0.001));
        }

        [Test]
        public void InputNullONonFinito_Rifiutato()
        {
            Assert.That(() => SchemaCalibrazione.Calcola(null, 1280, 720, out _), Throws.TypeOf<ArgumentNullException>());
            PuntoImmagine[] punti = SchemaCalibrazione.Punti.Select(Camera).ToArray();
            punti[2] = new PuntoImmagine(double.NaN, 100);
            Assert.That(() => SchemaCalibrazione.Calcola(punti, 1280, 720, out _), Throws.TypeOf<ArgumentException>());
        }

        [TestCase(4)]
        [TestCase(11)]
        [TestCase(13)]
        public void RaccoltaIncompletaONumeroErrato_NonDiventaCalibrazione(int numero) => Assert.That(() =>
            SchemaCalibrazione.Calcola(new PuntoImmagine[numero], 1280, 720, out _), Throws.TypeOf<ArgumentException>());

        [Test]
        public void FileNuovo_ConservaIDodiciRiferimentiInCal3()
        {
            string file = Path.Combine(Path.GetTempPath(), "vmbl-cal12-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                var originale = SchemaCalibrazione.Calcola(SchemaCalibrazione.Punti.Select(Camera).ToArray(),
                    1280, 720, out _);
                originale.Salva(file);
                Assert.That(File.ReadAllText(file), Does.StartWith("VMBL-CAL3"));
                Calibrazione caricata = Calibrazione.Carica(file);
                PuntoTavolo centro = caricata.DaCameraATavolo(Camera(new PuntoTavolo(0.5, 0.5)));
                Assert.That(centro.X, Is.EqualTo(0.5).Within(0.001));
                Assert.That(centro.Y, Is.EqualTo(0.5).Within(0.001));
                Assert.That(caricata.VerificaRisoluzione(1280, 720), Is.EqualTo(EsitoRisoluzione.Compatibile));
            }
            finally { if (File.Exists(file)) File.Delete(file); }
        }
    }
}
