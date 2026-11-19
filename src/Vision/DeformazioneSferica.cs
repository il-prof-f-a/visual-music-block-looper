using System;
using OpenCvSharp;

namespace VisualMusicBlockLooper.Vision
{
    /// <summary>
    /// Modello globale di lente assimilata a una sfera: la tavola viene proiettata su un piano
    /// ideale, poi la sfera applica la proiezione equisolida/equidistante/stereografica.
    /// I dodici punti stimano centro, focale e omografia; il calcolo per pixel è O(1).
    /// </summary>
    internal sealed class DeformazioneSferica
    {
        private const double MassimoAngolo = Math.PI / 2 * 0.999;
        private readonly double[] matrice;
        private readonly double[] inversa;
        private readonly double centroX, centroY, focale;
        private readonly TipoProiezioneSferica tipo;

        private DeformazioneSferica(double[] matrice, double centroX, double centroY,
            double focale, TipoProiezioneSferica tipo)
        {
            this.matrice = matrice;
            this.inversa = InvertiMatrice(matrice);
            this.centroX = centroX;
            this.centroY = centroY;
            this.focale = focale;
            this.tipo = tipo;
        }

        public double Focale => focale;
        public PuntoImmagine Centro => new PuntoImmagine(centroX, centroY);
        public TipoProiezioneSferica Tipo => tipo;

        public static DeformazioneSferica Calcola(PuntoTavolo[] tavolo, PuntoImmagine[] camera,
            int larghezza, int altezza, ParametriCorrezioneBordo parametri)
        {
            if (tavolo == null || camera == null || tavolo.Length != 12 || camera.Length != 12)
                throw new ArgumentException("Servono dodici riferimenti per la proiezione sferica.");
            if (larghezza <= 0 || altezza <= 0)
                throw new ArgumentException("La proiezione sferica richiede la risoluzione della camera.");
            ParametriCorrezioneBordo p = (parametri ?? new ParametriCorrezioneBordo()).Clona().Normalizzati();
            double cx = p.CentroX > 0 ? p.CentroX : larghezza / 2.0;
            double cy = p.CentroY > 0 ? p.CentroY : altezza / 2.0;
            double f = p.Focale > 0 ? p.Focale : 0;
            TipoProiezioneSferica tipo = p.TipoSferico;

            if (f > 0)
            {
                double[] h = ProvaHomografia(tavolo, camera, cx, cy, f, tipo, out double rms);
                if (h == null) throw new ArgumentException("Proiezione sferica non calcolabile con la focale indicata.");
                return new DeformazioneSferica(h, cx, cy, f, tipo);
            }

            double diagonale = Math.Sqrt((double)larghezza * larghezza + (double)altezza * altezza);
            double fMin = 0.35 * diagonale, fMax = 1.4 * diagonale;
            double miglioreF = 0, miglioreX = cx, miglioreY = cy, miglioreErrore = double.MaxValue;
            double[] migliore = null;

            // Prima la focale con il centro dell'immagine, poi un affinamento locale del centro.
            for (int i = 0; i <= 20; i++)
            {
                double provaF = fMin + (fMax - fMin) * i / 20.0;
                double[] h = ProvaHomografia(tavolo, camera, miglioreX, miglioreY, provaF, tipo, out double rms);
                if (h != null && rms < miglioreErrore)
                {
                    miglioreErrore = rms; migliore = h; miglioreF = provaF;
                }
            }

            if (migliore == null)
                throw new ArgumentException("Proiezione sferica: impossibile stimare centro e focale.");

            double passoF = (fMax - fMin) / 20.0;
            for (int ix = -1; ix <= 1; ix++)
                for (int iy = -1; iy <= 1; iy++)
                {
                    double sx = cx + ix * larghezza * 0.04;
                    double sy = cy + iy * altezza * 0.04;
                    for (int i = -3; i <= 3; i++)
                    {
                        double provaF = miglioreF + i * passoF / 3.0;
                        if (provaF <= 0) continue;
                        double[] h = ProvaHomografia(tavolo, camera, sx, sy, provaF, tipo, out double rms);
                        if (h != null && rms < miglioreErrore)
                        {
                            miglioreErrore = rms; migliore = h; miglioreF = provaF;
                            miglioreX = sx; miglioreY = sy;
                        }
                    }
                }

            // Ultimo affinamento della focale attorno al centro scelto.
            for (int i = -3; i <= 3; i++)
            {
                double provaF = miglioreF + i * passoF / 9.0;
                if (provaF <= 0) continue;
                double[] h = ProvaHomografia(tavolo, camera, miglioreX, miglioreY, provaF, tipo, out double rms);
                if (h != null && rms < miglioreErrore)
                {
                    miglioreErrore = rms; migliore = h; miglioreF = provaF;
                }
            }

            if (miglioreErrore > 80)
                throw new ArgumentException("Proiezione sferica: i dodici punti non sono compatibili con il modello.");
            var modello = new DeformazioneSferica(migliore, miglioreX, miglioreY, miglioreF, tipo);
            modello.Valida();
            return modello;
        }

