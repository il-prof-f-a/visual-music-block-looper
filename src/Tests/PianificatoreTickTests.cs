using System;
using System.Collections.Generic;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class PianificatoreTickTests
    {
        private const double Intervallo = 20.0;

        [Test]
        public void Prossima_NessunRitardo_AggiungeSoloUnIntervallo()
        {
            Assert.That(PianificatoreTick.Prossima(100, 100.3, Intervallo), Is.EqualTo(120).Within(1e-9));
        }

        [Test]
        public void Prossima_RitardoDi23Ms_RiallineaDopoUnIntervalloCompleto()
        {
            double scadenza = PianificatoreTick.Prossima(1000, 1023, 25);
            Assert.That(scadenza, Is.EqualTo(1048).Within(1e-9));
        }

        [Test]
        public void Prossima_RitardoInferioreA2Ms_ConservaLaScadenzaAssoluta()
        {
            double scadenza = PianificatoreTick.Prossima(1000, 1001.999, 25);
            Assert.That(scadenza, Is.EqualTo(1025).Within(1e-9));
        }

        [Test]
        public void Prossima_300Bpm_RiallineaAl20PercentoDellIntervallo()
        {
            double intervallo = Tempo.IntervalloTickMs(300);
            double invio = 1000 + 1.8;

            double scadenza = PianificatoreTick.Prossima(1000, invio, intervallo);

            Assert.That(intervallo * 0.2, Is.LessThan(2));
            Assert.That(scadenza, Is.EqualTo(invio + intervallo).Within(1e-9));
            Assert.That(scadenza - invio, Is.GreaterThanOrEqualTo(intervallo * 0.8));
        }

        [Test]
        public void Prossima_StallDiCinqueIntervalli_RiallineaUnaVolta()
        {
            double adesso = 100 + 5 * Intervallo;
            Assert.That(PianificatoreTick.Prossima(100, adesso, Intervallo), Is.EqualTo(adesso + Intervallo).Within(1e-9));
        }

        [Test]
        public void Prossima_CambioBpm_UsaIlNuovoIntervalloDallaScadenzaSuccessiva()
        {
            double scadenza = PianificatoreTick.Prossima(0, 0, 20);
            Assert.That(scadenza, Is.EqualTo(20).Within(1e-9));
            scadenza = PianificatoreTick.Prossima(scadenza, 20, 10);
            Assert.That(scadenza, Is.EqualTo(30).Within(1e-9));
        }

        [Test]
        public void Prossima_MilleTickA123Virgola4Bpm_NonHaDeriva()
        {
            double intervallo = Tempo.IntervalloTickMs(123.4);
            double scadenza = 0;
            Random casuale = new Random(1);
            for (int i = 0; i < 1000; i++)
            {
                // Il jitter resta sotto la soglia di riallineamento di 2 ms.
                double adesso = scadenza + casuale.NextDouble() * 1.9;
                scadenza = PianificatoreTick.Prossima(scadenza, adesso, intervallo);
            }

            Assert.That(scadenza, Is.EqualTo(1000 * intervallo).Within(1e-6));
        }

        private sealed class FonteVuota : IFonteImmagini
        {
            public OpenCvSharp.Mat Frame() => null;
        }

        private static Calibrazione CalibrazioneValida()
        {
            return Calibrazione.Calcola(
                new[] { new PuntoImmagine(10, 10), new PuntoImmagine(110, 10), new PuntoImmagine(110, 110), new PuntoImmagine(10, 110) },
                new[] { new PuntoTavolo(0, 0), new PuntoTavolo(1, 0), new PuntoTavolo(1, 1), new PuntoTavolo(0, 1) });
        }

        [Test]
        public void ApplicaCalibrazione_ImpostaIlRilevatorePrimaDelSalvataggio()
        {
            var rilevatore = new RilevatoreCommutabile();
            bool attivoDuranteSalvataggio = false;

            string errore = ApplicazioneCalibrazione.Applica(
                rilevatore, new FonteVuota(), CalibrazioneValida(), c => attivoDuranteSalvataggio = rilevatore.Attivo);

            Assert.That(attivoDuranteSalvataggio, Is.True);
            Assert.That(errore, Is.Null);
        }

        [Test]
        public void ApplicaCalibrazione_SalvataggioFallito_MantieneIlRilevatoreEdAvvisa()
        {
            var rilevatore = new RilevatoreCommutabile();

            string errore = ApplicazioneCalibrazione.Applica(
                rilevatore, new FonteVuota(), CalibrazioneValida(), c => throw new System.IO.IOException("disco protetto"));

            Assert.That(rilevatore.Attivo, Is.True);
            Assert.That(errore, Does.Contain("disco protetto"));
        }
    }
}
