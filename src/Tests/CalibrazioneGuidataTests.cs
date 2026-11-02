using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;
using Mat = OpenCvSharp.Mat;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture, Apartment(ApartmentState.STA)]
    public class CalibrazioneGuidataTests
    {
        private sealed class Mappatura : IMappaturaMidi
        {
            public int Canale(int traccia) => 0;
            public int Nota(int idClip) => 36;
            public int Velocity => 100;
        }

        private sealed class CameraFinta : ISorgenteWebcam
        {
            public int Larghezza { get; set; } = 1280;
            public int Altezza { get; set; } = 720;
            public double FpsCatturati => 0;
            public Size? DimensioniAnteprima { get; set; }
            public bool AnteprimaAssente { get; set; }
            public Mat Frame() => null;
            public Bitmap Anteprima() => AnteprimaAssente ? null
                : new Bitmap(DimensioniAnteprima?.Width ?? Larghezza, DimensioniAnteprima?.Height ?? Altezza);
            public Bitmap AnteprimaRaddrizzata(Calibrazione c, int w, int h) => new Bitmap(w, h);
            public void ApplicaImpostazioni(ConfigurazioneWebcam c) { }
            public StatoImpostazioniWebcam LeggiImpostazioni() => StatoImpostazioniWebcam.NonSupportato();
            public void Dispose() { }
        }

        private sealed class Scena : IDisposable
        {
            public readonly string Cartella = Path.Combine(Path.GetTempPath(), "vmbl-guidata-" + Guid.NewGuid().ToString("N"));
            public readonly CameraFinta Camera = new CameraFinta();
            public readonly RilevatoreCommutabile Rilevatore = new RilevatoreCommutabile();
            public readonly FormControllo Form;
            public int Aperture;
            public string FileCalibrazione => Path.Combine(Cartella, "calibrazione.txt");
            public PictureBox Anteprima => Form.Controls.OfType<PictureBox>().Single();
            public string Testo => string.Join("\n", Form.Controls.OfType<Label>().Select(c => c.Text));

            public Scena(bool precedente = false)
            {
                Directory.CreateDirectory(Cartella);
                var webcam = new GestoreWebcam(i => { Aperture++; return Camera; });
                webcam.Apri(0);
                if (precedente)
                {
                    Calibrazione calibrazione = Calibrazione.Calcola(new[]
                    {
                        new PuntoImmagine(0,0),new PuntoImmagine(1280,0),
                        new PuntoImmagine(1280,720),new PuntoImmagine(0,720)
                    }, new[] { new PuntoTavolo(0,0),new PuntoTavolo(1,0),new PuntoTavolo(1,1),new PuntoTavolo(0,1) },1280,720);
                    calibrazione.Salva(FileCalibrazione);
                    Rilevatore.Imposta(new RilevatoreCartoncini(webcam, calibrazione));
                }
                var config = new ConfigGriglia();
                var compositore = new Compositore(Rilevatore, new UscitaMidiSilenziosa(), null, new Mappatura(), config);
                Form = new FormControllo(compositore, new FormProiettore(config), webcam, Rilevatore,
                    FileCalibrazione, new ConfigurazioneApplicazione(), Path.Combine(Cartella,"configurazione.json"),
                    new List<InfoWebcam>(), () => 0, "MIDI test", new string[0], new IDisposable[0], precedente);
            }

            public T Campo<T>(string nome) => (T)typeof(FormControllo)
                .GetField(nome, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Form);

            public void Metodo(string nome) => typeof(FormControllo)
                .GetMethod(nome, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Form, null);

            public void Avvia()
            {
                typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(Campo<Button>("pulsanteCalibra"), new object[] { EventArgs.Empty });
                Assert.That(Testo, Does.Contain("1/12"));
            }

            public void Click(int indice, bool errato = false)
            {
                PuntoImmagine p = CalibrazioneDodiciPuntiTests.Camera(SchemaCalibrazione.Punti[indice]);
                if (errato) p = new PuntoImmagine(p.X + 120, p.Y + 60);
                Size frameMostrato = Anteprima.Image.Size;
                double scala = Math.Min((double)Anteprima.ClientSize.Width / frameMostrato.Width,
                    (double)Anteprima.ClientSize.Height / frameMostrato.Height);
                int x = (int)Math.Round((Anteprima.ClientSize.Width - frameMostrato.Width * scala) / 2 + p.X * scala);
                int y = (int)Math.Round((Anteprima.ClientSize.Height - frameMostrato.Height * scala) / 2 + p.Y * scala);
                typeof(Control).GetMethod("OnMouseClick", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(Anteprima, new object[] { new MouseEventArgs(MouseButtons.Left, 1, x, y, 0) });
            }

            public void Tasto(Keys tasto)
            {
                object[] argomenti = { Message.Create(IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero), tasto };
                bool gestito = (bool)typeof(FormControllo).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(Form, argomenti);
                Assert.That(gestito, Is.True, tasto.ToString());
            }

            public void Dispose()
            {
                Form.Dispose();
                Directory.Delete(Cartella, true);
            }
        }

        [Test]
        public void Calibrazione_CompletaSoloAlDodicesimoClick()
        {
            using (var scena = new Scena())
            {
                scena.Avvia();
                for (int i = 0; i < 11; i++)
                {
                    scena.Click(i);
                    Assert.That(File.Exists(scena.FileCalibrazione), Is.False);
                    Assert.That(scena.Testo, Does.Contain((i + 2) + "/12"));
                }
                scena.Click(11);
                Assert.That(File.Exists(scena.FileCalibrazione), Is.True);
                Assert.That(scena.Rilevatore.Attivo, Is.True);
                Assert.That(scena.Testo, Does.Contain("12 punti").And.Contain("correzione curva").And.Not.Contain("Scarto medio"));
                PuntoTavolo p = Calibrazione.Carica(scena.FileCalibrazione)
                    .DaCameraATavolo(CalibrazioneDodiciPuntiTests.Camera(new PuntoTavolo(0.5,0.5)));
                Assert.That(p.X, Is.EqualTo(0.5).Within(0.004));
                Assert.That(p.Y, Is.EqualTo(0.5).Within(0.004));
            }
        }

        [Test]
        public void UndoERiavvio_PermettonoDiRipetereIPunti()
        {
            using (var scena = new Scena())
            {
                scena.Avvia();
                scena.Tasto(Keys.Back);
                Assert.That(scena.Testo, Does.Contain("1/12"));
                scena.Click(0); scena.Click(1);
                Assert.That(scena.Testo, Does.Contain("3/12"));
                scena.Tasto(Keys.Back);
                Assert.That(scena.Testo, Does.Contain("2/12"));
                scena.Click(1);
                Assert.That(scena.Testo, Does.Contain("3/12"));
                scena.Avvia();
                Assert.That(File.Exists(scena.FileCalibrazione), Is.False);
            }
        }

        [Test]
        public void Annullamento_RipristinaControlliEConservaCalibrazionePrecedente()
        {
            using (var scena = new Scena(true))
            {
                Calibrazione precedente = scena.Rilevatore.CalibrazioneAttiva;
                byte[] file = File.ReadAllBytes(scena.FileCalibrazione);
                scena.Avvia(); scena.Click(0);
                scena.Tasto(Keys.Escape);
                Assert.That(scena.Testo, Does.Contain("annullata").And.Not.Contain("2/12"));
                Assert.That(scena.Rilevatore.CalibrazioneAttiva, Is.SameAs(precedente));
                Assert.That(File.ReadAllBytes(scena.FileCalibrazione), Is.EqualTo(file));
                Assert.That(scena.Campo<ComboBox>("comboWebcam").Enabled, Is.True);
                Assert.That(scena.Campo<ComboBox>("comboMonitor").Enabled, Is.True);
                Assert.That(scena.Campo<TrackBar>("regoloRisoluzione").Enabled, Is.True);
            }
        }

        [Test]
        public void PuntoErrato_NonApplicaOSalvaLaNuovaCalibrazione()
        {
            using (var scena = new Scena(true))
            {
                Calibrazione precedente = scena.Rilevatore.CalibrazioneAttiva;
                byte[] file = File.ReadAllBytes(scena.FileCalibrazione);
                scena.Avvia();
                for (int i = 0; i < 12; i++) scena.Click(i, i == 5);
                Assert.That(scena.Rilevatore.CalibrazioneAttiva, Is.SameAs(precedente));
                Assert.That(File.ReadAllBytes(scena.FileCalibrazione), Is.EqualTo(file));
                Assert.That(scena.Testo, Does.Contain("incoerenti").And.Contain("6"));
                Assert.That(scena.Testo, Does.Contain("6/12"));
                Assert.That(scena.Campo<ComboBox>("comboWebcam").Enabled, Is.False);
                scena.Click(5);
                Assert.That(scena.Rilevatore.CalibrazioneAttiva, Is.Not.SameAs(precedente));
                Assert.That(scena.Campo<ComboBox>("comboWebcam").Enabled, Is.True);
                Assert.That(scena.Testo, Does.Contain("completata"));
            }
        }

        [Test]
        public void DuePuntiIncoerenti_RichiedonoSoloLaRipetizioneDeiDueRiferimenti()
        {
            using (var scena = new Scena(true))
            {
                byte[] file = File.ReadAllBytes(scena.FileCalibrazione);
                scena.Avvia();
                for (int i = 0; i < 12; i++) scena.Click(i, i == 9 || i == 11);
                Assert.That(scena.Testo, Does.Contain("10/12"));
                Assert.That(File.ReadAllBytes(scena.FileCalibrazione), Is.EqualTo(file));
                scena.Click(9);
                Assert.That(scena.Testo, Does.Contain("12/12"));
                Assert.That(File.ReadAllBytes(scena.FileCalibrazione), Is.EqualTo(file));
                scena.Click(11);
                Assert.That(scena.Testo, Does.Contain("completata"));
                Assert.That(File.ReadAllBytes(scena.FileCalibrazione), Is.Not.EqualTo(file));
            }
        }

        [Test]
        public void Correzione_UndoEdEscConservanoLaCalibrazionePrecedente()
        {
            using (var scena = new Scena(true))
            {
                byte[] file = File.ReadAllBytes(scena.FileCalibrazione);
                scena.Avvia();
                for (int i = 0; i < 12; i++) scena.Click(i, i == 5);
                Assert.That(scena.Testo, Does.Contain("6/12"));
                scena.Tasto(Keys.Back);
                Assert.That(scena.Testo, Does.Contain("5/12"));
                scena.Click(4);
                Assert.That(scena.Testo, Does.Contain("6/12"));
                scena.Tasto(Keys.Escape);
                Assert.That(File.ReadAllBytes(scena.FileCalibrazione), Is.EqualTo(file));
                Assert.That(scena.Campo<ComboBox>("comboWebcam").Enabled, Is.True);
            }
        }

        [Test]
        public void Raccolta_BloccaGeometriaEAnnullaIlCambioRisoluzioneInSospeso()
        {
            using (var scena = new Scena())
            {
                var regolo = scena.Campo<TrackBar>("regoloRisoluzione");
                regolo.Value = 0;
                scena.Campo<System.Windows.Forms.Timer>("timerRisoluzione").Start();
                scena.Avvia();
                Assert.That(scena.Campo<System.Windows.Forms.Timer>("timerRisoluzione").Enabled, Is.False);
                Assert.That(regolo.Value, Is.EqualTo(PresetRisoluzione.IndiceVicino(1280,720)));
                Assert.That(regolo.Enabled, Is.False);
                Assert.That(scena.Campo<ComboBox>("comboWebcam").Enabled, Is.False);
                Assert.That(scena.Campo<ComboBox>("comboMonitor").Enabled, Is.False);
                Assert.That(scena.Campo<Button>("pulsanteAggiornaWebcam").Enabled, Is.False);
                scena.Metodo("ApplicaRisoluzione");
                Assert.That(scena.Aperture, Is.EqualTo(1));
            }
        }

        [Test]
        public void DimensioniFrameCambiate_AnnullanoLaRaccoltaSenzaRiscrivereIlFile()
        {
            using (var scena = new Scena(true))
            {
                byte[] file = File.ReadAllBytes(scena.FileCalibrazione);
                scena.Avvia(); scena.Click(0);
                scena.Camera.Larghezza = 640; scena.Camera.Altezza = 480;
                scena.Metodo("AggiornaInterfaccia");
                Assert.That(scena.Testo, Does.Contain("risoluzione").And.Contain("annullata"));
                Assert.That(File.ReadAllBytes(scena.FileCalibrazione), Is.EqualTo(file));
                Assert.That(scena.Rilevatore.Attivo, Is.False);
                Assert.That(scena.Campo<ComboBox>("comboWebcam").Enabled, Is.True);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DimensioniCambiatePrimaDellUltimoClick_IntercettateAncheSenzaRefreshUi(bool soloBitmap)
        {
            using (var scena = new Scena(true))
            {
                byte[] file = File.ReadAllBytes(scena.FileCalibrazione);
                scena.Avvia();
                for (int i = 0; i < 11; i++) scena.Click(i);
                if (soloBitmap) scena.Camera.DimensioniAnteprima = new Size(640, 480);
                else { scena.Camera.Larghezza = 640; scena.Camera.Altezza = 480; }
                scena.Click(11);
                Assert.That(File.ReadAllBytes(scena.FileCalibrazione), Is.EqualTo(file));
                Assert.That(scena.Rilevatore.Attivo, Is.False);
                Assert.That(scena.Testo, Does.Contain("risoluzione").And.Contain("annullata"));
            }
        }

        [Test]
        public void AnteprimaNonDisponibileAllaConferma_NonSalvaUnaCalibrazioneSuImmagineVecchia()
        {
            using (var scena = new Scena(true))
            {
                byte[] file = File.ReadAllBytes(scena.FileCalibrazione);
                Calibrazione precedente = scena.Rilevatore.CalibrazioneAttiva;
                scena.Avvia();
                for (int i = 0; i < 11; i++) scena.Click(i);
                scena.Camera.AnteprimaAssente = true;
                scena.Click(11);
                Assert.That(File.ReadAllBytes(scena.FileCalibrazione), Is.EqualTo(file));
                Assert.That(scena.Rilevatore.CalibrazioneAttiva, Is.SameAs(precedente));
                Assert.That(scena.Testo, Does.Contain("non disponibile").And.Contain("annullata"));
                Assert.That(scena.Campo<ComboBox>("comboWebcam").Enabled, Is.True);
            }
        }

        [Test]
        public void RiavvioSenzaFrame_NonUsaLAnteprimaResiduaDellaRaccoltaPrecedente()
        {
            using (var scena = new Scena())
            {
                scena.Avvia(); scena.Click(0); scena.Tasto(Keys.Escape);
                scena.Camera.AnteprimaAssente = true;
                scena.Metodo("AvviaCalibrazione");
                Assert.That(scena.Testo, Does.Contain("non disponibile").And.Not.Contain("1/12"));
                Assert.That(scena.Campo<ComboBox>("comboWebcam").Enabled, Is.True);
                Assert.That(File.Exists(scena.FileCalibrazione), Is.False);
            }
        }

        [Test]
        public void ResizeFraIClick_ConservaLeCoordinateCamera()
        {
            using (var scena = new Scena())
            {
                scena.Avvia();
                for (int i = 0; i < 12; i++)
                {
                    if (i == 5) scena.Form.ClientSize = new Size(1366, 900);
                    scena.Click(i);
                }
                Assert.That(scena.Rilevatore.Attivo, Is.True, scena.Testo);
                var centro = scena.Rilevatore.CalibrazioneAttiva.DaCameraATavolo(
                    CalibrazioneDodiciPuntiTests.Camera(new PuntoTavolo(0.5,0.5)));
                Assert.That(centro.X, Is.EqualTo(0.5).Within(0.004));
                Assert.That(centro.Y, Is.EqualTo(0.5).Within(0.004));
            }
        }
    }
}
