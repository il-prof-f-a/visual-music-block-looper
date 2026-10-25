using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class ComponentiApplicazioneTests
    {
        [Test]
        public void ValutaRisoluzione_ApplicaAvvisiESceglieAttivazione()
        {
            Calibrazione uguale = CalibrazioneConRisoluzione(1280, 720);
            Calibrazione diversa = CalibrazioneConRisoluzione(640, 480);
            Calibrazione ignota = CalibrazioneConRisoluzione(0, 0);

            Assert.That(ApplicazioneCalibrazione.ValutaRisoluzione(uguale, 1280, 720, out string a1), Is.True);
            Assert.That(a1, Is.Null);
            Assert.That(ApplicazioneCalibrazione.ValutaRisoluzione(diversa, 1280, 720, out string a2), Is.False);
            Assert.That(a2, Is.EqualTo("Calibrazione per una risoluzione diversa: rifai la calibrazione"));
            Assert.That(ApplicazioneCalibrazione.ValutaRisoluzione(ignota, 1280, 720, out string a3), Is.True);
            Assert.That(a3, Is.EqualTo("Calibrazione senza risoluzione: ripetila per sicurezza"));
        }

        private static Calibrazione CalibrazioneConRisoluzione(int larghezza, int altezza)
        {
            return Calibrazione.Calcola(
                new[] { new PuntoImmagine(0, 0), new PuntoImmagine(100, 0), new PuntoImmagine(100, 100), new PuntoImmagine(0, 100) },
                new[] { new PuntoTavolo(0, 0), new PuntoTavolo(1, 0), new PuntoTavolo(1, 1), new PuntoTavolo(0, 1) },
                larghezza,
                altezza);
        }

        private sealed class UscitaContaClock : IUscitaMidi
        {
            private int clock;

            public int Clock_ => Volatile.Read(ref clock);

            public void NotaOn(int canale, int nota, int velocity)
            {
            }

            public void NotaOff(int canale, int nota)
            {
            }

            public void Clock() => Interlocked.Increment(ref clock);

            public void Start()
            {
            }

            public void Stop()
            {
            }

            public void Continue()
            {
            }
        }

        private sealed class MappaturaFinta : IMappaturaMidi
        {
            public int Canale(int traccia) => traccia;

            public int Nota(int idClip) => 36 + idClip;

            public int Velocity => 100;
        }

        [Test]
        public void ConversioneAnteprima_ImmagineCentrataInZoom_MappaIPixel()
        {
            // Box 400x200, immagine 100x100: scala 2, offset orizzontale 100.
            var punto = ConversioneAnteprima.DaPictureBoxAImmagine(
                new Point(200, 100), new Size(400, 200), new Size(100, 100));

            Assert.That(punto, Is.Not.Null);
            Assert.That(punto.Value.X, Is.EqualTo(50).Within(1e-9));
            Assert.That(punto.Value.Y, Is.EqualTo(50).Within(1e-9));
        }

        [Test]
        public void ConversioneAnteprima_ClickNelBordoNero_RestituisceNull()
        {
            var punto = ConversioneAnteprima.DaPictureBoxAImmagine(
                new Point(10, 100), new Size(400, 200), new Size(100, 100));

            Assert.That(punto, Is.Null);
        }

        [Test]
        public void RilevatoreCommutabile_SenzaRilevatore_RestituisceListaVuota()
        {
            var commutabile = new RilevatoreCommutabile();

            Assert.That(commutabile.Attivo, Is.False);
            Assert.That(commutabile.Rileva(), Is.Empty);
        }

        [Test]
        public void TickMaster_InMasterGeneraClockEPoiSiFerma()
        {
            var uscita = new UscitaContaClock();
            var motore = new MotoreMusicale(new Sequencer(new ConfigGriglia()), uscita, null, new MappaturaFinta(), 300);
            using (var tickMaster = new TickMaster(motore, () => 0))
            {
                tickMaster.Avvia();
                Thread.Sleep(400);
                tickMaster.Ferma();
            }

            int dopoStop = uscita.Clock_;
            Thread.Sleep(100);

            // A 300 BPM il tick dura circa 8,3 ms: in 400 ms ne servono molti più di 10.
            Assert.That(dopoStop, Is.GreaterThan(10));
            Assert.That(uscita.Clock_, Is.EqualTo(dopoStop));
        }

        [Test]
        public void TickMaster_ChiamaDopoTickPerOgniTick()
        {
            var uscita = new UscitaContaClock();
            var motore = new MotoreMusicale(new Sequencer(new ConfigGriglia()), uscita, null, new MappaturaFinta(), 300);
            int chiamate = 0;
            using (var tickMaster = new TickMaster(motore, () => 0, () => Interlocked.Increment(ref chiamate)))
            {
                tickMaster.Avvia();
                Thread.Sleep(400);
                tickMaster.Ferma();
            }

            Assert.That(chiamate, Is.GreaterThan(10));
            Assert.That(chiamate, Is.EqualTo(uscita.Clock_));
        }

        [Test]
        public void FormControllo_CreataSenzaMostrareESmaltita_NonLancia()
        {
            var config = new ConfigGriglia();
            var rilevatore = new RilevatoreCommutabile();
            var compositore = new Compositore(
                rilevatore, new UscitaMidiSilenziosa(), new IngressoMidiNullo(), new MappaturaFinta(), config);

            Assert.DoesNotThrow(() =>
            {
                using (var form = new FormControllo(
                    compositore, new FormProiettore(config), new GestoreWebcam(i => null), rilevatore,
                    "calibrazione-test.txt", new ConfigurazioneApplicazione(), null, new List<InfoWebcam>(),
                    () => 0, "MIDI", new List<string>(), new List<System.IDisposable>(), false))
                {
                    Assert.That(form.Text, Is.Not.Empty);
                }
            });
        }
    }
}
