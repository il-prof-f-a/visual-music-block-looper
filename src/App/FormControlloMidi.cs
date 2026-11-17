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
        private NumericUpDown campoBpm;
        private bool aggiornamentoBpm;
        private readonly PresentatoreScansione presentatoreScansione=new PresentatoreScansione();

        private void AggiungiControlliMidi()
        {
            if(!compositore.EsecuzioneDeterministica)return;
            pulsanteTap.Width=75;
            var labelBpm=new Label{Text="BPM",Left=630,Top=194,Width=115,Height=18};
            campoBpm=new NumericUpDown{Name="campoBpm",AccessibleName="BPM",Left=630,Top=214,Width=115,Height=24,
                Minimum=(decimal)Tempo.MinBpm,Maximum=(decimal)Tempo.MaxBpm,DecimalPlaces=1,Increment=1,Value=(decimal)compositore.Motore.Bpm,TabIndex=4};
            campoBpm.ValueChanged+=(m,a)=>
            {
                if(aggiornamentoBpm)return;
                double bpm=Tempo.Limita((double)campoBpm.Value);
                configurazione.BpmIniziale=bpm;
                compositore.Motore.ImpostaBpm(bpm);
                Riavvia(timerSalvaConfigurazione);
            };
            campoBpm.KeyDown+=(m,a)=>{if(a.KeyCode==Keys.Enter){SelectNextControl(campoBpm,true,true,true,true);a.Handled=true;a.SuppressKeyPress=true;}};
            suggerimentiDebug.SetToolTip(campoBpm,"Tempo Master: scrivi il valore e premi Invio. Durante Play cambia alla pulsazione successiva; in Slave segue il clock esterno.");
            Controls.AddRange(new Control[]{labelBpm,campoBpm});
            ClientSize=new Size(760,825);
            var rileggi=new Button {Name="pulsanteRileggiFoglio",Text="Rileggi foglio",Left=10,Top=714,Width=180,Height=28,TabIndex=32};
            rileggi.Click+=(m,a)=>compositore.RileggiFoglio();
            pulsanteSalvaTempiMidi=new Button {Name="pulsanteSalvaTempiMidi",Text="Salva tempi MIDI",Left=200,Top=714,Width=145,Height=28,TabIndex=33};
            pulsanteSalvaTempiMidi.Click+=async(m,a)=>await SalvaTempiMidi();
            var combo=new ComboBox {Name="comboOsservazione",AccessibleName="Misure in anticipo per analisi",Left=360,Top=716,Width=385,DropDownStyle=ComboBoxStyle.DropDownList,TabIndex=34};
            combo.Items.AddRange(new object[]{"1 misura in anticipo","2 misure in anticipo","4 misure in anticipo","8 misure in anticipo","16 misure in anticipo"});
            if(compositore.Analizzatore!=null)compositore.Analizzatore.PulsazioniPerOsservazione=configurazione.PulsazioniPerOsservazione;
            int[] anticipi={1,2,4,8,16};
            int indice=Array.IndexOf(anticipi,configurazione.MisureAnticipoScansione);
            combo.SelectedIndex=indice<0?2:indice;
            if(compositore.Analizzatore!=null)compositore.Analizzatore.MisureAnticipoScansione=anticipi[combo.SelectedIndex];
            combo.SelectedIndexChanged+=(m,a)=>
            {
                configurazione.MisureAnticipoScansione=anticipi[combo.SelectedIndex];
                if(compositore.Analizzatore!=null)compositore.Analizzatore.MisureAnticipoScansione=anticipi[combo.SelectedIndex];
                AggiornaProiezioneMidi();
                Riavvia(timerSalvaConfigurazione);
            };
            etichettaVersioniFoglio=new Label {Name="etichettaVersioniFoglio",Left=10,Top=750,Width=735,Height=70,AutoEllipsis=true,ForeColor=Color.DarkBlue};
            suggerimentiDebug.SetToolTip(rileggi,"Forza una nuova scansione completa del foglio. Durante l'esecuzione la nuova partitura entrerà alla prossima frontiera del giro.");
            suggerimentiDebug.SetToolTip(combo,"Scegli 1, 2, 4, 8 o 16 misure in anticipo anche durante Play. La linea arancio salta subito alla destinazione; la lettura riparte sul primo frame nuovo senza leggere le misure intermedie. Azzurro: ultima lettura confermata.");
            Controls.AddRange(new Control[]{rileggi,pulsanteSalvaTempiMidi,combo,etichettaVersioniFoglio});
        }
        private void AggiornaStatoMidi()
        {
            var e=compositore.Esecutore;if(e==null||etichettaVersioniFoglio==null)return;
            campoBpm.Enabled=!inCalibrazione&&e.Modo==ModoSincronia.Master;
            if(e.Modo==ModoSincronia.Master)
            {
                double bpmRichiesto=Tempo.Limita(e.Stato.BpmRichiesto);
                if(Math.Abs(configurazione.BpmIniziale-bpmRichiesto)>0.0001)
                {
                    configurazione.BpmIniziale=bpmRichiesto;
                    Riavvia(timerSalvaConfigurazione);
                }
            }
            if(!campoBpm.ContainsFocus)
            {
                aggiornamentoBpm=true;
                try{campoBpm.Value=(decimal)Tempo.Limita(e.Modo==ModoSincronia.Master?e.Stato.BpmRichiesto:e.Bpm);}
                finally{aggiornamentoBpm=false;}
            }
            long osservata=compositore.Preparatore.Foglio.Ultima?.Versione??0;
            string errore=tickMaster.UltimoErrore??e.UltimoErrore??compositore.Preparatore.UltimoErrore??cicloCattura.UltimoErrore??preparatoreAnteprima?.UltimoErrore;
            string testo=$"Foglio letto v{osservata} | pronto v{e.Stato.VersionePronta} | in uso v{e.Stato.VersioneAttiva}"
                +$" | letture scena/zone: {compositore.Analizzatore?.NumeroLetture??0}";
            testo+=Environment.NewLine+(errore??messaggioTempi??compositore.Analizzatore?.UltimoEsito??"Preparazione del foglio");
            var analisi=compositore.Analizzatore;
            if(analisi?.IndicatoriScansioneAttivi==true)
            {
                var scansione=analisi.Scansione;var letta=scansione.UltimaCompletata;
                testo+=Environment.NewLine+"Arancio: scansione prevista +"+analisi.MisureAnticipoScansione+" misure | Azzurro: ";
                testo+=letta==null?scansione.Esito:
                    $"letta m{letta.MisuraAssoluta+1}, frame {letta.FrameId}, {letta.FineMs-letta.InizioMs} ms (età frame {Math.Max(0,orologioMs()-letta.IstanteFrameMs)} ms) | {scansione.Esito}";
            }
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
            var trasporto=e.Stato.Trasporto;
            var attiva=trasporto.Partitura;
            var posizione=attiva?.PosizioneAlTick(trasporto.TickNelGiro)??e.Posizione;
            var analisi=compositore.Analizzatore;
            proiettore.AggiornaIndicatori(presentatoreScansione.Leggi(trasporto,analisi?.Scansione,
                analisi?.MisureAnticipoScansione??4,orologioMs(),analisi?.IndicatoriScansioneAttivi==true));
            var osservato=compositore.UltimoStato;
            if(!ReferenceEquals(attiva,partituraDisegnata)||!ReferenceEquals(osservato,osservatoDisegnato))
            {
                var baseAttiva=attiva?.CopiaStato()??new StatoTavolo(null,null,null);
                var stato=new StatoTavolo(baseAttiva.Note,null,osservato.Errori,baseAttiva.Struttura);
                proiettore.Aggiorna(stato,posizione,trasporto.InEsecuzione);
                var attesa=osservato.Note.Where(n=>!baseAttiva.Note.Any(a=>a.Chiave==n.Chiave&&a.DurataStep==n.DurataStep)).ToArray();
                proiettore.MostraInAttesa(new StatoTavolo(attesa,null,null,osservato.Struttura));
                partituraDisegnata=attiva;osservatoDisegnato=osservato;
            }
            else proiettore.AggiornaPosizione(posizione,trasporto.InEsecuzione);
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
