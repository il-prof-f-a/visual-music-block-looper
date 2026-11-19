using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture,Apartment(ApartmentState.STA)]
    public class InterfacciaMidiDeterministicaTests
    {
        private sealed class FonteVuota : VisualMusicBlockLooper.Vision.IFonteImmagini
        { public OpenCvSharp.Mat Frame()=>null; }

        [Test]
        public void FermaAnnullaAncheUnPlayInAttesaDelPrimoFoglio()
        {
            var cfg=new ConfigGriglia();var fonte=new RilevatoreCommutabile();
            using(var c=new Compositore(fonte,new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,
                orologioMs:()=>0,esecuzioneDeterministica:true))
            using(var f=new FormControllo(c,new FormProiettore(cfg),new GestoreWebcam(i=>null),fonte,
                "calibrazione-test.txt",new ConfigurazioneApplicazione(),null,new List<InfoWebcam>(),()=>0,"MIDI",new List<string>(),new List<IDisposable>(),false))
            {
                Action<string> invoca=n=>typeof(FormControllo).GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,null);
                invoca("AvviaFerma");c.Esecutore.ElaboraPendenti();Assert.That(c.Esecutore.Stato.InPreparazione);
                invoca("AggiornaInterfaccia");
                Assert.That(f.Controls.OfType<Button>().Any(b=>b.Text=="Ferma"),Is.True);
                invoca("AvviaFerma");c.Esecutore.ElaboraPendenti();
                c.Preparatore.Aggiorna(new StatoTavolo(null,null,null),true);c.Motore.TickInterno();
                Assert.That(c.Motore.InEsecuzione,Is.False);Assert.That(c.Esecutore.Stato.InPreparazione,Is.False);
            }
        }
        [Test]
        public void StatoVersioniRiletturaETempiMidiSonoAccessibiliERidimensionabili()
        {
            var cfg=new ConfigGriglia();var fonte=new RilevatoreCommutabile();var configApp=new ConfigurazioneApplicazione{PulsazioniPerOsservazione=2};
            string percorso=Path.Combine(Path.GetTempPath(),"vmbl-bpm-"+Guid.NewGuid().ToString("N")+".json");
            try
            {
            using(var c=new Compositore(fonte,new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,
                orologioMs:()=>0,esecuzioneDeterministica:true,fonteVideo:new FonteVuota(),scansioneABlocchiAnticipata:true))
            using(var f=new FormControllo(c,new FormProiettore(cfg),new GestoreWebcam(i=>null),fonte,
                "calibrazione-test.txt",configApp,percorso,new List<InfoWebcam>(),()=>0,"MIDI",new List<string>(),new List<IDisposable>(),false))
            {
                Assert.That(f.Controls.Find("etichettaVersioniFoglio",true),Has.Length.EqualTo(1));
                Assert.That(f.Controls.Find("pulsanteSalvaTempiMidi",true),Has.Length.EqualTo(1));
                var bpm=(NumericUpDown)f.Controls.Find("campoBpm",true).Single();bpm.Value=137.5m;c.Esecutore.ElaboraPendenti();
                Assert.That(c.Motore.Bpm,Is.EqualTo(137.5));
                Assert.That(configApp.BpmIniziale,Is.EqualTo(137.5));
                var timerSalvataggio=(System.Windows.Forms.Timer)typeof(FormControllo)
                    .GetField("timerSalvaConfigurazione",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);
                Assert.That(timerSalvataggio.Enabled,Is.True,"Il BPM modificato deve essere salvato con il debounce di configurazione.");
                typeof(FormControllo).GetMethod("SalvaDaTimer",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,null);
                Assert.That(ConfigurazioneApplicazione.Carica(percorso).BpmIniziale,Is.EqualTo(137.5));
                c.Motore.CambiaModo();c.Esecutore.ElaboraPendenti();
                typeof(FormControllo).GetMethod("AggiornaInterfaccia",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,null);
                Assert.That(bpm.Enabled,Is.False);
                var bottone=f.Controls.Find("pulsanteRileggiFoglio",true).Single();int larghezza=bottone.Width;
                var iniziale=f.ClientSize;f.ClientSize=new Size(iniziale.Width*2,iniziale.Height*2);
                Assert.That(bottone.Width,Is.EqualTo(larghezza*2).Within(2));
                var combo=(ComboBox)f.Controls.Find("comboOsservazione",true).Single();Assert.That(combo.Items.Cast<string>(),Is.EqualTo(new[]{"1 cella in anticipo","2 celle in anticipo","4 celle in anticipo","8 celle in anticipo","16 celle in anticipo"}));
                combo.SelectedIndex=4;
                Assert.That(configApp.MisureAnticipoScansione,Is.EqualTo(16));
                Assert.That(c.Analizzatore.MisureAnticipoScansione,Is.EqualTo(16));
                Assert.That(c.Analizzatore.PulsazioniPerOsservazione,Is.EqualTo(2),"Il menu dell’anticipo preserva la cadenza configurata.");
                var modalita=(ComboBox)f.Controls.Find("comboModalitaLettura",true).Single();
                modalita.SelectedIndex=3;
                Assert.That(configApp.ModalitaLettura,Is.EqualTo("CompletaPeriodica"));
                Assert.That(c.Analizzatore.LetturaCompletaPeriodica,Is.True);
                Assert.That(c.Analizzatore.PeriodoLetturaCompleta,Is.EqualTo(4));
                typeof(FormControllo).GetMethod("AggiornaStatoMidi",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,null);
                Assert.That(combo.Enabled,Is.False,"Con lettura completa l'anticipo progressivo non serve.");
                typeof(FormControllo).GetMethod("SalvaDaTimer",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,null);
                var salvata=ConfigurazioneApplicazione.Carica(percorso);
                Assert.That(salvata.MisureAnticipoScansione,Is.EqualTo(16));
                Assert.That(salvata.ModalitaLettura,Is.EqualTo("CompletaPeriodica"));
                Assert.That(salvata.PeriodoLetturaCompleta,Is.EqualTo(4));
            }
            }
            finally
            {
                if(File.Exists(percorso))File.Delete(percorso);
            }
        }
    }
}
