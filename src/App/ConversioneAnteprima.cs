using System;
using System.Drawing;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Conversione dei click su una PictureBox in modalità Zoom nei pixel dell'immagine mostrata.</summary>
    public static class ConversioneAnteprima
    {
        /// <summary>
        /// Converte un punto in coordinate della PictureBox nel pixel corrispondente dell'immagine,
        /// tenendo conto del ridimensionamento proporzionale e dei bordi di centratura.
        /// </summary>
        /// <returns>Il punto immagine, oppure null se il click cade fuori dall'immagine o le dimensioni non sono valide.</returns>
        public static PuntoImmagine? DaPictureBoxAImmagine(Point click, Size dimensioniBox, Size dimensioniImmagine)
        {
            if (dimensioniBox.Width <= 0 || dimensioniBox.Height <= 0
                || dimensioniImmagine.Width <= 0 || dimensioniImmagine.Height <= 0)
            {
                return null;
            }

            double scala = Math.Min(
                (double)dimensioniBox.Width / dimensioniImmagine.Width,
                (double)dimensioniBox.Height / dimensioniImmagine.Height);
            double offsetX = (dimensioniBox.Width - dimensioniImmagine.Width * scala) / 2.0;
            double offsetY = (dimensioniBox.Height - dimensioniImmagine.Height * scala) / 2.0;

            double x = (click.X - offsetX) / scala;
            double y = (click.Y - offsetY) / scala;
            if (x < 0 || y < 0 || x > dimensioniImmagine.Width || y > dimensioniImmagine.Height)
            {
                return null;
            }

            return new PuntoImmagine(x, y);
        }
    }
}
