using System;
using System.Linq;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class VariazioniFoglioTests
    {
        [Test]
        public void ImmagineInvariataNonRichiedeRiletture()
        {
            using (var f = new Mat(360, 640, MatType.CV_8UC3, Scalar.White))
            using (var d = new RilevatoreVariazioni())
            {
                var prima = d.Confronta(f); Assert.That(prima.Regioni, Has.Count.EqualTo(1));
                d.Conferma(prima.Regioni);
                Assert.That(d.Confronta(f).Regioni, Is.Empty);
            }
        }
        [Test]
        public void UnaModificaLocaleNonInvadeIlRestoDelFoglioENonSparisceSenzaConferma()
        {
            using (var f = new Mat(360, 640, MatType.CV_8UC3, Scalar.White))
            using (var d = new RilevatoreVariazioni())
            {
                d.Conferma(d.Confronta(f).Regioni);
                Cv2.Rectangle(f, new Rect(250, 140, 45, 40), Scalar.Black, -1);
                var cambiata = d.Confronta(f);
                Assert.That(cambiata.Regioni.Count, Is.GreaterThan(0));
                Assert.That(cambiata.Regioni.All(r => r.Larghezza < .35 && r.Altezza < .4));
                Assert.That(d.Confronta(f).Regioni.Count, Is.GreaterThan(0));
                d.Conferma(cambiata.Regioni); Assert.That(d.Confronta(f).Regioni, Is.Empty);
            }
        }
        [Test]
        public void RumoreDeboleELuceUniformeNonAvvianoIlDecoder()
        {
            using (var f = new Mat(360, 640, MatType.CV_8UC3, new Scalar(220, 220, 220)))
            using (var g = new Mat(360, 640, MatType.CV_8UC3, new Scalar(190, 190, 190)))
            using (var d = new RilevatoreVariazioni())
            {
                d.Conferma(d.Confronta(f).Regioni);
                Assert.That(d.Confronta(g).Regioni, Is.Empty);
            }
        }
        [Test]
        public void RiempimentoArancioneDelPlayheadSulFondoNonEUnCartoncino()
        {
            using (var f = new Mat(360, 640, MatType.CV_8UC3, Scalar.White))
            using (var d = new RilevatoreVariazioni())
            {
                d.Conferma(d.Confronta(f).Regioni);
                Cv2.Rectangle(f, new Rect(96, 14, 27, 55), new Scalar(165, 214, 255), -1);
                Assert.That(d.Confronta(f).Regioni, Is.Empty);
            }
        }
        [Test]
        public void CambioColoreAParitaDiCorpoRichiedeRilettura()
        {
            using(var f=new Mat(360,640,MatType.CV_8UC3,Scalar.White))
            using(var d=new RilevatoreVariazioni())
            {
                Cv2.Rectangle(f,new Rect(250,140,60,35),new Scalar(0,0,255),-1);
                d.Conferma(d.Confronta(f).Regioni);
                Cv2.Rectangle(f,new Rect(250,140,60,35),new Scalar(255,0,0),-1);
                Assert.That(d.Confronta(f).Regioni,Is.Not.Empty);
            }
        }
        [Test]
        public void FrameQuasiNeroNonVieneConfermatoComeFoglioVuoto()
        {
            using (var f = new Mat(360, 640, MatType.CV_8UC3, new Scalar(10, 10, 10)))
            using (var d = new RilevatoreVariazioni())
                Assert.That(d.Confronta(f).Affidabile, Is.False);
        }
        [Test]
        public void EstensioneGiallaRilevataAncheSeIlGrigioAssomigliaAlFondo()
        {
            using (var f = new Mat(360, 640, MatType.CV_8UC3, new Scalar(210, 210, 210)))
            using (var d = new RilevatoreVariazioni())
            {
                d.Conferma(d.Confronta(f).Regioni);
                Cv2.Rectangle(f, new Rect(250, 140, 60, 35), new Scalar(0, 210, 250), -1);
                Assert.That(d.Confronta(f).Regioni.Count, Is.GreaterThan(0));
            }
        }
    }
}