        public PuntoImmagine Applica(PuntoTavolo p)
        {
            PuntoTavolo q = Proietta(matrice, p.X, p.Y);
            double dx = q.X - centroX, dy = q.Y - centroY;
            double r = Math.Sqrt(dx * dx + dy * dy);
            if (r < 1e-12) return new PuntoImmagine(centroX, centroY);
            double theta = Math.Atan2(r, focale);
            double rd = focale * Proiezione(theta, tipo);
            double fattore = rd / r;
            return new PuntoImmagine(centroX + dx * fattore, centroY + dy * fattore);
        }

        public PuntoTavolo Inverti(PuntoImmagine camera)
        {
            double dx = camera.X - centroX, dy = camera.Y - centroY;
            double rd = Math.Sqrt(dx * dx + dy * dy);
            if (rd < 1e-12) return Proietta(inversa, centroX, centroY);
            double theta = ProiezioneInversa(rd, focale, tipo);
            double ru = focale * Math.Tan(Math.Min(theta, MassimoAngolo));
            double fattore = ru / rd;
            PuntoTavolo q = Proietta(inversa, centroX + dx * fattore, centroY + dy * fattore);
            return q;
        }

        private void Valida()
        {
            for (int y = 0; y <= 10; y++)
                for (int x = 0; x <= 10; x++)
                {
                    var p = new PuntoTavolo(x / 10.0, y / 10.0);
                    PuntoImmagine c = Applica(p);
                    if (!Finito(c.X) || !Finito(c.Y)) throw new ArgumentException("Proiezione sferica non finita.");
                    PuntoTavolo q = Inverti(c);
                    double ex = q.X - p.X, ey = q.Y - p.Y;
                    if (!Finito(q.X) || !Finito(q.Y) || ex * ex + ey * ey > 1e-8)
                        throw new ArgumentException("Proiezione sferica non invertibile in modo stabile.");
                }
        }

        private static double Proiezione(double theta, TipoProiezioneSferica tipo)
        {
            switch (tipo)
            {
                case TipoProiezioneSferica.Equidistante: return theta;
                case TipoProiezioneSferica.Stereografico: return 2 * Math.Tan(theta / 2);
                default: return 2 * Math.Sin(theta / 2);
            }
        }

        private static double ProiezioneInversa(double rd, double focale, TipoProiezioneSferica tipo)
        {
            switch (tipo)
            {
                case TipoProiezioneSferica.Equidistante: return rd / focale;
                case TipoProiezioneSferica.Stereografico: return 2 * Math.Atan(rd / (2 * focale));
                default: return 2 * Math.Asin(Math.Min(0.999999, rd / (2 * focale)));
            }
        }

