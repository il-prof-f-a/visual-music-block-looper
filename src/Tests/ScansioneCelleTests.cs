using System;
using System.Linq;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class ScansioneCelleTests
    {
        [Test]
        public void TreCelleConMargineSuTutteLeVociParallele()
        {
            StrutturaTavolo.TryCrea(4,2,1,8,out var s);
            var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
            var p=new PosizioneIndicatore(4,new[]{0,2,4},4);
            var zone=AnalizzatoreFoglio.FinestreCelle(s,p,cfg);
            Assert.That(zone,Has.Length.EqualTo(3));
            for(int i=0;i<3;i++)
            {
                Assert.That(zone[i].X,Is.LessThan(cfg.Area.X+3*cfg.Area.Larghezza/16));
                Assert.That(zone[i].X+zone[i].Larghezza,Is.GreaterThan(cfg.Area.X+6*cfg.Area.Larghezza/16));
                Assert.That(zone[i].Larghezza,Is.LessThan(4*cfg.Area.Larghezza/16));
                Assert.That(zone[i].Y,Is.EqualTo(cfg.Area.Y+2*i*cfg.Area.Altezza/6).Within(1e-8));
            }
        }

        [Test]
        public void ConfineRigoNonLeggeAltraVoceONoteFuoriMisura()
        {
            StrutturaTavolo.TryCrea(2,2,1,4,out var s);
            var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
            var zone=AnalizzatoreFoglio.FinestreCelle(s,new PosizioneIndicatore(7,new[]{1,3,5},1),cfg);
            Assert.That(zone.All(z=>z.X+z.Larghezza<=cfg.Area.X+8*cfg.Area.Larghezza/16+1e-9),Is.True);
            Assert.That(zone.All(z=>z.Altezza<=cfg.Area.Altezza/6+1e-9),Is.True);
        }

        [Test]
        public void AvanzaNellaStessaMisuraConfermaSoloIlGruppoCompletoENonRiusaIlFrame()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
            var cal=NoteContigueVisionTests.Calibra(false);
            using(var bianco=new Mat(900,1600,MatType.CV_8UC3,Scalar.White))
            using(var scuro=bianco.Clone())
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var prep=new PreparatoreFoglio(e);
                using(var a=new AnalizzatoreFoglio(r,()=>cal,cfg,LayoutTavolo.Default,prep,new RegistroCatture(),()=>ora,
                    true,null,true,4,scansioneCelle:true))
                {
                    r.Pubblica(bianco,ora);a.Passo(true);
                    StrutturaTavolo.TryCrea(4,2,1,8,out var struttura);
                    prep.Aggiorna(new StatoTavolo(null,null,null,struttura),true);
                    e.Avvia();e.TickInterno();ora+=50;r.Pubblica(bianco,ora);a.Passo();
                    Assert.That(a.Scansione.UltimaCompletata,Is.Not.Null);
                    Assert.That(a.Scansione.UltimaCompletata.Posizione.RigheAttive,Is.EqualTo(new[]{0,2,4}));
                    Assert.That(a.Passo(),Is.False);
                    for(int i=0;i<6;i++)e.TickInterno();
                    ora+=50;r.Pubblica(bianco,ora);
                    Assert.That(a.Passo(),Is.True,"Una nuova cella non aspetta la misura successiva.");
                    Assert.That(a.Scansione.UltimaCompletata.Posizione.Step,Is.EqualTo(1).Within(1e-8));
                    Assert.That(a.UltimeRegioni.Where(z=>z.X>=cfg.Area.X).All(z=>z.Larghezza<cfg.Area.Larghezza*.26),Is.True);
                    var prima=a.Scansione.UltimaCompletata;
                    for(int i=0;i<6;i++)e.TickInterno();
                    Cv2.Rectangle(scuro,new Rect(210,580,300,145),Scalar.Black,-1);
                    ora+=50;r.Pubblica(scuro,ora);a.Passo();
                    Assert.That(a.Scansione.UltimaCompletata,Is.SameAs(prima),"Una voce buia non conferma le altre da sola.");
                }
            }
        }

        [TestCase(false),TestCase(true)]
        public void AggiuntaEPuliziaLocaleRichiedonoDueFrameEDiventanoPartitura(bool codaPiena)
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
            var cal=NoteContigueVisionTests.Calibra(false);
            using(var bianco=new Mat(900,1600,MatType.CV_8UC3,Scalar.White))
            using(var scena=NoteContigueVisionTests.Scena(false))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var prep=new PreparatoreFoglio(e);
                using(var a=new AnalizzatoreFoglio(r,()=>cal,cfg,LayoutTavolo.Default,prep,new RegistroCatture(),()=>ora,
                    true,null,true,2,scansioneCelle:true))
                {
                    r.Pubblica(bianco,ora);a.Passo(true);e.Avvia();e.TickInterno();
                    for(int i=0;i<18;i++)e.TickInterno();
                    ora+=30;r.Pubblica(scena,ora);a.Passo();
                    Assert.That(prep.Foglio.Ultima.CopiaStato().Note,Is.Empty,"Non pubblicare la prima candidata.");
                    if(codaPiena)
                    {
                        var p=e.Prepara(new StatoTavolo(null,null,null),1,1,0);
                        for(int i=4;i<=516;i++)if(i!=34)Assert.That(e.PubblicaCella(p,i),Is.True);
                    }
                    ora+=30;r.Pubblica(scena,ora);a.Passo();
                    if(codaPiena)
                    {
                        Assert.That(a.UltimoStato.Note,Is.Empty,"Il catalogo non anticipa una pubblicazione fallita.");
                        Assert.That(a.Scansione.UltimaCompletata,Is.Null,"Il blu non certifica una modifica rifiutata.");
                        for(int i=0;i<6;i++)e.TickInterno();
                        ora+=30;r.Pubblica(scena,ora);a.Passo();
                        Assert.That(prep.UltimoErrore,Is.Null);
                    }
                    Assert.That(prep.Foglio.Ultima.CopiaStato().Note.Any(n=>n.Riga==2&&n.StepIniziale==3),Is.True);
                    long versione=prep.Foglio.Ultima.Versione;
                    a.MisureAnticipoScansione=8; // stesso rigo nel giro successivo, nuova osservazione esplicita
                    ora+=30;r.Pubblica(bianco,ora);a.Passo();
                    Assert.That(prep.Foglio.Ultima.Versione,Is.EqualTo(versione));
                    ora+=30;r.Pubblica(bianco,ora);a.Passo();
                    Assert.That(prep.Foglio.Ultima.CopiaStato().Note,Is.Empty);
                    Assert.That(e.Stato.InEsecuzione,Is.True);
                }
            }
        }

        [Test]
        public void NuovaGenerazioneVideoAzzeraIlRisultatoDellaScansionePrecedente()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
            var cal=NoteContigueVisionTests.Calibra(false);
            using(var bianco=new Mat(900,1600,MatType.CV_8UC3,Scalar.White))
            using(var altroFormato=new Mat(450,800,MatType.CV_8UC3,Scalar.White))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var prep=new PreparatoreFoglio(e);
                using(var a=new AnalizzatoreFoglio(r,()=>cal,cfg,LayoutTavolo.Default,prep,new RegistroCatture(),()=>ora,
                    true,null,true,4,scansioneCelle:true))
                {
                    r.Pubblica(bianco,ora);a.Passo(true);e.Avvia();e.TickInterno();
                    ora+=40;r.Pubblica(bianco,ora);a.Passo();
                    Assert.That(a.Scansione.UltimaCompletata,Is.Not.Null);
                    r.Pubblica(altroFormato,ora);r.Pubblica(bianco,ora);
                    a.Passo(true);
                    Assert.That(a.Scansione.UltimaCompletata,Is.Null);
                }
            }
        }
    }
}
