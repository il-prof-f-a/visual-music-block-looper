using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;
using Mat = OpenCvSharp.Mat;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture, Apartment(ApartmentState.STA)]
    public class DiagnosticaInterfacciaTests
    {
        private sealed class Mappatura : IMappaturaMidi
        {
            public int Canale(int traccia) => 0;
            public int Nota(int idClip) => 36;
            public int Velocity => 100;
        }

        private sealed class WebcamFinta : ISorgenteWebcam
        {
            private readonly Func<Mat> acquisisci;
            public WebcamFinta(Func<Mat> acquisisci) { this.acquisisci = acquisisci; }
            public int Larghezza => 640;
            public int Altezza => 480;
            public double FpsCatturati => 0;
            public Mat Frame() => acquisisci();
            public Bitmap Anteprima() => null;
            public Bitmap AnteprimaRaddrizzata(Calibrazione c, int w, int h) => null;
            public void ApplicaImpostazioni(ConfigurazioneWebcam c) { }
            public StatoImpostazioniWebcam LeggiImpostazioni() => StatoImpostazioniWebcam.NonSupportato();
            public void Dispose() { }
        }

        private static FormControllo Crea(out Compositore compositore, Func<Mat> acquisisci = null)
        {
            var config = new ConfigGriglia();
            var rilevatore = new RilevatoreCommutabile();
            compositore = new Compositore(rilevatore, new UscitaMidiSilenziosa(), null, new Mappatura(), config);
            var webcam = new GestoreWebcam(i => acquisisci == null ? null : new WebcamFinta(acquisisci));
            if (acquisisci != null) webcam.Apri(0);
            return new FormControllo(compositore, new FormProiettore(config), webcam,
                rilevatore, "calibrazione-test.txt", new ConfigurazioneApplicazione(), null,
                new List<InfoWebcam>(), () => 0, "MIDI test", new string[0], new IDisposable[0], false);
        }

        private static T Controllo<T>(Form form, string nome) where T : Control
        {
            Control[] trovati = form.Controls.Find(nome, true);
            Assert.That(trovati, Has.Length.EqualTo(1), nome);
            return (T)trovati[0];
        }

        private static void Acquisisci(FormControllo form) => typeof(FormControllo)
            .GetMethod("PassoCattura", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);

        [Test]
        public void SenzaCalibrazione_FrameNulloNonDiventaQrIlleggibile()
        {
            using (var form = Crea(out Compositore compositore, () => null))
            {
                Controllo<CheckBox>(form, "casellaDebugCartoncini").Checked = true;
                Acquisisci(form);
                Assert.That(compositore.RegistroDiagnostica.Istantanea().Catture.Single().EsitoVisione, Is.EqualTo("FrameAssente"));
            }
        }

        [Test]
        public void SenzaCalibrazione_TentativoInVoloNonEntraNellaRaccoltaSuccessiva()
        {
            using (var entrato = new ManualResetEventSlim())
            using (var riprendi = new ManualResetEventSlim())
            using (var form = Crea(out Compositore compositore, () =>
                { entrato.Set(); riprendi.Wait(5000); return null; }))
            {
                var casella = Controllo<CheckBox>(form, "casellaDebugCartoncini");
                casella.Checked = true;
                Task acquisizione = Task.Run(() => Acquisisci(form));
                try
                {
                    Assert.That(entrato.Wait(3000), Is.True);
                    casella.Checked = false; casella.Checked = true;
                }
                finally { riprendi.Set(); Assert.That(acquisizione.Wait(3000), Is.True); }
                Assert.That(compositore.RegistroDiagnostica.NumeroCatture, Is.Zero);
            }
        }

        [Test]
        public void SenzaCalibrazione_DurataComprendeLAcquisizione()
        {
            using (var form = Crea(out Compositore compositore, () => { Thread.Sleep(40); return null; }))
            {
                Controllo<CheckBox>(form, "casellaDebugCartoncini").Checked = true;
                Acquisisci(form);
                Assert.That(compositore.RegistroDiagnostica.Istantanea().Catture.Single().DurataMs, Is.GreaterThanOrEqualTo(30));
            }
        }

        [Test]
        public void SpentoAllAvvioEPulsanteDisabilitato()
        {
            using (var form = Crea(out Compositore compositore))
            {
                Assert.That(Controllo<CheckBox>(form, "casellaDebugCartoncini").Checked, Is.False);
                Assert.That(Controllo<Button>(form, "pulsanteSalvaDebug").Enabled, Is.False);
                Assert.That(compositore.RegistroDiagnostica.Attivo, Is.False);
            }
        }

        [Test]
        public void ToggleAbilitaECongelaERiavviaLaRaccolta()
        {
            using (var form = Crea(out Compositore compositore))
            {
                var casella = Controllo<CheckBox>(form, "casellaDebugCartoncini");
                casella.Checked = true;
                Assert.That(compositore.RegistroDiagnostica.Attivo, Is.True);
                compositore.RegistraSenzaCalibrazione(false, 0, 0);
                casella.Checked = false;
                Assert.That(compositore.RegistroDiagnostica.NumeroCatture, Is.EqualTo(1));
                Assert.That(Controllo<Button>(form, "pulsanteSalvaDebug").Enabled, Is.True);
                casella.Checked = true;
                Assert.That(compositore.RegistroDiagnostica.NumeroCatture, Is.Zero);
            }
        }

        [Test]
        public void ControlliDebugVisibiliENonSovrapposti_RenderizzaForm()
        {
            using (var form = Crea(out Compositore compositore))
            {
                foreach (string nome in new[] { "casellaDebugCartoncini", "pulsanteSalvaDebug", "etichettaDebugCartoncini" })
                {
                    var controllo = Controllo<Control>(form, nome);
                    Assert.That(form.ClientRectangle.Contains(controllo.Bounds), Is.True, nome);
                    foreach (Control altro in form.Controls)
                        if (altro != controllo) Assert.That(controllo.Bounds.IntersectsWith(altro.Bounds), Is.False, nome + "/" + altro.Name);
                }
                using (var immagine = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                {
                    using (Graphics g = Graphics.FromImage(immagine))
                    {
                        g.Clear(form.BackColor);
                        // Renderizzare i figli singolarmente: la form nascosta non deve avviare i thread.
                        foreach (Control controllo in form.Controls)
                        using (var figlio = new Bitmap(controllo.Width, controllo.Height))
                        {
                            IntPtr handle = controllo.Handle;
                            controllo.DrawToBitmap(figlio, controllo.ClientRectangle);
                            g.DrawImageUnscaled(figlio, controllo.Left, controllo.Top);
                        }
                    }
                    immagine.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug-cartoncini-ui.png"));
                }
            }
        }

        [Test]
        public void SalvaDebug_DallaFormEsportaIlBufferCongelato()
        {
            using (var form = Crea(out Compositore compositore))
            {
                var casella = Controllo<CheckBox>(form, "casellaDebugCartoncini");
                casella.Checked = true;
                compositore.RegistraSenzaCalibrazione(false, 0, 0);
                casella.Checked = false;
                var etichetta = Controllo<Label>(form, "etichettaDebugCartoncini");
                // La form resta nascosta per non avviare webcam/proiettore: PerformClick usa CanSelect e
                // non invia Click a pulsanti di una form nascosta. Sollevare l'evento reale del controllo.
                typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(Controllo<Button>(form, "pulsanteSalvaDebug"), new object[] { EventArgs.Empty });
                DateTime limite = DateTime.UtcNow.AddSeconds(8);
                while (string.IsNullOrEmpty(etichetta.AccessibleDescription) && DateTime.UtcNow < limite)
                {
                    Application.DoEvents(); Thread.Sleep(10);
                }
                string file = etichetta.AccessibleDescription;
                try
                {
                    Assert.That(File.Exists(file), Is.True, etichetta.Text);
                    Assert.That(File.ReadAllText(file), Does.Contain("WebcamAssente"));
                    Assert.That(compositore.RegistroDiagnostica.NumeroCatture, Is.EqualTo(1));
                }
                finally { if (File.Exists(file)) File.Delete(file); }
            }
        }
    }
}
