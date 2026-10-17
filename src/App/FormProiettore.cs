using System;
using System.Collections.Generic;
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
        // Non compare nella barra delle applicazioni né in Alt+Tab.
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

        /// <summary>La form si mostra senza attivarsi, così non ruba il fuoco alla finestra di controllo.</summary>
        protected override bool ShowWithoutActivation => true;

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

        /// <summary>
        /// Aggiorna lo stato proiettato e richiede un nuovo disegno. Thread-safe.
        /// Lo stato viene copiato: le modifiche successive del chiamante non influiscono sul disegno.
        /// </summary>
        public void Aggiorna(StatoTavolo nuovoStato, int nuovoStep, bool nuovaEsecuzione)
        {
            StatoTavolo copia = CopiaStato(nuovoStato);

            lock (bloccoStato)
            {
                if (IsDisposed)
                {
                    return;
                }

                stato = copia;
                stepCorrente = nuovoStep;
                inEsecuzione = nuovaEsecuzione;
            }

            if (!IsHandleCreated)
            {
                return;
            }

            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(Invalidate));
                }
                else
                {
                    Invalidate();
                }
            }
            catch (ObjectDisposedException)
            {
                // La form è stata chiusa nel frattempo: nessun disegno da richiedere.
            }
            catch (InvalidOperationException)
            {
                // Handle non più valido durante la chiusura: nessun disegno da richiedere.
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parametri = base.CreateParams;
                parametri.ExStyle |= WsExToolWindow;
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

        private static StatoTavolo CopiaStato(StatoTavolo origine)
        {
            if (origine == null)
            {
                return null;
            }

            return new StatoTavolo(
                new List<CartoncinoNota>(origine.Note),
                new List<ComandoRilevato>(origine.Comandi),
                new List<ErroreTavolo>(origine.Errori));
        }
    }
}
