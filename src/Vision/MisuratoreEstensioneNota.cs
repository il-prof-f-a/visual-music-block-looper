using System;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Vision
{
    /// <summary>Segue il corpo nel verso musicale del tavolo, anche con camera capovolta o curva.</summary>
    public static class MisuratoreEstensioneNota
    {
        public static double Misura(Mat maschera, Calibrazione calibrazione, RettangoloTavolo qr, ConfigGriglia griglia = null)
        {
            if (qr.Larghezza <= 0 || qr.Altezza <= 0) return qr.X;
            griglia = griglia ?? new ConfigGriglia { Area = LayoutTavolo.AreaGrigliaDefault };
            double centro = qr.Y + qr.Altezza / 2;
            var area = griglia.Area;
            if (griglia.Tracce <= 0 || area.Altezza <= 0 || centro < area.Y || centro >= area.Y + area.Altezza) return qr.X;
            double altezzaRiga = area.Altezza / griglia.Tracce;
            int riga = (int)Math.Floor((centro-area.Y)/altezzaRiga);
            double bordoPixel = 1.0 / maschera.Rows;
            double minimoY = Math.Max(qr.Y - .18*qr.Altezza, area.Y + riga*altezzaRiga + bordoPixel);
            double massimoY = Math.Min(qr.Y + 1.18*qr.Altezza, area.Y + (riga+1)*altezzaRiga - bordoPixel);
            if (massimoY <= minimoY) return qr.X;
            double passo = 1.0 / Math.Max(maschera.Cols, maschera.Rows);
            double inizio = qr.X + 1.2 * qr.Larghezza;
            double ultimo = qr.X;
            int vuoti = 0;
            // La fascia include la zona bianca: ai lati della figura musicale rimane il corpo colorato.
            // Una fascia stretta evita di assorbire cartoncini sulla riga adiacente.
            for (double x = inizio; x <= 1 && vuoti <= 8; x += passo)
            {
                int pieni = 0;
                for (int i = 0; i < 9; i++)
                {
                    double y = minimoY + (massimoY-minimoY)*i/8;
                    if (y < 0 || y > 1) continue;
                    PuntoImmagine p = calibrazione.DaTavoloACamera(new PuntoTavolo(x, y));
                    int cx = (int)Math.Round(p.X), cy = (int)Math.Round(p.Y);
                    if (cx >= 0 && cy >= 0 && cx < maschera.Cols && cy < maschera.Rows
                        && maschera.At<byte>(cy, cx) != 0) pieni++;
                }
                if (pieni >= 2) { ultimo = x; vuoti = 0; }
                else vuoti++;
            }
            return ultimo;
        }
    }
}
