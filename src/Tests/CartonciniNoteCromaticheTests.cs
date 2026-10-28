using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Midi;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    public class CartonciniNoteCromaticheTests
    {
        private sealed class Fonte : IFonteImmagini
        {
            private readonly Mat frame;
            public Fonte(Mat frame) { this.frame = frame; }
            public Mat Frame() { return frame.Clone(); }
        }

        [Test]
        public void TutteLeNote_QrMappaturaEIngombroCorretti()
        {
            string root = GeneratoreCartoncini.CartellaCartoncini();
            string[] files = Directory.GetFiles(Path.Combine(root, "..", "cartoncini-note"), "nota-*.png");
            Assert.That(files.Length, Is.EqualTo(48));
            var mapping = MappaturaMidiJson.Carica(Path.Combine(root, "..", "config", "mappatura-default.json"));
            foreach (string file in files.OrderBy(f => f))
            {
                int midi = int.Parse(Path.GetFileName(file).Split('-')[1]);
                using (var original = Cv2.ImRead(file))
                using (var resized = new Mat())
                using (var frame = new Mat(600, 800, MatType.CV_8UC3, Scalar.White))
                {
                    Cv2.Resize(original, resized, new Size(360, 240));
                    using (var region = new Mat(frame, new Rect(100, 80, 360, 240)))
                        resized.CopyTo(region);
                    var calibration = Calibrazione.Calcola(
                        new[] { new PuntoImmagine(0, 0), new PuntoImmagine(800, 0), new PuntoImmagine(800, 600), new PuntoImmagine(0, 600) },
                        new[] { new PuntoTavolo(0, 0), new PuntoTavolo(1, 0), new PuntoTavolo(1, 1), new PuntoTavolo(0, 1) });
                    var found = new RilevatoreCartoncini(new Fonte(frame), calibration).Rileva();
                    Assert.That(found.Count, Is.EqualTo(1), file);
                    Assert.That(ParserPayload.TryParse(found[0].Payload, out var payload), Is.True, file);
                    Assert.That(mapping.Nota(payload.IdClip), Is.EqualTo(midi), file);
                    Assert.That(found[0].Area.Larghezza, Is.EqualTo(.45).Within(.02), file);
                    Assert.That(found[0].Area.Altezza, Is.EqualTo(.4).Within(.02), file);
                }
            }
        }
    }
}
