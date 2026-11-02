using System;
using OpenCvSharp;

namespace VisualMusicBlockLooper.Vision
{
    /// <summary>
    /// Spline TPS tavolo ideale → tavolo visto attraverso l'omografia. L'inversa è ricavata
    /// dalla stessa funzione, evitando fit separati e coordinate discordanti nel riconoscimento.
    /// </summary>
    internal sealed class DeformazioneCalibrazione
    {
        private readonly PuntoTavolo[] riferimenti;
        private readonly double[,] coefficienti;

        public DeformazioneCalibrazione(PuntoTavolo[] riferimenti, PuntoTavolo[] osservati)
        {
            if (riferimenti.Length != 12 || osservati.Length != 12)
                throw new ArgumentException("Servono dodici riferimenti perimetrali.");
            ControllaPerimetro(riferimenti);
            ControllaPerimetro(osservati);
            this.riferimenti = (PuntoTavolo[])riferimenti.Clone();
            int n = riferimenti.Length;
            coefficienti = new double[n + 3, 2];
            using (var sistema = new Mat(n + 3, n + 3, MatType.CV_64FC1, Scalar.All(0)))
            using (var valori = new Mat(n + 3, 2, MatType.CV_64FC1, Scalar.All(0)))
            using (var soluzione = new Mat())
            {
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        double dx = riferimenti[i].X - riferimenti[j].X;
                        double dy = riferimenti[i].Y - riferimenti[j].Y;
                        sistema.Set(i, j, Nucleo(dx * dx + dy * dy));
                    }
                    double[] affine = { 1, riferimenti[i].X, riferimenti[i].Y };
                    for (int j = 0; j < 3; j++)
                    {
                        sistema.Set(i, n + j, affine[j]);
                        sistema.Set(n + j, i, affine[j]);
                    }
                    valori.Set(i, 0, osservati[i].X - riferimenti[i].X);
                    valori.Set(i, 1, osservati[i].Y - riferimenti[i].Y);
                }
                if (!Cv2.Solve(sistema, valori, soluzione, DecompTypes.LU))
                    throw new ArgumentException("Riferimenti degeneri: impossibile interpolare la curva.");
                for (int i = 0; i < n + 3; i++)
                    for (int j = 0; j < 2; j++)
                    {
                        double v = soluzione.At<double>(i, j);
                        if (!Finito(v)) throw new ArgumentException("Deformazione non finita.");
                        coefficienti[i, j] = v;
                    }
            }

            // Controllo indipendente dall'errore sui dodici riferimenti (nullo per costruzione).
            // Si verifica anche il margine esterno alla cornice dei bersagli, fino all'intero tavolo.
            for (int y = 0; y <= 40; y++)
                for (int x = 0; x <= 40; x++)
                {
                    Valuta(x / 40.0, y / 40.0, out _, out _, out double a, out double b, out double c, out double d);
                    double det = a * d - b * c;
                    if (!Finito(det) || det < 0.05 || det > 20)
                        throw new ArgumentException("La deformazione contiene pieghe o zone troppo compresse: ricontrolla i riferimenti.");
                }
        }

        public PuntoTavolo Applica(PuntoTavolo p)
        {
            Valuta(p.X, p.Y, out double x, out double y, out _, out _, out _, out _);
            return new PuntoTavolo(x, y);
        }

        public PuntoTavolo Inverti(PuntoTavolo p)
        {
            double x = p.X, y = p.Y;
            for (int iterazione = 0; iterazione < 30; iterazione++)
            {
                Valuta(x, y, out double u, out double v, out double a, out double b, out double c, out double d);
                double ex = u - p.X, ey = v - p.Y, errore = ex * ex + ey * ey;
                if (errore < 1e-20) return new PuntoTavolo(x, y);
                double det = a * d - b * c;
                if (!Finito(errore) || Math.Abs(det) < 1e-10) break;
                double dx = (d * ex - b * ey) / det, dy = (a * ey - c * ex) / det;
                bool migliora = false;
                for (double passo = 1; passo >= 1.0 / 128; passo /= 2)
                {
                    double nx = x - passo * dx, ny = y - passo * dy;
                    Valuta(nx, ny, out u, out v, out _, out _, out _, out _);
                    double nuovo = (u - p.X) * (u - p.X) + (v - p.Y) * (v - p.Y);
                    if (nuovo < errore) { x = nx; y = ny; migliora = true; break; }
                }
                if (!migliora) break;
            }
            throw new InvalidOperationException("Impossibile invertire la calibrazione curva in questo punto.");
        }

        private void Valuta(double x, double y, out double u, out double v,
            out double a, out double b, out double c, out double d)
        {
            int n = riferimenti.Length;
            u = x + coefficienti[n, 0] + coefficienti[n + 1, 0] * x + coefficienti[n + 2, 0] * y;
            v = y + coefficienti[n, 1] + coefficienti[n + 1, 1] * x + coefficienti[n + 2, 1] * y;
            a = 1 + coefficienti[n + 1, 0]; b = coefficienti[n + 2, 0];
            c = coefficienti[n + 1, 1]; d = 1 + coefficienti[n + 2, 1];
            for (int i = 0; i < n; i++)
            {
                double dx = x - riferimenti[i].X, dy = y - riferimenti[i].Y;
                double r2 = dx * dx + dy * dy;
                if (r2 < 1e-24) continue;
                double log = Math.Log(r2), k = 0.5 * r2 * log;
                u += coefficienti[i, 0] * k; v += coefficienti[i, 1] * k;
                double gx = dx * (log + 1), gy = dy * (log + 1);
                a += coefficienti[i, 0] * gx; b += coefficienti[i, 0] * gy;
                c += coefficienti[i, 1] * gx; d += coefficienti[i, 1] * gy;
            }
        }

        private static double Nucleo(double r2) => r2 < 1e-24 ? 0 : 0.5 * r2 * Math.Log(r2);
        private static bool Finito(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        private static void ControllaPerimetro(PuntoTavolo[] punti)
        {
            for (int i = 0; i < punti.Length; i++)
            {
                if (!Finito(punti[i].X) || !Finito(punti[i].Y))
                    throw new ArgumentException("I riferimenti devono avere coordinate finite.");
                for (int j = i + 1; j < punti.Length; j++)
                {
                    double dx = punti[i].X - punti[j].X, dy = punti[i].Y - punti[j].Y;
                    if (dx * dx + dy * dy < 1e-10)
                        throw new ArgumentException("Riferimenti coincidenti: ripeti la raccolta.");
                    if (j == i + 1 || (i == 0 && j == punti.Length - 1)) continue;
                    var a = punti[i]; var b = punti[(i + 1) % punti.Length];
                    var c = punti[j]; var d = punti[(j + 1) % punti.Length];
                    if (Incrocia(a, b, c, d))
                        throw new ArgumentException("Ordine dei riferimenti errato: il perimetro si incrocia.");
                }
            }
        }

        private static double Lato(PuntoTavolo a, PuntoTavolo b, PuntoTavolo p) =>
            (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);

        private static bool Incrocia(PuntoTavolo a, PuntoTavolo b, PuntoTavolo c, PuntoTavolo d)
        {
            if (Math.Max(a.X, b.X) < Math.Min(c.X, d.X) || Math.Max(c.X, d.X) < Math.Min(a.X, b.X)
                || Math.Max(a.Y, b.Y) < Math.Min(c.Y, d.Y) || Math.Max(c.Y, d.Y) < Math.Min(a.Y, b.Y)) return false;
            return Lato(a, b, c) * Lato(a, b, d) <= 0 && Lato(c, d, a) * Lato(c, d, b) <= 0;
        }
    }
}
