using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Guida proiettata: i centri sono le coordinate effettive usate nell'omografia.</summary>
    public static class RenderCalibrazione
    {
        public static void Disegna(Graphics g, Size dimensioni, int puntoCorrente)
        {
            if (g == null) throw new ArgumentNullException(nameof(g));
            if (puntoCorrente < 0 || puntoCorrente >= SchemaCalibrazione.Punti.Count)
                throw new ArgumentOutOfRangeException(nameof(puntoCorrente));
            g.Clear(Color.White);
            if (dimensioni.Width <= 0 || dimensioni.Height <= 0) return;
            g.SmoothingMode = SmoothingMode.None;
            float lato = Math.Min(dimensioni.Width, dimensioni.Height);
            float raggio = Math.Max(5, lato * 0.012f);
            using (var bordo = new Pen(Color.LightGray, 1))
            using (var font = new Font(FontFamily.GenericSansSerif, Math.Max(12, lato * 0.033f), FontStyle.Bold, GraphicsUnit.Pixel))
            using (var formato = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawRectangle(bordo, dimensioni.Width * 0.03f, dimensioni.Height * 0.03f,
                    dimensioni.Width * 0.94f, dimensioni.Height * 0.94f);
                for (int i = 0; i < SchemaCalibrazione.Punti.Count; i++)
                {
                    var p = SchemaCalibrazione.Punti[i];
                    float x = (float)Math.Round(p.X * dimensioni.Width), y = (float)Math.Round(p.Y * dimensioni.Height);
                    Color colore = i == puntoCorrente ? Color.RoyalBlue : Color.Black;
                    using (var penna = new Pen(colore, i == puntoCorrente ? 3 : 2))
                    using (var pennello = new SolidBrush(colore))
                    {
                        g.DrawEllipse(penna, x - raggio, y - raggio, 2 * raggio, 2 * raggio);
                        g.DrawLine(penna, x - raggio, y, x + raggio, y);
                        g.DrawLine(penna, x, y - raggio, x, y + raggio);
                        g.FillRectangle(pennello, x - 1, y - 1, 3, 3);
                        string numero = (i + 1).ToString();
                        SizeF testo = g.MeasureString(numero, font);
                        float tx = p.X > 0.5 ? x - raggio - 6 - testo.Width : x + raggio + 6;
                        float ty = p.Y > 0.5 ? y - raggio - 6 - testo.Height : y + raggio + 6;
                        g.DrawString(numero, font, pennello, tx, ty);
                    }
                }
                g.DrawString($"Calibrazione — punto {puntoCorrente + 1}/12\n"
                    + "Nell'anteprima clicca il centro del bersaglio blu.\n"
                    + "Backspace: ultimo punto · Esc: annulla", font, Brushes.RoyalBlue,
                    new RectangleF(dimensioni.Width * 0.15f, dimensioni.Height * 0.3f,
                        dimensioni.Width * 0.7f, dimensioni.Height * 0.4f), formato);
            }
        }
    }
}
