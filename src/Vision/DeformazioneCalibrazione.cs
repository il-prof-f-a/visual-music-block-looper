using System;
using OpenCvSharp;

namespace VisualMusicBlockLooper.Vision
{
    /// <summary>
    /// Correzione della calibrazione: TPS sui dodici riferimenti perimetrali, attenuato verso
    /// l'omografia al centro per non incurvare le linee che nella camera sono già dritte.
    /// L'inversa è ricavata dalla stessa mappa, evitando fit separati e coordinate discordanti.
    /// </summary>
    internal sealed class DeformazioneCalibrazione
    {
        // Il TPS passa esattamente per i dodici click, ma al centro può incurvare linee
        // che nella camera sono già dritte. Nella fascia di bordo si usa il TPS per
        // rispettare i riferimenti; al centro si usa l'omografia, che conserva le rette.
        private readonly ParametriCorrezioneBordo parametri;
        private readonly PuntoTavolo[] riferimenti;
        private readonly double[,] coefficienti;

        public DeformazioneCalibrazione(PuntoTavolo[] riferimenti, PuntoTavolo[] osservati,
            ParametriCorrezioneBordo parametri = null)
        {
            if (riferimenti.Length != 12 || osservati.Length != 12)
                throw new ArgumentException("Servono dodici riferimenti perimetrali.");
            this.parametri = (parametri ?? Calibrazione.ParametriBordo ?? new ParametriCorrezioneBordo())
                .Clona().Normalizzati();
            if (!this.parametri.Validi())
                throw new ArgumentException("La fascia di correzione del bordo non è valida.");
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
            ValidaMisto();
        }

        public PuntoTavolo Applica(PuntoTavolo p)
        {
            Valuta(p.X, p.Y, out double x, out double y, out _, out _, out _, out _);
            return new PuntoTavolo(x, y);
        }

        /// <summary>
        /// TPS nella fascia di bordo e omografia al centro. Il peso vale 1 sui dodici
        /// riferimenti (distanza 0,03 dal bordo) e 0 quando la distanza supera 0,12.
        /// </summary>
        public PuntoTavolo ApplicaMisto(PuntoTavolo p)
        {
            double w = PesoBordo(p.X, p.Y);
            if (w <= 0) return new PuntoTavolo(p.X, p.Y);
            PuntoTavolo tps = Applica(p);
            if (w >= 1) return tps;
            return new PuntoTavolo(p.X + w * (tps.X - p.X), p.Y + w * (tps.Y - p.Y));
        }

        /// <summary>Inversa della mappa mista; al centro coincide con l'omografia.</summary>
        public PuntoTavolo InvertiMisto(PuntoTavolo p)
        {
            double x = p.X, y = p.Y;
            for (int iterazione = 0; iterazione < 30; iterazione++)
            {
                PuntoTavolo f = ApplicaMisto(new PuntoTavolo(x, y));
                double ex = f.X - p.X, ey = f.Y - p.Y, errore = ex * ex + ey * ey;
                if (errore < 1e-20) return new PuntoTavolo(x, y);

                const double passoDerivata = 1e-5;
                PuntoTavolo fx = ApplicaMisto(new PuntoTavolo(x + passoDerivata, y));
                PuntoTavolo fy = ApplicaMisto(new PuntoTavolo(x, y + passoDerivata));
                double a = (fx.X - f.X) / passoDerivata, b = (fy.X - f.X) / passoDerivata;
                double c = (fx.Y - f.Y) / passoDerivata, d = (fy.Y - f.Y) / passoDerivata;
                double det = a * d - b * c;
                if (!Finito(det) || Math.Abs(det) < 1e-10) break;
                double dx = (d * ex - b * ey) / det, dy = (a * ey - c * ex) / det;
                bool migliora = false;
                for (double passo = 1; passo >= 1.0 / 128; passo /= 2)
                {
                    double nx = x - passo * dx, ny = y - passo * dy;
                    PuntoTavolo nf = ApplicaMisto(new PuntoTavolo(nx, ny));
                    double nuovo = (nf.X - p.X) * (nf.X - p.X) + (nf.Y - p.Y) * (nf.Y - p.Y);
                    if (nuovo < errore) { x = nx; y = ny; migliora = true; break; }
                }
                if (!migliora) break;
            }

            // Ripiego a punto fisso: observed = p + w(p) * (TPS(p) - p).
            double px = p.X, py = p.Y;
            for (int iterazione = 0; iterazione < 80; iterazione++)
            {
                double peso = PesoBordo(px, py);
                PuntoTavolo tps = Applica(new PuntoTavolo(px, py));
                double nx = p.X - peso * (tps.X - px), ny = p.Y - peso * (tps.Y - py);
                if ((nx - px) * (nx - px) + (ny - py) * (ny - py) < 1e-24)
                    return new PuntoTavolo(nx, ny);
                px = nx; py = ny;
            }
            throw new InvalidOperationException("Impossibile invertire la calibrazione curva in questo punto.");
        }