        private static double[] ProvaHomografia(PuntoTavolo[] tavolo, PuntoImmagine[] camera,
            double cx, double cy, double focale, TipoProiezioneSferica tipo, out double errore)
        {
            errore = double.MaxValue;
            var ideali = new Point2d[12];
            var tavoloPunti = new Point2d[12];
            for (int i = 0; i < 12; i++)
            {
                double dx = camera[i].X - cx, dy = camera[i].Y - cy;
                double rd = Math.Sqrt(dx * dx + dy * dy);
                if (rd < 1e-9) return null;
                double theta = ProiezioneInversa(rd, focale, tipo);
                double ru = focale * Math.Tan(Math.Min(theta, MassimoAngolo));
                double fattore = ru / rd;
                ideali[i] = new Point2d(cx + dx * fattore, cy + dy * fattore);
                tavoloPunti[i] = new Point2d(tavolo[i].X, tavolo[i].Y);
            }
            double[] h = Homografia(tavoloPunti, ideali);
            if (h == null) return null;
            double somma = 0, massimo = 0;
            for (int i = 0; i < 12; i++)
            {
                PuntoTavolo q = Proietta(h, tavolo[i].X, tavolo[i].Y);
                double dx = q.X - cx, dy = q.Y - cy;
                double r = Math.Sqrt(dx * dx + dy * dy);
                double theta = Math.Atan2(r, focale);
                double rd = focale * Proiezione(theta, tipo);
                double predX = cx + dx * rd / Math.Max(r, 1e-12);
                double predY = cy + dy * rd / Math.Max(r, 1e-12);
                double ex = predX - camera[i].X, ey = predY - camera[i].Y;
                double e2 = ex * ex + ey * ey;
                somma += e2; massimo = Math.Max(massimo, Math.Sqrt(e2));
            }
            if (!Finito(somma) || !Finito(massimo)) return null;
            errore = Math.Sqrt(somma / 12);
            return h;
        }

        private static double[] Homografia(Point2d[] sorgente, Point2d[] destinazione)
        {
            using (Mat h = Cv2.FindHomography(sorgente, destinazione, HomographyMethods.None))
            {
                if (h == null || h.Empty() || h.Rows != 3 || h.Cols != 3) return null;
                var valori = new double[9];
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 3; c++) valori[r * 3 + c] = h.At<double>(r, c);
                return MatriceValida(valori) ? valori : null;
            }
        }

        private static PuntoTavolo Proietta(double[] m, double x, double y)
        {
            double w = m[6] * x + m[7] * y + m[8];
            if (!Finito(w) || Math.Abs(w) < 1e-12) throw new ArgumentException("Punto sferico non finito.");
            return new PuntoTavolo((m[0] * x + m[1] * y + m[2]) / w,
                (m[3] * x + m[4] * y + m[5]) / w);
        }

        private static double[] InvertiMatrice(double[] m)
        {
            double d = m[0] * (m[4] * m[8] - m[5] * m[7])
                - m[1] * (m[3] * m[8] - m[5] * m[6])
                + m[2] * (m[3] * m[7] - m[4] * m[6]);
            if (!Finito(d) || Math.Abs(d) < 1e-12) throw new ArgumentException("Proiezione sferica degenere.");
            return new[]
            {
                (m[4]*m[8]-m[5]*m[7])/d, (m[2]*m[7]-m[1]*m[8])/d, (m[1]*m[5]-m[2]*m[4])/d,
                (m[5]*m[6]-m[3]*m[8])/d, (m[0]*m[8]-m[2]*m[6])/d, (m[2]*m[3]-m[0]*m[5])/d,
                (m[3]*m[7]-m[4]*m[6])/d, (m[1]*m[6]-m[0]*m[7])/d, (m[0]*m[4]-m[1]*m[3])/d
            };
        }

        private static bool MatriceValida(double[] m)
        {
            for (int i = 0; i < 9; i++) if (!Finito(m[i])) return false;
            double d = m[0] * (m[4] * m[8] - m[5] * m[7])
                - m[1] * (m[3] * m[8] - m[5] * m[6])
                + m[2] * (m[3] * m[7] - m[4] * m[6]);
            return Finito(d) && Math.Abs(d) > 1e-12;
        }

        private static bool Finito(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
