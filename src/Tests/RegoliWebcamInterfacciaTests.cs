using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    /// <summary>Test del conteggio dei passi del ciclo di cattura e della costruzione della finestra con i regoli della webcam.</summary>
    [TestFixture]
    public class RegoliWebcamInterfacciaTests
    {
        [Test]
        public void CicloCattura_PassiAlSecondo_ZeroPrimaDellAvvio()
        {
            using (var ciclo = new CicloCattura(() => { }, 10))
            {
                Assert.That(ciclo.PassiAlSecondo, Is.EqualTo(0));
            }
        }

        [Test]
        public void CicloCattura_PassiAlSecondo_ApprossimaIlRitmo()
        {
            using (var ciclo = new CicloCattura(() => { }, 20))
            {
                ciclo.Avvia();
                Thread.Sleep(700);
                double fps = ciclo.PassiAlSecondo;
                ciclo.Ferma();

                // Periodo di 20 ms: ideale 50 passi/s; tolleranza larga per la precisione dei timer di Windows.
                Assert.That(fps, Is.GreaterThan(10));
                Assert.That(fps, Is.LessThan(100));
            }
        }

        [Test]
        public void CicloCattura_PassiAlSecondo_ContaAncheIPassiCheLanciano()
        {
            using (var ciclo = new CicloCattura(() => throw new InvalidOperationException("errore di prova"), 20))
            {
                ciclo.Avvia();
                Thread.Sleep(500);
                double fps = ciclo.PassiAlSecondo;
                ciclo.Ferma();

                Assert.That(fps, Is.GreaterThan(10));
            }
        }

        private sealed class WebcamImpostazioniFinta : ISorgenteWebcam
        {
            private readonly bool supporta;

            public WebcamImpostazioniFinta(bool supporta)
            {
                this.supporta = supporta;
            }

            public int Larghezza => 1280;

            public int Altezza => 720;

            public double FpsCatturati => 25;

            public Mat Frame() { return null; }

            public Bitmap Anteprima() { return null; }

            public Bitmap AnteprimaRaddrizzata(Calibrazione calibrazione, int larghezza, int altezza) { return null; }

            public void ApplicaImpostazioni(ConfigurazioneWebcam impostazioni) { }

            public StatoImpostazioniWebcam LeggiImpostazioni()
            {
                return supporta
                    ? new StatoImpostazioniWebcam
                    {
                        FuocoSupportato = true, FuocoAutomatico = false, Fuoco = 120,
                        EsposizioneSupportata = true, EsposizioneAutomatica = false, Esposizione = -7,
                        FpsDichiarati = 30
                    }
                    : StatoImpostazioniWebcam.NonSupportato();
            }

            public void Dispose() { }
        }

        private sealed class MappaturaMinima : IMappaturaMidi
        {
            public int Canale(int traccia) => 0;

            public int Nota(int idClip) => 36 + idClip;

            public int Velocity => 100;
        }

        private static void CreaESmaltisci(bool supporta, bool conWebcam)
        {
            var config = new ConfigGriglia();
            var rilevatore = new RilevatoreCommutabile();
            var compositore = new Compositore(
                rilevatore, new UscitaMidiSilenziosa(), new IngressoMidiNullo(), new MappaturaMinima(), config);
            var gestore = new GestoreWebcam(i => conWebcam ? new WebcamImpostazioniFinta(supporta) : null);
            if (conWebcam)
            {
                Assert.That(gestore.Apri(0), Is.True);
            }

            using (var form = new FormControllo(
                compositore, new FormProiettore(config), gestore, rilevatore,
                "calibrazione-test.txt", new ConfigurazioneApplicazione(), null, new List<InfoWebcam>(),
                () => 0, "MIDI", new List<string>(), new List<IDisposable>(), false))
            {
                Assert.That(form.Text, Is.Not.Empty);
            }
        }

        [Test]
        public void FormControllo_WebcamCheSupportaLeImpostazioni_CostruzioneEDisposeNonLanciano()
        {
            Assert.DoesNotThrow(() => CreaESmaltisci(supporta: true, conWebcam: true));
        }

        [Test]
        public void FormControllo_WebcamCheNonSupportaLeImpostazioni_CostruzioneEDisposeNonLanciano()
        {
            Assert.DoesNotThrow(() => CreaESmaltisci(supporta: false, conWebcam: true));
        }

        [Test]
        public void FormControllo_SenzaWebcam_CostruzioneEDisposeNonLanciano()
        {
            Assert.DoesNotThrow(() => CreaESmaltisci(supporta: false, conWebcam: false));
        }
    }
}
