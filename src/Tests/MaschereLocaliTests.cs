using System.Linq;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    public class MaschereLocaliTests
    {
        [TestCase(false,0)] [TestCase(false,8)] [TestCase(true,0)] [TestCase(true,8)]
        public void RitaglioDellaMascheraCoincideConLaGeometriaCompleta(bool curva,int restringi)
        {
            var punti=SchemaCalibrazione.Punti.Select(p=>CalibrazioneCurvaTests.Camera(p)).ToArray();
            var c=curva?SchemaCalibrazione.Calcola(punti,1280,720,out _):Calibrazione.Calcola(punti,SchemaCalibrazione.Punti.ToArray(),1280,720);
            var area=new Rect(25,70,1100,220);
            using(var completa=c.MascheraTavolo(1280,720,restringi))
            using(var attesa=new Mat(completa,area))
            using(var locale=c.MascheraTavolo(area,1280,720,restringi))
            using(var diff=new Mat())
            {
                Assert.That(locale.Size(),Is.EqualTo(area.Size));
                Cv2.Absdiff(attesa,locale,diff);Assert.That(Cv2.CountNonZero(diff),Is.Zero);
            }
        }
    }
}
