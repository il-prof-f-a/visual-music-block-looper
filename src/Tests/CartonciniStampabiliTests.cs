using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class CartonciniStampabiliTests
    {
        private sealed class FonteMat : IFonteImmagini
        {
            private readonly Mat mat;

            public FonteMat(Mat mat)
            {
                this.mat = mat;
            }

            public Mat Frame()
            {
                return mat?.Clone();
            }
        }

        // Calibrazione identita' sul frame intero: i punti camera sono gli angoli dell'immagine.
        private static Calibrazione CalibrazioneSuFrame(int larghezza, int altezza)
        {
            return Calibrazione.Calcola(
                new[]
                {
                    new PuntoImmagine(0, 0), new PuntoImmagine(larghezza, 0),
                    new PuntoImmagine(larghezza, altezza), new PuntoImmagine(0, altezza)
                },
                new[] { new PuntoTavolo(0, 0), new PuntoTavolo(1, 0), new PuntoTavolo(1, 1), new PuntoTavolo(0, 1) });
        }

        // Rigenera i PNG in assets/cartoncini (eseguire a mano con --filter Name=GeneraCartoncini).
        // Parametri opzionali da variabili d'ambiente: VMBL_PASSO_MM (mm per passo, 15-60) e
        // VMBL_PASSI (passi per cartoncino, 2-8). Senza variabili: 30 mm e 2 passi.
        // ATTENZIONE: ogni esecuzione riscrive tutti i 75 PNG in assets/cartoncini (53 di nota e comando, 22 speciali).
        [Test, Explicit("Rigenera il materiale stampabile in assets/cartoncini")]
        public void GeneraCartoncini()
        {
            string dir = GeneratoreCartoncini.CartellaCartoncini();
            Assert.IsNotNull(dir);

            double passoMm = LeggiDouble("VMBL_PASSO_MM", GeneratoreCartoncini.PassoMmDefault, 15, 60);
            int passi = LeggiIntero("VMBL_PASSI", GeneratoreCartoncini.PassiDefault, 2, 8);

            GeneratoreCartoncini.Genera(dir, passoMm, GeneratoreCartoncini.AltezzaMmDefault, passi);

            Assert.That(Directory.GetFiles(dir, "*.png"), Has.Length.EqualTo(75));
        }

        private static IReadOnlyList<CartoncinoRilevato> RilevaSul(Bitmap tavolo, int larghezza, int altezza)
        {
            Mat mat;
            using (MemoryStream flusso = new MemoryStream())
            {
                tavolo.Save(flusso, ImageFormat.Png);
                mat = Cv2.ImDecode(flusso.ToArray(), ImreadModes.Color);
            }

            using (mat)
            {
                return new RilevatoreCartoncini(new FonteMat(mat), CalibrazioneSuFrame(larghezza, altezza)).Rileva();
            }
        }

        // I cartoncini speciali stampati in assets/cartoncini devono essere letti dal rilevatore reale.
        [Test]
        public void Rileva_CartoncinoSpeciale_PayloadCorretto()
        {
            const int larghezza = 1600;
            const int altezza = 1100;
            string dir = GeneratoreCartoncini.CartellaCartoncini();
            Assert.IsNotNull(dir);

            using (Bitmap tavolo = new Bitmap(larghezza, altezza))
            using (Graphics g = Graphics.FromImage(tavolo))
            {
                g.Clear(Color.White);
                using (Bitmap misura = new Bitmap(Path.Combine(dir, "speciale-misura-3.png")))
                using (Bitmap link = new Bitmap(Path.Combine(dir, "speciale-link-2.png")))
                using (Bitmap ab = new Bitmap(Path.Combine(dir, "speciale-ab-2-5.png")))
                {
                    g.DrawImage(misura, 60, 40);
                    g.DrawImage(link, 60, 560);
                    g.DrawImage(ab, 800, 300);
                }

                var rilevati = RilevaSul(tavolo, larghezza, altezza);

                Assert.AreEqual(3, rilevati.Count, "Cartoncini letti: " + rilevati.Count);

                Dictionary<string, CartoncinoRilevato> per = new Dictionary<string, CartoncinoRilevato>();
                foreach (CartoncinoRilevato r in rilevati)
                {
                    per[r.Payload] = r;
                }

                Assert.That(per.ContainsKey("VMBL1;S;MISURA;3"), Is.True, "Payload misura non letto");
                Assert.That(per.ContainsKey("VMBL1;S;LINK;2"), Is.True, "Payload collegamento non letto");
                Assert.That(per.ContainsKey("VMBL1;S;AB;2;5"), Is.True, "Payload A-B non letto");
            }
        }

        // Area piccola al centro di una zona del layout di default: il centro cade nella zona voluta.
        private static RettangoloTavolo AreaAlCentro(RettangoloTavolo zona)
        {
            double cx = zona.X + zona.Larghezza / 2;
            double cy = zona.Y + zona.Altezza / 2;
            return new RettangoloTavolo(cx - 0.005, cy - 0.005, 0.01, 0.01);
        }

        // Ogni payload speciale: nella zona attesa, senza errori, con i valori risolti attesi
        // (misura, righe, A, B). Il test fallisce se il risolutore ignora il payload.
        [Test]
        public void Speciali_PayloadRisoltiNelLoroValoreEZona()
        {
            LayoutTavolo layout = LayoutTavolo.Default;
            var elenco = GeneratoreCartoncini.ElencoSpeciali();
            Assert.That(elenco, Has.Count.EqualTo(22));

            RettangoloTavolo areaMisura = AreaAlCentro(layout.Misura);
            RettangoloTavolo areaLink = AreaAlCentro(layout.Collegamento);
            RettangoloTavolo areaAb = AreaAlCentro(layout.Ab);
            Assert.AreEqual(ZonaSpeciale.Misura, layout.ZonaDelCentro(areaMisura), "centro misura");
            Assert.AreEqual(ZonaSpeciale.Collegamento, layout.ZonaDelCentro(areaLink), "centro collegamento");
            Assert.AreEqual(ZonaSpeciale.Ab, layout.ZonaDelCentro(areaAb), "centro A-B");

            Assert.That(ParserPayload.TryParse("VMBL1;S;LINK;6", out PayloadCartoncino seiRighe), Is.True);

            var misure = new List<int>();
            var collegamenti = new List<int>();
            var coppie = new List<string>();
            foreach (var (nome, testo, _) in elenco)
            {
                Assert.That(ParserPayload.TryParse(testo, out PayloadCartoncino payload), Is.True, "Payload non valido: " + nome);
                Assert.AreEqual(TipoCartoncino.Impostazione, payload.Tipo, nome);

                // Valori attesi: default per i parametri non letti dal cartoncino.
                int battitiAttesi = 4, righeAttese = 6, aInizioAttesa = 1, aFineAttesa = 24;
                RisoluzioneStruttura risultato;
                if (payload.Impostazione == TipoImpostazione.Misura)
                {
                    misure.Add(payload.Valore);
                    battitiAttesi = payload.Valore;
                    aFineAttesa = 6 * payload.Valore;
                    risultato = RisolutoreStruttura.Risolvi(new[] { (payload, areaMisura, ZonaSpeciale.Misura) });
                }
                else if (payload.Impostazione == TipoImpostazione.Collegamento)
                {
                    collegamenti.Add(payload.Valore);
                    righeAttese = payload.Valore;
                    aFineAttesa = payload.Valore * 4;
                    risultato = RisolutoreStruttura.Risolvi(new[] { (payload, areaLink, ZonaSpeciale.Collegamento) });
                }
                else
                {
                    coppie.Add($"{payload.Valore}-{payload.ValoreB}");
                    aInizioAttesa = payload.Valore;
                    aFineAttesa = payload.ValoreB;
                    risultato = RisolutoreStruttura.Risolvi(new[]
                    {
                        (seiRighe, areaLink, ZonaSpeciale.Collegamento),
                        (payload, areaAb, ZonaSpeciale.Ab),
                    });
                }

                Assert.That(risultato.Errori, Is.Empty, "Errori su " + nome);
                StrutturaTavolo s = risultato.Struttura;
                Assert.AreEqual(battitiAttesi, s.BattitiPerMisura, "battiti su " + nome);
                Assert.AreEqual(righeAttese, s.RigheCollegate, "righe su " + nome);
                Assert.AreEqual(aInizioAttesa, s.AInizio, "A su " + nome);
                Assert.AreEqual(aFineAttesa, s.AFine, "B su " + nome);
            }

            CollectionAssert.AreEquivalent(new[] { 2, 3, 4 }, misure);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 6 }, collegamenti);

            var attese = new List<string>();
            for (int a = 1; a <= 6; a++)
            {
                for (int b = a + 1; b <= 6; b++)
                {
                    attese.Add($"{a}-{b}");
                }
            }

            CollectionAssert.AreEquivalent(attese, coppie);
        }

        // Il corpo dei cartoncini speciali e' neutro: nessuna traccia di colore, sotto la soglia di saturazione.
        [Test]
        public void ColoreSpeciale_NeutroESottoSogliaDiSaturazione()
        {
            Color speciale = GeneratoreCartoncini.ColoreSpeciale;
            Assert.IsNull(ClassificatoreColore.Classifica(speciale.R, speciale.G, speciale.B), "Corpo speciale classificato come colore");
            Assert.IsNull(ClassificatoreColore.Classifica(GeneratoreCartoncini.ColoreComando.R,
                GeneratoreCartoncini.ColoreComando.G, GeneratoreCartoncini.ColoreComando.B), "Corpo comando classificato come colore");

            int max = Math.Max(speciale.R, Math.Max(speciale.G, speciale.B));
            int min = Math.Min(speciale.R, Math.Min(speciale.G, speciale.B));
            double saturazione = (max - min) / (double)max;
            Assert.That(saturazione, Is.LessThanOrEqualTo(0.25), "Saturazione del corpo speciale");
        }

        private static int LeggiIntero(string nome, int predefinito, int minimo, int massimo)
        {
            string testo = Environment.GetEnvironmentVariable(nome);
            if (string.IsNullOrWhiteSpace(testo))
            {
                return predefinito;
            }

            bool ok = int.TryParse(testo, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out int valore);
            Assert.That(ok && valore >= minimo && valore <= massimo, Is.True,
                $"{nome} deve essere un intero tra {minimo} e {massimo}: '{testo}'");
            return valore;
        }

        private static double LeggiDouble(string nome, double predefinito, double minimo, double massimo)
        {
            string testo = Environment.GetEnvironmentVariable(nome);
            if (string.IsNullOrWhiteSpace(testo))
            {
                return predefinito;
            }

            bool ok = double.TryParse(testo, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double valore);
            Assert.That(ok && valore >= minimo && valore <= massimo, Is.True,
                $"{nome} deve essere un numero tra {minimo} e {massimo}: '{testo}'");
            return valore;
        }

        // Guardia del generatore: i cartoncini generati devono essere letti dal rilevatore reale
        // con il payload e la classe di colore attesi.
        [Test]
        public void Rileva_CartoncinoGenerato_PayloadELarghezzaCorretti()
        {
            const int larghezza = 1600;
            const int altezza = 1100;

            using (Bitmap tavolo = new Bitmap(larghezza, altezza))
            using (Graphics g = Graphics.FromImage(tavolo))
            {
                g.Clear(Color.White);
                using (Bitmap rosso = GeneratoreCartoncini.CartoncinoNota(0, 3, passi: 2))
                using (Bitmap giallo = GeneratoreCartoncini.CartoncinoNota(2, 1, passi: 3))
                using (Bitmap comando = GeneratoreCartoncini.CartoncinoComando("VMBL1;C;PLAY", "PLAY", passi: 2))
                {
                    g.DrawImage(rosso, 60, 60);
                    g.DrawImage(giallo, 60, 600);
                    g.DrawImage(comando, 800, 200);
                }

                Mat mat;
                using (MemoryStream flusso = new MemoryStream())
                {
                    tavolo.Save(flusso, ImageFormat.Png);
                    mat = Cv2.ImDecode(flusso.ToArray(), ImreadModes.Color);
                }

                using (mat)
                {
                    var rilevati = new RilevatoreCartoncini(new FonteMat(mat), CalibrazioneSuFrame(larghezza, altezza)).Rileva();

                    Assert.AreEqual(3, rilevati.Count, "Cartoncini letti: " + rilevati.Count);

                    Dictionary<string, CartoncinoRilevato> per = new Dictionary<string, CartoncinoRilevato>();
                    foreach (CartoncinoRilevato r in rilevati)
                    {
                        per[r.Payload] = r;
                    }

                    Assert.That(per.ContainsKey("VMBL1;N;3"), Is.True);
                    Assert.That(per.ContainsKey("VMBL1;N;1"), Is.True);
                    Assert.That(per.ContainsKey("VMBL1;C;PLAY"), Is.True);

                    // Il colore non conta: la larghezza del corpo (giallo = 3 passi, rosso = 2 passi) da' la durata.
                    double rapporto = per["VMBL1;N;1"].Area.Larghezza / per["VMBL1;N;3"].Area.Larghezza;
                    Assert.AreEqual(1.5, rapporto, 0.15, "Rapporto larghezze giallo/rosso");
                }
            }
        }
    }
}
