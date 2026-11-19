using System;
using System.Drawing;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.App
{
    /// <summary>
    /// Colore della nota ricavato dal QR, non dal pixel. Riproduce la formula di
    /// <c>tools/genera_note_cromatiche.py</c>: MIDI → frequenza ottica → lunghezza d'onda → sRGB.
    /// </summary>
    public static class ColoriNote
    {
        /// <summary>Opacità dei riquadri-guida: 50%, così il colore resta leggero sul proiettore.</summary>
        public const int OpacitaRiquadri = 128;

        public static Color DaIdClip(int idClip) => DaMidi(36 + idClip);

        /// <summary>Stesso colore con l'opacità dei riquadri, senza schiarirlo in modo fisso.</summary>
        public static Color Sfumato(Color colore) =>
            Color.FromArgb(OpacitaRiquadri, colore.R, colore.G, colore.B);

        public static Color DaMidi(int midi)
        {
            double t = Math.Max(0, Math.Min(1, (midi - 21) / 87.0));
            // Il generatore interpola la frequenza ottica, non la lunghezza d'onda:
            // 1/λ = (1-t)/700 + t/400. La linearità di λ produrrebbe colori sbagliati
            // (per esempio Do5 verde invece del ciano del cartoncino).
            double inversoLunghezza = (1 - t) / 700.0 + t / 400.0;
            double lunghezzaOnda = 1.0 / inversoLunghezza;
            return Spettro(lunghezzaOnda);
        }

        /// <summary>Colore convenzionale dei cartoncini senza nota: A-B (ripetizione) è violetto.</summary>
        public static Color DaSpeciale(TipoImpostazione tipo)
        {
            switch (tipo)
            {
                case TipoImpostazione.Misura: return Color.FromArgb(70, 130, 180);
                case TipoImpostazione.Collegamento: return Color.FromArgb(40, 160, 70);
                case TipoImpostazione.IntervalloAb: return Color.FromArgb(130, 50, 170);
                default: return Color.FromArgb(135, 115, 150);
            }
        }

        public static Color DaComando(TipoComando tipo)
        {
            return Color.FromArgb(110, 120, 140);
        }

        private static Color Spettro(double w)
        {
            double r, g, b;
            if (w < 440) { r = (440 - w) / 60; g = 0; b = 1; }
            else if (w < 490) { r = 0; g = (w - 440) / 50; b = 1; }
            else if (w < 510) { r = 0; g = 1; b = (510 - w) / 20; }
            else if (w < 580) { r = (w - 510) / 70; g = 1; b = 0; }
            else if (w < 645) { r = 1; g = (645 - w) / 65; b = 0; }
            else { r = 1; g = 0; b = 0; }
            return Color.FromArgb(255,
                Canale(r), Canale(g), Canale(b));
        }

        private static int Canale(double valore)
        {
            double v = Math.Pow(Math.Max(0, Math.Min(1, valore)), 0.8) * 0.72;
            return (int)Math.Round(Math.Max(0, Math.Min(1, v)) * 255);
        }
    }
}
