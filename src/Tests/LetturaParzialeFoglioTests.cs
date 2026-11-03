using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    public class LetturaParzialeFoglioTests
    {
        [Test]
        public void ImpostazioneUsaLAncoraQrQuandoIlCorpoEsceDallaZona()
        {
            var cartoncino=new CartoncinoRilevato
            {
                Payload="VMBL1;S;LINK;2",
                Area=new RettangoloTavolo(.08,.30,.18,.40),
                AreaQr=new RettangoloTavolo(.02,.47,.08,.08)
            };
            var traccia=new TracciaCartoncini();
            var interprete=new InterpreteTavolo(new Griglia(new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault}),LayoutTavolo.Default);
            var stato=interprete.Interpreta(new[]{cartoncino},traccia);
            Assert.That(stato.Struttura.RigheCollegate,Is.EqualTo(2));
            Assert.That(stato.Errori,Is.Empty);
            Assert.That(traccia.Interpretazione.Any(e=>e.Codice=="CollegamentoAccettato"),Is.True);
        }

        [Test]
        public void FusioneParzialeMantieneDueQrNuoviUgualiInPosizioniDistinte()
        {
            const string payload="VMBL1;N;64;3";
            var a=new RettangoloTavolo(.20,.30,.05,.05);var b=new RettangoloTavolo(.26,.30,.05,.05);
            var primo=new CartoncinoRilevato{Payload=payload,Area=a,AreaQr=a};
            var secondo=new CartoncinoRilevato{Payload=payload,Area=b,AreaQr=b};
            var metodo=typeof(AnalizzatoreFoglio).GetMethod("UnisciCatalogoParziale",BindingFlags.NonPublic|BindingFlags.Static);
            var risultato=(System.Collections.Generic.IReadOnlyList<CartoncinoRilevato>)metodo.Invoke(null,
                new object[]{new CartoncinoRilevato[0],new[]{primo,secondo}});
            Assert.That(risultato,Has.Count.EqualTo(2));
        }

        [TestCase(false)] [TestCase(true)]
        public void LinkValidoVieneConfermatoAncheSeUnAltraZonaRestaIlleggibile(bool capovolta)
        {
            Esegui(capovolta,true,(a,p,r,registro,f,ora)=>
            {
                a.Rileggi();
                r.Pubblica(f,ora);a.Passo(true);
                var cattura=registro.Istantanea().Catture.Last();
                Assert.That(cattura.Visione.Any(e=>e.Codice=="LetturaZona"&&e.Dettaglio.Contains("completa=False")),Is.True,
                    "La stessa ROI deve includere una zona non risolta.");
                Assert.That(cattura.Visione.Any(e=>e.Payload=="VMBL1;S;LINK;2"),Is.True,
                    "Il decoder deve avere letto LINK 2 dentro la ROI incompleta.");
                Assert.That(a.UltimoStato.Struttura.RigheCollegate,Is.EqualTo(6),"Il cambio richiede ancora due frame distinti.");
                r.Pubblica(f,ora+1);a.Passo(true);
                var finale=registro.Istantanea().Catture.Last();
                Assert.That(finale.Interpretazione.Any(e=>e.Payload=="VMBL1;S;LINK;2"),Is.True,
                    "QR decodificati: "+string.Join(" | ",finale.Visione.Where(e=>e.Payload=="VMBL1;S;LINK;2").Select(e=>e.Fase+"/"+e.Codice+"/"+e.Zona+"/"+e.Dettaglio))+"; interpretazione: "+
                    string.Join(" | ",finale.Interpretazione.Select(e=>e.Payload+"/"+e.Codice+"/"+e.Esito+"/"+e.Dettaglio)));
                Assert.That(finale.Interpretazione.Any(e=>e.Payload=="VMBL1;S;LINK;2"&&e.Codice=="CollegamentoAccettato"),Is.True,
                    "LINK 2 non applicabile: "+string.Join(" | ",finale.Interpretazione.Where(e=>e.Payload=="VMBL1;S;LINK;2").Select(e=>e.Codice+"/"+e.Esito+"/"+e.Zona+"/"+e.Dettaglio)));
                Assert.That(a.UltimoStato.Struttura.RigheCollegate,Is.EqualTo(2),"Il LINK già letto non deve essere scartato per un corpo anonimo altrove.");
                Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10));
                Assert.That(p.Foglio.Ultima.CopiaStato().Note,Has.Count.EqualTo(10),"Una zona parziale non deve cancellare la nota coperta dalla chiazza scura.");
                Assert.That(p.Foglio.Ultima.CopiaStato().Struttura.RigheCollegate,Is.EqualTo(2));
            });
        }
        [Test]
        public void ZonaIncertaImmutataNonFaRipetereIlDecoderContinuamente()
        {
            Esegui(false,false,(a,p,r,registro,f,ora)=>
            {
                for(int n=0;n<2;n++){r.Pubblica(f,ora+n);a.Passo(true);}
                long prima=a.NumeroLettureZone,compilazioni=p.NumeroCompilazioni;
                for(int n=0;n<5;n++){r.Pubblica(f,ora+10+n);a.Passo(true);}
                Assert.That(a.NumeroLettureZone,Is.EqualTo(prima),"Una regione ancora incerta ma immutata attende un cambiamento o Rileggi.");
                Assert.That(p.NumeroCompilazioni,Is.EqualTo(compilazioni));
                Cv2.Rectangle(f,new Rect(45,680,100,100),new Scalar(0,0,180),-1);
                r.Pubblica(f,ora+15);a.Passo(true);
                Assert.That(a.NumeroLettureZone,Is.GreaterThan(prima),"Una modifica visibile deve riaprire la regione incerta.");
                a.Rileggi();r.Pubblica(f,ora+20);a.Passo(true);
                Assert.That(a.NumeroLettureZone,Is.GreaterThan(prima),"Il controllo manuale deve restare disponibile.");
            });
        }
        private static void Esegui(bool capovolta,bool oscuraNota,Action<AnalizzatoreFoglio,PreparatoreFoglio,RegistroFotogrammi,RegistroCatture,Mat,long> verifica)
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};var cal=NoteContigueVisionTests.Calibra(capovolta);
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                Cv2.Rectangle(f,new Rect(45,680,100,100),new Scalar(25,25,25),-1);
                if(capovolta)Cv2.Rotate(f,f,RotateFlags.Rotate180);
                var p=new PreparatoreFoglio(e);var registro=new RegistroCatture{Attivo=true};
                using(var a=new AnalizzatoreFoglio(r,()=>cal,cfg,LayoutTavolo.Default,p,registro,()=>ora))
                {
                    r.Pubblica(f,ora);a.Passo(true);
                    if(oscuraNota)
                    {
                        var nota=new LettoreIstantanea(cal,cfg).Leggi(f).Cartoncini.First();
                        var box=GeometriaRegioni.InCamera(cal,nota.AreaQr??nota.Area,f.Cols,f.Rows,60)&new Rect(0,0,f.Cols,f.Rows);
                        Cv2.Rectangle(f,box,new Scalar(15,15,15),-1);
                    }
                    if(capovolta)Cv2.Rotate(f,f,RotateFlags.Rotate180);
                    using(var qr=Cv2.ImRead(Path.Combine(GeneratoreCartoncini.CartellaCartoncini(),"speciale-link-2.png")))
                    using(var piccolo=new Mat())
                    using(var zona=new Mat(f,new Rect(25,387,180,120)))
                    {Cv2.Resize(qr,piccolo,new Size(180,120));piccolo.CopyTo(zona);}
                    if(capovolta)Cv2.Rotate(f,f,RotateFlags.Rotate180);
                    ora+=100;verifica(a,p,r,registro,f,ora);
                }
            }
        }
    }
}
