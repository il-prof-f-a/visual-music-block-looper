using System.Collections.Generic;
using System.Drawing;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.App;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class GestoreWebcamImpostazioniTests
    {
        private sealed class SorgenteImpostabile : ISorgenteWebcam
        {
            public SorgenteImpostabile(int larghezza, int altezza)
            {
                Larghezza = larghezza;
                Altezza = altezza;
            }

            public int Larghezza { get; }

            public int Altezza { get; }

            public bool Rilasciata { get; private set; }

            public List<ConfigurazioneWebcam> Applicate { get; } = new List<ConfigurazioneWebcam>();

            public double FpsCatturati => 25;

            public Mat Frame() { return null; }

            public Bitmap Anteprima() { return null; }

            public Bitmap AnteprimaRaddrizzata(VisualMusicBlockLooper.Vision.Calibrazione calibrazione, int larghezza, int altezza) { return null; }

            public void ApplicaImpostazioni(ConfigurazioneWebcam impostazioni)
            {
                Applicate.Add(impostazioni);
            }

            public StatoImpostazioniWebcam LeggiImpostazioni()
            {
                return new StatoImpostazioniWebcam { FuocoSupportato = true, Fuoco = 42, FpsDichiarati = 30 };
            }

            public void Dispose() { Rilasciata = true; }
        }

        private sealed class Ambiente
        {
            public readonly List<SorgenteImpostabile> Aperte = new List<SorgenteImpostabile>();
            public readonly List<int[]> Richieste = new List<int[]>();
            public int LarghezzaRifiutata = -1;

            public GestoreWebcam CreaGestore()
            {
                return new GestoreWebcam((indice, larghezza, altezza) =>
                {
                    Richieste.Add(new[] { indice, larghezza, altezza });
                    if (larghezza == LarghezzaRifiutata)
                    {
                        return null;
                    }

                    SorgenteImpostabile s = new SorgenteImpostabile(larghezza > 0 ? larghezza : 1280, altezza > 0 ? altezza : 720);
                    Aperte.Add(s);
                    return s;
                });
            }
        }

        [Test]
        public void ImpostaConfigurazionePrimaDiAprire_UsaDimensioniEImpostazioniAllApertura()
        {
            Ambiente amb = new Ambiente();
            using (GestoreWebcam gestore = amb.CreaGestore())
            {
                gestore.ImpostaConfigurazione(new ConfigurazioneWebcam { Larghezza = 1920, Altezza = 1080, Fuoco = 90 });

                Assert.That(gestore.Apri(new InfoWebcam(3, "Cam", "p")), Is.True);

                Assert.That(amb.Richieste[0], Is.EqualTo(new[] { 3, 1920, 1080 }));
                Assert.That(gestore.Larghezza, Is.EqualTo(1920));
                Assert.That(amb.Aperte[0].Applicate, Has.Count.EqualTo(1));
                Assert.That(amb.Aperte[0].Applicate[0].Fuoco, Is.EqualTo(90));
            }
        }

        [Test]
        public void CostruttoreVecchio_IgnoraLeDimensioni()
        {
            SorgenteImpostabile s = new SorgenteImpostabile(1280, 720);
            using (GestoreWebcam gestore = new GestoreWebcam(i => s))
            {
                gestore.ImpostaConfigurazione(new ConfigurazioneWebcam { Larghezza = 640, Altezza = 480 });

                Assert.That(gestore.Apri(0), Is.True);
                Assert.That(gestore.Larghezza, Is.EqualTo(1280));
            }
        }

        [Test]
        public void CambiaRisoluzione_RiapreConLeNuoveDimensioniMantenendoInfoEImpostazioni()
        {
            Ambiente amb = new Ambiente();
            using (GestoreWebcam gestore = amb.CreaGestore())
            {
                gestore.ImpostaConfigurazione(new ConfigurazioneWebcam { Larghezza = 640, Altezza = 480, EsposizioneAutomatica = false, Esposizione = -6 });
                gestore.Apri(new InfoWebcam(2, "Cam", "perc"));

                bool riuscito = gestore.CambiaRisoluzione(1600, 900);

                Assert.That(riuscito, Is.True);
                Assert.That(amb.Richieste[amb.Richieste.Count - 1], Is.EqualTo(new[] { 2, 1600, 900 }));
                Assert.That(gestore.Larghezza, Is.EqualTo(1600));
                Assert.That(gestore.Altezza, Is.EqualTo(900));
                Assert.That(gestore.Info.Nome, Is.EqualTo("Cam"));
                Assert.That(gestore.IndiceCorrente, Is.EqualTo(2));
                Assert.That(amb.Aperte[0].Rilasciata, Is.True);
                Assert.That(amb.Aperte[1].Applicate[0].Esposizione, Is.EqualTo(-6));
            }
        }

        [Test]
        public void CambiaRisoluzione_SeFallisce_TieneLaWebcamPrecedente()
        {
            Ambiente amb = new Ambiente();
            using (GestoreWebcam gestore = amb.CreaGestore())
            {
                gestore.ImpostaConfigurazione(new ConfigurazioneWebcam { Larghezza = 1280, Altezza = 720 });
                gestore.Apri(0);
                amb.LarghezzaRifiutata = 1920;

                bool riuscito = gestore.CambiaRisoluzione(1920, 1080);

                Assert.That(riuscito, Is.False);
                Assert.That(gestore.Disponibile, Is.True);
                Assert.That(gestore.Larghezza, Is.EqualTo(1280));

                // La richiesta fallita non deve restare memorizzata per le aperture successive.
                amb.LarghezzaRifiutata = -1;
                gestore.Apri(1);
                Assert.That(amb.Richieste[amb.Richieste.Count - 1], Is.EqualTo(new[] { 1, 1280, 720 }));
            }
        }

        [Test]
        public void CambiaRisoluzione_SenzaWebcam_MemorizzaESoloRestituisceFalse()
        {
            Ambiente amb = new Ambiente();
            using (GestoreWebcam gestore = amb.CreaGestore())
            {
                Assert.That(gestore.CambiaRisoluzione(800, 600), Is.False);
                Assert.That(amb.Richieste, Is.Empty);

                gestore.Apri(0);

                Assert.That(amb.Richieste[0], Is.EqualTo(new[] { 0, 800, 600 }));
            }
        }

        [Test]
        public void ApplicaImpostazioni_InoltraAllaSorgenteEMemorizzaPerLeAperture()
        {
            Ambiente amb = new Ambiente();
            using (GestoreWebcam gestore = amb.CreaGestore())
            {
                gestore.Apri(0);

                gestore.ApplicaImpostazioni(new ConfigurazioneWebcam { FuocoAutomatico = false, Fuoco = 70 });
                gestore.CambiaRisoluzione(800, 600);

                Assert.That(amb.Aperte[0].Applicate[amb.Aperte[0].Applicate.Count - 1].Fuoco, Is.EqualTo(70));
                Assert.That(amb.Aperte[1].Applicate[0].Fuoco, Is.EqualTo(70));
                Assert.That(amb.Aperte[1].Applicate[0].FuocoAutomatico, Is.False);
            }
        }

        [Test]
        public void LeggiImpostazioniEFps_ConESenzaWebcam()
        {
            Ambiente amb = new Ambiente();
            using (GestoreWebcam gestore = amb.CreaGestore())
            {
                StatoImpostazioniWebcam vuoto = gestore.LeggiImpostazioni();
                Assert.That(vuoto.FuocoSupportato, Is.False);
                Assert.That(vuoto.EsposizioneSupportata, Is.False);
                Assert.That(vuoto.Fuoco, Is.Null);
                Assert.That(gestore.FpsCatturati, Is.EqualTo(0));

                gestore.Apri(0);

                Assert.That(gestore.LeggiImpostazioni().Fuoco, Is.EqualTo(42));
                Assert.That(gestore.FpsCatturati, Is.EqualTo(25));
            }
        }
    }
}
