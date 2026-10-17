using NUnit.Framework;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class TapTempoTests
    {
        [Test]
        public void PrimoTapNessunBpm()
        {
            TapTempo tapTempo = new TapTempo();

            Assert.That(tapTempo.Tap(1000), Is.Null);
        }

        [Test]
        public void QuattroTapA500ms_120Bpm()
        {
            TapTempo tapTempo = new TapTempo();

            tapTempo.Tap(0);
            tapTempo.Tap(500);
            tapTempo.Tap(1000);
            double? bpm = tapTempo.Tap(1500);

            Assert.That(bpm, Is.Not.Null);
            Assert.That(bpm.Value, Is.EqualTo(120.0).Within(1e-9));
        }

        [Test]
        public void PausaLungaAzzera()
        {
            TapTempo tapTempo = new TapTempo();

            tapTempo.Tap(0);
            tapTempo.Tap(1000);
            Assert.That(tapTempo.Tap(5000), Is.Null);

            // Dopo la pausa la storia è vuota: il BPM dipende solo dai nuovi intervalli (500 ms -> 120).
            double? bpm = tapTempo.Tap(5500);

            Assert.That(bpm, Is.Not.Null);
            Assert.That(bpm.Value, Is.EqualTo(120.0).Within(1e-9));
        }

        [Test]
        public void TapDopoPausa_RipartePrimoTapNull()
        {
            TapTempo tapTempo = new TapTempo();

            tapTempo.Tap(0);
            tapTempo.Tap(500);
            Assert.That(tapTempo.Tap(5000), Is.Null);
            Assert.That(tapTempo.Tap(5500), Is.Not.Null);
        }

        [Test]
        public void Tap_TimestampNonMonotono_Azzera()
        {
            TapTempo tapTempo = new TapTempo();

            tapTempo.Tap(1000);
            Assert.That(tapTempo.Tap(500), Is.Null);

            // Il tap a 500 è il nuovo primo tap: il successivo a 1000 forma un intervallo di 500 ms (120 BPM).
            double? bpm = tapTempo.Tap(1000);

            Assert.That(bpm, Is.Not.Null);
            Assert.That(bpm.Value, Is.EqualTo(120.0).Within(1e-9));
        }

        [Test]
        public void BpmLimitatoA300()
        {
            TapTempo tapTempo = new TapTempo();

            tapTempo.Tap(0);
            tapTempo.Tap(100);
            double? bpm = tapTempo.Tap(200);

            Assert.That(bpm, Is.Not.Null);
            Assert.That(bpm.Value, Is.EqualTo(Tempo.MaxBpm).Within(1e-9));
        }

        [Test]
        public void BpmLimitatoA30()
        {
            TapTempo tapTempo = new TapTempo();

            tapTempo.Tap(0);
            double? bpm = tapTempo.Tap(2000);

            Assert.That(bpm, Is.Not.Null);
            Assert.That(bpm.Value, Is.EqualTo(Tempo.MinBpm).Within(1e-9));
        }

        [Test]
        public void MediaUltimiQuattroIntervalli()
        {
            TapTempo tapTempo = new TapTempo();

            tapTempo.Tap(0);
            tapTempo.Tap(1000);
            tapTempo.Tap(2000);
            tapTempo.Tap(3000);
            tapTempo.Tap(4000);
            double? bpm = tapTempo.Tap(4500);

            // Ultimi quattro intervalli: 1000, 1000, 1000, 500 -> media 875 ms -> 60000 / 875 BPM.
            Assert.That(bpm, Is.Not.Null);
            Assert.That(bpm.Value, Is.EqualTo(60000.0 / 875.0).Within(1e-9));
        }

        [Test]
        public void Azzera_CancellaLaStoria()
        {
            TapTempo tapTempo = new TapTempo();

            tapTempo.Tap(0);
            tapTempo.Tap(500);
            tapTempo.Azzera();

            Assert.That(tapTempo.Tap(1000), Is.Null);
        }

        [Test]
        public void Limita_NaN_Restituisce120()
        {
            Assert.That(Tempo.Limita(double.NaN), Is.EqualTo(120.0));
        }

        [Test]
        public void Limita_Infinito_Restituisce120()
        {
            Assert.That(Tempo.Limita(double.PositiveInfinity), Is.EqualTo(120.0));
            Assert.That(Tempo.Limita(double.NegativeInfinity), Is.EqualTo(120.0));
        }

        [Test]
        public void IntervalloTick120Bpm()
        {
            // 60000 / (120 * 24) = 20.8333... ms per tick MIDI.
            Assert.That(Tempo.IntervalloTickMs(120), Is.EqualTo(20.8333).Within(0.0001));
        }
    }
}
