using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture, Apartment(ApartmentState.STA)]
    public class RenderCalibrazioneTests
    {
        private sealed class Proiettore : FormProiettore
        {
            public Proiettore() : base(new ConfigGriglia(), LayoutTavolo.Default) { }
            public void Dipingi(Graphics g, Size dimensioni)
            {
                ClientSize = dimensioni;
                base.OnPaint(new PaintEventArgs(g, new Rectangle(Point.Empty, dimensioni)));
            }
        }

        private static Point Centro(int indice, Size dimensioni) => new Point(
            (int)Math.Round(SchemaCalibrazione.Punti[indice].X * dimensioni.Width),
            (int)Math.Round(SchemaCalibrazione.Punti[indice].Y * dimensioni.Height));

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        public void DodiciBersagli_VisibiliConIlCentroAttesoEQuelloCorrenteEvidenziato(int w, int h)
        {
            using (var bitmap = new Bitmap(w, h))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                RenderCalibrazione.Disegna(g, bitmap.Size, 5);
                for (int i = 0; i < 12; i++)
                {
                    Point p = Centro(i, bitmap.Size);
                    Color colore = bitmap.GetPixel(p.X, p.Y);
                    Assert.That(colore.ToArgb(), Is.EqualTo((i == 5 ? Color.RoyalBlue : Color.Black).ToArgb()), "bersaglio " + (i + 1));
                    Assert.That(p.X, Is.InRange(10, w - 10));
                    Assert.That(p.Y, Is.InRange(10, h - 10));
                }
            }
        }

        [Test]
        public void GuidaCalibrazione_NonVieneSostituitaDagliAggiornamentiMusicaliEPoiSiRipristina()
        {
            using (var form = new Proiettore())
            using (var bitmap = new Bitmap(1280, 720))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                form.MostraCalibrazione(2);
                form.Aggiorna(new StatoTavolo(null, null, null), 4, true);
                form.Dipingi(g, bitmap.Size);
                Point p = Centro(2, bitmap.Size);
                Assert.That(bitmap.GetPixel(p.X, p.Y).ToArgb(), Is.EqualTo(Color.RoyalBlue.ToArgb()));
                form.MostraCalibrazione(null);
                form.Dipingi(g, bitmap.Size);
                Assert.That(bitmap.GetPixel(p.X, p.Y).ToArgb(), Is.EqualTo(Color.White.ToArgb()));
            }
        }

        [TestCase(-1)]
        [TestCase(12)]
        public void IndiceNonValido_Rifiutato(int indice)
        {
            using (var bitmap = new Bitmap(640, 480))
            using (Graphics g = Graphics.FromImage(bitmap))
            using (var form = new Proiettore())
            {
                Assert.That(() => RenderCalibrazione.Disegna(g, bitmap.Size, indice), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => form.MostraCalibrazione(indice), Throws.TypeOf<ArgumentOutOfRangeException>());
            }
        }
    }
}
