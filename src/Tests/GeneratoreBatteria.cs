using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    /// <summary>
    /// Cartoncini per drum kit: stesso QR delle note classiche (VMBL1;N;...) con la nota MIDI
    /// esplicita nel quarto campo, immagine dello strumento al posto del pentagramma.
    /// </summary>
    internal static class GeneratoreBatteria
    {
        public sealed class Voce
        {
            public Voce(int midi, string nome, string slug)
            {
                Midi = midi;
                Nome = nome;
                Slug = slug;
            }

            public int Midi { get; }
            public string Nome { get; }
            public string Slug { get; }

            /// <summary>idClip del QR: per MIDI 35 (B0) si usa 0, il quarto campo porta la nota vera.</summary>
            public int IdClip => Math.Max(0, Midi - 36);

            public string Payload => $"VMBL1;N;{IdClip};{Midi}";

            public string FileCard => $"batteria-{Midi:000}-{Slug}.png";
        }

        /// <summary>Mappa completa del drum kit General MIDI usata dai cartoncini.</summary>
        public static readonly IReadOnlyList<Voce> Voci = new[]
        {
            new Voce(35, "Grancassa acustica", "grancassa-acustica"),
            new Voce(36, "Grancassa / Kick", "kick"),
            new Voce(37, "Rimshot / Side Stick", "rimshot"),
            new Voce(38, "Rullante acustico", "snare"),
            new Voce(39, "Hand Clap", "hand-clap"),
            new Voce(40, "Rullante elettronico", "rullante-elettronico"),
            new Voce(41, "Timpano grave", "floor-tom"),
            new Voce(42, "Charleston chiuso", "charleston-chiuso"),
            new Voce(43, "Timpano medio-grave", "tom-medio-grave"),
            new Voce(44, "Charleston a pedale", "charleston-pedale"),
            new Voce(45, "Tom grave", "tom-grave"),
            new Voce(46, "Charleston aperto", "charleston-aperto"),
            new Voce(47, "Tom medio-grave", "tom-medio-basso"),
            new Voce(48, "Tom medio-acuto", "tom-medio-alto"),
            new Voce(49, "Crash 1", "crash-1"),
            new Voce(50, "Tom acuto", "tom-acuto"),
            new Voce(51, "Ride", "ride"),
            new Voce(52, "Piatto China", "china"),
            new Voce(53, "Campana del Ride", "campana-ride"),
            new Voce(54, "Tamburello", "tamburello"),
            new Voce(55, "Splash", "splash"),
            new Voce(56, "Cowbell", "cowbell"),
            new Voce(57, "Crash 2", "crash-2"),
            new Voce(58, "Vibraslap", "vibraslap"),
            new Voce(59, "Ride 2", "ride-2"),
            new Voce(60, "Bongo alto", "bongo-alto"),
            new Voce(61, "Bongo basso", "bongo-basso"),
            new Voce(62, "Conga muta", "conga-muta"),
            new Voce(63, "Conga aperta alta", "conga-aperta-alta"),
            new Voce(64, "Conga bassa", "conga-bassa"),
            new Voce(65, "Timbales alto", "timbales-alto"),
            new Voce(66, "Timbales basso", "timbales-basso"),
            new Voce(67, "Agogô alto", "agogo-alto"),
            new Voce(68, "Agogô basso", "agogo-basso"),
            new Voce(69, "Cabasa", "cabasa"),
            new Voce(70, "Maracas", "maracas"),
            new Voce(71, "Fischietto corto", "fischietto-corto"),
            new Voce(72, "Fischietto lungo", "fischietto-lungo"),
            new Voce(73, "Guiro corto", "guiro-corto"),
            new Voce(74, "Guiro lungo", "guiro-lungo"),
            new Voce(75, "Claves", "claves"),
            new Voce(76, "Woodblock alto", "woodblock-alto"),
            new Voce(77, "Woodblock basso", "woodblock-basso"),
            new Voce(78, "Cuíca chiusa", "cuica-chiusa"),
            new Voce(79, "Cuíca aperta", "cuica-aperta"),
            new Voce(80, "Triangolo chiuso", "triangolo-chiuso"),
            new Voce(81, "Triangolo aperto", "triangolo-aperto")
        };

        /// <summary>Corpo scuro comune a tutti i cartoncini di batteria: il QR resta bianco.</summary>
        public static readonly Color ColoreBatteria = Color.FromArgb(52, 62, 76);

        private sealed class Tavolozza
        {
            public Color Corpo, CorpoScuro, Metallo, MetalloScuro, Pelle, Accento, Ombra;
        }

        private static Tavolozza TavolozzaElettronica() => new Tavolozza
        {
            Corpo = Color.FromArgb(32, 39, 50),
            CorpoScuro = Color.FromArgb(16, 20, 27),
            Metallo = Color.FromArgb(158, 168, 180),
            MetalloScuro = Color.FromArgb(92, 101, 113),
            Pelle = Color.FromArgb(232, 226, 212),
            Accento = Color.FromArgb(34, 211, 214),
            Ombra = Color.FromArgb(230, 233, 237)
        };

        private static Tavolozza TavolozzaRealistica() => new Tavolozza
        {
            Corpo = Color.FromArgb(170, 108, 58),
            CorpoScuro = Color.FromArgb(105, 62, 32),
            Metallo = Color.FromArgb(190, 197, 205),
            MetalloScuro = Color.FromArgb(120, 128, 138),
            Pelle = Color.FromArgb(243, 236, 223),
            Accento = Color.FromArgb(214, 122, 42),
            Ombra = Color.FromArgb(235, 232, 228)
        };

        /// <summary>Disegna le 25 icone della batteria base 35-59 nella cartella dello stile.</summary>
        public static void GeneraIcone(string cartella, bool realistica)
        {
            Directory.CreateDirectory(cartella);
            Tavolozza palette = realistica ? TavolozzaRealistica() : TavolozzaElettronica();
            foreach (Voce voce in Voci)
            {
                if (voce.Midi > 59)
                {
                    continue;
                }

                using (Bitmap icona = DisegnaIcona(voce.Slug, palette))
                using (Bitmap ritagliata = Ritaglia(icona))
                {
                    ritagliata.Save(Path.Combine(cartella, voce.Slug + ".png"), ImageFormat.Png);
                }
            }
        }

        /// <summary>Genera i 47 cartoncini di uno stile usando le icone nella cartella indicata.</summary>
        public static void GeneraCartoncini(string cartella, string cartellaIcone, bool realistica,
            double passoMm = GeneratoreCartoncini.PassoMmDefault,
            double altezzaMm = GeneratoreCartoncini.AltezzaMmDefault, int passi = GeneratoreCartoncini.PassiDefault)
        {
            Directory.CreateDirectory(cartella);
            foreach (string vecchio in Directory.GetFiles(cartella, "batteria-*.png"))
            {
                File.Delete(vecchio);
            }

            foreach (Voce voce in Voci)
            {
                string icona = Path.Combine(cartellaIcone, voce.Slug + ".png");
                string nota = NomeNota(voce.Midi) + " · MIDI " + voce.Midi;
                using (Bitmap card = GeneratoreCartoncini.Disegna(voce.Payload, ColoreBatteria, voce.Nome,
                    passi, passoMm, altezzaMm, ZXing.QrCode.Internal.ErrorCorrectionLevel.L, nota,
                    File.Exists(icona) ? icona : null))
                {
                    card.Save(Path.Combine(cartella, voce.FileCard), ImageFormat.Png);
                }
            }
        }

        private static string NomeNota(int midi)
        {
            string[] nomi = { "Do", "Do♯", "Re", "Re♯", "Mi", "Fa", "Fa♯", "Sol", "Sol♯", "La", "La♯", "Si" };
            // Ottave in convenzione Ableton: C1 = MIDI 36, C3 = MIDI 60.
            return nomi[midi % 12] + (midi / 12 - 2);
        }

        /// <summary>Ritaglia il bordo bianco dell'icona, così sul cartoncino occupa più spazio.</summary>
        private static Bitmap Ritaglia(Bitmap icona)
        {
            int minX = icona.Width, minY = icona.Height, maxX = -1, maxY = -1;
            for (int y = 0; y < icona.Height; y++)
            {
                for (int x = 0; x < icona.Width; x++)
                {
                    Color c = icona.GetPixel(x, y);
                    if (c.R < 248 || c.G < 248 || c.B < 248)
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (maxX < 0)
            {
                return new Bitmap(icona);
            }

            int margine = 4;
            var area = Rectangle.FromLTRB(
                Math.Max(0, minX - margine), Math.Max(0, minY - margine),
                Math.Min(icona.Width, maxX + margine + 1), Math.Min(icona.Height, maxY + margine + 1));
            return icona.Clone(area, PixelFormat.Format24bppRgb);
        }

        private static Bitmap DisegnaIcona(string slug, Tavolozza p)
        {
            var bmp = new Bitmap(420, 300, PixelFormat.Format24bppRgb);
            bmp.SetResolution(GeneratoreCartoncini.Dpi, GeneratoreCartoncini.Dpi);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var ombra = new SolidBrush(p.Ombra))
                {
                    g.FillEllipse(ombra, 70, 248, 280, 26);
                }

                switch (slug)
                {
                    case "grancassa-acustica":
                        Cassa(g, p, 210, 150, 230, 210);
                        break;
                    case "kick":
                        Cassa(g, p, 210, 145, 210, 200);
                        Battente(g, p, 210, 70);
                        break;
                    case "rimshot":
                        Rullante(g, p, 205, 175, 210, 110);
                        Bacchetta(g, p, 270, 50, 150, 155);
                        break;
                    case "snare":
                        Rullante(g, p, 210, 165, 220, 120);
                        Bacchetta(g, p, 300, 62, 175, 150);
                        break;
                    case "hand-clap":
                        Mani(g, p);
                        break;
                    case "rullante-elettronico":
                        Rullante(g, p, 210, 165, 220, 120);
                        Onda(g, p, 135, 135, 150);
                        break;
                    case "floor-tom":
                        Tom(g, p, 210, 175, 180, 150);
                        Gambe(g, p, 150, 245, 120, 280);
                        Gambe(g, p, 270, 245, 300, 280);
                        break;
                    case "charleston-chiuso":
                        Charleston(g, p, 210, 130, 150, 10, false);
                        break;
                    case "tom-medio-grave":
                        Tom(g, p, 210, 170, 200, 135);
                        break;
                    case "charleston-pedale":
                        Charleston(g, p, 210, 122, 150, 10, true);
                        break;
                    case "tom-grave":
                        Tom(g, p, 210, 170, 215, 145);
                        break;
                    case "charleston-aperto":
                        Charleston(g, p, 210, 130, 150, 32, false);
                        break;
                    case "tom-medio-basso":
                        Tom(g, p, 210, 168, 185, 130);
                        break;
                    case "tom-medio-alto":
                        Tom(g, p, 210, 166, 165, 120);
                        break;
                    case "crash-1":
                        Piatto(g, p, 210, 110, 175, 52, 0);
                        break;
                    case "tom-acuto":
                        Tom(g, p, 210, 164, 145, 108);
                        break;
                    case "ride":
                        Piatto(g, p, 210, 118, 190, 56, 1);
                        break;
                    case "china":
                        Piatto(g, p, 210, 120, 170, 60, 3);
                        break;
                    case "campana-ride":
                        Piatto(g, p, 210, 118, 185, 54, 4);
                        break;
                    case "tamburello":
                        Tamburello(g, p);
                        break;
                    case "splash":
                        Piatto(g, p, 210, 130, 115, 40, 2);
                        break;
                    case "cowbell":
                        Cowbell(g, p);
                        break;
                    case "crash-2":
                        Piatto(g, p, 210, 112, 185, 56, 0);
                        break;
                    case "vibraslap":
                        Vibraslap(g, p);
                        break;
                    case "ride-2":
                        Piatto(g, p, 210, 120, 200, 60, 1);
                        break;
                    default:
                        using (var testo = new SolidBrush(p.CorpoScuro))
                        using (var font = new Font("Arial", 18, FontStyle.Bold))
                        {
                            g.DrawString(slug, font, testo, 20, 120);
                        }

                        break;
                }
            }

            return bmp;
        }

        private static void Cassa(Graphics g, Tavolozza p, float cx, float cy, float w, float h)
        {
            using (var corpo = new SolidBrush(p.Corpo))
            using (var scuro = new SolidBrush(p.CorpoScuro))
            using (var pelle = new SolidBrush(p.Pelle))
            using (var centro = new SolidBrush(p.Accento))
            using (var accento = new Pen(p.Accento, 9))
            using (var metallo = new Pen(p.Metallo, 6))
            {
                g.FillEllipse(scuro, cx - w / 2, cy - h / 2 + 8, w, h);
                g.FillEllipse(corpo, cx - w / 2, cy - h / 2, w, h);
                g.FillEllipse(pelle, cx - w / 2 + 22, cy - h / 2 + 22, w - 44, h - 44);
                g.DrawEllipse(accento, cx - w / 2 + 10, cy - h / 2 + 10, w - 20, h - 20);
                g.DrawEllipse(metallo, cx - w / 2 + 30, cy - h / 2 + 30, w - 60, h - 60);
                g.FillEllipse(centro, cx - 22, cy - 22, 44, 44);
                // Gambe della grancassa.
                g.DrawLine(metallo, cx - w * 0.38f, cy + h * 0.42f, cx - w * 0.55f, cy + h * 0.62f);
                g.DrawLine(metallo, cx + w * 0.38f, cy + h * 0.42f, cx + w * 0.55f, cy + h * 0.62f);
            }
        }

        private static void Battente(Graphics g, Tavolozza p, float cx, float top)
        {
            using (var metallo = new Pen(p.MetalloScuro, 7))
            using (var testa = new SolidBrush(p.CorpoScuro))
            {
                g.DrawLine(metallo, cx, top, cx + 55, top + 105);
                g.FillEllipse(testa, cx + 38, top + 92, 34, 26);
            }
        }

        private static void Rullante(Graphics g, Tavolozza p, float cx, float cy, float w, float h)
        {
            using (var corpo = new SolidBrush(p.Corpo))
            using (var scuro = new SolidBrush(p.CorpoScuro))
            using (var pelle = new SolidBrush(p.Pelle))
            using (var metallo = new Pen(p.Metallo, 5))
            using (var accento = new Pen(p.Accento, 4))
            {
                g.FillRectangle(scuro, cx - w / 2, cy - h / 2 + 6, w, h);
                g.FillRectangle(corpo, cx - w / 2, cy - h / 2, w, h);
                g.FillEllipse(pelle, cx - w / 2, cy - h / 2 - 8, w, 34);
                g.DrawEllipse(accento, cx - w / 2, cy - h / 2 - 8, w, 34);
                for (int i = -1; i <= 1; i++)
                {
                    float x = cx + i * w * 0.28f;
                    g.DrawLine(metallo, x, cy - h / 2, x, cy + h / 2);
                }

                g.DrawArc(metallo, cx - w / 2 + 6, cy + h / 2 - 16, w - 12, 28, 5, 170);
            }
        }

        private static void Bacchetta(Graphics g, Tavolozza p, float x1, float y1, float x2, float y2)
        {
            using (var legno = new Pen(p.Pelle, 10) { EndCap = LineCap.Round })
            using (var punta = new SolidBrush(p.CorpoScuro))
            {
                g.DrawLine(legno, x1, y1, x2, y2);
                g.FillEllipse(punta, x2 - 9, y2 - 9, 18, 18);
            }
        }

        private static void Tom(Graphics g, Tavolozza p, float cx, float cy, float w, float h)
        {
            using (var corpo = new SolidBrush(p.Corpo))
            using (var scuro = new SolidBrush(p.CorpoScuro))
            using (var pelle = new SolidBrush(p.Pelle))
            using (var metallo = new Pen(p.Metallo, 5))
            {
                g.FillRectangle(scuro, cx - w / 2, cy - h / 2 + 7, w, h);
                g.FillRectangle(corpo, cx - w / 2, cy - h / 2, w, h);
                g.FillEllipse(pelle, cx - w / 2, cy - h / 2 - 12, w, 36);
                g.DrawArc(metallo, cx - w / 2, cy - h / 2 - 12, w, 36, 0, 180);
                g.DrawArc(metallo, cx - w / 2, cy + h / 2 - 18, w, 30, 180, 180);
                for (int i = -1; i <= 1; i += 2)
                {
                    float x = cx + i * (w / 2 - 4);
                    g.DrawLine(metallo, x, cy - h / 2 + 8, x, cy + h / 2 - 8);
                }
            }
        }

        private static void Gambe(Graphics g, Tavolozza p, float x1, float y1, float x2, float y2)
        {
            using (var metallo = new Pen(p.MetalloScuro, 6))
            {
                g.DrawLine(metallo, x1, y1, x2, y2);
            }
        }

        private static void Charleston(Graphics g, Tavolozza p, float cx, float cy, float w, float apertura, bool pedale)
        {
            using (var metallo = new Pen(p.Metallo, 6))
            using (var cima = new SolidBrush(p.Accento))
            using (var sotto = new SolidBrush(p.CorpoScuro))
            {
                g.DrawLine(metallo, cx, cy + 20 + apertura, cx, 275);
                g.DrawLine(metallo, cx - 45, 275, cx + 45, 275);
                if (pedale)
                {
                    g.DrawLine(metallo, cx, 250, cx + 60, 280);
                }

                float rx = w / 2;
                g.FillEllipse(sotto, cx - rx, cy - 6 + apertura, w, 26);
                g.FillEllipse(cima, cx - rx, cy - 16, w, 26);
                g.FillEllipse(sotto, cx - 26, cy - 22, 52, 20);
            }
        }

        private static void Piatto(Graphics g, Tavolozza p, float cx, float cy, float rx, float ry, int stile)
        {
            using (var metallo = new Pen(p.Metallo, 6))
            using (var piatto = new SolidBrush(stile == 1 || stile == 4 ? Color.FromArgb(196, 158, 78) : p.Accento))
            using (var bordo = new Pen(p.MetalloScuro, 3))
            using (var campana = new SolidBrush(p.Pelle))
            {
                g.DrawLine(metallo, cx, cy + ry * 0.5f, cx, 278);
                g.DrawLine(metallo, cx - 55, 278, cx + 55, 278);
                g.FillEllipse(piatto, cx - rx, cy - ry, rx * 2, ry * 2);
                g.DrawEllipse(bordo, cx - rx, cy - ry, rx * 2, ry * 2);
                if (stile == 1 || stile == 4)
                {
                    g.DrawEllipse(bordo, cx - rx * 0.62f, cy - ry * 0.62f, rx * 1.24f, ry * 1.24f);
                }

                float rCampana = stile == 4 ? rx * 0.30f : rx * 0.20f;
                g.FillEllipse(campana, cx - rCampana, cy - rCampana * 0.6f, rCampana * 2, rCampana * 1.2f);
                if (stile == 3)
                {
                    // Bordo arrotolato del China.
                    g.DrawArc(bordo, cx - rx, cy - ry - 10, rx * 2, ry * 2, 200, 140);
                }
            }
        }

        private static void Mani(Graphics g, Tavolozza p)
        {
            using (var pelle = new SolidBrush(p.Pelle))
            using (var accento = new Pen(p.Accento, 8))
            using (var metallo = new Pen(p.MetalloScuro, 5))
            {
                for (int lato = -1; lato <= 1; lato += 2)
                {
                    float cx = 210 + lato * 62;
                    g.FillEllipse(pelle, cx - 42, 120, 84, 100);
                    for (int dito = 0; dito < 4; dito++)
                    {
                        float x = cx - 34 + dito * 20;
                        g.FillRectangle(pelle, x, 58 + Math.Abs(dito - 1.5f) * 8, 15, 74);
                    }

                    g.DrawArc(accento, cx - 42, 120, 84, 100, lato < 0 ? 270 : 90, 180);
                }

                for (int i = -1; i <= 1; i++)
                {
                    g.DrawLine(metallo, 210 + i * 14, 70, 210 + i * 14, 108);
                }
            }
        }

        private static void Onda(Graphics g, Tavolozza p, float x, float y, float w)
        {
            using (var penna = new Pen(p.Accento, 7))
            {
                var punti = new[]
                {
                    new PointF(x, y + 10), new PointF(x + w * .15f, y + 10),
                    new PointF(x + w * .25f, y - 18), new PointF(x + w * .35f, y + 32),
                    new PointF(x + w * .45f, y - 2), new PointF(x + w * .55f, y + 20),
                    new PointF(x + w * .65f, y - 8), new PointF(x + w, y + 10)
                };
                g.DrawLines(penna, punti);
            }
        }

        private static void Tamburello(Graphics g, Tavolozza p)
        {
            using (var corpo = new SolidBrush(p.Pelle))
            using (var bordo = new Pen(p.CorpoScuro, 10))
            using (var accento = new SolidBrush(p.Accento))
            {
                g.FillEllipse(corpo, 110, 55, 200, 200);
                g.DrawEllipse(bordo, 110, 55, 200, 200);
                for (int i = 0; i < 10; i++)
                {
                    double angolo = i * Math.PI / 5;
                    float x = 210 + (float)Math.Cos(angolo) * 82 - 9;
                    float y = 155 + (float)Math.Sin(angolo) * 82 - 9;
                    g.FillEllipse(accento, x, y, 18, 18);
                }
            }
        }

        private static void Cowbell(Graphics g, Tavolozza p)
        {
            using (var corpo = new SolidBrush(p.Metallo))
            using (var bordo = new Pen(p.CorpoScuro, 6))
            using (var foro = new SolidBrush(p.CorpoScuro))
            {
                var punti = new[]
                {
                    new PointF(145, 70), new PointF(275, 70),
                    new PointF(250, 235), new PointF(170, 235)
                };
                g.FillPolygon(corpo, punti);
                g.DrawPolygon(bordo, punti);
                g.FillEllipse(foro, 190, 95, 20, 20);
                g.FillEllipse(foro, 225, 150, 26, 26);
                g.DrawLine(bordo, 160, 235, 160, 270);
                g.DrawLine(bordo, 260, 235, 260, 270);
            }
        }

        private static void Vibraslap(Graphics g, Tavolozza p)
        {
            using (var corpo = new SolidBrush(p.Corpo))
            using (var accento = new SolidBrush(p.Accento))
            using (var metallo = new Pen(p.Metallo, 8))
            using (var bordo = new Pen(p.CorpoScuro, 5))
            {
                g.FillRectangle(corpo, 95, 105, 170, 95);
                g.DrawRectangle(bordo, 95, 105, 170, 95);
                g.DrawLine(metallo, 265, 152, 345, 152);
                using (var denti = new Pen(p.Accento, 4))
                {
                    for (int i = 0; i < 6; i++)
                    {
                        g.DrawLine(denti, 112 + i * 24, 125, 124 + i * 24, 180);
                    }
                }

                g.FillEllipse(accento, 330, 140, 24, 24);
            }
        }
    }
}
