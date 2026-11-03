using System.Linq;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class LettoreZoneTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void PrimaLetturaDelleDieciNoteMantieneLaGeometriaAncheCapovolta(bool capovolta)
        {
            using (var f = NoteContigueVisionTests.Scena(capovolta))
            {
                var l = new LettoreZone(NoteContigueVisionTests.Calibra(capovolta), new ConfigGriglia { Area = LayoutTavolo.AreaGrigliaDefault });
                var r = l.Leggi(f, new RettangoloTavolo(0, 0, 1, 1), new CartoncinoRilevato[0]);
                Assert.That(r.Catalogo, Has.Count.EqualTo(10)); Assert.That(r.Completa, Is.True);
                var s = AggancioQrTests.Interprete().Interpreta(r.Catalogo);
                Assert.That(s.Note, Has.Count.EqualTo(10)); Assert.That(s.Errori, Is.Empty);
                foreach (int row in new[] { 2, 3 }) Assert.That(s.Note.Where(n => n.Riga == row).OrderBy(n => n.StepIniziale).Select(n => n.StepIniziale), Is.EqualTo(NoteContigueVisionTests.Celle));
            }
        }
        [Test]
        public void UnaRiletturaLocaleInviaAlDecoderUnRitaglioEConservaLeAltreNote()
        {
            using (var f = NoteContigueVisionTests.Scena(false))
            {
                var l = new LettoreZone(NoteContigueVisionTests.Calibra(false), new ConfigGriglia { Area = LayoutTavolo.AreaGrigliaDefault });
                var prima = l.Leggi(f, new RettangoloTavolo(0,0,1,1), new CartoncinoRilevato[0]);
                var dopo = l.Leggi(f, new RettangoloTavolo(.39,.34,.14,.32), prima.Catalogo);
                Assert.That(dopo.Catalogo, Has.Count.EqualTo(10));
                Assert.That(l.UltimaRegioneCamera.Width, Is.LessThan(f.Width*.5));
                Assert.That(l.UltimaRegioneCamera.Height, Is.LessThan(f.Height*.7));
                Assert.That(dopo.QrDecodificati, Is.LessThan(10));
                Assert.That(dopo.RegioneMaschera.Height,Is.LessThan(f.Height*.8),"Anche la maschera deve elaborare solo il rigo interessato.");
                Assert.That(AggancioQrTests.Interprete().Interpreta(dopo.Catalogo).Note.Select(n=>n.DurataStep),
                    Is.EqualTo(AggancioQrTests.Interprete().Interpreta(prima.Catalogo).Note.Select(n=>n.DurataStep)));
            }
        }
        [Test]
        public void AreaOscurataNonCancellaIlQrPrecedenteMaAreaVuotaLoRimuove()
        {
            using (var f = NoteContigueVisionTests.Scena(false))
            using (var scura = f.Clone())
            using (var vuota = f.Clone())
            {
                var l = new LettoreZone(NoteContigueVisionTests.Calibra(false), new ConfigGriglia { Area = LayoutTavolo.AreaGrigliaDefault });
                var prima = l.Leggi(f, new RettangoloTavolo(0,0,1,1), new CartoncinoRilevato[0]);
                Cv2.Rectangle(scura, new Rect(225,310,200,140), new Scalar(15,15,15), -1);
                Cv2.Rectangle(vuota, new Rect(225,310,200,140), Scalar.White, -1);
                var zona = new RettangoloTavolo(.13,.34,.16,.22);
                var incerta = l.Leggi(scura, zona, prima.Catalogo);
                Assert.That(incerta.Catalogo, Has.Count.EqualTo(10)); Assert.That(incerta.Completa, Is.False);
                var confermata = l.Leggi(vuota, zona, prima.Catalogo);
                Assert.That(confermata.Catalogo, Has.Count.EqualTo(9));
            }
        }
    }
}
