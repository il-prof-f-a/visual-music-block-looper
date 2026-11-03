using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
