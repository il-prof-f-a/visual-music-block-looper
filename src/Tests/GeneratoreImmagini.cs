using System;
using System.Collections.Generic;
using System.IO;
using OpenCvSharp;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace VisualMusicBlockLooper.Tests
{
    /// <summary>Costruisce in modo deterministico le immagini sintetiche di prova (QR con ZXing.Net).</summary>
    internal static class GeneratoreImmagini
    {
        public const int Larghezza = 640;
        public const int Altezza = 480;
        public const int PassoModulo = 4;
        public const int ZonaQuieta = 16;

        public static Mat QrSingolo(string testo)
        {
            var img = new Mat(new Size(300, 300), MatType.CV_8UC3, Scalar.All(255));
            DisegnaQr(img, testo, 100, 100);
            return img;
        }

        /// <summary>Cartoncino: corpo colorato con il QR (su fondo bianco con zona quieta) all'estremita sinistra.</summary>
        public static void DisegnaCartoncino(Mat img, Rect corpo, Scalar coloreBgr, string testo)
        {
            Cv2.Rectangle(img, corpo, coloreBgr, -1);
            DisegnaQr(img, testo, corpo.X + 10 + ZonaQuieta, corpo.Y + (corpo.Height - 21 * PassoModulo) / 2);
        }

        public static Mat DueCartoncini()
        {
            var img = new Mat(new Size(Larghezza, Altezza), MatType.CV_8UC3, Scalar.All(255));
            DisegnaCartoncino(img, new Rect(40, 40, 160, 160), new Scalar(0, 0, 255), "VMBL1;N;12");
            DisegnaCartoncino(img, new Rect(260, 260, 340, 160), new Scalar(0, 160, 0), "VMBL1;N;14");
            return img;
        }

        /// <summary>Disegna un QR (con zona quieta bianca) con angolo in alto a sinistra dei moduli in (x, y).</summary>
        private static void DisegnaQr(Mat img, string testo, int x, int y)
        {
            var scrittore = new QRCodeWriter();
            var hint = new Dictionary<EncodeHintType, object>
            {
                { EncodeHintType.MARGIN, 0 },
                { EncodeHintType.ERROR_CORRECTION, ZXing.QrCode.Internal.ErrorCorrectionLevel.M },
            };
            BitMatrix m = scrittore.encode(testo, BarcodeFormat.QR_CODE, 0, 0, hint);
            Cv2.Rectangle(img, new Rect(x - ZonaQuieta, y - ZonaQuieta, m.Width * PassoModulo + 2 * ZonaQuieta,
                m.Height * PassoModulo + 2 * ZonaQuieta), Scalar.All(255), -1);
            for (int j = 0; j < m.Height; j++)
            {
                for (int i = 0; i < m.Width; i++)
                {
                    if (m[i, j])
                    {
                        Cv2.Rectangle(img, new Rect(x + i * PassoModulo, y + j * PassoModulo, PassoModulo, PassoModulo), Scalar.All(0), -1);
                    }
                }
            }
        }

        public static string CartellaAsset()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null && !Directory.Exists(Path.Combine(dir, "assets", "prova")))
            {
                dir = Path.GetDirectoryName(dir);
            }

            return dir == null ? null : Path.Combine(dir, "assets", "prova");
        }
    }
}
