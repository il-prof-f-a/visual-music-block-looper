using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    public class QrScenaCompletaTests
    {
        [Test]
        public void QuarantottoNoteSpostateInProspettiva_MantengonoPayloadRigheEStep()
        {
            const int larghezza = 1920, altezza = 1080;
            var griglia = new ConfigGriglia { Area = LayoutTavolo.AreaGrigliaDefault };
            string[] file = Directory.GetFiles(Path.Combine(GeneratoreCartoncini.CartellaCartoncini(),
                "..", "cartoncini-note"), "nota-*.png").OrderBy(f => f).ToArray();
            Assert.That(file, Has.Length.EqualTo(48));
            var attesi = file.Select(f => "VMBL1;N;" +
                (int.Parse(Path.GetFileName(f).Split('-')[1]) - GeneratoreCartoncini.NotaBase)).ToArray();
            var camera = new[] { new Point2f(35, 30), new Point2f(1870, 12),
                new Point2f(1900, 1040), new Point2f(18, 1060) };
            using (var tavolo = new Mat(altezza, larghezza, MatType.CV_8UC3, Scalar.White))
            using (var frame = new Mat())
            using (var prospettiva = Cv2.GetPerspectiveTransform(new[] { new Point2f(0, 0),
                new Point2f(larghezza, 0), new Point2f(larghezza, altezza), new Point2f(0, altezza) }, camera))
            {
                for (int i = 0; i < file.Length; i++)
                    using (var originale = Cv2.ImRead(file[i]))
                    using (var carta = new Mat())
                    {
                        const int w = 150, h = 100;
                        Cv2.Resize(originale, carta, new Size(w, h));
                        // Il bordo nero del QR nei PNG parte a x=75/709; il centro e' a y=235.5/473.
                        double xQr = griglia.Area.X + (2 * (i % 8) - .30) * griglia.Area.Larghezza / 16;
                        double yQr = griglia.Area.Y + (i / 8 + .5 + .20) * griglia.Area.Altezza / 6;
                        int x = (int)Math.Round(xQr * larghezza - w * 75.0 / 709);
                        int y = (int)Math.Round(yQr * altezza - h * 235.5 / 473);
                        using (var zona = new Mat(tavolo, new Rect(x, y, w, h))) carta.CopyTo(zona);
                    }
                Cv2.WarpPerspective(tavolo, frame, prospettiva, new Size(larghezza, altezza),
                    InterpolationFlags.Linear, BorderTypes.Constant, Scalar.White);
                var calibrazione = Calibrazione.Calcola(camera.Select(p => new PuntoImmagine(p.X, p.Y)).ToArray(),
                    new[] { new PuntoTavolo(0, 0), new PuntoTavolo(1, 0),
                        new PuntoTavolo(1, 1), new PuntoTavolo(0, 1) }, larghezza, altezza);
                var traccia = new TracciaCartoncini();
                var lette = new RilevatoreCartoncini(new NoteContigueVisionTests.Fonte(frame), calibrazione)
                    .Rileva(traccia);
                Assert.That(lette.Select(n => n.Payload), Is.EquivalentTo(attesi),
                    string.Join("; ", traccia.Visione.Select(e => e.Codice + ":" + e.Payload)));
                Assert.That(lette.GroupBy(n => n.Payload).All(g => g.Count() == 1), Is.True);
                var stato = new InterpreteTavolo(new Griglia(griglia), LayoutTavolo.Default).Interpreta(lette);
                Assert.That(stato.Errori, Is.Empty);
                Assert.That(stato.Note, Has.Count.EqualTo(48));
                for (int riga = 0; riga < 6; riga++)
                    Assert.That(stato.Note.Where(n => n.Riga == riga).Select(n => n.StepIniziale).OrderBy(s => s),
                        Is.EqualTo(new[] { 0, 2, 4, 6, 8, 10, 12, 14 }), "Riga " + riga);
            }
        }
    }
}
