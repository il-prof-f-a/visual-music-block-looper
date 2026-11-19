using System;
using System.Drawing;
using System.Windows.Forms;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    /// <summary>
    /// Regolazione dal vivo della correzione di bordo o sferica. I regoli applicano subito i
    /// parametri alla calibrazione attiva; Salva li scrive nella configurazione.
    /// </summary>
    public sealed class FormCorrezioneBordo : Form
    {
        private readonly Action<ParametriCorrezioneBordo> applica;
        private readonly Action<ParametriCorrezioneBordo> salva;
        private readonly Timer timer = new Timer { Interval = 90 };
        private readonly int larghezza, altezza;
        private readonly ComboBox comboModello, comboProiezione;
        private readonly TrackBar regoloPieno, regoloEsterno, regoloFattoreX, regoloFattoreY, regoloSmussamento;
        private readonly TrackBar regoloFocale, regoloCentroX, regoloCentroY;
        private readonly Label valorePieno, valoreEsterno, valoreFattoreX, valoreFattoreY, valoreSmussamento;
        private readonly Label valoreFocale, valoreCentroX, valoreCentroY;
        private readonly Label etichettaProiezione;
        private ParametriCorrezioneBordo iniziali;
        private bool salvato;
        private bool aggiornamento;

        public FormCorrezioneBordo(ParametriCorrezioneBordo iniziali,
            Action<ParametriCorrezioneBordo> applica, Action<ParametriCorrezioneBordo> salva)
            : this(iniziali, 1920, 1080, applica, salva)
        {
        }

        public FormCorrezioneBordo(ParametriCorrezioneBordo iniziali, int larghezza, int altezza,
            Action<ParametriCorrezioneBordo> applica, Action<ParametriCorrezioneBordo> salva)
        {
            this.larghezza = larghezza > 0 ? larghezza : 1920;
            this.altezza = altezza > 0 ? altezza : 1080;
            this.iniziali = (iniziali ?? new ParametriCorrezioneBordo()).Clona().Normalizzati();
            this.applica = applica ?? throw new ArgumentNullException(nameof(applica));
            this.salva = salva ?? throw new ArgumentNullException(nameof(salva));

            Text = "Correzione bordo";
            ClientSize = new Size(430, 520);
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
            comboModello.Items.Add("Sferico (sfera/equisolido)");
            comboModello.SelectedIndexChanged += (m, a) => CambioModello();

            regoloPieno = CreaRegolo("Pieno", 55, 30, 100, out valorePieno);
            regoloEsterno = CreaRegolo("Esterno", 95, 100, 350, out valoreEsterno);
            regoloPieno.ValueChanged += (m, a) => AggiornaMinimoEsterno();
            regoloFattoreX = CreaRegolo("Fattore X", 135, 50, 200, out valoreFattoreX);
            regoloFattoreY = CreaRegolo("Fattore Y", 175, 50, 200, out valoreFattoreY);
            regoloSmussamento = CreaRegolo("Smussamento", 215, 0, 60, out valoreSmussamento);

            Label etichettaSfera = new Label
            {
                Left = 15, Top = 255, Width = 400, Height = 18, Font = new Font(Font, FontStyle.Bold),
                Text = "Modello sferico"
            };
            etichettaProiezione = new Label { Left = 15, Top = 281, Width = 110, Height = 18, Text = "Proiezione" };
            comboProiezione = new ComboBox
            {
                Left = 130, Top = 278, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList
            };
            comboProiezione.Items.Add("Equisolido");
            comboProiezione.Items.Add("Equidistante");
            comboProiezione.Items.Add("Stereografico");
            comboProiezione.SelectedIndexChanged += (m, a) => Programma();

            int fMax = Math.Max(2000, this.larghezza * 2);
            regoloFocale = CreaRegolo("Focale px", 315, 100, fMax, out valoreFocale, 10);
            regoloCentroX = CreaRegolo("Centro X", 355, 0, this.larghezza, out valoreCentroX, 5);
            regoloCentroY = CreaRegolo("Centro Y", 395, 0, this.altezza, out valoreCentroY, 5);

            Label descrizione = new Label
            {
                Left = 15, Top = 435, Width = 400, Height = 44, ForeColor = Color.DarkSlateGray,
                Text = "Le modifiche sono applicate subito. Salva le scrive in configurazione.json "
                    + "per il prossimo avvio; Annulla ripristina i valori iniziali."
            };

            Button pulsanteSalva = new Button { Text = "Salva", Left = 130, Top = 480, Width = 90, Height = 30 };
            pulsanteSalva.Click += (m, a) => Salva();
            Button pulsanteAnnulla = new Button { Text = "Annulla", Left = 230, Top = 480, Width = 90, Height = 30 };
            pulsanteAnnulla.Click += (m, a) => Annulla();

            Controls.Add(etichettaModello);
            Controls.Add(comboModello);
            Controls.Add(etichettaSfera);
            Controls.Add(etichettaProiezione);
            Controls.Add(comboProiezione);
            Controls.Add(descrizione);
            Controls.Add(pulsanteSalva);
            Controls.Add(pulsanteAnnulla);

            timer.Tick += (m, a) => ApplicaDalVivo();
            FormClosing += (m, a) => { if (!salvato) Ripristina(); };
            Shown += (m, a) => PosizionaVicinoAllaFinestra();
            Imposta(this.iniziali);
        }

        private TrackBar CreaRegolo(string testo, int top, int minimo, int massimo, out Label valore, int grande = 5)
        {
            Label etichetta = new Label { Left = 15, Top = top, Width = 110, Height = 18, Text = testo };
            valore = new Label { Left = 340, Top = top, Width = 75, Height = 18, TextAlign = ContentAlignment.MiddleRight };
            TrackBar regolo = new TrackBar
            {
                Left = 130, Top = top - 6, Width = 200, Height = 32,
                Minimum = minimo, Maximum = massimo, TickStyle = TickStyle.None,
                SmallChange = 1, LargeChange = grande
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
                comboModello.SelectedIndex = n.Modello == ModelloCorrezioneBordo.Sferico ? 2
                    : n.Modello == ModelloCorrezioneBordo.BordoSmussato ? 1 : 0;
                regoloPieno.Value = (int)Math.Round(n.Pieno * 1000);
                regoloEsterno.Minimum = Math.Min(regoloEsterno.Maximum, regoloPieno.Value + 10);
                regoloEsterno.Value = Math.Max(regoloEsterno.Minimum, (int)Math.Round(n.Esterno * 1000));
                regoloFattoreX.Value = (int)Math.Round(n.FattoreX * 100);
                regoloFattoreY.Value = (int)Math.Round(n.FattoreY * 100);
                regoloSmussamento.Value = (int)Math.Round(n.Smussamento);

                comboProiezione.SelectedIndex = n.TipoSferico == TipoProiezioneSferica.Equidistante ? 1
                    : n.TipoSferico == TipoProiezioneSferica.Stereografico ? 2 : 0;
                double focale = n.Focale > 0 ? n.Focale : Math.Sqrt((double)larghezza * larghezza + (double)altezza * altezza) / 2;
                regoloFocale.Value = Entro(regoloFocale, (int)Math.Round(focale));
                regoloCentroX.Value = Entro(regoloCentroX, n.CentroX > 0 ? (int)Math.Round(n.CentroX) : larghezza / 2);
                regoloCentroY.Value = Entro(regoloCentroY, n.CentroY > 0 ? (int)Math.Round(n.CentroY) : altezza / 2);
            }
            finally
            {
                aggiornamento = false;
            }
            AggiornaAbilitazioni();
            AggiornaEtichette();
        }

        private static int Entro(TrackBar regolo, int valore)
            => Math.Min(regolo.Maximum, Math.Max(regolo.Minimum, valore));

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
            bool sferico = comboModello.SelectedIndex == 2;
            bool smussato = comboModello.SelectedIndex == 1;
            regoloPieno.Enabled = !sferico;
            regoloEsterno.Enabled = !sferico;
            regoloFattoreX.Enabled = smussato;
            regoloFattoreY.Enabled = smussato;
            regoloSmussamento.Enabled = smussato;
            etichettaProiezione.Enabled = sferico;
            comboProiezione.Enabled = sferico;
            regoloFocale.Enabled = sferico;
            regoloCentroX.Enabled = sferico;
            regoloCentroY.Enabled = sferico;
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
            int indice = comboModello.SelectedIndex;
            var p = new ParametriCorrezioneBordo
            {
                Modello = indice == 2 ? ModelloCorrezioneBordo.Sferico
                    : indice == 1 ? ModelloCorrezioneBordo.BordoSmussato
                    : ModelloCorrezioneBordo.Bordo,
                Pieno = regoloPieno.Value / 1000.0,
                Esterno = regoloEsterno.Value / 1000.0,
                FattoreX = regoloFattoreX.Value / 100.0,
                FattoreY = regoloFattoreY.Value / 100.0,
                Smussamento = regoloSmussamento.Value,
                TipoSferico = comboProiezione.SelectedIndex == 1 ? TipoProiezioneSferica.Equidistante
                    : comboProiezione.SelectedIndex == 2 ? TipoProiezioneSferica.Stereografico
                    : TipoProiezioneSferica.Equisolido,
                Focale = regoloFocale.Value,
                CentroX = regoloCentroX.Value,
                CentroY = regoloCentroY.Value
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
            valoreFocale.Text = regoloFocale.Value.ToString("0");
            valoreCentroX.Text = regoloCentroX.Value.ToString("0");
            valoreCentroY.Text = regoloCentroY.Value.ToString("0");
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
            Top = Math.Max(area.Top + 8, Owner.Top + 40);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
