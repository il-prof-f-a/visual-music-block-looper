using System;
using System.Linq;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class IncrementalitaFoglioTests
    {
        [Test]
        public void PianificazioneABlocchiAnticipaRigheCollegateERispettaIlGiroAB()
        {
            var completa=StrutturaTavolo.Default;
            var ultima=completa.Posizione(5*completa.StepPerMisura);
            Assert.That(AnalizzatoreFoglio.RigheAnticipate(completa,ultima,1),Is.EqualTo(new[]{0,5}),
                "Dall'ultima riga del giro la scansione anticipata deve tornare alla prima.");
            Assert.That(AnalizzatoreFoglio.RigheAnticipate(completa,ultima,2),Is.EqualTo(new[]{0,1,5}),
                "Due misure di anticipo includono anche il blocco della seconda misura futura.");

            Assert.That(StrutturaTavolo.TryCrea(4,2,1,8,out var collegate),Is.True);
            var prima=collegate.Posizione(0);
            Assert.That(AnalizzatoreFoglio.RigheAnticipate(collegate,prima,1),Is.EqualTo(new[]{0,1,2,3,4,5}),
                "Per due gruppi collegati vanno lette le righe attive e quelle della misura successiva.");

            Assert.That(StrutturaTavolo.TryCrea(4,6,5,24,out var daA),Is.True);
            Assert.That(AnalizzatoreFoglio.RigheAnticipate(daA,daA.Posizione(0),1),Is.EqualTo(new[]{1,2}),
                "La misura anticipata deve partire dal punto A anche quando A cade dentro il foglio.");
        }

        [Test]
        public void DuranteLaRiproduzioneLeggeSoloRigheAttiveAnticipateEVincoli()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};var cal=NoteContigueVisionTests.Calibra(false);
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);
                using(var a=new AnalizzatoreFoglio(r,()=>cal,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>ora,
                    true,null,true,1))
                {
                    a.PulsazioniPerOsservazione=1;
                    r.Pubblica(f,ora);a.Passo(true);
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10));
                    long lettureIniziali=a.NumeroLettureZone;
                    e.Avvia();e.TickInterno();
                    for(int i=0;i<24;i++){ora+=21;e.TickInterno();}
                    ora+=21;r.Pubblica(f,ora);a.Passo(true);

                    int blocchiLetti=(int)(a.NumeroLettureZone-lettureIniziali);
                    Assert.That(blocchiLetti,Is.InRange(5,9),
                        "Una scansione live legge le righe attive/anticipate, le incertezze iniziali e tre zone dei vincoli, senza scansione globale.");
                    Assert.That(a.UltimeRegioni,Has.Count.EqualTo(blocchiLetti));
                    Assert.That(a.UltimoEsito,Is.EqualTo("ScansioneBlocchiAnticipata"));
                }
            }
        }

        [Test]
        public void ModalitaABlocchiFermiLeggeUnaSolaZonaPerOsservazione()
        {
            long ora = 0;
            var cfg = new ConfigGriglia { Area = LayoutTavolo.AreaGrigliaDefault };
            var cal = NoteContigueVisionTests.Calibra(false);
            using (var f = NoteContigueVisionTests.Scena(false))
            using (var r = new RegistroFotogrammi())
            using (var e = new EsecutoreMidi(new EsecutorePartituraTests.Uscita(), null,
                new EsecutorePartituraTests.Mappa(), cfg, () => ora))
            {
                var p = new PreparatoreFoglio(e);
                using (var a = new AnalizzatoreFoglio(r, () => cal, cfg, LayoutTavolo.Default, p,
                    new RegistroCatture(), () => ora, true, null, true, 2))
                {
                    r.Pubblica(f, ora);
                    a.Passo(true); // La prima istantanea inizializza l'intero catalogo.
                    long letture = a.NumeroLettureZone;

                    ora += 250;
                    r.Pubblica(f, ora);
                    a.Passo(true);

                    Assert.That(a.NumeroLettureZone - letture, Is.EqualTo(1));
                    Assert.That(a.UltimeRegioni, Has.Count.EqualTo(1));
                    Assert.That(a.UltimoEsito, Is.EqualTo("ScansioneBlocchiFermi"));
                }
            }
        }

        [Test]
        public void AnalisiSoloSeCambiaDisattivataForzaLaScenaCompletaAncheConBlocchiAnticipatiAttivi()
        {
            long ora=0;
            var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
            var cal=NoteContigueVisionTests.Calibra(false);
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);
                using(var a=new AnalizzatoreFoglio(r,()=>cal,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>ora,
                    false,null,true,2))
                {
                    a.PulsazioniPerOsservazione=1;
                    r.Pubblica(f,ora);a.Passo(true);
                    long letture=a.NumeroLettureZone;
                    e.Avvia();e.TickInterno();
                    for(int i=0;i<24;i++){ora+=21;e.TickInterno();}
                    r.Pubblica(f,ora);a.Passo(true);

                    Assert.That(a.NumeroLettureZone-letture,Is.EqualTo(1));
                    Assert.That(a.UltimoEsito,Does.StartWith("ScansioneCompleta"));
                }
            }
        }

        [Test]
        public void ScansioneAnticipataConfermaLaRimozioneSuDueFrameDelBlocco()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};var cal=NoteContigueVisionTests.Calibra(false);
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);
                using(var a=new AnalizzatoreFoglio(r,()=>cal,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>ora,
                    true,null,true,1))
                {
                    a.PulsazioniPerOsservazione=4;
                    r.Pubblica(f,ora);a.Passo(true);
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10));
                    Cv2.Rectangle(f,new Rect(225,310,200,140),Scalar.White,-1);
                    e.Avvia();e.TickInterno();

                    for(int i=0;i<96;i++){ora+=21;e.TickInterno();}
                    ora+=21;r.Pubblica(f,ora);a.Passo(true);
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10),
                        "Il primo blocco completo aggiornato richiede una seconda cattura indipendente.");

                    for(int i=0;i<96;i++){ora+=21;e.TickInterno();}
                    ora+=21;r.Pubblica(f,ora);a.Passo(true);
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(9),
                        "La stessa riga, osservata nella misura successiva, conferma la rimozione.");
                }
            }
        }

        [Test]
        public void ScansioneAnticipataNonCancellaLeNoteSuFrameNero()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};var cal=NoteContigueVisionTests.Calibra(false);
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var buio=new Mat(900,1600,MatType.CV_8UC3,Scalar.Black))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);
                using(var a=new AnalizzatoreFoglio(r,()=>cal,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>ora,
                    true,null,true,1))
                {
                    a.PulsazioniPerOsservazione=4;
                    r.Pubblica(f,ora);a.Passo(true);
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10));
                    e.Avvia();e.TickInterno();

                    for(int cattura=0;cattura<2;cattura++)
                    {
                        for(int i=0;i<96;i++){ora+=21;e.TickInterno();}
                        ora+=21;r.Pubblica(buio,ora);a.Passo(true);
                    }

                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10),
                        "Due frame neri non devono sembrare conferma della rimozione di tutte le righe.");
                }
            }
        }

        [Test]
        public void DueModificheIsolateNonConfermanoUnaNuovaPartitura()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};var c=NoteContigueVisionTests.Calibra(false);
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var vuoto=new Mat(900,1600,MatType.CV_8UC3,Scalar.White))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);
                using(var a=new AnalizzatoreFoglio(r,()=>c,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>ora))
                {
                    foreach(var frame in new[]{vuoto,vuoto,f,vuoto,f}){ora+=100;r.Pubblica(frame,ora);a.Passo(true);}
                    Assert.That(a.UltimoStato.Note,Is.Empty,"A,A,B,A,B non sono due conferme consecutive di B.");
                    Assert.That(p.NumeroCompilazioni,Is.EqualTo(1));
                    ora+=100;r.Pubblica(f,ora);a.Passo(true);Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10));
                }
            }
        }
        [Test]
        public void FoglioFermoNonVieneRidecodificatoONuovamenteCompilato()
        {
            long ora=0;var u=new EsecutorePartituraTests.Uscita();var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(u,null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);var c=NoteContigueVisionTests.Calibra(false);
                using(var a=new AnalizzatoreFoglio(r,()=>c,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>ora))
                {
                    for(int i=0;i<3;i++){ora+=100;r.Pubblica(f,ora);a.Passo(true);}
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10));Assert.That(p.NumeroCompilazioni,Is.EqualTo(1));
                    long letture=a.NumeroLettureZone;
                    for(int i=0;i<6;i++){ora+=100;r.Pubblica(f,ora);a.Passo(true);}
                    Assert.That(a.NumeroLettureZone,Is.EqualTo(letture));Assert.That(p.NumeroCompilazioni,Is.EqualTo(1));
                }
            }
        }
        [Test]
        public void UnaRimozioneConfermataAggiornaSoltantoLaRegioneCoinvolta()
        {
            long ora=0;var u=new EsecutorePartituraTests.Uscita();var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(u,null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);var c=NoteContigueVisionTests.Calibra(false);
                using(var a=new AnalizzatoreFoglio(r,()=>c,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>ora))
                {
                    for(int i=0;i<2;i++){ora+=100;r.Pubblica(f,ora);a.Passo(true);}
                    Cv2.Rectangle(f,new Rect(225,310,200,140),Scalar.White,-1);
                    for(int i=0;i<2;i++){ora+=100;r.Pubblica(f,ora);a.Passo(true);}
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(9));
                    Assert.That(a.UltimeRegioni.All(z=>z.Larghezza<.4&&z.Altezza<.5));
                    Assert.That(p.NumeroCompilazioni,Is.EqualTo(2));
                }
            }
        }

        [Test]
        public void RimozioneConfermataRestaApplicataAncheSeUnAltraZonaEIncompleta()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};var c=NoteContigueVisionTests.Calibra(false);
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);var debug=new RegistroCatture{Attivo=true};
                using(var a=new AnalizzatoreFoglio(r,()=>c,cfg,LayoutTavolo.Default,p,debug,()=>ora))
                {
                    for(int i=0;i<2;i++){ora+=100;r.Pubblica(f,ora);a.Passo(true);}
                    long lettureIniziali=a.NumeroLettureZone;
                    Cv2.Rectangle(f,new Rect(225,310,200,140),Scalar.White,-1);
                    Cv2.Rectangle(f,new Rect(45,680,100,100),new Scalar(25,25,25),-1);
                    for(int i=0;i<2;i++){ora+=100;r.Pubblica(f,ora);a.Passo(true);}
                    var cattura=debug.Istantanea().Catture.Last(x=>x.Visione.Any(v=>v.Codice=="LetturaZona"));
                    Assert.That(a.NumeroLettureZone,Is.GreaterThan(lettureIniziali),"La scena modificata deve riaprire almeno una ROI.");
                    Assert.That(cattura.Visione.Any(v=>v.Codice=="LetturaZona"&&v.Dettaglio.Contains("completa=False")),Is.True,
                        "La ROI volutamente ambigua deve restare incompleta. Esiti: "+string.Join(" | ",cattura.Visione.Select(v=>v.Codice+":"+v.Dettaglio)));
                    Assert.That(cattura.Visione.Any(v=>v.Codice=="LetturaZona"&&v.Dettaglio.Contains("completa=True")),Is.True,
                        "La ROI che contiene il cartoncino rimosso deve confermarne l'assenza.");
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(9),
                        "La zona incerta non deve ripristinare una rimozione confermata in un'altra ROI.");
                }
            }
        }

        [Test]
        public void RileggiRiparteDaZeroEForzaUnaNuovaIstantaneaConEsposizioneBassa()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};var cal=NoteContigueVisionTests.Calibra(false);
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var buio=new Mat(900,1600,MatType.CV_8UC3,Scalar.Black))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);var debug=new RegistroCatture{Attivo=true};
                using(var a=new AnalizzatoreFoglio(r,()=>cal,cfg,LayoutTavolo.Default,p,debug,()=>ora))
                {
                    for(int i=0;i<2;i++){ora+=100;r.Pubblica(f,ora);a.Passo(true);}
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10));
                    long letture=a.NumeroLettureZone;

                    a.Rileggi();ora+=100;r.Pubblica(buio,ora);a.Passo(true);

                    Assert.That(a.NumeroLettureZone,Is.GreaterThan(letture),
                        "Una rilettura manuale deve avviare la lettura completa senza passare dal filtro delle differenze.");
                    Assert.That(debug.Istantanea().Catture.Last().Visione.Any(v=>v.Codice=="TempiIstantanea"),Is.True,
                        "La nuova istantanea deve arrivare davvero al lettore QR.");
                    Assert.That(a.UltimoStato.Note,Is.Empty,
                        "Una rilettura deve azzerare il catalogo rilevato prima di ricostruirlo dalla nuova istantanea.");
                }
            }
        }

        [Test]
        public void ModalitaScenaCompletaLeggeAllaCadenzaMusicaleSelezionata()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};var cal=NoteContigueVisionTests.Calibra(false);
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);var debug=new RegistroCatture{Attivo=true};
                using(var a=new AnalizzatoreFoglio(r,()=>cal,cfg,LayoutTavolo.Default,p,debug,()=>ora,false))
                {
                    a.PulsazioniPerOsservazione=2;
                    r.Pubblica(f,ora);a.Passo(true);
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10));
                    long lettureIniziali=a.NumeroLettureZone;

                    e.Avvia();e.TickInterno();
                    for(int i=0;i<46;i++)
                    {
                        e.TickInterno();ora+=10;r.Pubblica(f,ora);a.Passo();
                    }
                    Assert.That(a.NumeroLettureZone,Is.EqualTo(lettureIniziali),
                        "Con cadenza ogni 2 pulsazioni non si rilegge prima del tick MIDI 48.");

                    e.TickInterno();ora+=10;r.Pubblica(f,ora);a.Passo();
                    Assert.That(a.NumeroLettureZone,Is.EqualTo(lettureIniziali+1),
                        "Alla cadenza scelta deve essere decodificata una nuova istantanea completa.");
                    Assert.That(debug.Istantanea().Catture.Last().Visione.Any(v=>v.Codice=="TempiIstantanea"),Is.True,
                        "La lettura pianificata deve passare dal lettore dell'intera istantanea.");

                    long secondaLettura=a.NumeroLettureZone;
                    for(int i=0;i<47;i++)
                    {
                        e.TickInterno();ora+=10;r.Pubblica(f,ora);a.Passo();
                    }
                    Assert.That(a.NumeroLettureZone,Is.EqualTo(secondaLettura),
                        "Non si deve avviare una seconda lettura prima della cadenza successiva.");
                    e.TickInterno();ora+=10;r.Pubblica(f,ora);a.Passo();
                    Assert.That(a.NumeroLettureZone,Is.EqualTo(secondaLettura+1));
                }
            }
        }

        [Test]
        public void ModalitaScenaCompletaConfermaLaRimozioneDiUnCartoncino()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};var cal=NoteContigueVisionTests.Calibra(false);
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);
                using(var a=new AnalizzatoreFoglio(r,()=>cal,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>ora,false))
                {
                    r.Pubblica(f,ora);a.Passo(true);
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10));
                    long letture=a.NumeroLettureZone;
                    Cv2.Rectangle(f,new Rect(225,310,200,140),Scalar.White,-1);
                    for(int i=0;i<2;i++)
                    {ora+=100;r.Pubblica(f,ora);a.Passo(true);}

                    Assert.That(a.NumeroLettureZone,Is.EqualTo(letture+2),
                        "Ogni osservazione deve decodificare l'intera scena, anche in assenza del filtro ROI.");
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(9),
                        "Due istantanee coerenti devono rimuovere la nota del cartoncino tolto.");
                }
            }
        }

        [Test]
        public void ModalitaScenaCompletaConservaLeNoteQuandoLaCatturaEIlleggibile()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};var cal=NoteContigueVisionTests.Calibra(false);
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var buio=new Mat(900,1600,MatType.CV_8UC3,Scalar.Black))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);
                using(var a=new AnalizzatoreFoglio(r,()=>cal,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>ora,false))
                {
                    r.Pubblica(f,ora);a.Passo(true);
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10));

                    ora+=100;r.Pubblica(buio,ora);a.Passo(true);

                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10),
                        "Un frame troppo buio per una lettura affidabile non deve cancellare il catalogo MIDI.");
                    Assert.That(a.UltimoEsito,Is.EqualTo("ScansioneCompletaNonAffidabile"));
                }
            }
        }

        [Test]
        public void ModalitaScenaCompletaNonConfermaDueVolteLoStessoFrameDurantePreparazione()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};var cal=NoteContigueVisionTests.Calibra(false);
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);
                using(var a=new AnalizzatoreFoglio(r,()=>cal,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>ora,false))
                {
                    e.Avvia();e.TickInterno();
                    r.Pubblica(f,ora);a.Passo(true);
                    Assert.That(e.Stato.InPreparazione,Is.True);
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10));

                    Cv2.Rectangle(f,new Rect(225,310,200,140),Scalar.White,-1);
                    ora+=100;r.Pubblica(f,ora);a.Passo(true);
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10),
                        "La prima scansione nuova deve soltanto iniziare la conferma della rimozione.");
                    long letture=a.NumeroLettureZone;

                    ora+=300;a.Passo();
                    Assert.That(a.NumeroLettureZone,Is.EqualTo(letture),
                        "Il frame ripetuto dalla webcam non è una seconda cattura indipendente.");
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10));

                    ora+=100;r.Pubblica(f,ora);a.Passo(true);
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(9),
                        "Un nuovo frame coerente può completare la conferma e rimuovere la nota.");
                }
            }
        }

        [Test]
        public void AvvioSingoloMaUnFotogrammaGiaUsatoNonConfermaUnaModifica()
        {
            var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>0))
            {
                var p=new PreparatoreFoglio(e);var c=NoteContigueVisionTests.Calibra(false);
                using(var a=new AnalizzatoreFoglio(r,()=>c,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>0))
                {
                    r.Pubblica(f,0);a.Passo(true);a.Passo(true);
                    Assert.That(p.NumeroCompilazioni,Is.EqualTo(1),"Il primo frame basta per l'avvio.");
                    Cv2.Rectangle(f,new Rect(225,310,200,140),Scalar.White,-1);
                    r.Pubblica(f,1);a.Passo(true);a.Passo(true);
                    Assert.That(p.NumeroCompilazioni,Is.EqualTo(1),"Riusare lo stesso ID non conferma una rimozione.");
                    r.Pubblica(f,2);a.Passo(true);
                    Assert.That(p.NumeroCompilazioni,Is.EqualTo(2));
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(9));
                }
            }
        }
    }
}
