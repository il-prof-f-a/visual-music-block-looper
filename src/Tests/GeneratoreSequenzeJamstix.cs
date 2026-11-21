using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using ZXing.QrCode.Internal;

namespace VisualMusicBlockLooper.Tests
{
    /// <summary>Cartoncini arancioni per richiamare le sei sequenze MIDI di Jamstix 4 Free.</summary>
    internal static class GeneratoreSequenzeJamstix
    {
        public sealed class Sequenza
        {
            public Sequenza(int midi, string nota, string nome, string slug, string descrizione)
            {
                Midi = midi;
                Nota = nota;
                Nome = nome;
                Slug = slug;
                Descrizione = descrizione;
            }

            public int Midi { get; }
            public string Nota { get; }
            public string Nome { get; }
            public string Slug { get; }
            public string Descrizione { get; }
            public string Payload => $"VMBL1;N;{Midi - 36};{Midi}";
            public string FileCard => $"jamstix-{Slug}.png";
        }

        public static readonly IReadOnlyList<Sequenza> Sequenze = new[]
        {
            new Sequenza(48, "C2", "Intro", "intro", "Suona l'introduzione\npoi passa alla strofa"),
            new Sequenza(50, "D2", "Strofa", "strofa", "Groove base\ncontinuo"),
            new Sequenza(52, "E2", "Ritornello", "ritornello", "Groove più\nenergico"),
            new Sequenza(53, "F2", "Fill", "fill", "Rullata e ritorno\nalla sezione"),
            new Sequenza(55, "G2", "Bridge", "bridge", "Variazione\nritmica"),
            new Sequenza(57, "A2", "Finale", "finale", "Esegue la chiusura\ne termina")
        };

        private static readonly Color Arancione = Color.FromArgb(255, 140, 0);
        private static readonly Color Inchiostro = Color.FromArgb(36, 45, 57);

        public static void Genera(string cartella)
        {
            Directory.CreateDirectory(cartella);
            foreach (string vecchio in Directory.GetFiles(cartella, "jamstix-*.png"))
            {
                File.Delete(vecchio);
            }

            foreach (Sequenza sequenza in Sequenze)
            {
                Action<Graphics, RectangleF> contenuto = (g, area) => DisegnaContenuto(g, sequenza, area);
                using (Bitmap cartoncino = GeneratoreCartoncini.Disegna(
                    sequenza.Payload,
                    Arancione,
                    "Jamstix 4",
                    2,
                    GeneratoreCartoncini.PassoMmDefault,
                    GeneratoreCartoncini.AltezzaMmDefault,
                    ErrorCorrectionLevel.L,
                    disegnaIllustrazione: contenuto,
                    altezzaDisegnoIllustrazione: 1f))
                {
                    cartoncino.Save(Path.Combine(cartella, sequenza.FileCard), ImageFormat.Png);
                }
            }
        }

        private static void DisegnaContenuto(Graphics g, Sequenza sequenza, RectangleF area)
        {
            GraphicsState stato = g.Save();
            try
            {
                g.SetClip(area);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                RectangleF areaIcona = new RectangleF(
                    area.X + area.Width * 0.07f,
                    area.Y + area.Height * 0.015f,
                    area.Width * 0.86f,
                    area.Height * 0.405f);
                DisegnaBatteria(g, areaIcona);

                DisegnaTesto(g, sequenza.Nota + " · MIDI " + sequenza.Midi,
                    new RectangleF(area.X + 2, area.Y + area.Height * 0.425f, area.Width - 4, area.Height * 0.105f),
                    7.6f, FontStyle.Bold);
                DisegnaTesto(g, sequenza.Nome.ToUpperInvariant(),
                    new RectangleF(area.X + 2, area.Y + area.Height * 0.535f, area.Width - 4, area.Height * 0.105f),
                    8.2f, FontStyle.Bold);
                DisegnaTesto(g, sequenza.Descrizione,
                    new RectangleF(area.X + 3, area.Y + area.Height * 0.645f, area.Width - 6, area.Height * 0.33f),
                    5.4f, FontStyle.Regular);
            }
            finally
            {
                g.Restore(stato);
            }
        }

        private static void DisegnaTesto(Graphics g, string testo, RectangleF area, float punti, FontStyle stile)
        {
            using (Font font = new Font("Segoe UI", punti, stile, GraphicsUnit.Point))
            using (SolidBrush pennello = new SolidBrush(Inchiostro))
            using (StringFormat formato = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            })
            {
                g.DrawString(testo, font, pennello, area, formato);
            }
        }

