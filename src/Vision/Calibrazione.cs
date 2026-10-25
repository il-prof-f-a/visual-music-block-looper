using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OpenCvSharp;

namespace VisualMusicBlockLooper.Vision
{
    /// <summary>Esito del confronto tra la risoluzione della calibrazione e quella della camera.</summary>
    public enum EsitoRisoluzione
    {
        /// <summary>Le risoluzioni coincidono (o quella della camera non è nota).</summary>
        Compatibile,

        /// <summary>La calibrazione non riporta la risoluzione per cui è stata calcolata.</summary>
        Sconosciuta,

        /// <summary>La calibrazione è stata calcolata per una risoluzione diversa.</summary>
        Diversa
    }

    /// <summary>
    /// Omografia che mappa i punti dell'immagine della camera sul sistema di coordinate del tavolo.
    /// La matrice 3x3 e' memorizzata in ordine row-major come double[9].
    /// </summary>
    public sealed class Calibrazione
    {
        private const string Intestazione = "VMBL-CAL2";
        private const string IntestazioneVecchia = "VMBL-CAL1";
        private const int NumeroPuntiMinimi = 4;
        private const int ValoriMatrice = 9;
        private const double SogliaW = 1e-12;
        private const double SogliaDeterminante = 1e-12;

        // Soglia RANSAC in unita' del tavolo (coordinate normalizzate 0..1).
        private const double SogliaRansacTavolo = 0.02;

        private readonly double[] matrice;

        private Calibrazione(double[] matrice, int larghezza, int altezza)
        {
            this.matrice = matrice;
            Larghezza = larghezza;
            Altezza = altezza;
        }

        /// <summary>Larghezza in pixel dell'immagine per cui la calibrazione è stata calcolata; 0 se ignota.</summary>
        public int Larghezza { get; }

        /// <summary>Altezza in pixel dell'immagine per cui la calibrazione è stata calcolata; 0 se ignota.</summary>
        public int Altezza { get; }

        /// <summary>
        /// Confronta la risoluzione della calibrazione con quella attuale della camera. Con una camera di
        /// dimensione ignota (0) non si può confrontare e l'esito è <see cref="EsitoRisoluzione.Compatibile"/>.
        /// </summary>
        public EsitoRisoluzione VerificaRisoluzione(int larghezzaCamera, int altezzaCamera)
        {
            if (larghezzaCamera <= 0 || altezzaCamera <= 0)
            {
                return EsitoRisoluzione.Compatibile;
            }

            if (Larghezza <= 0 || Altezza <= 0)
            {
                return EsitoRisoluzione.Sconosciuta;
            }

            return Larghezza == larghezzaCamera && Altezza == altezzaCamera
                ? EsitoRisoluzione.Compatibile
                : EsitoRisoluzione.Diversa;
        }

        /// <param name="larghezza">Larghezza in pixel dell'immagine dei punti camera (0 se ignota).</param>
        /// <param name="altezza">Altezza in pixel dell'immagine dei punti camera (0 se ignota).</param>
        public static Calibrazione Calcola(
            PuntoImmagine[] puntiCamera, PuntoTavolo[] puntiTavolo, int larghezza = 0, int altezza = 0)
        {
            if (larghezza < 0 || altezza < 0 || (larghezza == 0) != (altezza == 0))
            {
                throw new ArgumentException("La risoluzione deve essere nulla (ignota) o con entrambe le dimensioni positive.", nameof(larghezza));
            }

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

            return new Calibrazione(valori, larghezza, altezza);
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

        /// <summary>
        /// Maschera (CV_8UC1, 255 dentro) dei pixel della camera che cadono nell'area calibrata del tavolo,
        /// ristretta di <paramref name="restringi"/> pixel per escludere il bordo/la cornice.
        /// </summary>
        /// <param name="larghezzaCamera">Larghezza in pixel del frame della camera.</param>
        /// <param name="altezzaCamera">Altezza in pixel del frame della camera.</param>
        /// <param name="restringi">Pixel di cui restringere la maschera.</param>
        /// <returns>Una nuova <see cref="Mat"/>: il chiamante la rilascia.</returns>
        public Mat MascheraTavolo(int larghezzaCamera, int altezzaCamera, int restringi = 0)
        {
            const int Lato = 100;
            double[] riga0 = { matrice[0] * Lato, matrice[1] * Lato, matrice[2] * Lato };
            double[] riga1 = { matrice[3] * Lato, matrice[4] * Lato, matrice[5] * Lato };
            double[] riga2 = { matrice[6], matrice[7], matrice[8] };

            using (Mat tavolo = new Mat(Lato, Lato, MatType.CV_8UC1, Scalar.All(255)))
            using (Mat trasformazione = new Mat(3, 3, MatType.CV_64FC1))
            {
                for (int c = 0; c < 3; c++)
                {
                    trasformazione.Set(0, c, riga0[c]);
                    trasformazione.Set(1, c, riga1[c]);
                    trasformazione.Set(2, c, riga2[c]);
                }

                Mat maschera = new Mat();
                try
                {
                    // Con InverseMap la trasformazione porta i pixel della camera sui pixel del tavolo.
                    Cv2.WarpPerspective(
                        tavolo, maschera, trasformazione, new Size(larghezzaCamera, altezzaCamera),
                        InterpolationFlags.Linear | InterpolationFlags.WarpInverseMap, BorderTypes.Constant, Scalar.All(0));
                    Cv2.Threshold(maschera, maschera, 127, 255, ThresholdTypes.Binary);
                    if (restringi > 0)
                    {
                        using (Mat nucleo = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2 * restringi + 1, 2 * restringi + 1)))
                        {
                            Cv2.Erode(maschera, maschera, nucleo);
                        }
                    }

                    return maschera;
                }
                catch
                {
                    maschera.Dispose();
                    throw;
                }
            }
        }

