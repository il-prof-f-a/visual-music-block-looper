using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.App
{
    public partial class FormControllo
    {
        private readonly Timer timerProiezione=new Timer {Interval=33};
        private Label etichettaVersioniFoglio;
        private Button pulsanteSalvaTempiMidi;
        private PartituraMidi partituraDisegnata;
        private StatoTavolo osservatoDisegnato;
        private bool salvataggioTempi;
        private string messaggioTempi;

        private void AggiungiControlliMidi()
        {
            if(!compositore.EsecuzioneDeterministica)return;
            ClientSize=new Size(760,800);
            var rileggi=new Button {Name="pulsanteRileggiFoglio",Text="Rileggi foglio",Left=10,Top=714,Width=180,Height=28,TabIndex=32};
            rileggi.Click+=(m,a)=>compositore.RileggiFoglio();
            pulsanteSalvaTempiMidi=new Button {Name="pulsanteSalvaTempiMidi",Text="Salva tempi MIDI",Left=200,Top=714,Width=145,Height=28,TabIndex=33};
            pulsanteSalvaTempiMidi.Click+=async(m,a)=>await SalvaTempiMidi();
            var combo=new ComboBox {Name="comboOsservazione",AccessibleName="Cadenza lettura foglio",Left=360,Top=716,Width=385,DropDownStyle=ComboBoxStyle.DropDownList,TabIndex=34};
            combo.Items.AddRange(new object[]{"Lettura: ogni misura","Lettura: ogni pulsazione","Lettura: ogni 2 pulsazioni","Lettura: ogni 4 pulsazioni"});
            int[] cadenze={0,1,2,4};
            int indice=Array.IndexOf(cadenze,configurazione.PulsazioniPerOsservazione);
            combo.SelectedIndex=Math.Max(0,indice);
            if(compositore.Analizzatore!=null)compositore.Analizzatore.PulsazioniPerOsservazione=cadenze[combo.SelectedIndex];
            combo.SelectedIndexChanged+=(m,a)=>
            {
                configurazione.PulsazioniPerOsservazione=cadenze[combo.SelectedIndex];
                if(compositore.Analizzatore!=null)compositore.Analizzatore.PulsazioniPerOsservazione=cadenze[combo.SelectedIndex];
                Riavvia(timerSalvaConfigurazione);
            };
            etichettaVersioniFoglio=new Label {Name="etichettaVersioniFoglio",Left=10,Top=750,Width=735,Height=46,AutoEllipsis=true,ForeColor=Color.DarkBlue};
            suggerimentiDebug.SetToolTip(rileggi,"Rilegge il foglio e richiede conferma. Durante l'esecuzione le modifiche entreranno a una frontiera del giro.");
            Controls.AddRange(new Control[]{rileggi,pulsanteSalvaTempiMidi,combo,etichettaVersioniFoglio});
        }
        private void AggiornaStatoMidi()
        {
            var e=compositore.Esecutore;if(e==null||etichettaVersioniFoglio==null)return;
            long osservata=compositore.Preparatore.Foglio.Ultima?.Versione??0;
            string errore=tickMaster.UltimoErrore??e.UltimoErrore??compositore.Preparatore.UltimoErrore??cicloCattura.UltimoErrore;
            string testo=$"Foglio letto v{osservata} | pronto v{e.Stato.VersionePronta} | in uso v{e.Stato.VersioneAttiva}"
                +$" | letture zone: {compositore.Analizzatore?.NumeroLettureZone??0}";
            testo+=Environment.NewLine+(errore??messaggioTempi??compositore.Analizzatore?.UltimoEsito??"Preparazione del foglio");
            etichettaVersioniFoglio.Text=testo;
            etichettaVersioniFoglio.ForeColor=errore==null?Color.DarkBlue:Color.DarkRed;
            suggerimentiDebug.SetToolTip(etichettaVersioniFoglio,testo);
            pulsanteSalvaTempiMidi.Enabled=!salvataggioTempi;
        }
        private void AggiornaProiezioneMidi()
        {
            if(chiusuraEseguita||inCalibrazione)return;
            var e=compositore.Esecutore;
            if(e==null)
            {
                proiettore.AggiornaPosizione(compositore.Motore.Posizione,compositore.Motore.InEsecuzione);return;
            }
            var attiva=e.Stato.PartituraAttiva;
            var osservato=compositore.UltimoStato;
            if(!ReferenceEquals(attiva,partituraDisegnata)||!ReferenceEquals(osservato,osservatoDisegnato))
            {
                var baseAttiva=attiva?.CopiaStato()??new StatoTavolo(null,null,null);
                var stato=new StatoTavolo(baseAttiva.Note,null,osservato.Errori,baseAttiva.Struttura);
                proiettore.Aggiorna(stato,e.Posizione,e.InEsecuzione);
                var attesa=osservato.Note.Where(n=>!baseAttiva.Note.Any(a=>a.Chiave==n.Chiave&&a.DurataStep==n.DurataStep)).ToArray();
                proiettore.MostraInAttesa(new StatoTavolo(attesa,null,null,osservato.Struttura));
                partituraDisegnata=attiva;osservatoDisegnato=osservato;
            }
            else proiettore.AggiornaPosizione(e.Posizione,e.InEsecuzione);
        }
        private async Task SalvaTempiMidi()
        {
            if(salvataggioTempi)return;salvataggioTempi=true;AggiornaStatoMidi();
            long osservata=compositore.Preparatore.Foglio.Ultima?.Versione??0,attiva=compositore.Esecutore.Stato.VersioneAttiva;
            bool mmcss=tickMaster.MmcssAttiva;
            string errore=tickMaster.UltimoErrore??compositore.Esecutore.UltimoErrore;
            try
            {
                string file=await Task.Run(()=>EsportatoreTempiMidi.Salva(tickMaster.RegistroTempi,AppDomain.CurrentDomain.BaseDirectory,mmcss,errore,osservata,attiva));
                if(chiusuraEseguita||IsDisposed)return;
                messaggioTempi="Salvato: "+Path.GetFileName(file);etichettaVersioniFoglio.AccessibleDescription=file;
            }
            catch(Exception ex){if(!chiusuraEseguita)messaggioTempi="Salvataggio tempi fallito: "+ex.Message;}
            finally{salvataggioTempi=false;if(!chiusuraEseguita&&!IsDisposed)AggiornaStatoMidi();}
        }
    }
}
