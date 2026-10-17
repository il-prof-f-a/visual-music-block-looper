using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OpenCvSharp;

namespace VisualMusicBlockLooper.Vision
{
    /// <summary>
    /// Omografia che mappa i punti dell'immagine della camera sul sistema di coordinate del tavolo.
    /// La matrice 3x3 e' memorizzata in ordine row-major come double[9].
    /// </summary>
    public sealed class Calibrazione
    {
        private const string Intestazione = "VMBL-CAL1";
        private const int NumeroPuntiMinimi = 4;
        private const int ValoriMatrice = 9;
        private const double SogliaW = 1e-12;
        private const double SogliaDeterminante = 1e-12;

        // Soglia RANSAC in unita' del tavolo (coordinate normalizzate 0..1).
        private const double SogliaRansacTavolo = 0.02;

        private readonly double[] matrice;

        private Calibrazione(double[] matrice)
        {
            this.matrice = matrice;
        }

        public static Calibrazione Calcola(PuntoImmagine[] puntiCamera, PuntoTavolo[] puntiTavolo)
        {
            if (puntiCamera == null)
            {
                throw new ArgumentNullException(nameof(puntiCamera));
            }

            if (puntiTavolo == null)
            {
                throw new ArgumentNullException(nameof(puntiTavolo));
            }

            if (puntiCamera.Length < NumeroPuntiMinimi || puntiTavolo.Length < NumeroPuntiMinimi)
            {
                throw new ArgumentException(
                    "Servono almeno 4 punti di calibrazione (camera: " + puntiCamera.Length
                    + ", tavolo: " + puntiTavolo.Length + ").", nameof(puntiCamera));
            }

            if (puntiCamera.Length != puntiTavolo.Length)
            {
                throw new ArgumentException(
                    "Il numero di punti camera (" + puntiCamera.Length
                    + ") e tavolo (" + puntiTavolo.Length + ") deve coincidere.", nameof(puntiTavolo));
            }

            if (puntiCamera.Any(p => !Finito(p.X) || !Finito(p.Y)) || puntiTavolo.Any(p => !Finito(p.X) || !Finito(p.Y)))
            {
                throw new ArgumentException("I punti di calibrazione devono avere coordinate finite.", nameof(puntiCamera));
            }

            Point2d[] sorgente = puntiCamera.Select(p => new Point2d(p.X, p.Y)).ToArray();
            Point2d[] destinazione = puntiTavolo.Select(p => new Point2d(p.X, p.Y)).ToArray();

            // Con esattamente 4 punti l'omografia e' determinata: nessun RANSAC necessario.
            // Con piu' punti RANSAC scarta gli outlier; la soglia e' espressa in unita' del tavolo.
            double[] valori;
            if (sorgente.Length == NumeroPuntiMinimi)
            {
                valori = LeggiMatrice(Cv2.FindHomography(sorgente, destinazione, HomographyMethods.None));
            }
            else
            {
                valori = LeggiMatrice(Cv2.FindHomography(
                    sorgente, destinazione, HomographyMethods.Ransac, SogliaRansacTavolo));
            }

            if (valori == null || !MatriceValida(valori))
            {
                throw new ArgumentException("Punti di calibrazione degeneri: impossibile calcolare l'omografia.", nameof(puntiCamera));
            }

            return new Calibrazione(valori);
        }

        public PuntoTavolo DaCameraATavolo(PuntoImmagine p)
        {
            double x = matrice[0] * p.X + matrice[1] * p.Y + matrice[2];
            double y = matrice[3] * p.X + matrice[4] * p.Y + matrice[5];
            double w = matrice[6] * p.X + matrice[7] * p.Y + matrice[8];

            if (Math.Abs(w) < SogliaW)
            {
                throw new InvalidOperationException("Il punto camera non ha un'immagine finita sul tavolo (divisione per zero omogenea).");
            }

            return new PuntoTavolo(x / w, y / w);
        }

        public void Salva(string percorso)
        {
            if (percorso == null)
            {
                throw new ArgumentNullException(nameof(percorso));
            }

            // "G17" garantisce il round-trip esatto di un double su net472.
            string valori = string.Join(";", matrice.Select(v => v.ToString("G17", CultureInfo.InvariantCulture)));
            File.WriteAllText(percorso, Intestazione + Environment.NewLine + valori + Environment.NewLine, Encoding.UTF8);
        }

        public static Calibrazione Carica(string percorso)
        {
            if (percorso == null)
            {
                throw new ArgumentNullException(nameof(percorso));
            }

            if (!File.Exists(percorso))
            {
                throw new FileNotFoundException("File di calibrazione non trovato: " + percorso, percorso);
            }

            string[] righe = File.ReadAllLines(percorso, Encoding.UTF8)
                .Where(r => r.Trim().Length > 0)
                .ToArray();

            if (righe.Length != 2 || righe[0].Trim() != Intestazione)
            {
                throw new InvalidDataException("Formato del file di calibrazione non valido: intestazione o righe inattese.");
            }

            string[] parti = righe[1].Trim().Split(';');
            if (parti.Length != ValoriMatrice)
            {
                throw new InvalidDataException("Il file di calibrazione deve contenere 9 valori numerici.");
            }

            double[] valori = new double[ValoriMatrice];
            for (int i = 0; i < ValoriMatrice; i++)
            {
                if (!double.TryParse(parti[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                    || !Finito(v))
                {
                    throw new InvalidDataException("Valore non valido nel file di calibrazione: '" + parti[i] + "'.");
                }

                valori[i] = v;
            }

            if (!MatriceValida(valori))
            {
                throw new InvalidDataException("Matrice di calibrazione degenere nel file.");
            }

            return new Calibrazione(valori);
        }

        private static double[] LeggiMatrice(Mat h)
        {
            using (h)
            {
                if (h == null || h.Empty() || h.Rows != 3 || h.Cols != 3)
                {
                    return null;
                }

                double[] valori = new double[ValoriMatrice];
                for (int r = 0; r < 3; r++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        valori[r * 3 + c] = h.At<double>(r, c);
                    }
                }

                return valori;
            }
        }

        private static bool MatriceValida(double[] m)
        {
            return m.All(Finito) && Math.Abs(Determinante(m)) >= SogliaDeterminante;
        }

        private static bool Finito(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v);
        }

        private static double Determinante(double[] m)
        {
            return m[0] * (m[4] * m[8] - m[5] * m[7])
                 - m[1] * (m[3] * m[8] - m[5] * m[6])
                 + m[2] * (m[3] * m[7] - m[4] * m[6]);
        }
    }
}
