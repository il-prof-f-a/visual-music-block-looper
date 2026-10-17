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

        // Ogni payload prodotto dal generatore deve essere valido e risolto senza errori nella sua zona.
        [Test]
        public void Speciali_PayloadAccettatiERisoltiSenzaErrori()
        {
            var elenco = GeneratoreCartoncini.ElencoSpeciali();
            Assert.That(elenco, Has.Count.EqualTo(22));

            var area = new RettangoloTavolo(0.01, 0.04, 0.1, 0.1);
            Assert.That(ParserPayload.TryParse("VMBL1;S;LINK;6", out PayloadCartoncino seiRighe), Is.True);

            var misure = new List<int>();
            var collegamenti = new List<int>();
            var coppie = new List<string>();
            foreach (var (nome, testo, _) in elenco)
            {
                PayloadCartoncino payload;
                Assert.That(ParserPayload.TryParse(testo, out payload), Is.True, "Payload non valido: " + nome);
                Assert.AreEqual(TipoCartoncino.Impostazione, payload.Tipo, nome);

                RisoluzioneStruttura risultato;
                if (payload.Impostazione == TipoImpostazione.Misura)
                {
                    misure.Add(payload.Valore);
                    risultato = RisolutoreStruttura.Risolvi(new[] { (payload, area, ZonaSpeciale.Misura) });
                }
                else if (payload.Impostazione == TipoImpostazione.Collegamento)
                {
                    collegamenti.Add(payload.Valore);
                    risultato = RisolutoreStruttura.Risolvi(new[] { (payload, area, ZonaSpeciale.Collegamento) });
                }
                else
                {
                    coppie.Add($"{payload.Valore}-{payload.ValoreB}");
                    risultato = RisolutoreStruttura.Risolvi(new[]
                    {
                        (seiRighe, area, ZonaSpeciale.Collegamento),
                        (payload, area, ZonaSpeciale.Ab),
                    });
                }

                Assert.That(risultato.Errori, Is.Empty, "Errori su " + nome);
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
        public void Rileva_CartoncinoGenerato_PayloadEColoreCorretti()
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
                    Assert.AreEqual(ColoreTraccia.Rosso, per["VMBL1;N;3"].Colore);
                    Assert.AreEqual(ColoreTraccia.Giallo, per["VMBL1;N;1"].Colore);
                    Assert.IsNull(per["VMBL1;C;PLAY"].Colore, "Il corpo dei comandi deve essere neutro");
                }
            }
        }
    }
}
