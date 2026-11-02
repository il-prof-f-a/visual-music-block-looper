using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class DiagnosticaVisioneTests
    {
        private sealed class Fonte : IFonteImmagini
        {
            public Mat Immagine;
            public int Chiamate;
            public bool Lancia;
            public Mat Frame() { Chiamate++; if (Lancia) throw new IOException("camera di prova"); return Immagine?.Clone(); }
        }

        private static Calibrazione Calibra(int w = 640, int h = 480) => Calibrazione.Calcola(
            new[] { new PuntoImmagine(0, 0), new PuntoImmagine(w, 0), new PuntoImmagine(w, h), new PuntoImmagine(0, h) },
            new[] { new PuntoTavolo(0, 0), new PuntoTavolo(1, 0), new PuntoTavolo(1, 1), new PuntoTavolo(0, 1) });

        [Test]
        public void CorpoSenzaQr_RestaDistintoDaVincoloRifiutato()
        {
            using (var frame = new Mat(480, 640, MatType.CV_8UC3, Scalar.All(255)))
            {
                Cv2.Rectangle(frame, new Rect(50, 80, 160, 45), Scalar.All(30), -1);
                var fonte = new Fonte { Immagine = frame };
                var traccia = new TracciaCartoncini();
                var risultato = new RilevatoreCartoncini(fonte, Calibra()).Rileva(traccia);
                Assert.That(risultato, Is.Empty);
                Assert.That(fonte.Chiamate, Is.EqualTo(1));
                Assert.That(traccia.EsitoVisione, Is.EqualTo("NessunQrDecodificato"));
                Assert.That(traccia.Visione.Any(e => e.Codice == "CorpoSenzaQrDecodificato"), Is.True);
                Assert.That(traccia.Interpretazione, Is.Empty);
                Assert.That(traccia.Visione.All(e => e.Payload == null), Is.True);
            }
        }

        [Test]
        public void FrameNulloEFonteCheLancia_SonoEspliciti()
        {
            var fonte = new Fonte();
            var rilevatore = new RilevatoreCartoncini(fonte, Calibra());
            var vuota = new TracciaCartoncini();
            Assert.That(rilevatore.Rileva(vuota), Is.Empty);
            Assert.That(vuota.EsitoVisione, Is.EqualTo("FrameAssente"));
            fonte.Lancia = true;
            var errore = new TracciaCartoncini();
            Assert.Throws<IOException>(() => rilevatore.Rileva(errore));
            Assert.That(errore.EsitoVisione, Is.EqualTo("ErroreAcquisizione"));
            Assert.That(errore.Errore, Does.Contain("camera di prova"));
        }

        [Test]
        public void QrAbReale_DiagnosticaLoStessoFrameSenzaCambiareIlRisultato()
        {
            using (Bitmap carta = GeneratoreCartoncini.CartoncinoSpeciale("VMBL1;S;AB;1;2", "A-B 1-2", 2))
            using (Bitmap tavolo = new Bitmap(1280, 720))
            using (Graphics g = Graphics.FromImage(tavolo))
            {
                g.Clear(Color.White); g.DrawImage(carta, 200, 150, (int)(carta.Width * 0.4), (int)(carta.Height * 0.4));
                using (var stream = new MemoryStream())
                {
                    tavolo.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    using (Mat frame = Cv2.ImDecode(stream.ToArray(), ImreadModes.Color))
                    {
                        var fonte = new Fonte { Immagine = frame };
                        var rilevatore = new RilevatoreCartoncini(fonte, Calibra(1280, 720));
                        var prima = rilevatore.Rileva();
                        var traccia = new TracciaCartoncini();
                        var dopo = rilevatore.Rileva(traccia);
                        Assert.That(fonte.Chiamate, Is.EqualTo(2));
                        Assert.That(dopo.Select(c => c.Payload), Is.EqualTo(prima.Select(c => c.Payload)));
                        Assert.That(dopo.Select(c => c.Payload), Does.Contain("VMBL1;S;AB;1;2"));
                        Assert.That(traccia.Visione.Any(e => e.Payload == "VMBL1;S;AB;1;2" && e.Codice == "QrDecodificato"), Is.True);
                        Assert.That(traccia.LarghezzaFrame, Is.EqualTo(1280));
                        var corpo = traccia.Visione.Single(e => e.Codice == "CartoncinoRilevato");
                        Assert.That(corpo.AreaCamera.Value.Larghezza / 1280.0,
                            Is.EqualTo(corpo.AreaTavolo.Value.Larghezza).Within(0.00001), "Le aree devono descrivere lo stesso corpo.");

                        var vuota = new RettangoloTavolo(-1, -1, 0, 0);
                        var layout = new LayoutTavolo(vuota, vuota, new RettangoloTavolo(0, 0, 1, 1), vuota);
                        var compositore = new Compositore(rilevatore, new UscitaMidiSilenziosa(), null, new Mappatura(),
                            new ConfigGriglia(), layout);
                        compositore.RegistroDiagnostica.Attivo = true;
                        compositore.Passo();
                        Assert.That(compositore.RegistroDiagnostica.Istantanea().Catture.Single().Visione
                            .Single(e => e.Codice == "QrDecodificato").Zona, Is.EqualTo("Ab"), "Usare il layout reale, non quello di default.");
                    }
                }
            }
        }

        private sealed class RilevatoreFinto : IRilevatoreCartoncini
        {
            public bool Lancia;
            public bool Presente = true;
            public IReadOnlyList<CartoncinoRilevato> Rileva()
            {
                if (Lancia) throw new IOException("webcam non disponibile");
                return Presente ? new[] { new CartoncinoRilevato { Payload = "VMBL1;S;AB;1;2",
                    Area = new RettangoloTavolo(0.02, 0.70, 0.07, 0.10) } } : new CartoncinoRilevato[0];
            }
        }
        private sealed class Mappatura : IMappaturaMidi
        {
            public int Canale(int traccia) => traccia;
            public int Nota(int idClip) => 36;
            public int Velocity => 100;
        }

        [Test]
        public void Compositore_Ab12AttendeTreCattureERegistraAncheEccezioni()
        {
            var fonte = new RilevatoreFinto();
            var compositore = new Compositore(fonte, new UscitaMidiSilenziosa(), null, new Mappatura(),
                new ConfigGriglia { Area = LayoutTavolo.AreaGrigliaDefault }, LayoutTavolo.Default);
            compositore.RegistroDiagnostica.Attivo = true;
            compositore.Passo(); compositore.Passo(); compositore.Passo();
            var report = compositore.RegistroDiagnostica.Istantanea();
            Assert.That(report.Catture.Select(c => c.StrutturaStabile.AFine), Is.EqualTo(new[] { 24, 24, 2 }));
            Assert.That(report.Catture.Select(c => c.ConfermeStruttura), Is.EqualTo(new[] { 1, 2, 3 }));
            fonte.Lancia = true;
            Assert.DoesNotThrow(() => compositore.Passo());
            Assert.That(compositore.RegistroDiagnostica.Istantanea().Catture.Last().Errore, Does.Contain("webcam non disponibile"));
            Assert.That(compositore.UltimoStato.Struttura.AFine, Is.EqualTo(2));
            var fallita = compositore.RegistroDiagnostica.Istantanea().Catture.Last();
            Assert.That(fallita.StrutturaStabile, Is.EqualTo(compositore.UltimoStato.Struttura));
            Assert.That(fallita.EsitoStabilizzazione, Is.EqualTo("NonAggiornata"));
        }

        [Test]
        public void CommutabileInattivoEAssenzaCalibrazione_NonSonoQrIlleggibili()
        {
            var commutabile = new RilevatoreCommutabile();
            var traccia = new TracciaCartoncini();
            Assert.That(commutabile.Rileva(traccia), Is.Empty);
            Assert.That(traccia.EsitoVisione, Is.EqualTo("CalibrazioneAssente"));
            var compositore = new Compositore(commutabile, new UscitaMidiSilenziosa(), null, new Mappatura(), new ConfigGriglia());
            compositore.RegistroDiagnostica.Attivo = true;
            compositore.RegistraSenzaCalibrazione(false, 0, 0);
            Assert.That(compositore.RegistroDiagnostica.Istantanea().Catture.Single().EsitoVisione, Is.EqualTo("WebcamAssente"));
        }
    }
}
