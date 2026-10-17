using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.App
{
    /// <summary>
    /// Disegna la guida visiva proiettata sul tavolo: fondo bianco, griglia, playhead,
    /// note riconosciute (verde) ed errori (rosso). Non dipende dall'interfaccia utente.
    /// </summary>
    public class RenderGuida
    {
        private static readonly Color ColoreGriglia = Color.LightGray;
        private static readonly Color ColorePlayhead = Color.FromArgb(90, 255, 140, 0);
        private static readonly Color ColoreContornoPlayhead = Color.Gray;
        private static readonly Color ColoreNota = Color.FromArgb(255, 60, 179, 75);
        private static readonly Color ColoreBordoNota = Color.FromArgb(255, 30, 110, 45);
        private static readonly Color ColoreErrore = Color.FromArgb(255, 220, 30, 30);
        private static readonly Color ColoreErroreFondo = Color.FromArgb(60, 255, 120, 120);

        private readonly ConfigGriglia config;

        /// <summary>Crea il renderer per la griglia data. Non può essere null.</summary>
        public RenderGuida(ConfigGriglia config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Disegna la guida sulla superficie di dimensione indicata. Lo stato null è trattato come vuoto.
        /// </summary>
        /// <param name="g">Superficie di disegno.</param>
        /// <param name="dimensione">Dimensione in pixel della superficie.</param>
        /// <param name="stato">Stato del tavolo, può essere null.</param>
        /// <param name="stepCorrente">Step indicato dal playhead.</param>
        /// <param name="inEsecuzione">True se la sequenza è in esecuzione: il playhead viene riempito.</param>
        public void Disegna(Graphics g, Size dimensione, StatoTavolo stato, int stepCorrente, bool inEsecuzione)
        {
            if (g == null)
            {
                throw new ArgumentNullException(nameof(g));
            }

            g.SmoothingMode = SmoothingMode.None;
            g.Clear(Color.White);

            if (config.Tracce <= 0 || config.Step <= 0)
            {
                return;
            }

            Rectangle griglia = InPixel(config.Area, dimensione);

            DisegnaCelleLibere(g, griglia);
            DisegnaPlayhead(g, griglia, stepCorrente, inEsecuzione);
            DisegnaNote(g, griglia, stato);
            DisegnaErrori(g, dimensione, stato);
        }

        private void DisegnaCelleLibere(Graphics g, Rectangle griglia)
        {
            using (var penna = new Pen(ColoreGriglia, 1))
            {
                for (int traccia = 0; traccia < config.Tracce; traccia++)
                {
                    for (int step = 0; step < config.Step; step++)
                    {
                        g.DrawRectangle(penna, CellaRect(griglia, traccia, step));
                    }
                }
            }
        }

        private void DisegnaPlayhead(Graphics g, Rectangle griglia, int stepCorrente, bool inEsecuzione)
        {
            if (stepCorrente < 0 || stepCorrente >= config.Step)
            {
                return;
            }

            var colonna = Rectangle.FromLTRB(
                BordoX(griglia, stepCorrente),
                griglia.Y,
                BordoX(griglia, stepCorrente + 1),
                griglia.Bottom);

            if (inEsecuzione)
            {
                using (var pennello = new SolidBrush(ColorePlayhead))
                {
                    g.FillRectangle(pennello, colonna);
                }
            }
            else
            {
                using (var penna = new Pen(ColoreContornoPlayhead, 1))
                {
                    g.DrawRectangle(penna, colonna);
                }
            }
        }

        private void DisegnaNote(Graphics g, Rectangle griglia, StatoTavolo stato)
        {
            if (stato == null)
            {
                return;
            }

            using (var pennello = new SolidBrush(ColoreNota))
            using (var penna = new Pen(ColoreBordoNota, 2))
            {
                foreach (CartoncinoNota nota in stato.Note)
                {
                    if (nota == null
                        || nota.Traccia < 0 || nota.Traccia >= config.Tracce
                        || nota.StepIniziale < 0 || nota.StepIniziale >= config.Step)
                    {
                        continue;
                    }

                    // Durata troncata a fine griglia: oltre il bordo la nota non viene disegnata.
                    int durata = Math.Min(Math.Max(nota.DurataStep, 1), config.Step - nota.StepIniziale);
                    var area = Rectangle.FromLTRB(
                        BordoX(griglia, nota.StepIniziale),
                        BordoY(griglia, nota.Traccia),
                        BordoX(griglia, nota.StepIniziale + durata),
                        BordoY(griglia, nota.Traccia + 1));

                    g.FillRectangle(pennello, area);
                    g.DrawRectangle(penna, area);
                }
            }
        }

        private void DisegnaErrori(Graphics g, Size dimensione, StatoTavolo stato)
        {
            if (stato == null)
            {
                return;
            }

            using (var pennello = new SolidBrush(ColoreErroreFondo))
            using (var penna = new Pen(ColoreErrore, 3))
            {
                foreach (ErroreTavolo errore in stato.Errori)
                {
                    if (errore == null)
                    {
                        continue;
                    }

                    Rectangle area = InPixel(errore.Area, dimensione);
                    g.FillRectangle(pennello, area);
                    g.DrawRectangle(penna, area);
                }
            }
        }

        // Bordi calcolati dagli spigoli (non dalla larghezza intera della cella), così che
        // i resti della divisione finiscano su bordi reali e nessuna linea venga persa.
        private int BordoX(Rectangle griglia, int indice)
        {
            return griglia.X + (int)Math.Round(indice * griglia.Width / (double)config.Step);
        }

        private int BordoY(Rectangle griglia, int indice)
        {
            return griglia.Y + (int)Math.Round(indice * griglia.Height / (double)config.Tracce);
        }

        private Rectangle CellaRect(Rectangle griglia, int traccia, int step)
        {
            return Rectangle.FromLTRB(
                BordoX(griglia, step),
                BordoY(griglia, traccia),
                BordoX(griglia, step + 1),
                BordoY(griglia, traccia + 1));
        }

        private static Rectangle InPixel(RettangoloTavolo area, Size dimensione)
        {
            return new Rectangle(
                (int)Math.Round(area.X * dimensione.Width),
                (int)Math.Round(area.Y * dimensione.Height),
                (int)Math.Round(area.Larghezza * dimensione.Width),
                (int)Math.Round(area.Altezza * dimensione.Height));
        }
    }
}
