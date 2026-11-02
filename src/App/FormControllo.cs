using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    /// <summary>
    /// Finestra di controllo dell'operatore: stato del motore, anteprima webcam, calibrazione manuale
    /// a 12 click, comandi di trasporto e scelta del monitor del proiettore. Contiene solo UI:
    /// la logica resta nel <see cref="Compositore"/> e nel motore.
    /// </summary>
    public class FormControllo : Form
    {
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
        private int? puntoDaCorreggere;
        private int PuntoCalibrazioneCorrente => puntoDaCorreggere ?? puntiCalibrazione.Count;
        private Dictionary<Control, Rectangle> limitiIniziali;
        private Dictionary<Control, Font> fontIniziali;
        private Dictionary<Font, Font> fontRidimensionati = new Dictionary<Font, Font>();
        private Size dimensioniLayout;
        private float scalaFontApplicata = 1;
        private bool ridimensionamentoInCorso;
        private readonly TickMaster tickMaster;
        private readonly CicloCattura cicloCattura;
        private readonly Timer timerInterfaccia = new Timer { Interval = 150 };

        // Debounce dei regoli della webcam: risoluzione al rilascio (500 ms), fuoco/esposizione dal vivo (150 ms)
        // e salvataggio nel file di configurazione (500 ms).
        private readonly Timer timerRisoluzione = new Timer { Interval = 500 };
        private readonly Timer timerApplicaImpostazioni = new Timer { Interval = 150 };
        private readonly Timer timerSalvaConfigurazione = new Timer { Interval = 500 };
        private volatile int ultimoStepProiettato = -1;

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
        private Button pulsanteSalvaFrame;
        private CheckBox casellaDebugCartoncini;
        private Button pulsanteSalvaDebug;
        private Label etichettaDebugCartoncini;
        private readonly ToolTip suggerimentiDebug = new ToolTip();
        private bool salvataggioDebug;
        private string messaggioDebug;
        private bool erroreDebug;
        private bool aggiornamentoComboWebcam;
        private Label etichettaRisoluzione;
        private Label etichettaFuoco;
        private Label etichettaEsposizione;
        private Label etichettaFps;
        private TrackBar regoloRisoluzione;
        private TrackBar regoloFuoco;
        private TrackBar regoloEsposizione;
        private CheckBox casellaFuocoAuto;
        private CheckBox casellaEsposizioneAuto;
        private bool aggiornamentoRegoli;
        private bool fuocoSupportato;
        private bool esposizioneSupportata;
        private double fpsDichiarati;
        private ConfigurazioneWebcam impostazioniInSospeso;
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

            tickMaster = new TickMaster(compositore.Motore, orologioMs, AggiornaProiezioneSeCambiaStep);
            cicloCattura = new CicloCattura(PassoCattura);

            CostruisciInterfaccia();
            PopolaElencoWebcam(elencoWebcam ?? new List<InfoWebcam>());
            timerInterfaccia.Tick += (mittente, argomenti) => AggiornaInterfaccia();
            timerRisoluzione.Tick += (mittente, argomenti) => ApplicaRisoluzione();
            timerApplicaImpostazioni.Tick += (mittente, argomenti) => ApplicaImpostazioniInSospeso();
            timerSalvaConfigurazione.Tick += (mittente, argomenti) => SalvaDaTimer();

            // Una sola lettura (con sonda) della webcam all'avvio: il timer da 150 ms non la ripete mai.
            AggiornaStatoControlli();
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
                timerRisoluzione.Dispose();
                timerApplicaImpostazioni.Dispose();
                timerSalvaConfigurazione.Dispose();
                suggerimentiDebug.Dispose();
            }

            base.Dispose(disposing);
            if (disposing)
            {
                foreach (Font font in fontRidimensionati.Values) font.Dispose();
                fontRidimensionati.Clear();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RidimensionaInterfaccia();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (inCalibrazione && keyData == Keys.Escape)
            {
                TerminaCalibrazione("Calibrazione: annullata; calibrazione precedente conservata.");
                AggiornaInterfaccia();
                return true;
            }
            if (inCalibrazione && keyData == Keys.Back)
            {
                if (puntoDaCorreggere.HasValue) puntoDaCorreggere = Math.Max(0, puntoDaCorreggere.Value - 1);
                else if (puntiCalibrazione.Count > 0) puntiCalibrazione.RemoveAt(puntiCalibrazione.Count - 1);
                proiettore.MostraCalibrazione(PuntoCalibrazioneCorrente);
                anteprima.Invalidate();
                AggiornaInterfaccia();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void RidimensionaInterfaccia()
        {
            if (limitiIniziali == null || ridimensionamentoInCorso || Disposing || WindowState == FormWindowState.Minimized
                || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            double scalaX = (double)ClientSize.Width / dimensioniLayout.Width;
            double scalaY = (double)ClientSize.Height / dimensioniLayout.Height;
            float scalaFont = (float)Math.Min(scalaX, scalaY);
            ridimensionamentoInCorso = true;
            SuspendLayout();
            try
            {
                if (Math.Abs(scalaFont - scalaFontApplicata) > 0.001f)
                {
                    var nuoviFont = new Dictionary<Font, Font>();
                    foreach (var coppia in fontIniziali)
                    {
                        Font originale = coppia.Value;
                        if (Math.Abs(scalaFont - 1) < 0.001f) coppia.Key.Font = originale;
                        else
                        {
                            if (!nuoviFont.TryGetValue(originale, out Font font))
                            {
                                font = new Font(originale.FontFamily, originale.SizeInPoints * scalaFont,
                                    originale.Style, GraphicsUnit.Point);
                                nuoviFont.Add(originale, font);
                            }
                            coppia.Key.Font = font;
                        }
                    }
                    foreach (Font precedente in fontRidimensionati.Values) precedente.Dispose();
                    fontRidimensionati = nuoviFont;
                    scalaFontApplicata = scalaFont;
                }

                // Ripartire sempre dai limiti iniziali evita errori di arrotondamento cumulativi.
                foreach (var coppia in limitiIniziali)
                {
                    Rectangle area = coppia.Value;
                    coppia.Key.Bounds = Rectangle.FromLTRB(
                        (int)Math.Round(area.Left * scalaX), (int)Math.Round(area.Top * scalaY),
                        (int)Math.Round(area.Right * scalaX), (int)Math.Round(area.Bottom * scalaY));
                }
                anteprima.Invalidate();
            }
            finally
            {
                ResumeLayout(false);
                ridimensionamentoInCorso = false;
            }
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
            timerRisoluzione.Stop();
            timerApplicaImpostazioni.Stop();

            // Un salvataggio ancora in attesa del debounce non va perso alla chiusura.
            bool salvataggioInSospeso = timerSalvaConfigurazione.Enabled;
            timerSalvaConfigurazione.Stop();
            if (salvataggioInSospeso)
            {
                SalvaConfigurazione();
            }

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
            // Layout iniziale (760 x 716), usato come riferimento per il ridimensionamento:
            //  - fascia alta x 10..750: stato (y 10), porte (y 38);
            //  - anteprima x 10..530, y 64..454; sotto di lei, larghe come lei (520) e a capo, la descrizione
            //    dell'anteprima (y 458..510) e gli avvisi (y 514..664), così non toccano la colonna destra;
            //  - colonna destra x 545..745 (larghezza 200): pulsanti fino a «Salva frame» (y 394..422), poi
            //    Risoluzione (etichetta y 428, regolo y 446..476), Messa a fuoco (etichetta y 484,
            //    «Auto» + regolo y 504..534), Esposizione (etichetta y 542, «Auto» + regolo y 562..592)
            //    e l'etichetta dei fps (y 598..662, va a capo). Diagnostica nella fascia y 674..708.
            ClientSize = new Size(760, 716);
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
            pulsanteSalvaFrame = new Button { Text = "Salva frame", Left = 545, Top = 394, Width = 200, Height = 28 };
            pulsanteSalvaFrame.Click += (m, a) => SalvaFrame();

            etichettaAnteprima = new Label
            {
                Left = 10, Top = 458, Width = 520, Height = 52, ForeColor = Color.DarkBlue,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold), Text = AnteprimaTavolo.TestoGrezza
            };

            etichettaAvviso = new Label
            {
                Left = 10, Top = 514, Width = 520, Height = 150, ForeColor = Color.DarkRed
            };

            etichettaRisoluzione = new Label { Left = 545, Top = 428, Width = 200, Height = 18 };
            regoloRisoluzione = CreaRegolo(545, 446, 200, 0, PresetRisoluzione.Elenco.Count - 1, 1, 1);
            regoloRisoluzione.Scroll += OnScrollRisoluzione;

            etichettaFuoco = new Label { Left = 545, Top = 484, Width = 200, Height = 18 };
            casellaFuocoAuto = new CheckBox { Text = "Auto", Left = 545, Top = 507, Width = 58, Height = 22 };
            regoloFuoco = CreaRegolo(605, 504, 140, 0, PresentatoreRegoliWebcam.RegoloFuocoMax, 1, 5);
            casellaFuocoAuto.CheckedChanged += OnCambioFuoco;
            regoloFuoco.Scroll += OnCambioFuoco;
            regoloFuoco.MouseUp += (m, a) => RilasciaRegolo();

            etichettaEsposizione = new Label { Left = 545, Top = 542, Width = 200, Height = 18 };
            casellaEsposizioneAuto = new CheckBox { Text = "Auto", Left = 545, Top = 565, Width = 58, Height = 22 };
            regoloEsposizione = CreaRegolo(
                605, 562, 140, PresentatoreRegoliWebcam.RegoloEsposizioneMin, PresentatoreRegoliWebcam.RegoloEsposizioneMax, 1, 2);
            casellaEsposizioneAuto.CheckedChanged += OnCambioEsposizione;
            regoloEsposizione.Scroll += OnCambioEsposizione;
            regoloEsposizione.MouseUp += (m, a) => RilasciaRegolo();

            etichettaFps = new Label { Left = 545, Top = 598, Width = 200, Height = 64 };

            casellaDebugCartoncini = new CheckBox
            {
                Name = "casellaDebugCartoncini", Text = "Debug cartoncini", AccessibleName = "Debug cartoncini",
                Left = 10, Top = 676, Width = 180, Height = 24, TabIndex = 30
            };
            casellaDebugCartoncini.CheckedChanged += (m, a) =>
            {
                compositore.RegistroDiagnostica.Attivo = casellaDebugCartoncini.Checked;
                messaggioDebug = null; erroreDebug = false;
                etichettaDebugCartoncini.AccessibleDescription = null;
                suggerimentiDebug.SetToolTip(etichettaDebugCartoncini, null);
                AggiornaDebugCartoncini();
            };
            pulsanteSalvaDebug = new Button
            {
                Name = "pulsanteSalvaDebug", Text = "Salva debug", Left = 200, Top = 674,
                Width = 135, Height = 28, TabIndex = 31, Enabled = false
            };
            pulsanteSalvaDebug.Click += async (m, a) => await SalvaDebugCartoncini();
            etichettaDebugCartoncini = new Label
            {
                Name = "etichettaDebugCartoncini", Left = 350, Top = 674, Width = 395, Height = 34,
                AutoEllipsis = true
            };
            suggerimentiDebug.SetToolTip(casellaDebugCartoncini,
                "Conserva le ultime 50 catture. Disattiva per congelare; Salva debug esporta il rapporto JSON.");

            Controls.AddRange(new Control[]
            {
                etichettaStato, etichettaPorte, anteprima, pulsanteCalibra, pulsanteAvviaFerma,
                pulsanteModo, pulsanteTap, etichettaMonitor, comboMonitor,
                etichettaWebcam, comboWebcam, pulsanteAggiornaWebcam, pulsanteSalvaFrame, etichettaAnteprima, etichettaAvviso,
                etichettaRisoluzione, regoloRisoluzione, etichettaFuoco, casellaFuocoAuto, regoloFuoco,
                etichettaEsposizione, casellaEsposizioneAuto, regoloEsposizione, etichettaFps,
                casellaDebugCartoncini, pulsanteSalvaDebug, etichettaDebugCartoncini
            });

            dimensioniLayout = ClientSize;
            limitiIniziali = Controls.Cast<Control>().ToDictionary(c => c, c => c.Bounds);
            fontIniziali = Controls.Cast<Control>().ToDictionary(c => c, c => c.Font);
            MinimumSize = Size;
            AggiornaInterfaccia();
        }

        private static TrackBar CreaRegolo(int sinistra, int alto, int larghezza, int minimo, int massimo, int piccolo, int grande)
        {
            return new TrackBar
            {
                AutoSize = false, Left = sinistra, Top = alto, Width = larghezza, Height = 30,
                Minimum = minimo, Maximum = massimo, SmallChange = piccolo, LargeChange = grande,
                TickStyle = TickStyle.BottomRight, TickFrequency = Math.Max(1, (massimo - minimo) / 10)
            };
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
            if (inCalibrazione) return;
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
                CatturaInCorso cattura = compositore.RegistroDiagnostica.IniziaCattura();
                string errore = null;
                bool frameDisponibile = false;
                int larghezza = webcam.Larghezza, altezza = webcam.Altezza;
                try
                {
                    using (var frame = webcam?.Frame())
                    {
                        frameDisponibile = frame != null && !frame.Empty();
                        if (frameDisponibile) { larghezza = frame.Cols; altezza = frame.Rows; }
                    }
                }
                catch (Exception e)
                {
                    if (cattura != null) errore = e.GetType().Name + ": " + e.Message;
                    throw;
                }
                finally
                {
                    compositore.RegistraSenzaCalibrazione(cattura, webcam.Disponibile, frameDisponibile, larghezza, altezza, errore);
                }
            }

            MotoreMusicale motore = compositore.Motore;
            proiettore.Aggiorna(compositore.UltimoStato, motore.Posizione, motore.InEsecuzione);
        }

        /// <summary>Sul thread dei tick: ridisegna il proiettore appena cambia lo step, senza attendere il frame webcam.</summary>
        private void AggiornaProiezioneSeCambiaStep()
        {
            MotoreMusicale motore = compositore.Motore;
            PosizioneGiro posizione = motore.Posizione;
            int chiave = posizione.MisuraNelGiro * 1000 + posizione.StepInMisura;
            if (chiave == ultimoStepProiettato)
            {
                return;
            }

            ultimoStepProiettato = chiave;
            proiettore.Aggiorna(compositore.UltimoStato, posizione, motore.InEsecuzione);
        }

        private void AggiornaInterfaccia()
        {
            AggiornaAnteprima();
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
                righe.Add($"Calibrazione: punto {PuntoCalibrazioneCorrente + 1}/12 — "
                    + SchemaCalibrazione.Descrizioni[PuntoCalibrazioneCorrente] + ".\n"
                    + "Clicca il centro nero del bersaglio cerchiato in blu. "
                    + "Backspace: punto precedente; Esc: annulla; Calibra: ricomincia.");
            }

            etichettaAvviso.Text = string.Join(Environment.NewLine, righe);
            etichettaFps.Text = PresentatoreRegoliWebcam.TestoFps(
                webcam.FpsCatturati, fpsDichiarati, cicloCattura.PassiAlSecondo, webcam.Larghezza, webcam.Altezza);
            AggiornaDebugCartoncini();
        }

        private void AggiornaDebugCartoncini()
        {
            RegistroCatture registro = compositore.RegistroDiagnostica;
            pulsanteSalvaDebug.Enabled = !salvataggioDebug && registro.NumeroCatture > 0;
            etichettaDebugCartoncini.ForeColor = erroreDebug ? Color.DarkRed : Color.DarkBlue;
            etichettaDebugCartoncini.Text = $"Debug {(registro.Attivo ? "attivo" : "fermo")}: {registro.NumeroCatture}/50"
                + (string.IsNullOrEmpty(messaggioDebug) ? string.Empty : Environment.NewLine + messaggioDebug);
        }

        private async Task SalvaDebugCartoncini()
        {
            if (salvataggioDebug) return;
            RapportoDiagnosticaCartoncini rapporto = compositore.RegistroDiagnostica.Istantanea();
            if (rapporto.Catture.Count == 0) return;
            salvataggioDebug = true; messaggioDebug = "Salvataggio in corso…"; erroreDebug = false;
            AggiornaDebugCartoncini();
            try
            {
                string percorso = await Task.Run(() => EsportatoreDiagnosticaCartoncini.Salva(
                    rapporto, AppDomain.CurrentDomain.BaseDirectory));
                if (IsDisposed || Disposing || chiusuraEseguita) return;
                messaggioDebug = "Salvato: " + Path.GetFileName(percorso);
                etichettaDebugCartoncini.AccessibleDescription = percorso;
                suggerimentiDebug.SetToolTip(etichettaDebugCartoncini, percorso);
            }
            catch (Exception e)
            {
                if (IsDisposed || Disposing || chiusuraEseguita) return;
                erroreDebug = true; messaggioDebug = "Salvataggio non riuscito: " + e.Message;
                suggerimentiDebug.SetToolTip(etichettaDebugCartoncini, messaggioDebug);
            }
            finally
            {
                salvataggioDebug = false;
                if (!IsDisposed && !Disposing && !chiusuraEseguita) AggiornaDebugCartoncini();
            }
        }

        private bool AggiornaAnteprima()
        {
            // Con una calibrazione attiva l'anteprima mostra l'area del tavolo raddrizzata (stirata);
            // durante la calibrazione serve invece l'immagine originale per cliccare i dodici bersagli.
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
                return false;
            }

            if (inCalibrazione && !dimensioniFrame.IsEmpty && nuova.Size != dimensioniFrame)
            {
                AnnullaCalibrazionePerRisoluzione();
            }

            Image vecchia = anteprima.Image;
            anteprima.Image = nuova;
            vecchia?.Dispose();
            immagineRaddrizzata = raddrizza;
            etichettaAnteprima.Text = raddrizza ? AnteprimaTavolo.TestoRaddrizzata : AnteprimaTavolo.TestoGrezza;
            return true;
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

            timerRisoluzione.Stop();
            aggiornamentoRegoli = true;
            try
            {
                regoloRisoluzione.Value = EntroLimiti(regoloRisoluzione,
                    PresetRisoluzione.IndiceVicino(webcam.Larghezza, webcam.Altezza));
            }
            finally { aggiornamentoRegoli = false; }
            AggiornaTestiRegoli();
            avvisi.RemoveAll(a => a.StartsWith("Calibrazione: "));
            dimensioniFrame = Size.Empty;
            inCalibrazione = true;
            puntiCalibrazione.Clear();
            puntoDaCorreggere = null;
            bool immagineDisponibile = AggiornaAnteprima();
            if (!immagineDisponibile || anteprima.Image == null || immagineRaddrizzata)
            {
                TerminaCalibrazione("Calibrazione: immagine originale non disponibile. Attendi un frame e riprova.");
                AggiornaInterfaccia();
                return;
            }
            dimensioniFrame = anteprima.Image.Size;
            ImpostaControlliCalibrazione(true);
            proiettore.MostraCalibrazione(0);
            anteprima.Invalidate();
            AggiornaInterfaccia();
        }

        private void ImpostaControlliCalibrazione(bool raccolta)
        {
            comboWebcam.Enabled = !raccolta;
            comboMonitor.Enabled = !raccolta;
            pulsanteAggiornaWebcam.Enabled = !raccolta;
            regoloRisoluzione.Enabled = !raccolta && webcam.Disponibile;
        }

        private void TerminaCalibrazione(string messaggio)
        {
            inCalibrazione = false;
            puntiCalibrazione.Clear();
            puntoDaCorreggere = null;
            dimensioniFrame = Size.Empty;
            proiettore.MostraCalibrazione(null);
            ImpostaControlliCalibrazione(false);
            avvisi.RemoveAll(a => a.StartsWith("Calibrazione: "));
            if (!string.IsNullOrEmpty(messaggio)) avvisi.Add(messaggio);
            anteprima.Invalidate();
        }

        private void AnnullaCalibrazionePerRisoluzione()
        {
            rilevatore.Imposta(null);
            calibrazionePresente = false;
            TerminaCalibrazione("Calibrazione: risoluzione del frame cambiata, raccolta annullata. Premi Calibra per ripetere.");
        }

        private void OnClickAnteprima(object mittente, MouseEventArgs argomenti)
        {
            // Si clicca solo sull'immagine originale: i punti sono pixel della webcam, non dell'area raddrizzata.
            if (!inCalibrazione || anteprima.Image == null || immagineRaddrizzata)
            {
                return;
            }

            // Il frame può essere cambiato dall'ultimo tick UI: non usare una bitmap di vecchia geometria.
            if (!AggiornaAnteprima())
            {
                TerminaCalibrazione("Calibrazione: immagine originale non disponibile, raccolta annullata. Riprova.");
                AggiornaInterfaccia();
                return;
            }
            if (!inCalibrazione)
            {
                AggiornaInterfaccia();
                return;
            }
            if (anteprima.Image.Size != dimensioniFrame)
            {
                AnnullaCalibrazionePerRisoluzione();
                AggiornaInterfaccia();
                return;
            }

            PuntoImmagine? punto = ConversioneAnteprima.DaPictureBoxAImmagine(
                argomenti.Location, anteprima.ClientSize, anteprima.Image.Size);
            if (punto == null)
            {
                return;
            }

            if (puntoDaCorreggere.HasValue) puntiCalibrazione[puntoDaCorreggere.Value] = punto.Value;
            else puntiCalibrazione.Add(punto.Value);
            puntoDaCorreggere = null;
            anteprima.Invalidate();

            if (puntiCalibrazione.Count == SchemaCalibrazione.Punti.Count)
            {
                CompletaCalibrazione();
            }
            else proiettore.MostraCalibrazione(PuntoCalibrazioneCorrente);

            AggiornaInterfaccia();
        }

        private void CompletaCalibrazione()
        {
            string messaggio;
            try
            {
                // L'omografia è in pixel: si memorizza la risoluzione del frame su cui sono stati cliccati i punti.
                Calibrazione calibrazione = SchemaCalibrazione.Calcola(
                    puntiCalibrazione.ToArray(), dimensioniFrame.Width, dimensioniFrame.Height, out double scartoMedio);
                // Ricontrollare il frame grezzo prima di applicare: le dimensioni dichiarate dal driver
                // possono restare quelle dell'apertura anche se il frame consegnato è cambiato.
                using (Bitmap corrente = webcam.Anteprima())
                {
                    if (corrente == null)
                    {
                        TerminaCalibrazione("Calibrazione: immagine originale non disponibile, raccolta annullata. Riprova.");
                        return;
                    }
                    if (corrente.Size != dimensioniFrame)
                    {
                        AnnullaCalibrazionePerRisoluzione();
                        return;
                    }
                }
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
                messaggio = $"Calibrazione: completata con 12 punti. Scarto medio {scartoMedio * 100:0.00}% del tavolo.";
            }
            catch (PuntiCalibrazioneIncoerentiException eccezione)
            {
                puntoDaCorreggere = eccezione.Numeri[0] - 1;
                avvisi.RemoveAll(a => a.StartsWith("Calibrazione: "));
                avvisi.Add("Calibrazione: punti incoerenti: " + string.Join(", ", eccezione.Numeri)
                    + ". Ripeti il riferimento evidenziato; gli altri punti sono conservati.");
                proiettore.MostraCalibrazione(PuntoCalibrazioneCorrente);
                anteprima.Invalidate();
                return;
            }
            catch (Exception eccezione)
            {
                messaggio = "Calibrazione: non riuscita. " + eccezione.Message;
            }

            TerminaCalibrazione(messaggio);
        }

        /// <summary>Dopo una calibrazione riuscita salva nel file di configurazione la webcam in uso.</summary>
        private void SalvaWebcamInConfigurazione()
        {
            InfoWebcam inUso = webcam.Info;
            if (string.IsNullOrEmpty(percorsoConfigurazione) || inUso == null)
            {
                return;
            }

            // Si salva l'identità (nome e percorso), non solo la posizione: al prossimo avvio
            // l'ordine delle webcam può essere diverso.
            configurazione.Webcam.Imposta(inUso);
            SalvaConfigurazione();
        }

        private const string PrefissoAvvisoConfigurazione = "Configurazione non salvata su disco: ";

        /// <summary>Scrive il file di configurazione; un errore di scrittura diventa un avviso e non fa crollare la finestra.</summary>
        private void SalvaConfigurazione()
        {
            if (string.IsNullOrEmpty(percorsoConfigurazione))
            {
                return;
            }

            avvisi.RemoveAll(a => a.StartsWith(PrefissoAvvisoConfigurazione));
            try
            {
                configurazione.Salva(percorsoConfigurazione);
            }
            catch (Exception eccezione)
            {
                avvisi.Add(PrefissoAvvisoConfigurazione + eccezione.Message);
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

        /// <summary>Salva l'ultimo frame grezzo della webcam accanto all'eseguibile, per analizzare i QR non letti.</summary>
        private void SalvaFrame()
        {
            using (Bitmap frame = webcam.Anteprima())
            {
                if (frame == null)
                {
                    MessageBox.Show(this, "Nessun frame disponibile: la webcam non è attiva.", Text);
                    return;
                }

                string percorso = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"frame-{DateTime.Now:yyyyMMdd-HHmmss}.png");
                frame.Save(percorso, System.Drawing.Imaging.ImageFormat.Png);
                MessageBox.Show(this, $"Frame {frame.Width}x{frame.Height} salvato in: {percorso}", Text);
            }
        }

        private void AggiornaElencoWebcam()
        {
            if (inCalibrazione) return;
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
            if (inCalibrazione || aggiornamentoComboWebcam || !(comboWebcam.SelectedItem is InfoWebcam scelta))
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

            RicaricaCalibrazione(scelta);
            AggiornaStatoControlli();
            AggiornaInterfaccia();
        }

        /// <summary>
        /// La calibrazione è legata alla webcam e alla sua risoluzione: la si ricarica solo se quella
        /// salvata è stata fatta con questa stessa webcam (avvisi di risoluzione compresi).
        /// </summary>
        private void RicaricaCalibrazione(InfoWebcam inUso)
        {
            rilevatore.Imposta(null);
            avvisi.RemoveAll(a => a == ApplicazioneCalibrazione.AvvisoRisoluzioneDiversa
                || a == ApplicazioneCalibrazione.AvvisoRisoluzioneSconosciuta);
            calibrazionePresente = inUso != null
                && configurazione.Webcam.Corrisponde(inUso)
                && ApplicazioneCalibrazione.TentaCaricamento(percorsoCalibrazione, webcam, rilevatore, avvisi);
        }

        /// <summary>
        /// Rilegge UNA volta lo stato della webcam (la lettura imposta proprietà del driver: va chiamata solo
        /// all'avvio e dopo un cambio di webcam o di risoluzione, mai dal timer) e riposiziona i controlli:
        /// valori dal file di configurazione se presenti, altrimenti quelli letti. Senza webcam, o per le
        /// impostazioni che il driver non accetta, i controlli sono disabilitati.
        /// </summary>
        private void AggiornaStatoControlli()
        {
            bool conWebcam = webcam.Disponibile;
            StatoImpostazioniWebcam stato = StatoImpostazioniWebcam.NonSupportato();
            if (conWebcam)
            {
                try
                {
                    stato = webcam.LeggiImpostazioni() ?? stato;
                }
                catch (Exception)
                {
                    // Lettura fallita: si tratta come webcam senza impostazioni.
                }
            }

            fpsDichiarati = stato.FpsDichiarati;
            fuocoSupportato = conWebcam && stato.FuocoSupportato;
            esposizioneSupportata = conWebcam && stato.EsposizioneSupportata;
            ConfigurazioneWebcam config = configurazione.Webcam;

            aggiornamentoRegoli = true;
            try
            {
                regoloRisoluzione.Value = EntroLimiti(regoloRisoluzione, PresentatoreRegoliWebcam.RegoloRisoluzione(
                    config.Larghezza, config.Altezza, webcam.Larghezza, webcam.Altezza));
                casellaFuocoAuto.Checked = PresentatoreRegoliWebcam.FuocoAutomaticoIniziale(config, stato);
                regoloFuoco.Value = EntroLimiti(regoloFuoco, PresentatoreRegoliWebcam.RegoloFuocoIniziale(config, stato));
                casellaEsposizioneAuto.Checked = PresentatoreRegoliWebcam.EsposizioneAutomaticaIniziale(config, stato);
                regoloEsposizione.Value = EntroLimiti(
                    regoloEsposizione, PresentatoreRegoliWebcam.RegoloEsposizioneIniziale(config, stato));
            }
            finally
            {
                aggiornamentoRegoli = false;
            }

            regoloRisoluzione.Enabled = conWebcam && !inCalibrazione;
            AggiornaTestiRegoli();
        }

        private static int EntroLimiti(TrackBar regolo, int valore)
        {
            return Math.Min(regolo.Maximum, Math.Max(regolo.Minimum, valore));
        }

        /// <summary>Aggiorna le etichette e abilita/disabilita caselle «Auto» e regoli di fuoco ed esposizione.</summary>
        private void AggiornaTestiRegoli()
        {
            etichettaRisoluzione.Text = PresentatoreRegoliWebcam.EtichettaRisoluzione(regoloRisoluzione.Value);
            etichettaFuoco.Text = PresentatoreRegoliWebcam.EtichettaFuoco(
                fuocoSupportato, casellaFuocoAuto.Checked, regoloFuoco.Value);
            casellaFuocoAuto.Enabled = fuocoSupportato;
            regoloFuoco.Enabled = fuocoSupportato && !casellaFuocoAuto.Checked;
            etichettaEsposizione.Text = PresentatoreRegoliWebcam.EtichettaEsposizione(
                esposizioneSupportata, casellaEsposizioneAuto.Checked, regoloEsposizione.Value);
            casellaEsposizioneAuto.Enabled = esposizioneSupportata;
            regoloEsposizione.Enabled = esposizioneSupportata && !casellaEsposizioneAuto.Checked;
        }

        private static void Riavvia(Timer timer)
        {
            timer.Stop();
            timer.Start();
        }

        private void OnScrollRisoluzione(object mittente, EventArgs argomenti)
        {
            if (aggiornamentoRegoli || inCalibrazione)
            {
                return;
            }

            etichettaRisoluzione.Text = PresentatoreRegoliWebcam.EtichettaRisoluzione(regoloRisoluzione.Value);
            Riavvia(timerRisoluzione);
        }

        /// <summary>
        /// Dopo il debounce del regolo: riapre la webcam alla nuova risoluzione, la salva nel file di
        /// configurazione e ricarica la calibrazione con le regole di un cambio webcam.
        /// </summary>
        private void ApplicaRisoluzione()
        {
            timerRisoluzione.Stop();
            if (inCalibrazione || !webcam.Disponibile)
            {
                return;
            }

            int indice = regoloRisoluzione.Value;
            (int larghezza, int altezza) = PresentatoreRegoliWebcam.RisoluzioneDaRegolo(indice);
            avvisi.RemoveAll(a => a.StartsWith(PrefissoAvvisoWebcam));

            if (larghezza != webcam.Larghezza || altezza != webcam.Altezza)
            {
                if (!webcam.CambiaRisoluzione(larghezza, altezza))
                {
                    avvisi.Add(PrefissoAvvisoWebcam + "non riaperta a " + PresetRisoluzione.Etichetta(indice)
                        + ": resta la risoluzione precedente.");
                    AggiornaStatoControlli();
                    if (webcam.Disponibile)
                    {
                        // Il regolo torna sul preset della risoluzione realmente in uso.
                        aggiornamentoRegoli = true;
                        try
                        {
                            regoloRisoluzione.Value = EntroLimiti(
                                regoloRisoluzione, PresetRisoluzione.IndiceVicino(webcam.Larghezza, webcam.Altezza));
                        }
                        finally
                        {
                            aggiornamentoRegoli = false;
                        }

                        AggiornaTestiRegoli();
                    }

                    AggiornaInterfaccia();
                    return;
                }

                RicaricaCalibrazione(webcam.Info);
            }

            PresentatoreRegoliWebcam.Unisci(configurazione.Webcam, PresentatoreRegoliWebcam.ParzialeRisoluzione(indice));
            SalvaConfigurazione();
            AggiornaStatoControlli();
            AggiornaInterfaccia();
        }

        private void OnCambioFuoco(object mittente, EventArgs argomenti)
        {
            if (aggiornamentoRegoli)
            {
                return;
            }

            AggiornaTestiRegoli();
            ConfigurazioneWebcam parziale = PresentatoreRegoliWebcam.ParzialeFuoco(casellaFuocoAuto.Checked, regoloFuoco.Value);
            PresentatoreRegoliWebcam.Unisci(configurazione.Webcam, parziale);
            ConfigurazioneWebcam sospeso = InSospeso();
            sospeso.FuocoAutomatico = parziale.FuocoAutomatico;
            sospeso.Fuoco = parziale.Fuoco;
            Riavvia(timerApplicaImpostazioni);
            Riavvia(timerSalvaConfigurazione);
        }

        private void OnCambioEsposizione(object mittente, EventArgs argomenti)
        {
            if (aggiornamentoRegoli)
            {
                return;
            }

            AggiornaTestiRegoli();
            ConfigurazioneWebcam parziale = PresentatoreRegoliWebcam.ParzialeEsposizione(
                casellaEsposizioneAuto.Checked, regoloEsposizione.Value);
            PresentatoreRegoliWebcam.Unisci(configurazione.Webcam, parziale);
            ConfigurazioneWebcam sospeso = InSospeso();
            sospeso.EsposizioneAutomatica = parziale.EsposizioneAutomatica;
            sospeso.Esposizione = parziale.Esposizione;
            Riavvia(timerApplicaImpostazioni);
            Riavvia(timerSalvaConfigurazione);
        }

        /// <summary>Configurazione parziale in attesa del debounce (solo i campi cambiati; gli altri restano null).</summary>
        private ConfigurazioneWebcam InSospeso()
        {
            return impostazioniInSospeso ?? (impostazioniInSospeso = new ConfigurazioneWebcam());
        }

        /// <summary>Dopo il debounce: invia alla webcam i soli campi cambiati. Non rilegge la webcam.</summary>
        private void ApplicaImpostazioniInSospeso()
        {
            timerApplicaImpostazioni.Stop();
            ConfigurazioneWebcam daApplicare = impostazioniInSospeso;
            impostazioniInSospeso = null;
            if (daApplicare == null || !webcam.Disponibile)
            {
                return;
            }

            try
            {
                webcam.ApplicaImpostazioni(daApplicare);
            }
            catch (Exception eccezione)
            {
                avvisi.RemoveAll(a => a.StartsWith(PrefissoAvvisoWebcam));
                avvisi.Add(PrefissoAvvisoWebcam + "impostazioni non applicate: " + eccezione.Message);
            }
        }

        private void SalvaDaTimer()
        {
            timerSalvaConfigurazione.Stop();
            SalvaConfigurazione();
        }

        /// <summary>Al rilascio del mouse su un regolo: applica e salva subito, senza attendere il debounce.</summary>
        private void RilasciaRegolo()
        {
            if (impostazioniInSospeso == null && !timerSalvaConfigurazione.Enabled)
            {
                return;
            }

            ApplicaImpostazioniInSospeso();
            SalvaDaTimer();
        }

        private const string PrefissoAvvisoWebcam = "Webcam ";

        private void OnPaintAnteprima(object mittente, PaintEventArgs argomenti)
        {
            if (anteprima.Image == null || puntiCalibrazione.Count == 0) return;
            double scala = Math.Min((double)anteprima.ClientSize.Width / anteprima.Image.Width,
                (double)anteprima.ClientSize.Height / anteprima.Image.Height);
            double offsetX = (anteprima.ClientSize.Width - anteprima.Image.Width * scala) / 2;
            double offsetY = (anteprima.ClientSize.Height - anteprima.Image.Height * scala) / 2;
            float raggio = 5 * scalaFontApplicata;
            for (int i = 0; i < puntiCalibrazione.Count; i++)
            {
                PuntoImmagine p = puntiCalibrazione[i];
                float x = (float)(offsetX + p.X * scala), y = (float)(offsetY + p.Y * scala);
                argomenti.Graphics.DrawEllipse(puntoDaCorreggere == i ? Pens.Red : Pens.Lime,
                    x - raggio, y - raggio, 2 * raggio, 2 * raggio);
            }
        }
    }
}
