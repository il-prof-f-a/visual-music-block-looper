using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;
using Mat = OpenCvSharp.Mat;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture, Apartment(ApartmentState.STA)]
    public class RidimensionamentoFinestraTests
    {
        private sealed class Mappatura : IMappaturaMidi
        {
            public int Canale(int traccia) => 0;
            public int Nota(int idClip) => 36;
            public int Velocity => 100;
        }

        private sealed class WebcamFinta : ISorgenteWebcam
        {
            public int Larghezza => 640;
            public int Altezza => 480;
            public double FpsCatturati => 0;
            public Mat Frame() => null;
            public Bitmap Anteprima() => new Bitmap(Larghezza, Altezza);
            public Bitmap AnteprimaRaddrizzata(Calibrazione c, int w, int h) => null;
            public void ApplicaImpostazioni(ConfigurazioneWebcam c) { }
            public StatoImpostazioniWebcam LeggiImpostazioni() => StatoImpostazioniWebcam.NonSupportato();
            public void Dispose() { }
        }

        private static FormControllo Crea()
        {
            var config = new ConfigGriglia();
            var rilevatore = new RilevatoreCommutabile();
            var compositore = new Compositore(rilevatore, new UscitaMidiSilenziosa(), null, new Mappatura(), config);
            var webcam = new GestoreWebcam(i => new WebcamFinta());
            webcam.Apri(0);
            return new FormControllo(compositore, new FormProiettore(config), webcam,
                rilevatore, null, new ConfigurazioneApplicazione(), null, new List<InfoWebcam>(),
                () => 0, "MIDI test", new string[0], new IDisposable[0], false);
        }

        private static void Evento(Control controllo, string nome, EventArgs argomenti) =>
            typeof(Control).GetMethod(nome, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controllo, new object[] { argomenti });

        [TestCase(1024, 900)]
        [TestCase(1366, 768)]
        [TestCase(1920, 1080)]
        public void Ingrandendo_AnteprimaControlliETestiCresconoSenzaSovrapporsi(int larghezza, int altezza)
        {
            using (var form = Crea())
            {
                var anteprima = form.Controls.OfType<PictureBox>().Single();
                var calibra = form.Controls.OfType<Button>().Single(c => c.Text == "Calibra");
                var regolo = form.Controls.OfType<TrackBar>().OrderBy(c => c.Top).First();
                var dimensioni = anteprima.Size;
                int larghezzaPulsante = calibra.Width, larghezzaRegolo = regolo.Width;
                float font = calibra.Font.Size;
                form.ClientSize = new Size(larghezza, altezza);

                Assert.Multiple(() =>
                {
                    Assert.That(anteprima.Width, Is.GreaterThan(dimensioni.Width));
                    Assert.That(anteprima.Height, Is.GreaterThan(dimensioni.Height));
                    Assert.That(calibra.Width, Is.GreaterThan(larghezzaPulsante));
                    Assert.That(regolo.Width, Is.GreaterThan(larghezzaRegolo));
                    Assert.That(calibra.Font.Size, Is.GreaterThan(font));
                    Assert.That(anteprima.SizeMode, Is.EqualTo(PictureBoxSizeMode.Zoom));
                });
                Control[] controlli = form.Controls.Cast<Control>().ToArray();
                foreach (Control controllo in controlli)
                    Assert.That(form.ClientRectangle.Contains(controllo.Bounds), Is.True, controllo.Text);
                for (int i = 0; i < controlli.Length; i++)
                    for (int j = i + 1; j < controlli.Length; j++)
                        Assert.That(controlli[i].Bounds.IntersectsWith(controlli[j].Bounds), Is.False,
                            controlli[i].Text + " / " + controlli[j].Text);
            }
        }

        [Test]
        public void ResizeRipetuti_RitornanoAlLayoutInizialeSenzaDeriva()
        {
            using (var form = Crea())
            {
                Size iniziale = form.ClientSize;
                var limiti = form.Controls.Cast<Control>().ToDictionary(c => c, c => c.Bounds);
                var font = form.Controls.Cast<Control>().ToDictionary(c => c, c => c.Font.Size);
                for (int i = 0; i < 8; i++)
                {
                    form.ClientSize = new Size(1366 + i, 900 + i);
                    form.ClientSize = iniziale;
                }
                foreach (Control controllo in form.Controls)
                {
                    Assert.That(controllo.Bounds, Is.EqualTo(limiti[controllo]), controllo.Text);
                    Assert.That(controllo.Font.Size, Is.EqualTo(font[controllo]).Within(0.01), controllo.Text);
                }
            }
        }

        [Test]
        public void DimensioneMinima_MantieneAccessibiliIControlli()
        {
            using (var form = Crea())
            {
                Size iniziale = form.ClientSize;
                // Il bordo della finestra modifica Size; ClientSize impostata direttamente su una
                // form nascosta può ignorare i limiti nativi di Windows.
                form.Size = new Size(400, 300);
                Assert.That(form.ClientSize.Width, Is.GreaterThanOrEqualTo(iniziale.Width));
                Assert.That(form.ClientSize.Height, Is.GreaterThanOrEqualTo(iniziale.Height));
            }
        }

        [Test]
        public void ResizeDuranteCalibrazione_IlPuntoRestaSulloStessoPixelDellImmagine()
        {
            using (var form = Crea())
            {
                var anteprima = form.Controls.OfType<PictureBox>().Single();
                Evento(form.Controls.OfType<Button>().Single(c => c.Text == "Calibra"), "OnClick", EventArgs.Empty);
                Evento(anteprima, "OnMouseClick", new MouseEventArgs(MouseButtons.Left, 1,
                    anteprima.ClientSize.Width / 2, anteprima.ClientSize.Height / 2, 0));
                int larghezzaIniziale = anteprima.Width;
                form.ClientSize = new Size(1520, 1432);
                Assert.That(anteprima.Width, Is.GreaterThan(larghezzaIniziale));

                using (var bitmap = new Bitmap(anteprima.Width, anteprima.Height))
                {
                    _ = anteprima.Handle;
                    anteprima.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bool trovato = false;
                    int cx = anteprima.ClientSize.Width / 2, cy = anteprima.ClientSize.Height / 2;
                    for (int x = cx - 15; x <= cx + 15; x++)
                        for (int y = cy - 15; y <= cy + 15; y++)
                        {
                            Color pixel = bitmap.GetPixel(x, y);
                            trovato |= pixel.G > 180 && pixel.R < 80 && pixel.B < 80;
                        }
                    Assert.That(trovato, Is.True, "Il marker deve seguire il centro dell'immagine dopo il resize.");
                }
            }
        }
    }
}
