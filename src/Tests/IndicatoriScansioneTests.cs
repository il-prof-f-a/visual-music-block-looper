using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class IndicatoriScansioneTests
    {
        private sealed class Proiettore : FormProiettore
        {
            public Proiettore(ConfigGriglia cfg):base(cfg,LayoutTavolo.Default){}
            public void Dipingi(Graphics g,Size size)
            {ClientSize=size;base.OnPaint(new PaintEventArgs(g,new Rectangle(Point.Empty,size)));}
        }
        private static IstantaneaTrasporto Trasporto(int tick=0,long istante=100,long esecuzione=1,bool attivo=true)
            =>new IstantaneaTrasporto(StrutturaTavolo.Default,esecuzione,1,attivo,0,0,tick,istante,20);
        private static LetturaScansione Lettura(long misura,long inizio,long fine,long frame=1)
            =>new LetturaScansione(Trasporto(0,inizio),misura,(int)(misura%6),frame,inizio,inizio,fine);

        [TestCase(1),TestCase(2),TestCase(4),TestCase(8),TestCase(16)]
        public void AnticipoSceltoSpostaSubitoLaLineaSenzaCambiareRiproduzione(int anticipo)
        {
            var p=new PresentatoreScansione();var t=Trasporto(36);
            var prima=p.Leggi(t,StatoScansione.Vuoto,4,110);
            var dopo=p.Leggi(t,StatoScansione.Vuoto,anticipo,110);
            Assert.That(dopo.Prevista.MisuraAssoluta,Is.EqualTo(anticipo));
            Assert.That(dopo.Prevista.RigheAttive,Is.EqualTo(new[]{anticipo%6}));
            Assert.That(dopo.Esecuzione.Step,Is.EqualTo(prima.Esecuzione.Step));
            Assert.That(dopo.Prevista.Step,Is.EqualTo(prima.Prevista.Step));
            Assert.That(RenderGuida.ColoreScansionePrevista,Is.EqualTo(Color.DarkOrange));
        }

        [Test]
        public void LineaPrevistaCondivideClockEOffsetConLaPianificazione()
        {
            var p=new PresentatoreScansione();
            var i=p.Leggi(Trasporto(36),StatoScansione.Vuoto,4,110);
            Assert.That(i.Esecuzione.Step,Is.EqualTo(36.5/6).Within(1e-9));
            Assert.That(i.Prevista.Step,Is.EqualTo(i.Esecuzione.Step));
            Assert.That(i.Prevista.MisuraAssoluta,Is.EqualTo(4));
            Assert.That(i.Prevista.RigheAttive,Is.EqualTo(new[]{4}));
            Assert.That(i.Completata.HasValue,Is.False);
        }

        [Test]
        public void RiduzioneAnticipoMostraIlNuovoRisultatoAncheSeLaUiNonVedeIlResetIntermedio()
        {
            var p=new PresentatoreScansione();var lontana=Lettura(16,100,120);
            p.Leggi(Trasporto(),new StatoScansione(null,lontana,lontana,"ok"),16,120);
            var vicina=Lettura(1,140,160,2);
            var i=p.Leggi(Trasporto(),new StatoScansione(null,vicina,vicina,"ok"),1,160);
            Assert.That(i.Prevista.MisuraAssoluta,Is.EqualTo(1));
            Assert.That(i.Completata.Value.MisuraAssoluta,Is.EqualTo(1));
        }

        [Test]
        public void LetturaInCorsoNonFaAvanzareIlRisultatoConfermato()
        {
            var p=new PresentatoreScansione();var completa=Lettura(4,100,120);
            var prima=p.Leggi(Trasporto(),new StatoScansione(null,completa,completa,"ok"),4,120);
            var inCorso=Lettura(5,140,0,2);
            var dopo=p.Leggi(Trasporto(),new StatoScansione(inCorso,completa,inCorso,"lettura"),4,180);
            Assert.That(dopo.Completata.Value.MisuraAssoluta,Is.EqualTo(prima.Completata.Value.MisuraAssoluta));
            Assert.That(dopo.Completata.Value.Step,Is.EqualTo(prima.Completata.Value.Step));
            Assert.That(dopo.InLettura,Is.True);
        }

        [Test]
        public void TransizioneLineareRestaEntroDueRisultatiRealiEPoiSiFerma()
        {
            var p=new PresentatoreScansione();var a=Lettura(4,100,120);var b=Lettura(5,200,300,2);
            p.Leggi(Trasporto(),new StatoScansione(null,a,a,"ok"),4,120);
            var stato=new StatoScansione(null,b,b,"ok");
            var inizio=p.Leggi(Trasporto(),stato,4,300);
            var mezzo=p.Leggi(Trasporto(),stato,4,350);
            var fine=p.Leggi(Trasporto(),stato,4,400);
            Assert.That(inizio.Completata.Value.MisuraAssoluta,Is.EqualTo(4));
            Assert.That(mezzo.Completata.Value.MisuraAssoluta,Is.EqualTo(4));
            Assert.That(mezzo.Completata.Value.Step,Is.EqualTo(8).Within(1e-6));
            Assert.That(fine.Completata.Value.MisuraAssoluta,Is.EqualTo(5));
            var ferma=p.Leggi(Trasporto(),stato,4,1000);
            Assert.That(ferma.Completata.Value.Step,Is.EqualTo(fine.Completata.Value.Step));
            Assert.That(ferma.Completata.Value.MisuraAssoluta,Is.EqualTo(5));
        }

        [Test]
        public void StopONuovaEsecuzioneNonMostranoDatiDiUnaVecchiaScansione()
        {
            var p=new PresentatoreScansione();var a=Lettura(4,100,120);var stato=new StatoScansione(null,a,a,"ok");
            Assert.That(p.Leggi(Trasporto(),stato,4,120).Completata.HasValue,Is.True);
            Assert.That(p.Leggi(Trasporto(attivo:false),stato,4,130).Visibili,Is.False);
            Assert.That(p.Leggi(Trasporto(esecuzione:2),stato,4,140).Completata.HasValue,Is.False);
        }

        [Test]
        public void RiletturaCompletaAzzeraLaLineaAncheSenzaRiavviareIlTrasporto()
        {
            var p=new PresentatoreScansione();var a=Lettura(4,100,120);
            Assert.That(p.Leggi(Trasporto(),new StatoScansione(null,a,a,"ok"),4,120).Completata.HasValue,Is.True);
            Assert.That(p.Leggi(Trasporto(),StatoScansione.Vuoto,4,150).Completata.HasValue,Is.False);
        }

        [TestCase(1280,720)]
        [TestCase(640,360)]
        public void RendererDisegnaLineeSulleRigheEffettiveEAllaScalaDellaFinestra(int larghezza,int altezza)
        {
            var p=new PresentatoreScansione();var t=Trasporto(36);
            var a=Lettura(3,40,60);
            var indicatori=p.Leggi(t,new StatoScansione(null,a,a,"ok"),4,100);
            var cfg=new ConfigGriglia();var size=new Size(larghezza,altezza);
            using(var bmp=new Bitmap(larghezza,altezza))
            using(var g=Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                new RenderGuida(cfg).DisegnaIndicatori(g,size,indicatori);
                int xPrevisto=(int)System.Math.Round(6*larghezza/16.0);
                Assert.That(bmp.GetPixel(xPrevisto,altezza*9/12).ToArgb(),Is.EqualTo(RenderGuida.ColoreScansionePrevista.ToArgb()));
                Assert.That(bmp.GetPixel(1,altezza*7/12).ToArgb(),Is.EqualTo(RenderGuida.ColoreScansioneCompletata.ToArgb()));
                Assert.That(bmp.GetPixel(xPrevisto,altezza*3/12).ToArgb(),Is.EqualTo(Color.White.ToArgb()),
                    "Non disegnare linee nelle righe estranee alla destinazione.");
            }
        }

        [Test,Apartment(ApartmentState.STA)]
        public void FinestraProiettaLeLineeELeNascondeDuranteLaCalibrazione()
        {
            var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
            var s=StrutturaTavolo.Default;var size=new Size(1280,720);
            var t=Trasporto(36);var a=Lettura(3,40,60);
            var indicatori=new PresentatoreScansione().Leggi(t,new StatoScansione(null,a,a,"ok"),4,100);
            using(var f=new Proiettore(cfg))
            using(var bmp=new Bitmap(size.Width,size.Height))
            using(var g=Graphics.FromImage(bmp))
            {
                f.Aggiorna(new StatoTavolo(null,null,null,s),s.Posizione(6),true);
                f.AggiornaIndicatori(indicatori);f.Dipingi(g,size);
                int x=(int)System.Math.Round((cfg.Area.X+6*cfg.Area.Larghezza/16)*size.Width);
                int y=(int)System.Math.Round((cfg.Area.Y+4.5*cfg.Area.Altezza/6)*size.Height);
                Assert.That(bmp.GetPixel(x,y).ToArgb(),Is.EqualTo(RenderGuida.ColoreScansionePrevista.ToArgb()));
                string immagine=Path.Combine(Path.GetTempPath(),"VMBL-indicatori-scansione.png");
                bmp.Save(immagine,System.Drawing.Imaging.ImageFormat.Png);
                TestContext.WriteLine("Proiezione di prova: "+immagine);
                f.MostraCalibrazione(0);f.Dipingi(g,size);
                Assert.That(bmp.GetPixel(x,y).ToArgb(),Is.EqualTo(Color.White.ToArgb()));
                f.MostraCalibrazione(null);f.AggiornaIndicatori(default);f.Dipingi(g,size);
                Assert.That(bmp.GetPixel(x,y).ToArgb(),Is.EqualTo(Color.LightGray.ToArgb()));
            }
        }
    }
}
