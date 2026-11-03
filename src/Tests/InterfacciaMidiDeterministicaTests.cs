using System;
using System.Collections.Generic;
using System.Drawing;
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
            var cfg=new ConfigGriglia();var fonte=new RilevatoreCommutabile();var configApp=new ConfigurazioneApplicazione();
            using(var c=new Compositore(fonte,new EsecutorePartituraTests.Uscita(),null,new EsecutorePartituraTests.Mappa(),cfg,
                orologioMs:()=>0,esecuzioneDeterministica:true))
            using(var f=new FormControllo(c,new FormProiettore(cfg),new GestoreWebcam(i=>null),fonte,
                "calibrazione-test.txt",configApp,null,new List<InfoWebcam>(),()=>0,"MIDI",new List<string>(),new List<IDisposable>(),false))
            {
                Assert.That(f.Controls.Find("etichettaVersioniFoglio",true),Has.Length.EqualTo(1));
                Assert.That(f.Controls.Find("pulsanteSalvaTempiMidi",true),Has.Length.EqualTo(1));
                var bottone=f.Controls.Find("pulsanteRileggiFoglio",true).Single();int larghezza=bottone.Width;
                var iniziale=f.ClientSize;f.ClientSize=new Size(iniziale.Width*2,iniziale.Height*2);
                Assert.That(bottone.Width,Is.EqualTo(larghezza*2).Within(2));
                var combo=(ComboBox)f.Controls.Find("comboOsservazione",true).Single();combo.SelectedIndex=2;
                Assert.That(configApp.PulsazioniPerOsservazione,Is.EqualTo(2));
            }
        }
    }
}