        private static void DisegnaBatteria(Graphics g, RectangleF area)
        {
            const float larghezza = 240;
            const float altezza = 140;
            float scala = Math.Min(area.Width / larghezza, area.Height / altezza);
            float offsetX = area.X + (area.Width - larghezza * scala) / 2f;
            float offsetY = area.Y + (area.Height - altezza * scala) / 2f;

            GraphicsState stato = g.Save();
            try
            {
                g.TranslateTransform(offsetX, offsetY);
                g.ScaleTransform(scala, scala);

                using (Pen contorno = new Pen(Inchiostro, 3.2f))
                using (Pen metallo = new Pen(Color.FromArgb(105, 116, 130), 3f))
                using (Pen accento = new Pen(Color.FromArgb(213, 112, 25), 3f))
                using (SolidBrush pelle = new SolidBrush(Color.FromArgb(250, 250, 248)))
                using (SolidBrush fusto = new SolidBrush(Color.FromArgb(245, 160, 55)))
                using (SolidBrush fustoScuro = new SolidBrush(Color.FromArgb(220, 118, 28)))
                using (SolidBrush piatto = new SolidBrush(Color.FromArgb(238, 190, 90)))
                using (SolidBrush ombra = new SolidBrush(Color.FromArgb(225, 228, 232)))
                {
                    // Supporti e piatti.
                    g.DrawLine(metallo, 39, 45, 39, 130);
                    g.DrawLine(metallo, 203, 42, 203, 130);
                    g.DrawLine(metallo, 22, 78, 22, 130);
                    g.FillEllipse(piatto, 10, 23, 58, 12);
                    g.DrawEllipse(contorno, 10, 23, 58, 12);
                    g.FillEllipse(piatto, 172, 18, 63, 13);
                    g.DrawEllipse(contorno, 172, 18, 63, 13);
                    g.FillEllipse(piatto, 4, 68, 38, 9);
                    g.DrawEllipse(contorno, 4, 68, 38, 9);

                    // Charleston, rullante, tom e timpano.
                    DisegnaTamburo(g, contorno, metallo, fusto, pelle, 53, 85, 46, 29, true);
                    DisegnaTamburo(g, contorno, metallo, fusto, pelle, 98, 48, 43, 29, false);
                    DisegnaTamburo(g, contorno, metallo, fustoScuro, pelle, 140, 44, 43, 31, false);
                    DisegnaTamburo(g, contorno, metallo, fusto, pelle, 180, 78, 43, 38, true);

                    // Cassa centrale con battente.
                    g.FillEllipse(ombra, 77, 123, 96, 9);
                    g.FillEllipse(fustoScuro, 84, 67, 78, 66);
                    g.DrawEllipse(contorno, 84, 67, 78, 66);
                    g.FillEllipse(pelle, 92, 73, 62, 54);
                    g.DrawEllipse(metallo, 92, 73, 62, 54);
                    g.DrawEllipse(accento, 106, 85, 34, 30);
                    g.FillEllipse(fusto, 118, 96, 10, 10);

                    // Gambe e aste, disegnate davanti ai fusti per rendere leggibile il kit.
                    g.DrawLine(metallo, 94, 120, 85, 137);
                    g.DrawLine(metallo, 151, 120, 160, 137);
                    g.DrawLine(metallo, 193, 111, 184, 136);
                    g.DrawLine(metallo, 211, 111, 220, 136);
                    g.DrawLine(metallo, 63, 106, 55, 133);
                    g.DrawLine(metallo, 91, 105, 98, 132);

                }
            }
            finally
            {
                g.Restore(stato);
            }
        }

        private static void DisegnaTamburo(Graphics g, Pen contorno, Pen metallo, SolidBrush fusto,
            SolidBrush pelle, float x, float y, float larghezza, float altezza, bool conGambe)
        {
            g.FillEllipse(fusto, x, y, larghezza, altezza);
            g.DrawEllipse(contorno, x, y, larghezza, altezza);
            g.FillEllipse(pelle, x + 4, y + 3, larghezza - 8, Math.Max(10, altezza * 0.44f));
            g.DrawEllipse(metallo, x + 4, y + 3, larghezza - 8, Math.Max(10, altezza * 0.44f));
            if (conGambe)
            {
                g.DrawLine(metallo, x + 5, y + altezza - 2, x + 5, y + altezza + 8);
                g.DrawLine(metallo, x + larghezza - 5, y + altezza - 2, x + larghezza - 5, y + altezza + 8);
            }
        }
    }
}
