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
        [Test, Explicit("Rigenera il materiale stampabile in assets/cartoncini")]
        public void GeneraCartoncini()
        {
            string dir = GeneratoreCartoncini.CartellaCartoncini();
            Assert.IsNotNull(dir);

            GeneratoreCartoncini.Genera(dir);

            Assert.That(Directory.GetFiles(dir, "*.png"), Has.Length.EqualTo(53));
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
