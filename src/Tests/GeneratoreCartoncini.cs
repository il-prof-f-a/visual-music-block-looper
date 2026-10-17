using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace VisualMusicBlockLooper.Tests
{
    /// <summary>
    /// Genera i cartoncini stampabili (QR VMBL1 su corpo colorato) con System.Drawing e ZXing.Net.
    /// Scala fisica: 1 passo di griglia = <c>passoMm</c> millimetri, altezza = <c>altezzaMm</c>, 300 dpi.
    /// Solo per uso di test e di preparazione del materiale: non fa parte dell'applicazione.
    /// </summary>
    internal static class GeneratoreCartoncini
    {
        /// <summary>Risoluzione di stampa dei PNG, in punti per pollice.</summary>
        public const int Dpi = 300;

        /// <summary>Larghezza di un passo di griglia in millimetri (1 step = 30 mm).</summary>
        public const double PassoMmDefault = 30;

        /// <summary>Altezza del cartoncino in millimetri.</summary>
        public const double AltezzaMmDefault = 40;

        /// <summary>Passi minimi di un cartoncino: sotto questa larghezza il QR non entra con la sua zona quieta.</summary>
        public const int PassiMinimi = 2;

        /// <summary>Passi di default per cartoncino.</summary>
        public const int PassiDefault = 2;

        /// <summary>Nota MIDI base per clip, come in mappatura-default.json (36 + idClip).</summary>
        public const int NotaBase = 36;

        /// <summary>Numero di clip stampati per colore.</summary>
        public const int ClipPerColore = 8;

        // Margini e zona quieta in millimetri / moduli QR.
        private const double MargineSinistroMm = 2;
        private const double MargineVerticaleMm = 4;
        private const double MargineDestroMm = 2;
        private const double SpessoreTagliaMm = 0.3;
        private const int ModuliZonaQuieta = 4;

        /// <summary>Colori di corpo dei sei cartoncini (indice = traccia), scelti per essere distinti alla camera.</summary>
        public static readonly Color[] ColoriCorpo =
        {
            Color.FromArgb(220, 40, 40),    // Rosso
            Color.FromArgb(255, 140, 0),    // Arancione
            Color.FromArgb(250, 210, 0),    // Giallo
            Color.FromArgb(40, 160, 70),    // Verde
            Color.FromArgb(30, 90, 200),    // Blu
            Color.FromArgb(130, 50, 170),   // Viola
        };

        /// <summary>Corpo neutro (grigio-azzurro, sotto la soglia di saturazione) per i cartoncini comando.</summary>
        public static readonly Color ColoreComando = Color.FromArgb(110, 120, 140);

        /// <summary>Corpo neutro viola-grigio dei cartoncini speciali (misura, collegamento, A-B), distinto da <see cref="ColoreComando"/>.</summary>
        public static readonly Color ColoreSpeciale = Color.FromArgb(255, 120, 100, 150);

        /// <summary>Nomi dei colori in italiano, nello stesso ordine di <see cref="ColoriCorpo"/>.</summary>
        public static readonly string[] NomiColori = { "Rosso", "Arancione", "Giallo", "Verde", "Blu", "Viola" };

        /// <summary>Crea il cartoncino di una nota: colore = traccia, clip = idClip.</summary>
        public static Bitmap CartoncinoNota(int traccia, int idClip, int passi = 2, double passoMm = PassoMmDefault,
            double altezzaMm = AltezzaMmDefault)
        {
            if (traccia < 0 || traccia >= ColoriCorpo.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(traccia));
            }

            string etichetta = $"{NomiColori[traccia]} {idClip} · nota {NotaBase + idClip}";
            return Disegna($"VMBL1;N;{idClip}", ColoriCorpo[traccia], etichetta, passi, passoMm, altezzaMm);
        }

        /// <summary>Crea il cartoncino di un comando (corpo neutro, payload VMBL1;C;...).</summary>
        public static Bitmap CartoncinoComando(string payload, string etichetta, int passi = 2,
            double passoMm = PassoMmDefault, double altezzaMm = AltezzaMmDefault)
        {
            return Disegna(payload, ColoreComando, "Comando " + etichetta, passi, passoMm, altezzaMm);
        }

        /// <summary>
        /// Crea un cartoncino speciale (corpo viola-grigio, payload VMBL1;S;...).
        /// Correzione d'errore L: i payload speciali (fino a 16 byte) entrano nella versione QR 1, come gli altri
        /// cartoncini; con il livello M il payload di MISURA passerebbe alla versione 2 e il rilevatore non lo legge
        /// quando più QR sono visibili insieme. La griglia resta di 21 moduli, quindi il layout non cambia.
        /// </summary>
        public static Bitmap CartoncinoSpeciale(string payload, string etichetta, int passi = 2,
            double passoMm = PassoMmDefault, double altezzaMm = AltezzaMmDefault)
        {
            return Disegna(payload, ColoreSpeciale, etichetta, passi, passoMm, altezzaMm,
                ZXing.QrCode.Internal.ErrorCorrectionLevel.L);
        }

        /// <summary>
        /// Elenco dei 22 cartoncini speciali: misura 2-4 (3), collegamento 1, 2, 3, 6 (4) e A-B a&lt;b in 1..6 (15).
        /// Ogni voce è (nome file, payload, etichetta).
        /// </summary>
        public static IReadOnlyList<(string Nome, string Payload, string Etichetta)> ElencoSpeciali()
        {
            var elenco = new List<(string Nome, string Payload, string Etichetta)>();

            foreach (int battiti in new[] { 2, 3, 4 })
            {
                elenco.Add(($"speciale-misura-{battiti}.png", $"VMBL1;S;MISURA;{battiti}", $"{battiti}/4"));
            }

            foreach (int righe in new[] { 1, 2, 3, 6 })
            {
                string etichetta = righe == 1 ? "1 riga" : $"{righe} righe";
                elenco.Add(($"speciale-link-{righe}.png", $"VMBL1;S;LINK;{righe}", etichetta));
            }

            for (int a = 1; a <= 6; a++)
            {
                for (int b = a + 1; b <= 6; b++)
                {
                    elenco.Add(($"speciale-ab-{a}-{b}.png", $"VMBL1;S;AB;{a};{b}", $"A-B {a}-{b}"));
                }
            }

            return elenco;
        }

        /// <summary>
        /// Rigenera tutti i cartoncini in <paramref name="cartella"/>: 6 colori x 8 clip di nota, 5 comandi
        /// e 22 cartoncini speciali (75 PNG in totale).
        /// </summary>
        public static void Genera(string cartella, double passoMm = PassoMmDefault, double altezzaMm = AltezzaMmDefault,
            int passi = PassiDefault)
        {
            Directory.CreateDirectory(cartella);

            for (int traccia = 0; traccia < ColoriCorpo.Length; traccia++)
            {
                for (int idClip = 0; idClip < ClipPerColore; idClip++)
                {
                    string nome = $"nota-{NomiColori[traccia].ToLowerInvariant()}-{idClip}.png";
                    Salva(CartoncinoNota(traccia, idClip, passi, passoMm, altezzaMm), Path.Combine(cartella, nome));
                }
            }

            Salva(CartoncinoComando("VMBL1;C;PLAY", "PLAY", passi, passoMm, altezzaMm), Path.Combine(cartella, "comando-play.png"));
            Salva(CartoncinoComando("VMBL1;C;STOP", "STOP", passi, passoMm, altezzaMm), Path.Combine(cartella, "comando-stop.png"));
            Salva(CartoncinoComando("VMBL1;C;TAP", "TAP", passi, passoMm, altezzaMm), Path.Combine(cartella, "comando-tap.png"));
            Salva(CartoncinoComando("VMBL1;C;MODO", "MODO", passi, passoMm, altezzaMm), Path.Combine(cartella, "comando-modo.png"));
            Salva(CartoncinoComando($"VMBL1;C;ONESHOT;{NotaBase}", $"ONESHOT · nota {NotaBase}", passi, passoMm, altezzaMm),
                Path.Combine(cartella, "comando-oneshot-36.png"));

            foreach (var (nome, payload, etichetta) in ElencoSpeciali())
            {
                Salva(CartoncinoSpeciale(payload, etichetta, passi, passoMm, altezzaMm), Path.Combine(cartella, nome));
            }
        }

        /// <summary>
        /// Disegna un cartoncino: QR bianco con zona quieta sul bordo sinistro, corpo colorato intorno,
        /// etichetta in alto a destra del corpo, area di disegno incorniciata, contorno di taglio nero.
        /// </summary>
        public static Bitmap Disegna(string payload, Color colore, string etichetta, int passi, double passoMm, double altezzaMm,
            ZXing.QrCode.Internal.ErrorCorrectionLevel? livelloEcc = null)
        {
            if (passi < PassiMinimi)
            {
                throw new ArgumentOutOfRangeException(nameof(passi), $"Almeno {PassiMinimi} passi per far entrare il QR.");
            }

            double larghezzaMm = passi * passoMm;
            double k = Dpi / 25.4;
            int wPx = (int)Math.Round(larghezzaMm * k);
            int hPx = (int)Math.Round(altezzaMm * k);

            Bitmap bmp = new Bitmap(wPx, hPx, PixelFormat.Format24bppRgb);
            bmp.SetResolution(Dpi, Dpi);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                using (Brush corpo = new SolidBrush(colore))
                {
                    g.FillRectangle(corpo, 0, 0, wPx, hPx);
                }

                // QR: quadrato bianco con zona quieta, a sinistra, centrato in verticale.
                double latoQrMm = altezzaMm - 2 * MargineVerticaleMm;
                RettangoloPx qr = new RettangoloPx(Mm(MargineSinistroMm, k), Mm(MargineVerticaleMm, k), Mm(latoQrMm, k));
                DisegnaQr(g, payload, qr, livelloEcc ?? ZXing.QrCode.Internal.ErrorCorrectionLevel.M);

                // Area di disegno incorniciata a destra del QR, sotto la fascia dell'etichetta.
                double xAreaMm = MargineSinistroMm + latoQrMm + 4;
                double xFineMm = larghezzaMm - MargineDestroMm;
                double yAreaMm = 11;
                double yFineMm = altezzaMm - MargineVerticaleMm;
                int xArea = Mm(xAreaMm, k), yArea = Mm(yAreaMm, k);
                int wArea = Mm(xFineMm - xAreaMm, k), hArea = Mm(yFineMm - yAreaMm, k);
                using (Brush bianco = new SolidBrush(Color.White))
                using (Pen cornice = new Pen(Color.Black, Math.Max(1f, (float)(0.25 * k))))
                {
                    g.FillRectangle(bianco, xArea, yArea, wArea, hArea);
                    g.DrawRectangle(cornice, xArea, yArea, wArea, hArea);
                }

                DisegnaEtichetta(g, etichetta, colore,
                    new RectangleF(Pt(xAreaMm, k), Pt(1.5, k), Pt(xFineMm - xAreaMm, k), Pt(8, k)));

                // Contorno di taglio: rettangolo nero sottile lungo il bordo del cartoncino.
                float spessore = Math.Max(1f, (float)(SpessoreTagliaMm * k));
                using (Pen taglio = new Pen(Color.Black, spessore))
                {
                    g.DrawRectangle(taglio, spessore / 2f, spessore / 2f, wPx - spessore, hPx - spessore);
                }
            }

            return bmp;
        }

        /// <summary>Salva il PNG a 300 dpi (metadati pHYs), senza ricampionare.</summary>
        public static void Salva(Bitmap bmp, string percorso)
        {
            using (bmp)
            {
                bmp.Save(percorso, ImageFormat.Png);
            }
        }

        /// <summary>Cartella <c>assets/cartoncini</c> del repository, risolta risalendo dalla cartella di esecuzione.</summary>
        public static string CartellaCartoncini()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null && !Directory.Exists(Path.Combine(dir, "assets", "config")))
            {
                dir = Path.GetDirectoryName(dir);
            }

            return dir == null ? null : Path.Combine(dir, "assets", "cartoncini");
        }

        private static void DisegnaQr(Graphics g, string testo, RettangoloPx area, ZXing.QrCode.Internal.ErrorCorrectionLevel livelloEcc)
        {
            var hint = new System.Collections.Generic.Dictionary<EncodeHintType, object>
            {
                { EncodeHintType.MARGIN, 0 },
                { EncodeHintType.ERROR_CORRECTION, livelloEcc },
            };
            BitMatrix m = new QRCodeWriter().encode(testo, BarcodeFormat.QR_CODE, 0, 0, hint);

            // Quadrato bianco di lato area.Lato (zona quieta inclusa); i moduli occupano il centro.
            using (Brush bianco = new SolidBrush(Color.White))
            using (Brush nero = new SolidBrush(Color.Black))
            {
                g.FillRectangle(bianco, area.X, area.Y, area.Lato, area.Lato);
                double modulo = area.Lato / (double)(Math.Max(m.Width, m.Height) + 2 * ModuliZonaQuieta);
                double origine = area.X + ModuliZonaQuieta * modulo;
                double origineY = area.Y + ModuliZonaQuieta * modulo;
                for (int j = 0; j < m.Height; j++)
                {
                    for (int i = 0; i < m.Width; i++)
                    {
                        if (!m[i, j])
                        {
                            continue;
                        }

                        int x0 = (int)Math.Round(origine + i * modulo);
                        int x1 = (int)Math.Round(origine + (i + 1) * modulo);
                        int y0 = (int)Math.Round(origineY + j * modulo);
                        int y1 = (int)Math.Round(origineY + (j + 1) * modulo);
                        g.FillRectangle(nero, x0, y0, x1 - x0, y1 - y0);
                    }
                }
            }
        }

        private static void DisegnaEtichetta(Graphics g, string testo, Color colore, RectangleF area)
        {
            Color inchiostro = Luminanza(colore) > 0.6 ? Color.Black : Color.White;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            // Dimensione del font scelta per stare nella fascia dell'etichetta.
            float punti = 12f;
            using (StringFormat formato = new StringFormat { Trimming = StringTrimming.None, FormatFlags = StringFormatFlags.NoWrap })
            {
                Font font;
                do
                {
                    font = new Font("Arial", punti, FontStyle.Bold, GraphicsUnit.Point);
                    SizeF misura = g.MeasureString(testo, font, new SizeF(float.MaxValue, float.MaxValue), formato);
                    bool entra = misura.Width * 1.08f <= area.Width && misura.Height <= area.Height;
                    if (entra || punti <= 4f)
                    {
                        break;
                    }

                    font.Dispose();
                    punti -= 0.5f;
                } while (true);

                using (font)
                using (Brush penna = new SolidBrush(inchiostro))
                {
                    g.DrawString(testo, font, penna, new PointF(area.X, area.Y));
                }
            }
        }

        // Luminanza relativa approssimata (0..1) per scegliere il colore dell'inchiostro.
        private static double Luminanza(Color c)
        {
            return (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
        }

        private static float Pt(double mm, double k)
        {
            return (float)(mm * k);
        }

        private static int Mm(double mm, double k)
        {
            return (int)Math.Round(mm * k);
        }

        private struct RettangoloPx
        {
            public RettangoloPx(int x, int y, int lato)
            {
                X = x;
                Y = y;
                Lato = lato;
            }

            public int X { get; }

            public int Y { get; }

            public int Lato { get; }
        }
    }
}
