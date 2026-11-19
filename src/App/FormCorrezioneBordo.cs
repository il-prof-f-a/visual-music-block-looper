using System;
using System.Drawing;
using System.Windows.Forms;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    /// <summary>
    /// Regolazione dal vivo della correzione di bordo. I regoli applicano subito i parametri
    /// alla calibrazione attiva; «Salva» li scrive nella configurazione per il prossimo avvio.
    /// </summary>
    public sealed class FormCorrezioneBordo : Form
    {
        private readonly Action<ParametriCorrezioneBordo> applica;
        private readonly Action<ParametriCorrezioneBordo> salva;
        private readonly Timer timer = new Timer { Interval = 90 };
        private readonly ComboBox comboModello;
        private readonly TrackBar regoloPieno, regoloEsterno, regoloFattoreX, regoloFattoreY, regoloSmussamento;
        private readonly Label valorePieno, valoreEsterno, valoreFattoreX, valoreFattoreY, valoreSmussamento;
        private ParametriCorrezioneBordo iniziali;
        private bool salvato;
        private bool aggiornamento;

        public FormCorrezioneBordo(ParametriCorrezioneBordo iniziali,
            Action<ParametriCorrezioneBordo> applica, Action<ParametriCorrezioneBordo> salva)
        {
            this.iniziali = (iniziali ?? new ParametriCorrezioneBordo()).Clona().Normalizzati();
            this.applica = applica ?? throw new ArgumentNullException(nameof(applica));
            this.salva = salva ?? throw new ArgumentNullException(nameof(salva));

            Text = "Correzione bordo";
            ClientSize = new Size(430, 390);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Font = new Font(Font.FontFamily, 9f);
            BackColor = SystemColors.Control;

            Label etichettaModello = new Label { Left = 15, Top = 15, Width = 110, Height = 18, Text = "Modello" };
            comboModello = new ComboBox
            {
                Left = 130, Top = 12, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList
            };
            comboModello.Items.Add("Bordo (smoothstep)");
            comboModello.Items.Add("Bordo smussato (softmin)");
            comboModello.SelectedIndexChanged += (m, a) => CambioModello();

            regoloPieno = CreaRegolo("Pieno", 55, 30, 100, out valorePieno);
            regoloEsterno = CreaRegolo("Esterno", 95, 100, 350, out valoreEsterno);
            regoloPieno.ValueChanged += (m, a) => AggiornaMinimoEsterno();
            regoloFattoreX = CreaRegolo("Fattore X", 135, 50, 200, out valoreFattoreX);
            regoloFattoreY = CreaRegolo("Fattore Y", 175, 50, 200, out valoreFattoreY);
            regoloSmussamento = CreaRegolo("Smussamento", 215, 0, 60, out valoreSmussamento);

            Label descrizione = new Label
            {
                Left = 15, Top = 255, Width = 400, Height = 44, ForeColor = Color.DarkSlateGray,
                Text = "Le modifiche sono applicate subito all'anteprima. «Salva» le scrive in "
                    + "configurazione.json per il prossimo avvio; «Annulla» ripristina i valori iniziali."
            };

            Button pulsanteSalva = new Button { Text = "Salva", Left = 130, Top = 320, Width = 90, Height = 30 };
            pulsanteSalva.Click += (m, a) => Salva();
            Button pulsanteAnnulla = new Button { Text = "Annulla", Left = 230, Top = 320, Width = 90, Height = 30 };
            pulsanteAnnulla.Click += (m, a) => Annulla();

            Controls.Add(etichettaModello);
            Controls.Add(comboModello);
            Controls.Add(descrizione);
            Controls.Add(pulsanteSalva);
            Controls.Add(pulsanteAnnulla);

            timer.Tick += (m, a) => ApplicaDalVivo();
            FormClosing += (m, a) => { if (!salvato) Ripristina(); };
            Shown += (m, a) => PosizionaVicinoAllaFinestra();
            Imposta(this.iniziali);
        }

        private TrackBar CreaRegolo(string testo, int top, int minimo, int massimo, out Label valore)
        {
            Label etichetta = new Label { Left = 15, Top = top, Width = 110, Height = 18, Text = testo };
            valore = new Label { Left = 340, Top = top, Width = 75, Height = 18, TextAlign = ContentAlignment.MiddleRight };
            TrackBar regolo = new TrackBar
            {
                Left = 130, Top = top - 6, Width = 200, Height = 32,
                Minimum = minimo, Maximum = massimo, TickStyle = TickStyle.None,
                SmallChange = 1, LargeChange = 5
            };
            regolo.ValueChanged += (m, a) => Programma();
            Controls.Add(etichetta);
            Controls.Add(regolo);
            Controls.Add(valore);
            return regolo;
        }

        private void Imposta(ParametriCorrezioneBordo p)
        {
            aggiornamento = true;
            try
            {
                ParametriCorrezioneBordo n = (p ?? new ParametriCorrezioneBordo()).Clona().Normalizzati();
                comboModello.SelectedIndex = n.Modello == ModelloCorrezioneBordo.BordoSmussato ? 1 : 0;
                regoloPieno.Value = (int)Math.Round(n.Pieno * 1000);
                regoloEsterno.Minimum = Math.Min(regoloEsterno.Maximum, regoloPieno.Value + 10);
                regoloEsterno.Value = Math.Max(regoloEsterno.Minimum, (int)Math.Round(n.Esterno * 1000));
                regoloFattoreX.Value = (int)Math.Round(n.FattoreX * 100);
                regoloFattoreY.Value = (int)Math.Round(n.FattoreY * 100);
                regoloSmussamento.Value = (int)Math.Round(n.Smussamento);
            }
            finally
            {
                aggiornamento = false;
            }
            AggiornaAbilitazioni();
            AggiornaEtichette();
        }

        private void CambioModello()
        {
            if (aggiornamento) return;
            if (comboModello.SelectedIndex == 0)
            {
                aggiornamento = true;
                regoloFattoreX.Value = 100;
                regoloFattoreY.Value = 100;
                regoloSmussamento.Value = 0;
                aggiornamento = false;
            }
            AggiornaAbilitazioni();
            Programma();
        }

        private void AggiornaAbilitazioni()
        {
            bool smussato = comboModello.SelectedIndex == 1;
            regoloFattoreX.Enabled = smussato;
            regoloFattoreY.Enabled = smussato;
            regoloSmussamento.Enabled = smussato;
        }

        private void AggiornaMinimoEsterno()
        {
            if (aggiornamento) return;
            int minimo = Math.Min(regoloEsterno.Maximum, regoloPieno.Value + 10);
            regoloEsterno.Minimum = minimo;
            if (regoloEsterno.Value < minimo) regoloEsterno.Value = minimo;
            Programma();
        }

        private void Programma()
        {
            if (aggiornamento) return;
            AggiornaEtichette();
            timer.Stop();
            timer.Start();
        }

        private void ApplicaDalVivo()
        {
            timer.Stop();
            if (!aggiornamento) applica(Leggi());
        }

        private ParametriCorrezioneBordo Leggi()
        {
            var p = new ParametriCorrezioneBordo
            {
                Modello = comboModello.SelectedIndex == 1
                    ? ModelloCorrezioneBordo.BordoSmussato
                    : ModelloCorrezioneBordo.Bordo,
                Pieno = regoloPieno.Value / 1000.0,
                Esterno = regoloEsterno.Value / 1000.0,
                FattoreX = regoloFattoreX.Value / 100.0,
                FattoreY = regoloFattoreY.Value / 100.0,
                Smussamento = regoloSmussamento.Value
            };
            if (p.Modello == ModelloCorrezioneBordo.Bordo)
            {
                p.FattoreX = 1.0;
                p.FattoreY = 1.0;
                p.Smussamento = 0.0;
            }
            if (p.Esterno <= p.Pieno) p.Esterno = p.Pieno + 0.01;
            return p.Normalizzati();
        }

        private void AggiornaEtichette()
        {
            valorePieno.Text = (regoloPieno.Value / 1000.0).ToString("0.000");
            valoreEsterno.Text = (regoloEsterno.Value / 1000.0).ToString("0.000");
            valoreFattoreX.Text = (regoloFattoreX.Value / 100.0).ToString("0.00");
            valoreFattoreY.Text = (regoloFattoreY.Value / 100.0).ToString("0.00");
            valoreSmussamento.Text = regoloSmussamento.Value.ToString();
        }

        private void Salva()
        {
            timer.Stop();
            ParametriCorrezioneBordo p = Leggi();
            applica(p);
            salva(p);
            iniziali = p.Clona();
            salvato = true;
            Close();
        }

        private void Annulla()
        {
            timer.Stop();
            applica(iniziali);
            salvato = true;
            Close();
        }

        private void Ripristina()
        {
            timer.Stop();
            applica(iniziali);
        }

        private void PosizionaVicinoAllaFinestra()
        {
            if (Owner == null) return;
            Rectangle area = Screen.FromControl(Owner).WorkingArea;
            int sinistra = Owner.Right + 8;
            if (sinistra + Width > area.Right) sinistra = area.Right - Width - 8;
            Left = Math.Max(area.Left + 8, sinistra);
            Top = Math.Max(area.Top + 8, Owner.Top + 80);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }
}

