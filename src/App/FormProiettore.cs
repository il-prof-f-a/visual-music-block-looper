using System;
using System.Drawing;
using System.Windows.Forms;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.App
{
    /// <summary>
    /// Finestra a schermo intero, senza bordi e in primo piano, che proietta la guida del tavolo.
    /// Lo stato viene aggiornato da fuori con <see cref="Aggiorna"/>; il disegno avviene in <see cref="OnPaint"/>.
    /// </summary>
    public class FormProiettore : Form
    {
        private const int WsExTransparent = 0x20;
        private const int WsExToolWindow = 0x80;

        private readonly object bloccoStato = new object();
        private readonly RenderGuida renderGuida;
        private StatoTavolo stato;
        private int stepCorrente;
        private bool inEsecuzione;

        /// <summary>Crea la form per la griglia indicata. Non viene mostrata.</summary>
        public FormProiettore(ConfigGriglia config)
        {
            renderGuida = new RenderGuida(config ?? throw new ArgumentNullException(nameof(config)));

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.White;
            DoubleBuffered = true;
        }

        /// <summary>Posiziona la form a schermo intero sullo schermo indicato e la mostra.</summary>
        public void MostraSuSchermo(Screen schermo)
        {
            if (schermo == null)
            {
                throw new ArgumentNullException(nameof(schermo));
            }

            Bounds = schermo.Bounds;
            Show();
        }

        /// <summary>Aggiorna lo stato proiettato e richiede un nuovo disegno. Thread-safe.</summary>
        public void Aggiorna(StatoTavolo nuovoStato, int nuovoStep, bool nuovaEsecuzione)
        {
            lock (bloccoStato)
            {
                stato = nuovoStato;
                stepCorrente = nuovoStep;
                inEsecuzione = nuovaEsecuzione;
            }

            if (!IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => Invalidate()));
            }
            else
            {
                Invalidate();
            }
        }

        /// <summary>Le click passano attraverso la finestra: il proiettore non intercetta il mouse.</summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parametri = base.CreateParams;
                parametri.ExStyle |= WsExTransparent | WsExToolWindow;
                return parametri;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            StatoTavolo statoCorrente;
            int stepDisegnato;
            bool esecuzione;

            lock (bloccoStato)
            {
                statoCorrente = stato;
                stepDisegnato = stepCorrente;
                esecuzione = inEsecuzione;
            }

            renderGuida.Disegna(e.Graphics, ClientSize, statoCorrente, stepDisegnato, esecuzione);
        }
    }
}
