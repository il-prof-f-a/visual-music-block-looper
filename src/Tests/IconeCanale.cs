using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace VisualMusicBlockLooper.Tests
{
    /// <summary>Illustrazioni vettoriali stampabili per i cartoncini canale.</summary>
    internal static class IconeCanale
    {
        private const int Lato = 512;
        private static readonly Color Inchiostro = Color.FromArgb(42, 53, 69);
        private static readonly Color Legno = Color.FromArgb(202, 137, 72);
        private static readonly Color LegnoChiaro = Color.FromArgb(235, 190, 126);
        private static readonly Color Ottone = Color.FromArgb(218, 164, 50);
        private static readonly Color Argento = Color.FromArgb(181, 197, 207);
        private static readonly Color Turchese = Color.FromArgb(55, 157, 174);
        private static readonly Color Corallo = Color.FromArgb(211, 91, 78);
        private static readonly Color Blu = Color.FromArgb(60, 105, 157);
        private static readonly Color Crema = Color.FromArgb(250, 247, 239);

        public static void Disegna(Graphics destinazione, int canale, RectangleF area)
        {
            if (canale < 1 || canale > 15 || area.Width <= 0 || area.Height <= 0)
            {
                return;
            }

            using (Bitmap icona = new Bitmap(Lato, Lato))
            using (Graphics g = Graphics.FromImage(icona))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                switch (canale)
                {
                    case 1: Pianoforte(g); break;
                    case 2: ChitarraAcustica(g); break;
                    case 3: BassoElettrico(g); break;
                    case 4: Archi(g); break;
                    case 5: Tromba(g); break;
                    case 6: Sax(g); break;
                    case 7: Organo(g); break;
                    case 8: Sintetizzatore(g); break;
                    case 9: Percussioni(g); break;
                    case 10: Batteria(g); break;
                    case 11: ChitarraElettrica(g); break;
                    case 12: Pad(g); break;
                    case 13: Violino(g); break;
                    case 14: Flauto(g); break;
                    case 15: Effetti(g); break;
                }

                InterpolationMode precedente = destinazione.InterpolationMode;
                try
                {
                    destinazione.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    float scala = Math.Min(area.Width / Lato, area.Height / Lato);
                    float larghezza = Lato * scala;
                    float altezza = Lato * scala;
                    RectangleF destinazioneIcona = new RectangleF(
                        area.X + (area.Width - larghezza) / 2,
                        area.Y + (area.Height - altezza) / 2,
                        larghezza, altezza);
                    destinazione.DrawImage(icona, destinazioneIcona);
                }
                finally
                {
                    destinazione.InterpolationMode = precedente;
                }
            }
        }

        private static void Pianoforte(Graphics g)
        {
            Poligono(g, Inchiostro, new PointF(86, 142), new PointF(398, 142), new PointF(426, 172), new PointF(426, 383), new PointF(86, 383));
            Poligono(g, Legno, new PointF(74, 129), new PointF(377, 129), new PointF(398, 142), new PointF(86, 142));
            Linea(g, Crema, 16, new PointF(99, 181), new PointF(386, 181));
            Rettangolo(g, LegnoChiaro, Inchiostro, 10, 101, 194, 292, 161);
            Tastiera(g, 105, 198, 284, 151, 14);
            Rettangolo(g, Legno, Inchiostro, 12, 94, 363, 323, 27);
            Rettangolo(g, Inchiostro, Inchiostro, 1, 112, 388, 34, 34);
            Rettangolo(g, Inchiostro, Inchiostro, 1, 365, 388, 34, 34);
            Ellisse(g, Ottone, Inchiostro, 8, 167, 387, 17, 17);
            Ellisse(g, Ottone, Inchiostro, 8, 283, 387, 17, 17);
        }

        private static void ChitarraAcustica(Graphics g)
        {
            Collo(g, 228, 43, 56, 247, LegnoChiaro);
            CorpoChitarra(g, Legno);
            Rettangolo(g, Inchiostro, Inchiostro, 1, 230, 36, 53, 60);
            for (int i = 0; i < 6; i++)
            {
                float x = 237 + i * 9;
                Linea(g, Argento, 2.5f, new PointF(x, 59), new PointF(256 + (i - 2.5f) * 7, 387));
            }
            Ellisse(g, LegnoChiaro, Inchiostro, 9, 217, 274, 79, 79);
            Ellisse(g, Inchiostro, Inchiostro, 1, 232, 289, 49, 49);
            Rettangolo(g, Inchiostro, Inchiostro, 1, 220, 366, 72, 19);
            Linea(g, LegnoChiaro, 10, new PointF(221, 365), new PointF(291, 365));
        }

        private static void BassoElettrico(Graphics g)
        {
            Collo(g, 235, 25, 45, 270, Blu);
            Rettangolo(g, Inchiostro, Inchiostro, 1, 232, 18, 45, 69);
            Poligono(g, Corallo, new PointF(244, 251), new PointF(279, 257), new PointF(301, 293), new PointF(341, 312),
                new PointF(360, 354), new PointF(344, 395), new PointF(304, 414), new PointF(261, 401),
                new PointF(224, 419), new PointF(185, 398), new PointF(173, 359), new PointF(195, 322), new PointF(227, 303));
            for (int i = 0; i < 4; i++)
            {
                float x = 241 + i * 10;
                Linea(g, Argento, 3, new PointF(x, 50), new PointF(252 + i * 4, 378));
            }
            Rettangolo(g, Argento, Inchiostro, 7, 215, 330, 80, 22);
            Rettangolo(g, Inchiostro, Inchiostro, 1, 219, 361, 73, 19);
            Ellisse(g, Ottone, Inchiostro, 6, 309, 376, 17, 17);
            Ellisse(g, Ottone, Inchiostro, 6, 331, 365, 15, 15);
        }

        private static void Archi(Graphics g)
        {
            DisegnaViolino(g, 62, 58, 0.55f, LegnoChiaro);
            DisegnaViolino(g, 204, 25, 0.75f, Legno);
            DisegnaViolino(g, 332, 60, 0.52f, Ottone);
            Linea(g, Inchiostro, 8, new PointF(87, 421), new PointF(421, 105));
            Linea(g, Crema, 3, new PointF(87, 421), new PointF(421, 105));
        }

        private static void Tromba(Graphics g)
        {
            Poligono(g, Ottone, new PointF(335, 225), new PointF(402, 202), new PointF(452, 180),
                new PointF(465, 235), new PointF(452, 291), new PointF(402, 269), new PointF(335, 257));
            Ellisse(g, LegnoChiaro, Inchiostro, 9, 414, 197, 50, 100);
            Linea(g, Inchiostro, 37, new PointF(82, 264), new PointF(385, 264));
            Linea(g, Ottone, 25, new PointF(82, 264), new PointF(385, 264));
            Rettangolo(g, Argento, Inchiostro, 8, 55, 251, 37, 26);
            for (int i = 0; i < 3; i++)
            {
                int x = 174 + i * 63;
                Rettangolo(g, Ottone, Inchiostro, 8, x, 212, 26, 77);
                Ellisse(g, LegnoChiaro, Inchiostro, 7, x - 4, 184, 34, 28);
                Linea(g, LegnoChiaro, 8, new PointF(x + 13, 288), new PointF(x + 13, 313));
            }
            Curva(g, Ottone, 18, new PointF(186, 312), new PointF(205, 355), new PointF(278, 352), new PointF(300, 313));
            Linea(g, LegnoChiaro, 11, new PointF(120, 245), new PointF(347, 245));
        }

        private static void Sax(Graphics g)
        {
            Curva(g, Ottone, 24, new PointF(283, 82), new PointF(390, 87), new PointF(363, 167), new PointF(330, 194));
            Curva(g, Ottone, 46, new PointF(330, 190), new PointF(300, 214), new PointF(326, 309), new PointF(256, 357));
            Curva(g, Ottone, 42, new PointF(256, 357), new PointF(191, 401), new PointF(182, 335), new PointF(215, 309));
            Poligono(g, LegnoChiaro, new PointF(188, 315), new PointF(255, 342), new PointF(219, 415), new PointF(145, 383));
            Ellisse(g, Ottone, Inchiostro, 8, 156, 365, 75, 46);
            Curva(g, Inchiostro, 13, new PointF(273, 103), new PointF(322, 102), new PointF(333, 129), new PointF(328, 165));
            for (int i = 0; i < 5; i++)
            {
                int y = 205 + i * 35;
                Linea(g, Inchiostro, 7, new PointF(267 - i * 5, y), new PointF(326 + i * 3, y + 5));
                Ellisse(g, Crema, Inchiostro, 6, 300 + i * 2, y - 9, 25, 25);
            }
            Ellisse(g, Crema, Inchiostro, 6, 221, 291, 21, 21);
            Ellisse(g, Crema, Inchiostro, 6, 248, 308, 21, 21);
            Ellisse(g, Crema, Inchiostro, 6, 275, 295, 21, 21);
        }

        private static void Organo(Graphics g)
        {
            Rettangolo(g, Legno, Inchiostro, 16, 87, 190, 338, 211);
            Rettangolo(g, LegnoChiaro, Inchiostro, 11, 108, 112, 294, 90);
            for (int i = 0; i < 7; i++)
            {
                int x = 128 + i * 40;
                Rettangolo(g, Argento, Inchiostro, 7, x, 60 - (i % 3) * 8, 23, 76 + (i % 3) * 8);
            }
            Rettangolo(g, Crema, Inchiostro, 7, 108, 231, 296, 59);
            Tastiera(g, 116, 234, 280, 53, 12);
            Rettangolo(g, Crema, Inchiostro, 7, 108, 304, 296, 59);
            Tastiera(g, 116, 307, 280, 53, 12);
            Rettangolo(g, Inchiostro, Inchiostro, 1, 104, 374, 303, 23);
            for (int i = 0; i < 7; i++)
            {
                Linea(g, LegnoChiaro, 5, new PointF(129 + i * 42, 399), new PointF(111 + i * 45, 447));
            }
        }

        private static void Sintetizzatore(Graphics g)
        {
            Poligono(g, Blu, new PointF(72, 189), new PointF(443, 189), new PointF(419, 389), new PointF(93, 389));
            Rettangolo(g, Inchiostro, Inchiostro, 1, 103, 215, 301, 51);
            Rettangolo(g, Turchese, Crema, 8, 128, 224, 102, 34);
            for (int i = 0; i < 8; i++)
            {
                Ellisse(g, i % 2 == 0 ? Ottone : Corallo, Inchiostro, 5, 253 + i * 20, 226, 15, 15);
            }
            Tastiera(g, 104, 285, 307, 74, 16);
            Linea(g, Crema, 9, new PointF(116, 174), new PointF(172, 143));
            Linea(g, Crema, 9, new PointF(172, 143), new PointF(230, 171));
            Linea(g, Crema, 9, new PointF(230, 171), new PointF(288, 135));
            Linea(g, Crema, 9, new PointF(288, 135), new PointF(344, 164));
        }

        private static void Percussioni(Graphics g)
        {
            Conga(g, 5, 118, 0.57f, Legno);
            Tamburello(g, 212, 45, 0.50f);
            Maraca(g, 246, 265, 0.41f, Turchese);
        }

        private static void Batteria(Graphics g)
        {
            Linea(g, Inchiostro, 12, new PointF(115, 159), new PointF(115, 406));
            Linea(g, Inchiostro, 12, new PointF(376, 160), new PointF(376, 413));
            Piatto(g, 41, 102, 146, 184, Ottone);
            Piatto(g, 337, 95, 159, 192, Ottone);
            Tamburo(g, 160, 199, 117, 92, Turchese);
            Tamburo(g, 260, 201, 119, 91, Corallo);
            Tamburo(g, 101, 312, 101, 81, LegnoChiaro);
            Ellisse(g, Blu, Inchiostro, 14, 196, 286, 176, 160);
            Ellisse(g, Crema, Inchiostro, 12, 215, 305, 138, 122);
            Ellisse(g, Blu, Inchiostro, 8, 252, 339, 63, 56);
            Linea(g, Inchiostro, 13, new PointF(105, 395), new PointF(76, 441));
            Linea(g, Inchiostro, 13, new PointF(344, 420), new PointF(391, 444));
        }

        private static void ChitarraElettrica(Graphics g)
        {
            Collo(g, 230, 39, 54, 248, Crema);
            Poligono(g, Corallo, new PointF(246, 238), new PointF(285, 246), new PointF(305, 275), new PointF(352, 286),
                new PointF(343, 322), new PointF(304, 331), new PointF(293, 387), new PointF(256, 416),
                new PointF(222, 391), new PointF(214, 344), new PointF(175, 326), new PointF(167, 290), new PointF(212, 273));
            for (int i = 0; i < 6; i++)
            {
                float x = 239 + i * 8;
                Linea(g, Argento, 2.5f, new PointF(x, 54), new PointF(252 + (i - 2.5f) * 5, 397));
            }
            Rettangolo(g, Inchiostro, Crema, 4, 218, 314, 76, 19);
            Rettangolo(g, Inchiostro, Crema, 4, 222, 343, 70, 18);
            Rettangolo(g, Argento, Inchiostro, 6, 229, 377, 57, 12);
            for (int i = 0; i < 3; i++) Ellisse(g, Ottone, Inchiostro, 5, 303 + i * 15, 378, 12, 12);
        }

        private static void Pad(Graphics g)
        {
            Rettangolo(g, Blu, Inchiostro, 17, 68, 135, 376, 254);
            Rettangolo(g, Inchiostro, Inchiostro, 1, 101, 168, 134, 188);
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    Color colore = new[] { Turchese, Corallo, Ottone, Blu, Turchese, Corallo, Ottone, Blu, Turchese }[r * 3 + c];
                    Rettangolo(g, colore, Crema, 6, 111 + c * 41, 180 + r * 53, 31, 39);
                }
            }
            Rettangolo(g, LegnoChiaro, Crema, 5, 264, 171, 147, 77);
            Curva(g, Crema, 9, new PointF(279, 309), new PointF(321, 267), new PointF(360, 358), new PointF(399, 313));
            Curva(g, Ottone, 8, new PointF(281, 339), new PointF(328, 301), new PointF(364, 385), new PointF(403, 340));
        }

        private static void Violino(Graphics g)
        {
            DisegnaViolino(g, 0, 0, 1, Legno);
        }

        private static void Flauto(Graphics g)
        {
            Linea(g, Inchiostro, 33, new PointF(78, 279), new PointF(433, 279));
            Linea(g, Argento, 22, new PointF(78, 268), new PointF(433, 268));
            Ellisse(g, Crema, Inchiostro, 8, 59, 249, 39, 39);
            for (int i = 0; i < 7; i++)
            {
                float x = 133 + i * 41;
                Ellisse(g, Crema, Inchiostro, 7, x, 246 + (i % 2) * 16, 21, 21);
                Linea(g, Inchiostro, 5, new PointF(x + 10, 263 + (i % 2) * 16), new PointF(x + 10, 310));
            }
            Ellisse(g, Ottone, Inchiostro, 7, 416, 251, 25, 35);
            Linea(g, Crema, 5, new PointF(88, 229), new PointF(121, 229));
        }

        private static void Effetti(Graphics g)
        {
            Poligono(g, Blu, new PointF(91, 205), new PointF(177, 205), new PointF(268, 136), new PointF(294, 136),
                new PointF(294, 376), new PointF(268, 376), new PointF(177, 307), new PointF(91, 307));
            Rettangolo(g, Turchese, Inchiostro, 11, 67, 197, 48, 118);
            Curva(g, Corallo, 17, new PointF(333, 194), new PointF(395, 219), new PointF(395, 293), new PointF(333, 318));
            Curva(g, Ottone, 17, new PointF(355, 151), new PointF(459, 194), new PointF(459, 313), new PointF(355, 357));
            Stella(g, Ottone, 283, 70, 35);
            Stella(g, Corallo, 372, 386, 25);
            Ellisse(g, Crema, Inchiostro, 7, 109, 229, 20, 20);
        }

        private static void DisegnaViolino(Graphics g, float x, float y, float scala, Color legno)
        {
            GraphicsState stato = g.Save();
            g.TranslateTransform(x, y);
            g.ScaleTransform(scala, scala);
            Collo(g, 231, 37, 50, 254, LegnoChiaro);
            CorpoViolino(g, legno);
            Rettangolo(g, Inchiostro, Inchiostro, 1, 234, 32, 46, 58);
            for (int i = 0; i < 4; i++)
            {
                float dx = 245 + i * 8;
                Linea(g, Argento, 2.5f, new PointF(dx, 57), new PointF(256 + (i - 1.5f) * 6, 401));
            }
            Rettangolo(g, Inchiostro, Inchiostro, 1, 224, 367, 65, 18);
            Fforo(g, 207, 287);
            Fforo(g, 291, 287);
            Linea(g, Inchiostro, 8, new PointF(353, 80), new PointF(165, 439));
            Linea(g, LegnoChiaro, 3, new PointF(353, 80), new PointF(165, 439));
            g.Restore(stato);
        }

        private static void CorpoChitarra(Graphics g, Color legno)
        {
            using (GraphicsPath path = new GraphicsPath())
            using (SolidBrush fill = new SolidBrush(legno))
            using (Pen outline = Penna(Inchiostro, 15))
            {
                path.StartFigure();
                path.AddBezier(new PointF(256, 447), new PointF(196, 445), new PointF(132, 414), new PointF(131, 355));
                path.AddBezier(new PointF(131, 355), new PointF(128, 315), new PointF(171, 296), new PointF(184, 267));
                path.AddBezier(new PointF(184, 267), new PointF(197, 235), new PointF(166, 205), new PointF(183, 170));
                path.AddBezier(new PointF(183, 170), new PointF(200, 135), new PointF(237, 151), new PointF(256, 184));
                path.AddBezier(new PointF(256, 184), new PointF(275, 151), new PointF(312, 135), new PointF(329, 170));
                path.AddBezier(new PointF(329, 170), new PointF(346, 205), new PointF(315, 235), new PointF(328, 267));
                path.AddBezier(new PointF(328, 267), new PointF(341, 296), new PointF(384, 315), new PointF(381, 355));
                path.AddBezier(new PointF(381, 355), new PointF(380, 414), new PointF(316, 445), new PointF(256, 447));
                path.CloseFigure();
                g.FillPath(fill, path);
                g.DrawPath(outline, path);
            }
        }

        private static void CorpoViolino(Graphics g, Color legno)
        {
            using (GraphicsPath path = new GraphicsPath())
            using (SolidBrush fill = new SolidBrush(legno))
            using (Pen outline = Penna(Inchiostro, 15))
            {
                path.StartFigure();
                path.AddBezier(new PointF(256, 439), new PointF(212, 434), new PointF(182, 408), new PointF(180, 375));
                path.AddBezier(new PointF(180, 375), new PointF(177, 348), new PointF(199, 329), new PointF(207, 304));
                path.AddBezier(new PointF(207, 304), new PointF(215, 279), new PointF(197, 254), new PointF(201, 229));
                path.AddBezier(new PointF(201, 229), new PointF(205, 203), new PointF(229, 189), new PointF(256, 189));
                path.AddBezier(new PointF(256, 189), new PointF(283, 189), new PointF(307, 203), new PointF(311, 229));
                path.AddBezier(new PointF(311, 229), new PointF(315, 254), new PointF(297, 279), new PointF(305, 304));
                path.AddBezier(new PointF(305, 304), new PointF(313, 329), new PointF(335, 348), new PointF(332, 375));
                path.AddBezier(new PointF(332, 375), new PointF(330, 408), new PointF(300, 434), new PointF(256, 439));
                path.CloseFigure();
                g.FillPath(fill, path);
                g.DrawPath(outline, path);
            }
        }

        private static void Collo(Graphics g, int x, int y, int w, int h, Color fill)
        {
            Rettangolo(g, fill, Inchiostro, 10, x, y, w, h);
            for (int i = 1; i < 6; i++)
            {
                int fy = y + 22 + i * 37;
                Linea(g, LegnoChiaro, 3, new PointF(x, fy), new PointF(x + w, fy));
            }
        }

        private static void Tastiera(Graphics g, int x, int y, int w, int h, int tasti)
        {
            Rettangolo(g, Crema, Inchiostro, 4, x, y, w, h);
            float larghezza = (float)w / tasti;
            for (int i = 1; i < tasti; i++)
            {
                float tx = x + i * larghezza;
                Linea(g, Inchiostro, 2, new PointF(tx, y), new PointF(tx, y + h));
            }
            for (int i = 0; i < tasti; i++)
            {
                if (i % 7 == 2 || i % 7 == 6) continue;
                float tx = x + (i + 0.64f) * larghezza;
                Rettangolo(g, Inchiostro, Inchiostro, 1, tx, y, larghezza * 0.38f, h * 0.57f);
            }
        }

        private static void Tamburo(Graphics g, int x, int y, int w, int h, Color colore)
        {
            Rettangolo(g, colore, Inchiostro, 11, x, y, w, h);
            Ellisse(g, Crema, Inchiostro, 9, x, y - 13, w, 32);
            Linea(g, Argento, 8, new PointF(x + 9, y + h - 15), new PointF(x + w - 9, y + h - 15));
        }

        private static void Piatto(Graphics g, int x, int y, int w, int h, Color colore)
        {
            Linea(g, Inchiostro, 8, new PointF(x + w / 2, y + 15), new PointF(x + w / 2, y + h));
            using (Pen pen = Penna(Inchiostro, 10))
            using (SolidBrush brush = new SolidBrush(colore))
            {
                PointF[] punti = { new PointF(x, y + 39), new PointF(x + w * .45f, y + 18), new PointF(x + w, y + 45), new PointF(x + w * .55f, y + 61) };
                g.FillPolygon(brush, punti);
                g.DrawPolygon(pen, punti);
            }
        }

        private static void Conga(Graphics g, int x, int y, float scala, Color colore)
        {
            GraphicsState stato = g.Save();
            g.TranslateTransform(x, y);
            g.ScaleTransform(scala, scala);
            Poligono(g, colore, new PointF(143, 107), new PointF(357, 107), new PointF(323, 393), new PointF(184, 393));
            Ellisse(g, Crema, Inchiostro, 13, 142, 82, 216, 66);
            Linea(g, Argento, 13, new PointF(174, 195), new PointF(326, 195));
            Linea(g, Inchiostro, 10, new PointF(187, 319), new PointF(320, 319));
            g.Restore(stato);
        }

        private static void Tamburello(Graphics g, int x, int y, float scala)
        {
            GraphicsState stato = g.Save();
            g.TranslateTransform(x, y);
            g.ScaleTransform(scala, scala);
            Ellisse(g, Ottone, Inchiostro, 14, 92, 92, 328, 328);
            Ellisse(g, Crema, Inchiostro, 8, 137, 137, 238, 238);
            for (int i = 0; i < 8; i++)
            {
                double a = Math.PI * i / 4;
                float cx = 256 + (float)Math.Cos(a) * 168;
                float cy = 256 + (float)Math.Sin(a) * 168;
                Ellisse(g, Argento, Inchiostro, 5, cx - 23, cy - 14, 46, 28);
            }
            g.Restore(stato);
        }

        private static void Maraca(Graphics g, int x, int y, float scala, Color colore)
        {
            GraphicsState stato = g.Save();
            g.TranslateTransform(x, y);
            g.ScaleTransform(scala, scala);
            Ellisse(g, colore, Inchiostro, 12, 136, 62, 240, 230);
            Curva(g, Inchiostro, 37, new PointF(258, 269), new PointF(282, 347), new PointF(300, 404), new PointF(351, 465));
            for (int i = 0; i < 5; i++) Ellisse(g, Ottone, Inchiostro, 4, 182 + i * 33, 142 + (i % 2) * 39, 13, 13);
            g.Restore(stato);
        }

        private static void Fforo(Graphics g, int x, int y)
        {
            Curva(g, Inchiostro, 9, new PointF(x + 23, y), new PointF(x, y + 10), new PointF(x + 5, y + 41), new PointF(x + 20, y + 54));
            Curva(g, Inchiostro, 9, new PointF(x + 20, y + 54), new PointF(x + 36, y + 40), new PointF(x + 32, y + 10), new PointF(x + 23, y));
        }

        private static void Stella(Graphics g, Color colore, int cx, int cy, int raggio)
        {
            PointF[] punti = new PointF[10];
            for (int i = 0; i < punti.Length; i++)
            {
                double angolo = -Math.PI / 2 + i * Math.PI / 5;
                float r = i % 2 == 0 ? raggio : raggio * 0.43f;
                punti[i] = new PointF(cx + (float)Math.Cos(angolo) * r, cy + (float)Math.Sin(angolo) * r);
            }
            Poligono(g, colore, punti);
        }

        private static void Curva(Graphics g, Color colore, float larghezza, PointF p1, PointF p2, PointF p3, PointF p4)
        {
            using (Pen pen = Penna(colore, larghezza))
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddBezier(p1, p2, p3, p4);
                g.DrawPath(pen, path);
            }
        }

        private static void Linea(Graphics g, Color colore, float larghezza, PointF p1, PointF p2)
        {
            using (Pen pen = Penna(colore, larghezza)) g.DrawLine(pen, p1, p2);
        }

        private static Pen Penna(Color colore, float larghezza)
        {
            return new Pen(colore, larghezza) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        }

        private static void Ellisse(Graphics g, Color fill, Color outline, float larghezza, float x, float y, float w, float h)
        {
            using (SolidBrush brush = new SolidBrush(fill)) g.FillEllipse(brush, x, y, w, h);
            if (larghezza > 0) using (Pen pen = Penna(outline, larghezza)) g.DrawEllipse(pen, x, y, w, h);
        }

        private static void Rettangolo(Graphics g, Color fill, Color outline, float larghezza, float x, float y, float w, float h)
        {
            using (SolidBrush brush = new SolidBrush(fill)) g.FillRectangle(brush, x, y, w, h);
            if (larghezza > 0) using (Pen pen = Penna(outline, larghezza)) g.DrawRectangle(pen, x, y, w, h);
        }

        private static void Poligono(Graphics g, Color fill, params PointF[] punti)
        {
            using (SolidBrush brush = new SolidBrush(fill)) g.FillPolygon(brush, punti);
            using (Pen pen = Penna(Inchiostro, 12)) g.DrawPolygon(pen, punti);
        }
    }
}
