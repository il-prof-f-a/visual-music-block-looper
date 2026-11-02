using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    public class NoteContigueVisionTests
    {
        internal const int Larghezza = 1600, Altezza = 900;
        internal static readonly int[] Celle = { 0, 3, 6, 11, 13 };
        internal sealed class Fonte : IFonteImmagini
        {
            private readonly Mat frame;
            public Fonte(Mat frame) { this.frame = frame; }
            public Mat Frame() => frame.Clone();
        }
        private static string FileNota(int midi) => Directory.GetFiles(
            Path.Combine(GeneratoreCartoncini.CartellaCartoncini(), "..", "cartoncini-note"), "nota-" + midi.ToString("000") + "-*.png").Single();

        internal static Calibrazione Calibra(bool capovolta, int w = Larghezza, int h = Altezza)
        {
            var tavolo = new[] { new PuntoTavolo(0, 0), new PuntoTavolo(1, 0), new PuntoTavolo(1, 1), new PuntoTavolo(0, 1) };
            return Calibrazione.Calcola(tavolo.Select(p => new PuntoImmagine(
                (capovolta ? 1-p.X : p.X)*w, (capovolta ? 1-p.Y : p.Y)*h)).ToArray(), tavolo, w, h);
        }

        internal static Mat Scena(bool capovolta)
        {
            var frame = new Mat(Altezza, Larghezza, MatType.CV_8UC3, Scalar.White);
            int[][] note = { new[] { 64, 69, 76, 76, 76 }, new[] { 72, 74, 71, 71, 71 } };
            for (int r = 0; r < 2; r++)
                for (int i = 0; i < 5; i++)
                {
                    using (var originale = Cv2.ImRead(FileNota(note[r][i])))
                    using (var ridotta = new Mat())
                    {
                        Cv2.Resize(originale, ridotta, new Size(180, 120));
                        int x = (int)Math.Round((.15 + (Celle[i]+.2)*AggancioQrTests.Passo)*Larghezza - 180*75.0/709);
                        int y = (int)Math.Round((.04+(r+2+.5)*AggancioQrTests.Riga)*Altezza - 60);
                        using (var zona = new Mat(frame, new Rect(x, y, 180, 120))) ridotta.CopyTo(zona);
                    }
                }
            if (capovolta) Cv2.Rotate(frame, frame, RotateFlags.Rotate180);
            return frame;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DieciQr_InclusiGliUgualiSovrapposti_HannoAggancioSeparato(bool capovolta)
        {
            using (var frame = Scena(capovolta))
            {
                var rilevatore = new RilevatoreCartoncini(new Fonte(frame), Calibra(capovolta));
                for (int passaggio = 0; passaggio < 2; passaggio++)
                {
                    var letti = rilevatore.Rileva();
                    Assert.That(letti, Has.Count.EqualTo(10));
                    Assert.That(letti.All(n => n.AreaQr.HasValue && n.FineEstensioneX.HasValue), Is.True);
                    var stato = AggancioQrTests.Interprete().Interpreta(letti);
                    Assert.That(stato.Errori, Is.Empty);
                    Assert.That(stato.Note, Has.Count.EqualTo(10));
                    for (int r = 2; r <= 3; r++)
                    {
                        var note = stato.Note.Where(n => n.Riga == r).OrderBy(n => n.StepIniziale).ToArray();
                        Assert.That(note.Select(n => n.StepIniziale), Is.EqualTo(Celle));
                        Assert.That(note.Select(n => n.DurataStep), Is.EqualTo(new[] { 3, 3, 3, 2, 3 }));
                    }
                }
            }
        }

        [Test]
        public void ColoreStaccatoNonProlungaLaNota()
        {
            using (var frame = new Mat(600, 800, MatType.CV_8UC3, Scalar.White))
            using (var originale = Cv2.ImRead(FileNota(64)))
            using (var piccola = new Mat())
            {
                Cv2.Resize(originale, piccola, new Size(180, 120));
                using (var zona = new Mat(frame, new Rect(150, 150, 180, 120))) piccola.CopyTo(zona);
                Cv2.Rectangle(frame, new Rect(500, 150, 180, 120), new Scalar(20, 60, 160), -1);
                var nota = new RilevatoreCartoncini(new Fonte(frame), Calibra(false, 800, 600)).Rileva().Single();
                Assert.That(nota.FineEstensioneX, Is.Not.Null);
                Assert.That(nota.FineEstensioneX.Value, Is.EqualTo(330.0/800).Within(.015));
            }
        }

        [Test]
        public void SoloQrSenzaColore_NonProlungaLaNota()
        {
            using (var frame = new Mat(600, 800, MatType.CV_8UC3, Scalar.White))
            using (var originale = Cv2.ImRead(FileNota(64)))
            using (var qr = new Mat(originale, new Rect(25, 48, 375, 375)))
            using (var piccolo = new Mat())
            {
                Cv2.Resize(qr, piccolo, new Size(120, 120));
                using (var zona = new Mat(frame, new Rect(150, 150, 120, 120))) piccolo.CopyTo(zona);
                var nota = new RilevatoreCartoncini(new Fonte(frame), Calibra(false, 800, 600)).Rileva().Single();
                Assert.That(nota.AreaQr, Is.Not.Null);
                Assert.That(nota.FineEstensioneX, Is.EqualTo(nota.AreaQr.Value.X).Within(.002));
            }
        }

        [Test]
        public void QrVicinoAlConfineRigo_NonSegueIlCorpoDelRigoAdiacente()
        {
            using (var maschera = new Mat(600, 800, MatType.CV_8UC1, Scalar.All(0)))
            {
                Cv2.Rectangle(maschera, new Rect(216, 117, 64, 51), Scalar.All(255), -1);
                Cv2.Rectangle(maschera, new Rect(280, 84, 280, 32), Scalar.All(255), -1);
                double fine = MisuratoreEstensioneNota.Misura(maschera, Calibra(false, 800, 600),
                    new RettangoloTavolo(.2, .155, .06, .08),
                    new ConfigGriglia { Area = LayoutTavolo.AreaGrigliaDefault });
                Assert.That(fine, Is.EqualTo(.35).Within(.005));
            }
        }

        [Test]
        public void MisuraColore_UsaLaGrigliaConfigurata()
        {
            using (var maschera = new Mat(600, 800, MatType.CV_8UC1, Scalar.All(0)))
            {
                Cv2.Rectangle(maschera, new Rect(216, 181, 64, 80), Scalar.All(255), -1);
                Cv2.Rectangle(maschera, new Rect(280, 144, 280, 36), Scalar.All(255), -1);
                double fine = MisuratoreEstensioneNota.Misura(maschera, Calibra(false, 800, 600),
                    new RettangoloTavolo(.2, .26, .06, .09),
                    new ConfigGriglia { Area = new RettangoloTavolo(0, .1, 1, .6), Tracce = 3 });
                Assert.That(fine, Is.EqualTo(.35).Within(.005));
            }
        }

        [Test]
        public void RecuperoConPrimoQrGiaLetto_NonSaltaIlSecondoNelloStessoCorpo()
        {
            using (var frame = CorpoConQr(2))
            {
                var lettore = new RilevatoreCartoncini(new Fonte(frame), Calibra(false, 1280, 720));
                var qr = LeggiCatalogo(frame);
                Assert.That(qr, Has.Length.EqualTo(2));
                // Confine fra decoder globale e recupero: il globale ha letto soltanto il primo.
                // Il recupero deve trovare il secondo anche se non è nel catalogo dei localizzati.
                Assert.That(Recupera(lettore, frame, qr.Take(1).ToArray()), Has.Length.EqualTo(1));
            }
        }

        [Test]
        public void RecuperoCorpoImmutato_NuovoQrInvalidaLaCacheEMantieneTutteLeIdentita()
        {
            using (var frame = CorpoConQr(1))
            using (var due = CorpoConQr(2))
            {
                var lettore = new RilevatoreCartoncini(new Fonte(frame), Calibra(false, 1280, 720));
                Assert.That(Recupera(lettore, frame, new object[0]), Has.Length.EqualTo(1));
                due.CopyTo(frame); // Stesso rettangolo esterno del corpo, secondo QR ora visibile.
                Assert.That(Recupera(lettore, frame, new object[0]), Has.Length.EqualTo(2));
                var ripetuta = Recupera(lettore, frame, new object[0]);
                Assert.That(ripetuta, Has.Length.EqualTo(2));
                Assert.That(ripetuta.All(q => (string)q.GetType().GetField("Origine").GetValue(q) == "Memoria"), Is.True);
            }
        }

        [Test]
        public void CorpoSpostato_AncheIlQrInMemoriaSegueLaNuovaPosizione()
        {
            using (var frame = CorpoConQr(1))
            using (var spostato = new Mat(720, 1280, MatType.CV_8UC3, Scalar.White))
            {
                var lettore = new RilevatoreCartoncini(new Fonte(frame), Calibra(false, 1280, 720));
                var primo = Recupera(lettore, frame, new object[0]).Single();
                using (var sorgente = new Mat(frame, new Rect(200, 200, 500, 165)))
                using (var destinazione = new Mat(spostato, new Rect(210, 200, 500, 165))) sorgente.CopyTo(destinazione);
                var secondo = Recupera(lettore, spostato, new object[0], new Rect(210, 200, 500, 165)).Single();
                var a = (Point2f[])primo.GetType().GetField("Angoli").GetValue(primo);
                var b = (Point2f[])secondo.GetType().GetField("Angoli").GetValue(secondo);
                Assert.That(b.Average(p => p.X)-a.Average(p => p.X), Is.EqualTo(10).Within(.5));
                Assert.That((string)secondo.GetType().GetField("Origine").GetValue(secondo), Is.EqualTo("Memoria"));
            }
        }

        private static Mat CorpoConQr(int numero)
        {
            var frame = new Mat(720, 1280, MatType.CV_8UC3, Scalar.White);
            Cv2.Rectangle(frame, new Rect(200, 200, 500, 165), new Scalar(30, 50, 180), -1);
            using (var originale = Cv2.ImRead(FileNota(64)))
            using (var carta = new Mat())
            {
                Cv2.Resize(originale, carta, new Size(210, 140));
                for (int i = 0; i < numero; i++)
                    using (var zona = new Mat(frame, new Rect(210 + i*240, 212, 210, 140))) carta.CopyTo(zona);
            }
            return frame;
        }

        private static object[] LeggiCatalogo(Mat frame)
        {
            using (var grigio = new Mat())
            {
                Cv2.CvtColor(frame, grigio, ColorConversionCodes.BGR2GRAY);
                var m = typeof(RilevatoreCartoncini).GetMethod("DecodificaConZxing", BindingFlags.Static|BindingFlags.NonPublic,
                    null, new[] { typeof(Mat) }, null);
                return ((IEnumerable)m.Invoke(null, new object[] { grigio })).Cast<object>().ToArray();
            }
        }

        private static object[] Recupera(RilevatoreCartoncini lettore, Mat frame, object[] noti, Rect? corpo = null)
        {
            Type tipo = typeof(RilevatoreCartoncini).GetNestedType("QrTrovato", BindingFlags.NonPublic);
            Type lista = typeof(System.Collections.Generic.List<>).MakeGenericType(tipo);
            var letti = (IList)Activator.CreateInstance(lista);
            foreach (var q in noti) letti.Add(q);
            var localizzati = (IList)Activator.CreateInstance(lista);
            var metodo = typeof(RilevatoreCartoncini).GetMethod("RecuperaQrMancanti", BindingFlags.Instance|BindingFlags.NonPublic);
            return ((IEnumerable)metodo.Invoke(lettore, new object[] { frame, letti, localizzati,
                new[] { corpo ?? new Rect(200, 200, 500, 165) }, new TracciaCartoncini() })).Cast<object>().ToArray();
        }
    }
}