        private double PesoBordo(double x, double y)
        {
            double dx = x * parametri.FattoreX;
            double dx2 = (1 - x) * parametri.FattoreX;
            double dy = y * parametri.FattoreY;
            double dy2 = (1 - y) * parametri.FattoreY;
            double distanza;
            if (parametri.Modello == ModelloCorrezioneBordo.BordoSmussato && parametri.Smussamento > 0)
            {
                double minimo = Math.Min(Math.Min(dx, dx2), Math.Min(dy, dy2));
                double k = parametri.Smussamento;
                double somma = Math.Exp(-k * (dx - minimo)) + Math.Exp(-k * (dx2 - minimo))
                    + Math.Exp(-k * (dy - minimo)) + Math.Exp(-k * (dy2 - minimo));
                distanza = minimo - Math.Log(somma) / k;
            }
            else
            {
                distanza = Math.Min(Math.Min(dx, dx2), Math.Min(dy, dy2));
            }

            if (distanza <= parametri.Pieno) return 1;
            if (distanza >= parametri.Esterno) return 0;
            double t = (distanza - parametri.Pieno) / (parametri.Esterno - parametri.Pieno);
            return 1 - t * t * (3 - 2 * t);
        }

        private DeformazioneCalibrazione(DeformazioneCalibrazione origine, ParametriCorrezioneBordo nuovi)
        {
            parametri = (nuovi ?? origine.parametri).Clona().Normalizzati();
            if (!parametri.Validi())
                throw new ArgumentException("La fascia di correzione del bordo non è valida.");
            riferimenti = (PuntoTavolo[])origine.riferimenti.Clone();
            coefficienti = (double[,])origine.coefficienti.Clone();
            ValidaMisto();
        }

        /// <summary>Copia la spline e cambia solo i parametri della fascia di bordo.</summary>
        public DeformazioneCalibrazione ClonaConParametri(ParametriCorrezioneBordo nuovi)
            => new DeformazioneCalibrazione(this, nuovi);

        /// <summary>Controlla che la mappa mista non contenga pieghe e che l'inversa converga.</summary>
        private void ValidaMisto()
        {
            const double h = 1e-4;
            for (int y = 1; y < 40; y++)
                for (int x = 1; x < 40; x++)
                {
                    double px = x / 40.0, py = y / 40.0;
                    double w = PesoBordo(px, py);
                    Valuta(px, py, out double tx, out double ty, out double a, out double b, out double c, out double d);
                    double gx = (PesoBordo(px + h, py) - PesoBordo(px - h, py)) / (2 * h);
                    double gy = (PesoBordo(px, py + h) - PesoBordo(px, py - h)) / (2 * h);
                    double j00 = 1 + w * (a - 1) + (tx - px) * gx;
                    double j01 = w * b + (tx - px) * gy;
                    double j10 = w * c + (ty - py) * gx;
                    double j11 = 1 + w * (d - 1) + (ty - py) * gy;
                    double det = j00 * j11 - j01 * j10;
                    if (!Finito(det) || det < 0.05 || det > 20)
                        throw new ArgumentException("La correzione di bordo contiene pieghe o zone troppo compresse: ricontrolla i riferimenti.");
                }

            // Verifica pratica dell'inversa, compresi i punti nella fascia di transizione.
            double[] campioni = { 0.03, 0.06, 0.09, 0.20, 0.50, 0.80, 0.97 };
            foreach (double cx in campioni)
                foreach (double cy in campioni)
                {
                    var p = new PuntoTavolo(cx, cy);
                    PuntoTavolo camera = ApplicaMisto(p);
                    PuntoTavolo riletto;
                    try { riletto = InvertiMisto(camera); }
                    catch (InvalidOperationException)
                    { throw new ArgumentException("La correzione di bordo non è invertibile in modo stabile."); }
                    double ex = riletto.X - p.X, ey = riletto.Y - p.Y;
                    if (ex * ex + ey * ey > 1e-16)
                        throw new ArgumentException("La correzione di bordo non è invertibile in modo stabile.");
                }
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