        /// <summary>
        /// Raddrizza l'immagine della camera sull'area del tavolo definita dalla calibrazione: il risultato è
        /// una vista dall'alto, "stirata", in cui il rettangolo calibrato riempie tutta l'immagine
        /// (tavolo (0,0) in alto a sinistra, (1,1) in basso a destra). Ciò che sta fuori dall'area
        /// calibrata non compare.
        /// </summary>
        /// <param name="sorgente">Frame della camera (non viene modificato).</param>
        /// <param name="larghezza">Larghezza in pixel dell'immagine raddrizzata.</param>
        /// <param name="altezza">Altezza in pixel dell'immagine raddrizzata.</param>
        /// <returns>Una nuova <see cref="Mat"/>: il chiamante la rilascia.</returns>
        public Mat Raddrizza(Mat sorgente, int larghezza, int altezza)
        {
            if (sorgente == null || sorgente.Empty())
            {
                throw new ArgumentException("Il frame da raddrizzare è nullo o vuoto.", nameof(sorgente));
            }

            if (larghezza <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(larghezza), "La larghezza deve essere positiva.");
            }

            if (altezza <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(altezza), "L'altezza deve essere positiva.");
            }

            // M = S * H: H porta la camera nelle coordinate del tavolo (0..1), S le scala in pixel di uscita.
            double[] riga0 = { matrice[0] * larghezza, matrice[1] * larghezza, matrice[2] * larghezza };
            double[] riga1 = { matrice[3] * altezza, matrice[4] * altezza, matrice[5] * altezza };
            double[] riga2 = { matrice[6], matrice[7], matrice[8] };

            using (Mat trasformazione = new Mat(3, 3, MatType.CV_64FC1))
            {
                for (int c = 0; c < 3; c++)
                {
                    trasformazione.Set(0, c, riga0[c]);
                    trasformazione.Set(1, c, riga1[c]);
                    trasformazione.Set(2, c, riga2[c]);
                }

                Mat uscita = new Mat();
                try
                {
                    Cv2.WarpPerspective(
                        sorgente, uscita, trasformazione, new Size(larghezza, altezza),
                        InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));
                    return uscita;
                }
                catch
                {
                    uscita.Dispose();
                    throw;
                }
            }
        }

        public void Salva(string percorso)
        {
            if (percorso == null)
            {
                throw new ArgumentNullException(nameof(percorso));
            }

            // "G17" garantisce il round-trip esatto di un double su net472.
            string valori = string.Join(";", matrice.Select(v => v.ToString("G17", CultureInfo.InvariantCulture)));
            string risoluzione = Larghezza.ToString(CultureInfo.InvariantCulture) + ";" + Altezza.ToString(CultureInfo.InvariantCulture);
            File.WriteAllText(
                percorso,
                Intestazione + Environment.NewLine + valori + Environment.NewLine + risoluzione + Environment.NewLine,
                Encoding.UTF8);
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

            bool nuovoFormato = righe.Length > 0 && righe[0].Trim() == Intestazione;
            bool vecchioFormato = righe.Length > 0 && righe[0].Trim() == IntestazioneVecchia;
            if ((!nuovoFormato && !vecchioFormato) || righe.Length != (nuovoFormato ? 3 : 2))
            {
                throw new InvalidDataException("Formato del file di calibrazione non valido: intestazione o righe inattese.");
            }

            int larghezza = 0;
            int altezza = 0;
            if (nuovoFormato)
            {
                string[] dimensioni = righe[2].Trim().Split(';');
                if (dimensioni.Length != 2
                    || !int.TryParse(dimensioni[0], NumberStyles.None, CultureInfo.InvariantCulture, out larghezza)
                    || !int.TryParse(dimensioni[1], NumberStyles.None, CultureInfo.InvariantCulture, out altezza)
                    || (larghezza == 0) != (altezza == 0))
                {
                    throw new InvalidDataException("Risoluzione non valida nel file di calibrazione: attese due dimensioni intere positive (o 0;0).");
                }
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

            return new Calibrazione(valori, larghezza, altezza);
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
