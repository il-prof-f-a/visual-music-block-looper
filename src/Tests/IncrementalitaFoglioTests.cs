using System;
using System.Linq;
using System.IO;
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
            Assert.That(AnalizzatoreFoglio.RigheAnticipate(completa,ultima,1),Is.EqualTo(new[]{0}),
                "L'offset esatto dalla sesta misura deve puntare alla prima riga del giro successivo.");
            Assert.That(AnalizzatoreFoglio.RigheAnticipate(completa,ultima,2),Is.EqualTo(new[]{1}),
                "Due misure di anticipo devono puntare soltanto alla seconda riga futura.");

            Assert.That(StrutturaTavolo.TryCrea(4,2,1,8,out var collegate),Is.True);
            var prima=collegate.Posizione(0);
            Assert.That(AnalizzatoreFoglio.RigheAnticipate(collegate,prima,1),Is.EqualTo(new[]{1,3,5}),
                "L'offset esatto conserva i tre gruppi collegati senza includere la misura corrente.");

            Assert.That(StrutturaTavolo.TryCrea(4,6,5,24,out var daA),Is.True);
            Assert.That(AnalizzatoreFoglio.RigheAnticipate(daA,daA.Posizione(0),1),Is.EqualTo(new[]{2}),
                "La misura anticipata deve partire dal punto A anche quando A cade dentro il foglio.");

            Assert.That(StrutturaTavolo.TryCrea(4,2,2,6,out var daMezzaBattuta),Is.True);
            Assert.That(daMezzaBattuta.MisurePerGiro,Is.EqualTo(2));
            Assert.That(AnalizzatoreFoglio.RigheAnticipate(daMezzaBattuta,daMezzaBattuta.Posizione(0),1),
                Is.EqualTo(new[]{1,3,5}),
                "Le frontiere devono seguire la misura reale anche quando A-B inizia a metà battuta.");
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
                    double altezzaRiga=cfg.Area.Altezza/cfg.Tracce;
                    var zoneRighe=a.UltimeRegioni.Where(z=>z.Larghezza>cfg.Area.Larghezza*.8
                        &&z.Y>=cfg.Area.Y&&z.Y+z.Altezza<=cfg.Area.Y+cfg.Area.Altezza).ToArray();
                    Assert.That(zoneRighe,Is.Not.Empty,"La scansione live deve includere almeno una ROI di riga.");
                    Assert.That(zoneRighe.All(z=>Math.Abs(z.Altezza-altezzaRiga)<1e-9),Is.True,
                        "Ogni ROI deve coprire solo la propria riga; allargarla ridigitalizza le righe adiacenti.");
                }
            }
        }

        [Test]
        public void ScansioneAnticipataNonRileggeBlocchiGiaLettiNellaFinestraDelGiro()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};var cal=NoteContigueVisionTests.Calibra(false);
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);
                using(var a=new AnalizzatoreFoglio(r,()=>cal,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>ora,
                    true,null,true,2))
                {
                    a.PulsazioniPerOsservazione=1;
                    r.Pubblica(f,ora);a.Passo(true);
                    long lettureIniziali=a.NumeroLettureZone;

                    e.Avvia();e.TickInterno();
                    ora+=25;r.Pubblica(f,ora);a.Passo(true);
                    long primaFinestra=a.NumeroLettureZone-lettureIniziali;
                    Assert.That(primaFinestra,Is.GreaterThan(0),"L'avvio deve scandire i blocchi attivi e anticipati.");

                    ora+=25;r.Pubblica(f,ora);a.Passo(true);
                    Assert.That(a.NumeroLettureZone-lettureIniziali,Is.EqualTo(primaFinestra),
                        "Una nuova cattura nella stessa misura non deve ridigitalizzare blocchi già confermati.");

                    for(int i=0;i<96;i++){ora+=25;e.TickInterno();}
                    r.Pubblica(f,ora);a.Passo(true);
                    Assert.That(a.NumeroLettureZone-lettureIniziali-primaFinestra,Is.EqualTo(1),
                        "Alla misura successiva va letto solo il nuovo blocco entrato nella finestra di anticipo.");

                    for(int i=0;i<4*96;i++){ora+=25;e.TickInterno();}
                    r.Pubblica(f,ora);a.Passo(true);
                    long scansioneConfine=a.NumeroLettureZone;

                    for(int i=0;i<96;i++){ora+=25;e.TickInterno();}
                    r.Pubblica(f,ora);a.Passo(true);
                    Assert.That(a.NumeroLettureZone-scansioneConfine,Is.EqualTo(4),
                        "All'avvio del giro successivo vanno letti soltanto la nuova riga di anticipo e i tre vincoli del giro.");
                }
            }
        }

        [Test]
        public void ModalitaABlocchiFermiSospendeLeLettureDopoIstantaneaCompleta()
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

                    Assert.That(a.NumeroLettureZone, Is.EqualTo(letture),
                        "A riproduzione ferma, i blocchi si leggono solo con istantanea iniziale o richiesta manuale.");
                    Assert.That(a.UltimeRegioni, Is.Empty);
                    Assert.That(a.UltimoEsito, Is.EqualTo("ScansioneBlocchiInPausa"));
                }
            }
        }

        [Test]
        public void DuranteLaPreparazioneContinuaLaScansioneAnticipata()
        {
            long ora=0;
            var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
            var cal=NoteContigueVisionTests.Calibra(false);
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,
                new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);
                using(var a=new AnalizzatoreFoglio(r,()=>cal,cfg,LayoutTavolo.Default,p,
                    new RegistroCatture(),()=>ora,true,null,true,2))
                {
                    e.Avvia();e.TickInterno();
                    Assert.That(e.Stato.InPreparazione,Is.True);
                    r.Pubblica(f,ora);a.Passo(true);
                    long letture=a.NumeroLettureZone;

                    ora+=250;r.Pubblica(f,ora);a.Passo(true);

                    Assert.That(a.NumeroLettureZone,Is.GreaterThan(letture),
                        "La scansione deve proseguire durante la preparazione del primo avvio MIDI.");
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
        public void RimozioneScansionataQuattroMisurePrimaSiAttivaAllaMisuraDestinazione()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};var cal=NoteContigueVisionTests.Calibra(false);
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);
                using(var a=new AnalizzatoreFoglio(r,()=>cal,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>ora,
                    true,null,true,4))
                {
                    a.PulsazioniPerOsservazione=4;
                    r.Pubblica(f,ora);a.Passo(true);
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10));
                    e.Avvia();e.TickInterno();
                    for(int i=0;i<4*96;i++){ora+=21;e.TickInterno();}
                    Assert.That(e.Stato.MisuraAssoluta,Is.EqualTo(4));

                    Cv2.Rectangle(f,new Rect(225,310,200,140),Scalar.White,-1);
                    ora+=21;r.Pubblica(f,ora);a.Passo();
                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(10),"La prima cattura crea il candidato ma non cancella la nota.");
                    Assert.That(a.Scansione.UltimaCompletata,Is.Null,"Un primo candidato non è ancora una lettura confermata.");
                    Assert.That(a.Passo(),Is.False,"Il medesimo frame non conferma una rimozione.");
                    ora+=66;r.Pubblica(f,ora);
                    Assert.That(a.Passo(),Is.True,"La seconda cattura deve essere letta senza attendere una nuova misura.");

                    Assert.That(a.UltimoStato.Note,Has.Count.EqualTo(9));
                    var completata=a.Scansione.UltimaCompletata;
                    Assert.That(completata,Is.Not.Null);
                    Assert.That(completata.MisuraAssoluta,Is.EqualTo(8));
                    Assert.That(completata.FrameId,Is.EqualTo(r.NumeroFotogrammi));
                    Assert.That(completata.Posizione.RigheAttive,Is.EqualTo(new[]{2}));
                    long lette=a.NumeroLetture;
                    ora+=66;r.Pubblica(f,ora);a.Passo();
                    Assert.That(a.NumeroLetture,Is.EqualTo(lette),"Conclusa la conferma si riprende la cadenza ordinaria.");
                    Assert.That(e.Stato.VersioneAttiva,Is.EqualTo(1));
                    Assert.That(e.Stato.VersionePronta,Is.GreaterThan(e.Stato.VersioneAttiva),
                        "La versione rimossa è pronta per la misura assoluta 8, quattro misure avanti.");
                    Assert.That(e.Stato.PartituraAttiva.CopiaStato().Note,Has.Count.EqualTo(10));

                    for(int i=0;i<4*96;i++){ora+=21;e.TickInterno();}

                    Assert.That(e.Stato.MisuraAssoluta,Is.EqualTo(8));
                    Assert.That(e.Stato.VersioneAttiva,Is.GreaterThan(1));
                    Assert.That(e.Stato.PartituraAttiva.CopiaStato().Note,Has.Count.EqualTo(9));
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
                    Assert.That(a.Scansione.UltimaCompletata,Is.Null,"Il buio non deve avanzare la linea dei risultati.");
                }
            }
        }

        [Test]
        public void CambioAnticipoInPlaySaltaSubitoAllaNuovaDestinazioneNellaStessaMisura()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
            var cal=NoteContigueVisionTests.Calibra(false);
            using(var vuoto=new Mat(900,1600,MatType.CV_8UC3,Scalar.White))
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);
                using(var a=new AnalizzatoreFoglio(r,()=>cal,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>ora,true,null,true,4))
                {
                    r.Pubblica(vuoto,ora);a.Passo(true);e.Avvia();e.TickInterno();
                    ora+=66;r.Pubblica(vuoto,ora);a.Passo();
                    Assert.That(a.Scansione.UltimaCompletata.MisuraAssoluta,Is.EqualTo(4));
                    var proprieta=typeof(AnalizzatoreFoglio).GetProperty("MisureAnticipoScansione");
                    Assert.That(proprieta.CanWrite,Is.True,"L'anticipo deve poter cambiare durante Play.");
                    foreach(int anticipo in new[]{16,1,8,2,4})
                    {
                        proprieta.SetValue(a,anticipo);
                        ora+=66;r.Pubblica(vuoto,ora);
                        Assert.That(a.Passo(),Is.True,"Non attendere la prossima misura dopo la selezione.");
                        Assert.That(a.Scansione.UltimaCompletata.MisuraAssoluta,Is.EqualTo(anticipo));
                        Assert.That(e.Stato.InEsecuzione,Is.True);
                    }
                }
            }
        }

        [Test]
        public void LineaCompletataAttendeTutteLeRigheSimultaneeERiprovaSoloLaRigaBuia()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};var cal=NoteContigueVisionTests.Calibra(false);
            using(var f=NoteContigueVisionTests.Scena(false))
            using(var qr=Cv2.ImRead(Path.Combine(GeneratoreCartoncini.CartellaCartoncini(),"speciale-link-2.png")))
            using(var piccolo=new Mat())
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                Cv2.Resize(qr,piccolo,new Size(180,120));
                using(var zona=new Mat(f,new Rect(25,387,180,120)))piccolo.CopyTo(zona);
                var p=new PreparatoreFoglio(e);
                using(var a=new AnalizzatoreFoglio(r,()=>cal,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>ora,true,null,true,4))
                using(var buio=f.Clone())
                {
                    r.Pubblica(f,ora);a.Passo(true);
                    Assert.That(a.UltimoStato.Struttura.RigheCollegate,Is.EqualTo(2));
                    Cv2.Rectangle(buio,new Rect(220,588,1100,138),Scalar.Black,-1);
                    e.Avvia();e.TickInterno();ora+=66;r.Pubblica(buio,ora);a.Passo();
                    Assert.That(a.Scansione.UltimaCompletata,Is.Null,
                        "Le righe 0 e 2 da sole non completano il gruppo 0,2,4.");
                    long letture=a.NumeroLetture;
                    ora+=66;r.Pubblica(f,ora);a.Passo();
                    Assert.That(a.NumeroLetture-letture,Is.EqualTo(1),"Ritentare solo la riga incompleta.");
                    Assert.That(a.Scansione.UltimaCompletata,Is.Not.Null);
                    Assert.That(a.Scansione.UltimaCompletata.Posizione.RigheAttive,Is.EqualTo(new[]{0,2,4}));
                    Assert.That(a.Scansione.UltimaCompletata.MisuraAssoluta,Is.EqualTo(4));
                }
            }
        }

        [Test]
        public void QrIntegratiDaUnaRoiParzialeDiventanoProntiSoloDopoConfermaCompleta()
        {
            long ora=0;var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};var cal=NoteContigueVisionTests.Calibra(false);
            using(var vuoto=new Mat(900,1600,MatType.CV_8UC3,Scalar.White))
            using(var scena=NoteContigueVisionTests.Scena(false))
            using(var parziale=scena.Clone())
            using(var r=new RegistroFotogrammi())
            using(var e=new EsecutoreMidi(new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,()=>ora))
            {
                var p=new PreparatoreFoglio(e);
                using(var a=new AnalizzatoreFoglio(r,()=>cal,cfg,LayoutTavolo.Default,p,new RegistroCatture(),()=>ora,true,null,true,4))
                {
                    r.Pubblica(vuoto,ora);a.Passo(true);e.Avvia();e.TickInterno();
                    for(int i=0;i<4*96;i++){ora+=21;e.TickInterno();}
                    Cv2.Rectangle(parziale,new Rect(850,340,60,80),Scalar.Black,-1);
                    ora+=66;r.Pubblica(parziale,ora);a.Passo();
                    Assert.That(a.UltimoStato.Note.Count,Is.GreaterThan(0),"I QR leggibili possono entrare nel catalogo osservato.");
                    Assert.That(a.Scansione.UltimaCompletata,Is.Null);
                    ora+=66;r.Pubblica(scena,ora);a.Passo();
                    ora+=66;r.Pubblica(scena,ora);a.Passo();
                    Assert.That(p.Foglio.Ultima.CopiaStato().Note,Has.Count.EqualTo(5),
                        "La conferma deve trasferire al preparatore anche i QR già integrati dalla lettura parziale.");
                    Assert.That(e.Stato.VersionePronta,Is.GreaterThan(e.Stato.VersioneAttiva));
                    Assert.That(a.Scansione.UltimaCompletata,Is.Not.Null);
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
                using(var a=new AnalizzatoreFoglio(r,()=>cal,cfg,LayoutTavolo.Default,p,debug,()=>ora,
                    true,null,true,2))
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
