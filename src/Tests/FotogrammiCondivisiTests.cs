using System;
using System.Threading;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class FotogrammiCondivisiTests
    {
        [Test]
        public void OgniLettorePossiedeUnaCopiaAncheQuandoIlProduttoreAvanza()
        {
            using (var r = new RegistroFotogrammi())
            using (var a = new Mat(40, 60, MatType.CV_8UC3, new Scalar(10, 20, 30)))
            {
                r.Pubblica(a, 100); using (var primo = r.Copia())
                {
                    a.SetTo(Scalar.White); r.Pubblica(a, 200);
                    using (var secondo = r.Copia())
                    {
                        Assert.That(primo.Id, Is.LessThan(secondo.Id));
                        Assert.That(primo.Immagine.At<Vec3b>(0, 0).Item0, Is.EqualTo(10));
                        Assert.That(secondo.Immagine.At<Vec3b>(0, 0).Item0, Is.EqualTo(255));
                    }
                }
            }
        }
        [Test]
        public void CambiareDimensioniAvanzaLaGenerazioneVideo()
        {
            using (var r = new RegistroFotogrammi())
            using (var a = new Mat(40, 60, MatType.CV_8UC3, Scalar.White))
            using (var b = new Mat(80, 120, MatType.CV_8UC3, Scalar.White))
            {
                r.Pubblica(a, 1); using (var prima = r.Copia())
                { r.Pubblica(b, 2); using (var dopo = r.Copia()) Assert.That(dopo.Generazione, Is.GreaterThan(prima.Generazione)); }
            }
        }
        private sealed class Fonte : IFonteImmagini
        {
            public int Letti;
            public Mat Frame() { Interlocked.Increment(ref Letti); return new Mat(40, 60, MatType.CV_8UC3, Scalar.White); }
        }
        [Test]
        public void AcquisizioneProsegueSenzaAspettareUnDecoderCheTieneUnaCopia()
        {
            var fonte = new Fonte();
            using (var a = new AcquisitoreVideo(fonte, () => Environment.TickCount))
            {
                a.Avvia(); Assert.That(SpinWait.SpinUntil(() => a.Registro.NumeroFotogrammi >= 1, 1000));
                using (var frame = a.Registro.Copia())
                {
                    long iniziale = frame.Id;
                    Assert.That(SpinWait.SpinUntil(() => a.Registro.NumeroFotogrammi >= iniziale + 3, 1000));
                    Assert.That(frame.Immagine.Empty(), Is.False);
                }
                a.Ferma(); int letti = fonte.Letti; Thread.Sleep(100); Assert.That(fonte.Letti, Is.EqualTo(letti));
            }
        }
    }
}
