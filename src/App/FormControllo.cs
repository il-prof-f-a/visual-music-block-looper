using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    /// <summary>
    /// Finestra di controllo dell'operatore: stato del motore, anteprima webcam, calibrazione manuale
    /// a 4 click, comandi di trasporto e scelta del monitor del proiettore. Contiene solo UI:
    /// la logica resta nel <see cref="Compositore"/> e nel motore.
    /// </summary>
    public class FormControllo : Form
    {
        private static readonly string[] NomiAngoli =
        {
            "alto-sinistra", "alto-destra", "basso-destra", "basso-sinistra"
        };

        private static readonly PuntoTavolo[] AngoliTavolo =
        {
            new PuntoTavolo(0, 0), new PuntoTavolo(1, 0), new PuntoTavolo(1, 1), new PuntoTavolo(0, 1)
        };

        private readonly Compositore compositore;
        private readonly FormProiettore proiettore;
        private readonly GestoreWebcam webcam;
        private readonly RilevatoreCommutabile rilevatore;
        private readonly string percorsoCalibrazione;
        private readonly ConfigurazioneApplicazione configurazione;
        private readonly string percorsoConfigurazione;
        private readonly Func<long> orologioMs;
        private readonly List<IDisposable> risorse;
        private readonly string testoPorte;
        private readonly List<string> avvisi;
        private readonly List<PuntoImmagine> puntiCalibrazione = new List<PuntoImmagine>();
        private readonly List<Point> puntiSchermo = new List<Point>();
        private readonly TickMaster tickMaster;
        private readonly CicloCattura cicloCattura;
        private readonly Timer timerInterfaccia = new Timer { Interval = 150 };

        private Label etichettaStato;
        private Label etichettaPorte;
        private Label etichettaAvviso;
        private Label etichettaAnteprima;
        private bool immagineRaddrizzata;
        private PictureBox anteprima;
        private Button pulsanteCalibra;
        private Button pulsanteAvviaFerma;
        private Button pulsanteModo;
        private Button pulsanteTap;
        private ComboBox comboMonitor;
        private ComboBox comboWebcam;
        private Button pulsanteAggiornaWebcam;
        private bool aggiornamentoComboWebcam;
        private Screen[] schermi = new Screen[0];
        private bool inCalibrazione;
        private bool calibrazionePresente;
        private bool chiusuraEseguita;
        private Size dimensioniFrame;

        /// <summary>Crea la finestra. Thread e ciclo di cattura partono solo quando la finestra viene mostrata.</summary>
        /// <param name="webcam">Gestore della webcam (può non avere una webcam aperta: modalità simulazione).</param>
        /// <param name="configurazione">Configurazione dell'applicazione; riscritta dopo ogni calibrazione riuscita.</param>
        /// <param name="percorsoConfigurazione">File in cui salvare la configurazione.</param>
        /// <param name="elencoWebcam">Webcam rilevate all'avvio, mostrate nella lista.</param>
        /// <param name="calibrazionePresente">True se un rilevatore calibrato è già stato impostato.</param>
        /// <param name="risorse">Oggetti da rilasciare alla chiusura (porte MIDI, ...).</param>
        public FormControllo(
            Compositore compositore,
            FormProiettore proiettore,
            GestoreWebcam webcam,
            RilevatoreCommutabile rilevatore,
            string percorsoCalibrazione,
            ConfigurazioneApplicazione configurazione,
            string percorsoConfigurazione,
            IReadOnlyList<InfoWebcam> elencoWebcam,
            Func<long> orologioMs,
            string testoPorte,
            IEnumerable<string> avvisi,
            IEnumerable<IDisposable> risorse,
            bool calibrazionePresente)
        {
            this.compositore = compositore ?? throw new ArgumentNullException(nameof(compositore));
            this.proiettore = proiettore ?? throw new ArgumentNullException(nameof(proiettore));
            this.rilevatore = rilevatore ?? throw new ArgumentNullException(nameof(rilevatore));
            this.orologioMs = orologioMs ?? throw new ArgumentNullException(nameof(orologioMs));
            this.webcam = webcam ?? throw new ArgumentNullException(nameof(webcam));
            this.percorsoCalibrazione = percorsoCalibrazione;
            this.configurazione = configurazione ?? new ConfigurazioneApplicazione();
            this.percorsoConfigurazione = percorsoConfigurazione;
            this.testoPorte = testoPorte ?? string.Empty;
            this.avvisi = (avvisi ?? Enumerable.Empty<string>()).ToList();
            this.risorse = (risorse ?? Enumerable.Empty<IDisposable>()).ToList();
            this.calibrazionePresente = calibrazionePresente;

            tickMaster = new TickMaster(compositore.Motore, orologioMs);
            cicloCattura = new CicloCattura(PassoCattura);

            CostruisciInterfaccia();
            PopolaElencoWebcam(elencoWebcam ?? new List<InfoWebcam>());
            timerInterfaccia.Tick += (mittente, argomenti) => AggiornaInterfaccia();
        }

        /// <summary>Avvia thread di clock, ciclo di cattura e aggiornamento dell'interfaccia.</summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            tickMaster.Avvia();
            cicloCattura.Avvia();
            timerInterfaccia.Start();
            SelezionaMonitorIniziale();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            Chiudi();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Chiudi();
                timerInterfaccia.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>Ferma i thread e rilascia tutte le risorse una sola volta.</summary>
        private void Chiudi()
        {
            if (chiusuraEseguita)
            {
                return;
            }

            chiusuraEseguita = true;
            timerInterfaccia.Stop();
            cicloCattura.Ferma();
            tickMaster.Ferma();

            try
            {
                // Invia Stop al DAW (Master), poi spegne comunque note e one-shot in qualsiasi
                // modo, prima di chiudere le porte.
                compositore.Motore.Arresta();
                compositore.Motore.Silenzia();
            }
            catch (Exception)
            {
                // La porta MIDI potrebbe essere già non disponibile.
            }

            proiettore.Dispose();
            webcam.Dispose();
            foreach (IDisposable risorsa in risorse)
            {
                try
                {
                    risorsa.Dispose();
                }
                catch (Exception)
                {
                    // Si continua a rilasciare le altre risorse.
                }
            }
        }

        private void CostruisciInterfaccia()
        {
            Text = "Visual Music Block Looper - Controllo";
            ClientSize = new Size(760, 580);
            StartPosition = FormStartPosition.CenterScreen;

            etichettaStato = new Label
            {
                Left = 10, Top = 10, Width = 740, Height = 24, Font = new Font(Font.FontFamily, 11f, FontStyle.Bold)
            };
            etichettaPorte = new Label { Left = 10, Top = 38, Width = 740, Height = 20, Text = testoPorte };

            anteprima = new PictureBox
            {
                Left = 10, Top = 64, Width = 520, Height = 390,
                SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black, BorderStyle = BorderStyle.FixedSingle
            };
            anteprima.MouseClick += OnClickAnteprima;
            anteprima.Paint += OnPaintAnteprima;

            pulsanteCalibra = CreaPulsante("Calibra", 545, 64, (m, a) => AvviaCalibrazione());
            pulsanteAvviaFerma = CreaPulsante("Avvia", 545, 108, (m, a) => AvviaFerma());
            pulsanteModo = CreaPulsante("Master / Slave", 545, 152, (m, a) => compositore.Motore.CambiaModo());
            pulsanteTap = CreaPulsante("Tap", 545, 196, (m, a) => compositore.Motore.Tap(orologioMs()));

            Label etichettaMonitor = new Label { Left = 545, Top = 250, Width = 200, Height = 18, Text = "Monitor proiettore" };
            comboMonitor = new ComboBox { Left = 545, Top = 272, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            schermi = Screen.AllScreens;
            for (int i = 0; i < schermi.Length; i++)
            {
                Rectangle limiti = schermi[i].Bounds;
                string primario = schermi[i].Primary ? " (principale)" : string.Empty;
                comboMonitor.Items.Add($"Schermo {i + 1}: {limiti.Width}x{limiti.Height}{primario}");
            }

            comboMonitor.SelectedIndexChanged += OnMonitorSelezionato;

            Label etichettaWebcam = new Label { Left = 545, Top = 308, Width = 200, Height = 18, Text = "Webcam" };
            comboWebcam = new ComboBox { Left = 545, Top = 330, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            comboWebcam.SelectedIndexChanged += OnWebcamSelezionata;
            pulsanteAggiornaWebcam = new Button { Text = "Aggiorna elenco", Left = 545, Top = 360, Width = 200, Height = 28 };
            pulsanteAggiornaWebcam.Click += (m, a) => AggiornaElencoWebcam();

            etichettaAnteprima = new Label
            {
                Left = 10, Top = 458, Width = 740, Height = 40, ForeColor = Color.DarkBlue,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold), Text = AnteprimaTavolo.TestoGrezza
            };

            etichettaAvviso = new Label
            {
                Left = 10, Top = 502, Width = 740, Height = 70, ForeColor = Color.DarkRed
            };

            Controls.AddRange(new Control[]
            {
                etichettaStato, etichettaPorte, anteprima, pulsanteCalibra, pulsanteAvviaFerma,
                pulsanteModo, pulsanteTap, etichettaMonitor, comboMonitor,
                etichettaWebcam, comboWebcam, pulsanteAggiornaWebcam, etichettaAnteprima, etichettaAvviso
            });

            AggiornaInterfaccia();
        }

        private static Button CreaPulsante(string testo, int sinistra, int alto, EventHandler gestore)
        {
            Button pulsante = new Button { Text = testo, Left = sinistra, Top = alto, Width = 200, Height = 38 };
            pulsante.Click += gestore;
            return pulsante;
        }

        private void AvviaFerma()
        {
            if (compositore.Motore.InEsecuzione)
            {
                compositore.Motore.Arresta();
            }
            else
            {
                compositore.Motore.Avvia();
            }
        }

        private void SelezionaMonitorIniziale()
        {
            // Con più monitor il proiettore va sul primo non principale; con uno solo non si copre la finestra di controllo.
            int indice = Array.FindIndex(schermi, s => !s.Primary);
            if (indice >= 0)
            {
                comboMonitor.SelectedIndex = indice;
            }
        }

        private void OnMonitorSelezionato(object mittente, EventArgs argomenti)
        {
            int indice = comboMonitor.SelectedIndex;
            if (indice >= 0 && indice < schermi.Length)
            {
                proiettore.MostraSuSchermo(schermi[indice]);
            }
        }

        /// <summary>Un passo del ciclo di cattura (thread in background): visione, motore e proiezione.</summary>
        private void PassoCattura()
        {
            if (rilevatore.Attivo)
            {
                compositore.Passo();
            }
            else
            {
                // Senza calibrazione il rilevatore non gira, ma l'anteprima deve restare viva.
                using (webcam?.Frame())
                {
                }
            }

            MotoreMusicale motore = compositore.Motore;
            proiettore.Aggiorna(compositore.UltimoStato, motore.StepCorrente, motore.InEsecuzione);
        }

        private void AggiornaInterfaccia()
        {
            MotoreMusicale motore = compositore.Motore;
            string modo = motore.Modo == ModoSincronia.Master ? "Master" : "Slave";
            string stato = motore.InEsecuzione ? "in esecuzione" : "fermo";
            etichettaStato.Text = $"BPM {motore.Bpm:0.#}   |   {modo}   |   {stato}";
            pulsanteAvviaFerma.Text = motore.InEsecuzione ? "Ferma" : "Avvia";
            pulsanteTap.Enabled = motore.Modo == ModoSincronia.Master;

            List<string> righe = new List<string>(avvisi);
            if (!webcam.Disponibile)
            {
                righe.Add("Webcam non trovata: modalità simulazione.");
            }
            else if (!calibrazionePresente)
            {
                righe.Add("Calibrazione mancante: premi Calibra.");
            }

            if (inCalibrazione)
            {
                righe.Add($"Calibrazione: clicca l'angolo {puntiCalibrazione.Count + 1}/4 ({NomiAngoli[puntiCalibrazione.Count]}) dell'area bianca proiettata.");
            }

            etichettaAvviso.Text = string.Join(Environment.NewLine, righe);
            AggiornaAnteprima();
        }

        private void AggiornaAnteprima()
        {
            // Con una calibrazione attiva l'anteprima mostra l'area del tavolo raddrizzata (stirata);
            // durante la calibrazione serve invece l'immagine originale per cliccare i quattro angoli.
            Calibrazione calibrazioneAttiva = rilevatore.CalibrazioneAttiva;
            bool raddrizza = AnteprimaTavolo.Raddrizzata(calibrazioneAttiva != null, inCalibrazione);

            Bitmap nuova;
            if (raddrizza)
            {
                Size dimensioni = AnteprimaTavolo.Dimensioni(
                    AspettoDelTavolo(), anteprima.ClientSize.Width, anteprima.ClientSize.Height);
                nuova = webcam.AnteprimaRaddrizzata(calibrazioneAttiva, dimensioni.Width, dimensioni.Height);
            }
            else
            {
                nuova = webcam.Anteprima();
            }

            if (nuova == null)
            {
                return;
            }

            Image vecchia = anteprima.Image;
            anteprima.Image = nuova;
            vecchia?.Dispose();
            immagineRaddrizzata = raddrizza;
            etichettaAnteprima.Text = raddrizza ? AnteprimaTavolo.TestoRaddrizzata : AnteprimaTavolo.TestoGrezza;
        }

        /// <summary>Rapporto larghezza/altezza del tavolo: quello dello schermo del proiettore, se scelto.</summary>
        private double AspettoDelTavolo()
        {
            int indice = comboMonitor.SelectedIndex;
            if (indice >= 0 && indice < schermi.Length && schermi[indice].Bounds.Height > 0)
            {
                return (double)schermi[indice].Bounds.Width / schermi[indice].Bounds.Height;
            }

            return AnteprimaTavolo.AspettoPredefinito;
        }

        private void AvviaCalibrazione()
        {
            if (!webcam.Disponibile)
            {
                MessageBox.Show(this, "Webcam non disponibile: impossibile calibrare.", Text);
                return;
            }

            inCalibrazione = true;
            puntiCalibrazione.Clear();
            puntiSchermo.Clear();
            anteprima.Invalidate();
            AggiornaInterfaccia();
        }

        private void OnClickAnteprima(object mittente, MouseEventArgs argomenti)
        {
            // Si clicca solo sull'immagine originale: i punti sono pixel della webcam, non dell'area raddrizzata.
            if (!inCalibrazione || anteprima.Image == null || immagineRaddrizzata)
            {
                return;
            }

            PuntoImmagine? punto = ConversioneAnteprima.DaPictureBoxAImmagine(
                argomenti.Location, anteprima.ClientSize, anteprima.Image.Size);
            if (punto == null)
            {
                return;
            }

            dimensioniFrame = anteprima.Image.Size;
            puntiCalibrazione.Add(punto.Value);
            puntiSchermo.Add(argomenti.Location);
            anteprima.Invalidate();

            if (puntiCalibrazione.Count == AngoliTavolo.Length)
            {
                CompletaCalibrazione();
            }

            AggiornaInterfaccia();
        }

        private void CompletaCalibrazione()
        {
            inCalibrazione = false;
            try
            {
                // L'omografia è in pixel: si memorizza la risoluzione del frame su cui sono stati cliccati i punti.
                Calibrazione calibrazione = Calibrazione.Calcola(
                    puntiCalibrazione.ToArray(), AngoliTavolo, dimensioniFrame.Width, dimensioniFrame.Height);
                string avvisoSalvataggio = ApplicazioneCalibrazione.Applica(
                    rilevatore, webcam, calibrazione, c => c.Salva(percorsoCalibrazione));
                calibrazionePresente = true;
                avvisi.RemoveAll(a => a == ApplicazioneCalibrazione.AvvisoRisoluzioneDiversa
                    || a == ApplicazioneCalibrazione.AvvisoRisoluzioneSconosciuta);
                if (avvisoSalvataggio != null)
                {
                    avvisi.Add(avvisoSalvataggio);
                }

                SalvaWebcamInConfigurazione();
            }
            catch (Exception eccezione)
            {
                MessageBox.Show(this, "Calibrazione non riuscita: " + eccezione.Message, Text);
            }

            puntiCalibrazione.Clear();
            puntiSchermo.Clear();
            anteprima.Invalidate();
        }

        /// <summary>Dopo una calibrazione riuscita salva nel file di configurazione la webcam in uso.</summary>
        private void SalvaWebcamInConfigurazione()
        {
            InfoWebcam inUso = webcam.Info;
            if (string.IsNullOrEmpty(percorsoConfigurazione) || inUso == null)
            {
                return;
            }

            try
            {
                // Si salva l'identità (nome e percorso), non solo la posizione: al prossimo avvio
                // l'ordine delle webcam può essere diverso.
                configurazione.Webcam.Imposta(inUso);
                configurazione.Salva(percorsoConfigurazione);
            }
            catch (Exception eccezione)
            {
                avvisi.Add("Configurazione non salvata su disco: " + eccezione.Message);
            }
        }

        private void PopolaElencoWebcam(IReadOnlyList<InfoWebcam> elenco)
        {
            aggiornamentoComboWebcam = true;
            try
            {
                comboWebcam.Items.Clear();
                foreach (InfoWebcam info in elenco)
                {
                    comboWebcam.Items.Add(info);
                }

                comboWebcam.SelectedItem = TrovaInElenco(elenco, webcam.Info);
            }
            finally
            {
                aggiornamentoComboWebcam = false;
            }
        }

        private void AggiornaElencoWebcam()
        {
            PopolaElencoWebcam(ElencoWebcam.RilevaReali(webcam.IndiceCorrente));
        }

        /// <summary>Trova nell'elenco la webcam aperta, per identità (nome/percorso) e non per posizione.</summary>
        private static InfoWebcam TrovaInElenco(IReadOnlyList<InfoWebcam> elenco, InfoWebcam inUso)
        {
            if (inUso == null)
            {
                return null;
            }

            ConfigurazioneWebcam identita = new ConfigurazioneWebcam();
            identita.Imposta(inUso);
            return AbbinamentoWebcam.Trova(elenco, identita);
        }

        private void OnWebcamSelezionata(object mittente, EventArgs argomenti)
        {
            if (aggiornamentoComboWebcam || !(comboWebcam.SelectedItem is InfoWebcam scelta))
            {
                return;
            }

            if (scelta.Indice == webcam.IndiceCorrente)
            {
                return;
            }

            avvisi.RemoveAll(a => a.StartsWith(PrefissoAvvisoWebcam));
            if (!webcam.Apri(scelta))
            {
                avvisi.Add(PrefissoAvvisoWebcam + "«" + scelta.Descrizione + "» non apribile: resta attiva la precedente.");
                aggiornamentoComboWebcam = true;
                try
                {
                    comboWebcam.SelectedItem = TrovaInElenco(comboWebcam.Items.Cast<InfoWebcam>().ToList(), webcam.Info);
                }
                finally
                {
                    aggiornamentoComboWebcam = false;
                }

                return;
            }

            // La calibrazione è legata alla webcam e alla sua risoluzione: la si ricarica solo se quella
            // salvata è stata fatta con questa stessa webcam.
            rilevatore.Imposta(null);
            avvisi.RemoveAll(a => a == ApplicazioneCalibrazione.AvvisoRisoluzioneDiversa
                || a == ApplicazioneCalibrazione.AvvisoRisoluzioneSconosciuta);
            calibrazionePresente = configurazione.Webcam.Corrisponde(scelta)
                && ApplicazioneCalibrazione.TentaCaricamento(percorsoCalibrazione, webcam, rilevatore, avvisi);
            AggiornaInterfaccia();
        }

        private const string PrefissoAvvisoWebcam = "Webcam ";

        private void OnPaintAnteprima(object mittente, PaintEventArgs argomenti)
        {
            foreach (Point p in puntiSchermo)
            {
                argomenti.Graphics.DrawEllipse(Pens.Lime, p.X - 5, p.Y - 5, 10, 10);
            }
        }
    }
}
