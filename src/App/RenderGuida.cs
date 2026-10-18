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

        private static readonly Color ColoreColonnaOltreMisura = Color.FromArgb(255, 215, 215, 215);
        private static readonly Color ColoreRigaFuoriAb = Color.FromArgb(255, 240, 240, 240);
        private static readonly Color ColoreBarraA = Color.FromArgb(255, 70, 130, 180);
        private static readonly Color ColoreBarraB = Color.FromArgb(255, 120, 170, 210);
        private static readonly Color ColoreContornoZona = Color.FromArgb(255, 128, 128, 128);
        private static readonly Color ColoreTesto = Color.DarkGray;

        private const int LarghezzaBarra = 6;
        private const int DistanzaBarra = 10;

        private readonly ConfigGriglia config;
        private readonly LayoutTavolo layout;

        /// <summary>
        /// Crea il renderer per la griglia data. Con <paramref name="layout"/> null non vengono
        /// disegnate le zone speciali.
        /// </summary>
        public RenderGuida(ConfigGriglia config, LayoutTavolo layout = null)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.layout = layout;
        }

        /// <summary>
        /// Disegna la guida con tutte le righe attive (playhead su ogni riga della colonna indicata).
        /// </summary>
        public void Disegna(Graphics g, Size dimensione, StatoTavolo stato, int stepCorrente, bool inEsecuzione)
        {
            var righe = new int[Math.Max(config.Tracce, 0)];
            for (int i = 0; i < righe.Length; i++)
            {
                righe[i] = i;
            }

            Disegna(g, dimensione, stato, new PosizioneGiro(stepCorrente, 0, righe), inEsecuzione);
        }

        /// <summary>
        /// Disegna la guida sulla superficie di dimensione indicata. Lo stato null è trattato come vuoto.
        /// </summary>
        /// <param name="g">Superficie di disegno.</param>
        /// <param name="dimensione">Dimensione in pixel della superficie.</param>
        /// <param name="stato">Stato del tavolo, può essere null.</param>
        /// <param name="posizione">Posizione del giro: colonna del playhead e righe attive.</param>
        /// <param name="inEsecuzione">True se la sequenza è in esecuzione: il playhead viene riempito.</param>
        public void Disegna(Graphics g, Size dimensione, StatoTavolo stato, PosizioneGiro posizione, bool inEsecuzione)
        {
            if (g == null)
            {
                throw new ArgumentNullException(nameof(g));
            }

            if (posizione == null)
            {
                throw new ArgumentNullException(nameof(posizione));
            }

            g.SmoothingMode = SmoothingMode.None;
            g.Clear(Color.White);

            StrutturaTavolo struttura = stato?.Struttura ?? StrutturaTavolo.Default;

            if (layout != null)
            {
                DisegnaZone(g, dimensione, struttura);
            }

            if (config.Tracce <= 0 || config.Step <= 0)
            {
                return;
            }

            Rectangle griglia = InPixel(config.Area, dimensione);

            DisegnaCelle(g, griglia, struttura);
            DisegnaBarreGruppo(g, griglia, struttura);
            DisegnaPlayhead(g, griglia, posizione, inEsecuzione);
            DisegnaNote(g, griglia, stato, struttura);
            DisegnaErrori(g, dimensione, stato);
        }

        private void DisegnaCelle(Graphics g, Rectangle griglia, StrutturaTavolo struttura)
        {
            using (var penna = new Pen(ColoreGriglia, 1))
            using (var fondoOltreMisura = new SolidBrush(ColoreColonnaOltreMisura))
            using (var fondoFuoriAb = new SolidBrush(ColoreRigaFuoriAb))
            {
                for (int traccia = 0; traccia < config.Tracce; traccia++)
                {
                    int posizioneNelGruppo = traccia % struttura.RigheCollegate + 1;
                    bool fuoriAb = posizioneNelGruppo < struttura.AInizio || posizioneNelGruppo > struttura.AFine;

                    for (int step = 0; step < config.Step; step++)
                    {
                        Rectangle cella = CellaRect(griglia, traccia, step);
                        if (step >= struttura.StepPerMisura)
                        {
                            g.FillRectangle(fondoOltreMisura, cella);
                        }
                        else if (fuoriAb)
                        {
                            g.FillRectangle(fondoFuoriAb, cella);
                        }

                        g.DrawRectangle(penna, cella);
                    }
                }
            }
        }

        private void DisegnaBarreGruppo(Graphics g, Rectangle griglia, StrutturaTavolo struttura)
        {
            int sinistra = griglia.X - DistanzaBarra - LarghezzaBarra / 2;
            using (var carattere = new Font(FontFamily.GenericSansSerif, 9))
            using (var pennelloTesto = new SolidBrush(Color.DarkSlateGray))
            using (var formato = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            {
                for (int gruppo = 0; gruppo < struttura.NumeroGruppi; gruppo++)
                {
                    int primaRiga = gruppo * struttura.RigheCollegate;
                    if (primaRiga >= config.Tracce)
                    {
                        break;
                    }

                    int ultimaRiga = Math.Min(primaRiga + struttura.RigheCollegate, config.Tracce);
                    // Un pixel di scarto sopra e sotto: tra gruppi consecutivi resta un vuoto di 2 px.
                    var barra = Rectangle.FromLTRB(
                        sinistra, BordoY(griglia, primaRiga) + 1, sinistra + LarghezzaBarra, BordoY(griglia, ultimaRiga) - 1);
                    using (var pennello = new SolidBrush(gruppo % 2 == 0 ? ColoreBarraA : ColoreBarraB))
                    {
                        g.FillRectangle(pennello, barra);
                    }
                }

                for (int riga = 0; riga < config.Tracce; riga++)
                {
                    var testo = new RectangleF(
                        sinistra - 40, BordoY(griglia, riga), 37, BordoY(griglia, riga + 1) - BordoY(griglia, riga));
                    g.DrawString((riga + 1).ToString(), carattere, pennelloTesto, testo, formato);
                }
            }
        }

        /// <summary>Testo della zona di collegamento: singolare per una riga, plurale altrimenti.</summary>
        public static string EtichettaRighe(int righe)
        {
            return righe + (righe == 1 ? " riga" : " righe");
        }

        private void DisegnaZone(Graphics g, Size dimensione, StrutturaTavolo struttura)
        {
            DisegnaZona(g, dimensione, layout.Misura, "MISURA", struttura.BattitiPerMisura + "/4");
            DisegnaZona(g, dimensione, layout.Collegamento, "COLLEGA", EtichettaRighe(struttura.RigheCollegate));
            DisegnaZona(g, dimensione, layout.Ab, "A-B", struttura.AInizio + "-" + struttura.AFine);
            DisegnaZona(g, dimensione, layout.Comandi, "COMANDI", null);
        }

        private static void DisegnaZona(Graphics g, Size dimensione, RettangoloTavolo zona, string etichetta, string valore)
        {
            if (zona.Larghezza <= 0 || zona.Altezza <= 0)
            {
                return;
            }

            Rectangle area = InPixel(zona, dimensione);
            using (var penna = new Pen(ColoreContornoZona, 2))
            using (var pennelloTesto = new SolidBrush(ColoreTesto))
            using (var carattere = new Font(FontFamily.GenericSansSerif, 10, FontStyle.Bold))
            using (var carattereValore = new Font(FontFamily.GenericSansSerif, 11))
            using (var formato = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near })
            {
                g.DrawRectangle(penna, area);

                var testoEtichetta = new RectangleF(area.X + 4, area.Y + 6, Math.Max(area.Width - 8, 1), Math.Max(area.Height - 12, 1));
                g.DrawString(etichetta, carattere, pennelloTesto, testoEtichetta, formato);

                if (valore != null)
                {
                    float sotto = g.MeasureString(etichetta, carattere).Height + 10;
                    var testoValore = new RectangleF(
                        area.X + 4, area.Y + sotto, Math.Max(area.Width - 8, 1), Math.Max(area.Height - sotto - 4, 1));
                    g.DrawString(valore, carattereValore, pennelloTesto, testoValore, formato);
                }
            }
        }

        private void DisegnaPlayhead(Graphics g, Rectangle griglia, PosizioneGiro posizione, bool inEsecuzione)
        {
            int colonna = posizione.StepInMisura;
            if (colonna < 0 || colonna >= config.Step || posizione.RigheAttive == null)
            {
                return;
            }

            using (var pennello = new SolidBrush(ColorePlayhead))
            using (var penna = new Pen(ColoreContornoPlayhead, 1))
            {
                foreach (int riga in posizione.RigheAttive)
                {
                    if (riga < 0 || riga >= config.Tracce)
                    {
                        continue;
                    }

                    Rectangle cella = CellaRect(griglia, riga, colonna);
                    if (inEsecuzione)
                    {
                        g.FillRectangle(pennello, cella);
                    }
                    else
                    {
                        g.DrawRectangle(penna, cella);
                    }
                }
            }
        }

        private void DisegnaNote(Graphics g, Rectangle griglia, StatoTavolo stato, StrutturaTavolo struttura)
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
                        || nota.Riga < 0 || nota.Riga >= config.Tracce
                        || nota.StepIniziale < 0 || nota.StepIniziale >= Math.Min(config.Step, struttura.StepPerMisura))
                    {
                        continue;
                    }

                    // Durata troncata a fine griglia: oltre il bordo la nota non viene disegnata.
                    int durata = Math.Min(Math.Max(nota.DurataStep, 1), Math.Min(config.Step, struttura.StepPerMisura) - nota.StepIniziale);
                    var area = Rectangle.FromLTRB(
                        BordoX(griglia, nota.StepIniziale),
                        BordoY(griglia, nota.Riga),
                        BordoX(griglia, nota.StepIniziale + durata),
                        BordoY(griglia, nota.Riga + 1));

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
