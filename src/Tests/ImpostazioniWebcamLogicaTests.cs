using System;
using System.Collections.Generic;
using NUnit.Framework;
using VisualMusicBlockLooper.App;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class ImpostazioniWebcamLogicaTests
    {
        /// <summary>Webcam finta: memorizza i valori, con opzioni per ignorare proprietà o cambiare convenzione.</summary>
        private sealed class CameraFinta : IProprietaCamera
        {
            public readonly Dictionary<ProprietaCamera, double> Valori = new Dictionary<ProprietaCamera, double>
            {
                { ProprietaCamera.Fps, 30 }
            };

            public readonly HashSet<ProprietaCamera> Ignorate = new HashSet<ProprietaCamera>();
            public readonly List<KeyValuePair<ProprietaCamera, double>> Chiamate = new List<KeyValuePair<ProprietaCamera, double>>();
            public bool SoloConvenzioneUnoZero;
            public bool Lancia;

            public bool Imposta(ProprietaCamera proprieta, double valore)
            {
                if (Lancia)
                {
                    throw new InvalidOperationException("errore driver");
                }

                Chiamate.Add(new KeyValuePair<ProprietaCamera, double>(proprieta, valore));
                if (Ignorate.Contains(proprieta))
                {
                    return true; // dichiara di aver accettato, ma non cambia nulla
                }

                if (SoloConvenzioneUnoZero && proprieta == ProprietaCamera.EsposizioneAutomatica
                    && valore != 1 && valore != 0)
                {
                    return false;
                }

                Valori[proprieta] = valore;
                return true;
            }

            public double Leggi(ProprietaCamera proprieta)
            {
                if (Lancia)
                {
                    throw new InvalidOperationException("errore driver");
                }

                double v;
                return Valori.TryGetValue(proprieta, out v) ? v : 0;
            }
        }

        [Test]
        public void WebcamCheAccettaTutto_ApplicaERilegge()
        {
            CameraFinta camera = new CameraFinta();
            ImpostazioniWebcamLogica logica = new ImpostazioniWebcamLogica(camera);

            logica.Applica(new ConfigurazioneWebcam
            {
                FuocoAutomatico = false,
                Fuoco = 120,
                EsposizioneAutomatica = false,
                Esposizione = -6
            });
            StatoImpostazioniWebcam stato = logica.Leggi();

            Assert.That(stato.FuocoSupportato, Is.True);
            Assert.That(stato.EsposizioneSupportata, Is.True);
            Assert.That(stato.FuocoAutomatico, Is.False);
            Assert.That(stato.Fuoco, Is.EqualTo(120));
            Assert.That(stato.EsposizioneAutomatica, Is.False);
            Assert.That(stato.Esposizione, Is.EqualTo(-6).Within(0.001));
            Assert.That(stato.FpsDichiarati, Is.EqualTo(30));
            Assert.That(camera.Valori[ProprietaCamera.FuocoAutomatico], Is.EqualTo(0));
            Assert.That(camera.Valori[ProprietaCamera.EsposizioneAutomatica], Is.EqualTo(0.25));
        }

        [Test]
        public void FuocoAutomatico_NonImpostaIlFuocoManuale()
        {
            CameraFinta camera = new CameraFinta();
            ImpostazioniWebcamLogica logica = new ImpostazioniWebcamLogica(camera);

            logica.Applica(new ConfigurazioneWebcam { FuocoAutomatico = true, Fuoco = 50 });

            Assert.That(camera.Chiamate, Has.None.Matches<KeyValuePair<ProprietaCamera, double>>(c => c.Key == ProprietaCamera.Fuoco));
            Assert.That(logica.Leggi().FuocoAutomatico, Is.True);
        }

        [Test]
        public void CampiNull_NonToccanoIlDriver()
        {
            CameraFinta camera = new CameraFinta();
            ImpostazioniWebcamLogica logica = new ImpostazioniWebcamLogica(camera);

            logica.Applica(new ConfigurazioneWebcam());

            Assert.That(camera.Chiamate, Is.Empty);
        }

        [Test]
        public void WebcamCheIgnoraIlFuoco_FuocoNonSupportatoMaEsposizioneSi()
        {
            CameraFinta camera = new CameraFinta();
            camera.Ignorate.Add(ProprietaCamera.Fuoco);
            camera.Ignorate.Add(ProprietaCamera.FuocoAutomatico);
            camera.Valori[ProprietaCamera.FuocoAutomatico] = 1;
            ImpostazioniWebcamLogica logica = new ImpostazioniWebcamLogica(camera);

            logica.Applica(new ConfigurazioneWebcam
            {
                FuocoAutomatico = false,
                Fuoco = 100,
                EsposizioneAutomatica = false,
                Esposizione = -5
            });
            StatoImpostazioniWebcam stato = logica.Leggi();

            Assert.That(stato.FuocoSupportato, Is.False);
            Assert.That(stato.FuocoAutomatico, Is.Null);
            Assert.That(stato.Fuoco, Is.Null);
            Assert.That(stato.EsposizioneSupportata, Is.True);
            Assert.That(stato.Esposizione, Is.EqualTo(-5).Within(0.001));
        }

        [Test]
        public void AutoEsposizione_ConvenzioneUnoZero_ViaDiRipiego()
        {
            CameraFinta camera = new CameraFinta { SoloConvenzioneUnoZero = true };
            ImpostazioniWebcamLogica logica = new ImpostazioniWebcamLogica(camera);

            logica.Applica(new ConfigurazioneWebcam { EsposizioneAutomatica = true });
            StatoImpostazioniWebcam stato = logica.Leggi();

            Assert.That(camera.Valori[ProprietaCamera.EsposizioneAutomatica], Is.EqualTo(1));
            Assert.That(stato.EsposizioneSupportata, Is.True);
            Assert.That(stato.EsposizioneAutomatica, Is.True);

            logica.Applica(new ConfigurazioneWebcam { EsposizioneAutomatica = false, Esposizione = -4 });
            stato = logica.Leggi();

            Assert.That(camera.Valori[ProprietaCamera.EsposizioneAutomatica], Is.EqualTo(0));
            Assert.That(stato.EsposizioneAutomatica, Is.False);
            Assert.That(stato.Esposizione, Is.EqualTo(-4).Within(0.001));
        }

        [Test]
        public void AutoEsposizione_ConvenzioneDirectShow_UsaSettantacinque()
        {
            CameraFinta camera = new CameraFinta();
            ImpostazioniWebcamLogica logica = new ImpostazioniWebcamLogica(camera);

            logica.Applica(new ConfigurazioneWebcam { EsposizioneAutomatica = true });

            Assert.That(camera.Valori[ProprietaCamera.EsposizioneAutomatica], Is.EqualTo(0.75));
            Assert.That(logica.Leggi().EsposizioneAutomatica, Is.True);
        }

        [Test]
        public void AutoEsposizioneIgnorata_NonSupportata()
        {
            CameraFinta camera = new CameraFinta();
            camera.Ignorate.Add(ProprietaCamera.EsposizioneAutomatica);
            camera.Ignorate.Add(ProprietaCamera.Esposizione);
            ImpostazioniWebcamLogica logica = new ImpostazioniWebcamLogica(camera);

            logica.Applica(new ConfigurazioneWebcam { EsposizioneAutomatica = false, Esposizione = -3 });
            StatoImpostazioniWebcam stato = logica.Leggi();

            Assert.That(stato.EsposizioneSupportata, Is.False);
            Assert.That(stato.EsposizioneAutomatica, Is.Null);
            Assert.That(stato.Esposizione, Is.Null);
        }

        [Test]
        public void Valori_SonoLimitatiAgliIntervalli()
        {
            CameraFinta camera = new CameraFinta();
            ImpostazioniWebcamLogica logica = new ImpostazioniWebcamLogica(camera);

            logica.Applica(new ConfigurazioneWebcam
            {
                FuocoAutomatico = false,
                Fuoco = 400,
                EsposizioneAutomatica = false,
                Esposizione = -20
            });

            Assert.That(camera.Valori[ProprietaCamera.Fuoco], Is.EqualTo(255));
            Assert.That(camera.Valori[ProprietaCamera.Esposizione], Is.EqualTo(-13));
            Assert.That(logica.Leggi().Fuoco, Is.EqualTo(255));
        }

        [Test]
        public void ErroriDelDriver_DiventanoNonSupportato_SenzaEccezioni()
        {
            CameraFinta camera = new CameraFinta { Lancia = true };
            ImpostazioniWebcamLogica logica = new ImpostazioniWebcamLogica(camera);
            StatoImpostazioniWebcam stato = null;

            Assert.DoesNotThrow(() =>
            {
                logica.Applica(new ConfigurazioneWebcam
                {
                    FuocoAutomatico = false,
                    Fuoco = 10,
                    EsposizioneAutomatica = true,
                    Esposizione = -2
                });
                stato = logica.Leggi();
            });

            Assert.That(stato.FuocoSupportato, Is.False);
            Assert.That(stato.EsposizioneSupportata, Is.False);
            Assert.That(stato.Fuoco, Is.Null);
            Assert.That(stato.Esposizione, Is.Null);
        }

        [Test]
        public void LeggiSenzaApplicare_VerificaIlSupportoSenzaCambiareIValori()
        {
            CameraFinta camera = new CameraFinta();
            camera.Valori[ProprietaCamera.Fuoco] = 80;
            camera.Valori[ProprietaCamera.FuocoAutomatico] = 0;
            camera.Valori[ProprietaCamera.Esposizione] = -7;
            camera.Valori[ProprietaCamera.EsposizioneAutomatica] = 0.25;
            ImpostazioniWebcamLogica logica = new ImpostazioniWebcamLogica(camera);

            StatoImpostazioniWebcam stato = logica.Leggi();

            Assert.That(stato.FuocoSupportato, Is.True);
            Assert.That(stato.Fuoco, Is.EqualTo(80));
            Assert.That(stato.FuocoAutomatico, Is.False);
            Assert.That(stato.EsposizioneSupportata, Is.True);
            Assert.That(stato.Esposizione, Is.EqualTo(-7).Within(0.001));
            Assert.That(stato.EsposizioneAutomatica, Is.False);
        }
    }
}
