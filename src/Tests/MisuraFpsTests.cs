using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using VisualMusicBlockLooper.App;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class MisuraFpsTests
    {
        private long _ora;

        [SetUp]
        public void Prepara()
        {
            _ora = 0;
        }

        private MisuraFps Crea(int finestraMs = 1000)
        {
            return new MisuraFps(() => _ora, finestraMs);
        }

        private void RegistraA(MisuraFps misura, long istante)
        {
            _ora = istante;
            misura.Registra();
        }

        [Test]
        public void Costruttore_OrologioNullo_LanciaArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => new MisuraFps(null));
        }

        [Test]
        public void Costruttore_FinestraTroppoBreve_LanciaArgumentOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MisuraFps(() => 0, 99));
        }

        [Test]
        public void NessunEvento_FpsZero()
        {
            MisuraFps misura = Crea();

            Assert.That(misura.Fps, Is.EqualTo(0.0));
        }

        [Test]
        public void UnSoloEvento_FpsZero()
        {
            MisuraFps misura = Crea();
            RegistraA(misura, 100);

            Assert.That(misura.Fps, Is.EqualTo(0.0));
        }

        [Test]
        public void EventiOgniTrentaMs_FpsAttesi()
        {
            // 11 eventi a 0, 30, ..., 300 ms: 10 intervalli in 0,3 s = 33,33 fps.
            MisuraFps misura = Crea();
            for (int i = 0; i <= 10; i++)
            {
                RegistraA(misura, i * 30);
            }

            Assert.That(misura.Fps, Is.EqualTo(33.333).Within(0.01));
        }

        [Test]
        public void EventiVecchiQuantoLaFinestra_VengonoScartati()
        {
            // Il primo evento (0) e' esattamente finestra ms prima di ora: non conta (timestamp > ora - finestra).
            MisuraFps misura = Crea(1000);
            RegistraA(misura, 0);
            RegistraA(misura, 500);
            _ora = 1000;

            Assert.That(misura.Fps, Is.EqualTo(0.0), "Resta un solo evento valido.");
        }

        [Test]
        public void EventiPiuVecchiDellaFinestra_NonInfluisconoSulValore()
        {
            MisuraFps misura = Crea(1000);
            for (int i = 0; i < 100; i++)
            {
                RegistraA(misura, i * 10);
            }

            // Dopo un lungo intervallo restano solo i due eventi recenti: 1 intervallo in 0,1 s = 10 fps.
            RegistraA(misura, 5000);
            RegistraA(misura, 5100);

            Assert.That(misura.Fps, Is.EqualTo(10.0).Within(0.0001));
        }

        [Test]
        public void StessoIstante_FpsZero()
        {
            MisuraFps misura = Crea();
            RegistraA(misura, 200);
            RegistraA(misura, 200);

            Assert.That(misura.Fps, Is.EqualTo(0.0));
        }

        [Test]
        public void Azzera_CancellaGliEventi()
        {
            MisuraFps misura = Crea();
            for (int i = 0; i <= 10; i++)
            {
                RegistraA(misura, i * 30);
            }

            misura.Azzera();

            Assert.That(misura.Fps, Is.EqualTo(0.0));
        }

        [Test]
        public void RegistrazioniDaPiuThread_NonLanciano()
        {
            long contatore = 0;
            MisuraFps misura = new MisuraFps(() => Interlocked.Increment(ref contatore));

            Assert.DoesNotThrow(() => Parallel.For(0, 4, _ =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    misura.Registra();
                }
            }));
            Assert.That(misura.Fps, Is.GreaterThan(0.0));
        }
    }
}
