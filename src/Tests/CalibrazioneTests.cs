using System;
using System.IO;
using NUnit.Framework;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class CalibrazioneTests
    {
        private const double Tolleranza = 1e-6;

        private static void AsseriPunto(PuntoTavolo atteso, PuntoTavolo ottenuto, double tolleranza = Tolleranza)
        {
            Assert.That(ottenuto.X, Is.EqualTo(atteso.X).Within(tolleranza));
            Assert.That(ottenuto.Y, Is.EqualTo(atteso.Y).Within(tolleranza));
        }

        private static string PercorsoTemporaneo()
        {
            return Path.Combine(Path.GetTempPath(), "vmbl-cal-" + Guid.NewGuid().ToString("N") + ".txt");
        }

        [Test]
        public void RansacScartaUnPuntoErrato()
        {
            // Mappatura nota: tavolo = camera / (640, 480). Un punto del tavolo e' grossolanamente errato.
            PuntoImmagine[] camera =
            {
                new PuntoImmagine(0, 0), new PuntoImmagine(640, 0), new PuntoImmagine(640, 480),
                new PuntoImmagine(0, 480), new PuntoImmagine(320, 240), new PuntoImmagine(160, 360)
            };
            PuntoTavolo[] tavolo =
            {
                new PuntoTavolo(0, 0), new PuntoTavolo(1, 0), new PuntoTavolo(1, 1),
                new PuntoTavolo(0, 1), new PuntoTavolo(0.9, 0.1), new PuntoTavolo(0.25, 0.75)
            };

            Calibrazione calibrazione = Calibrazione.Calcola(camera, tavolo);

            AsseriPunto(new PuntoTavolo(0, 0), calibrazione.DaCameraATavolo(new PuntoImmagine(0, 0)), 1e-3);
            AsseriPunto(new PuntoTavolo(1, 0), calibrazione.DaCameraATavolo(new PuntoImmagine(640, 0)), 1e-3);
            AsseriPunto(new PuntoTavolo(1, 1), calibrazione.DaCameraATavolo(new PuntoImmagine(640, 480)), 1e-3);
            AsseriPunto(new PuntoTavolo(0, 1), calibrazione.DaCameraATavolo(new PuntoImmagine(0, 480)), 1e-3);
            AsseriPunto(new PuntoTavolo(0.25, 0.25), calibrazione.DaCameraATavolo(new PuntoImmagine(160, 120)), 1e-3);
        }

        [Test]
        public void PuntoNonFiniteRifiutati()
        {
            PuntoTavolo[] tavolo =
            {
                new PuntoTavolo(0, 0), new PuntoTavolo(1, 0), new PuntoTavolo(1, 1), new PuntoTavolo(0, 1)
            };
            PuntoImmagine[] conNaN =
            {
                new PuntoImmagine(0, 0), new PuntoImmagine(640, 0),
                new PuntoImmagine(double.NaN, 480), new PuntoImmagine(0, 480)
            };
            PuntoImmagine[] conInfinito =
            {
                new PuntoImmagine(0, 0), new PuntoImmagine(640, 0),
                new PuntoImmagine(640, 480), new PuntoImmagine(double.PositiveInfinity, 480)
            };

            Assert.That(() => Calibrazione.Calcola(conNaN, tavolo), Throws.TypeOf<ArgumentException>());
            Assert.That(() => Calibrazione.Calcola(conInfinito, tavolo), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void MessaggioLunghezzeDiverse()
        {
            PuntoImmagine[] camera =
            {
                new PuntoImmagine(0, 0), new PuntoImmagine(640, 0),
                new PuntoImmagine(640, 480), new PuntoImmagine(0, 480)
            };
            PuntoTavolo[] tavolo =
            {
                new PuntoTavolo(0, 0), new PuntoTavolo(1, 0),
                new PuntoTavolo(1, 1), new PuntoTavolo(0, 1), new PuntoTavolo(0.5, 0.5)
            };

            ArgumentException ex = Assert.Throws<ArgumentException>(() => Calibrazione.Calcola(camera, tavolo));

            Assert.That(ex.Message, Does.Contain("4").And.Contain("5"));
            Assert.That(ex.ParamName, Is.EqualTo("puntiTavolo"));
        }

        [Test]
        public void CaricaMatriceDegenere()
        {
            string percorso = PercorsoTemporaneo();

            try
            {
                File.WriteAllText(percorso, "VMBL-CAL1\n1;0;0;0;1;0;0;0;0\n");
                Assert.That(() => Calibrazione.Carica(percorso), Throws.TypeOf<InvalidDataException>());
            }
            finally
            {
                if (File.Exists(percorso))
                {
                    File.Delete(percorso);
                }
            }
        }

        [Test]
        public void OmografiaIdentita()
        {
            PuntoImmagine[] camera =
            {
                new PuntoImmagine(0, 0), new PuntoImmagine(1, 0),
                new PuntoImmagine(1, 1), new PuntoImmagine(0, 1)
            };
            PuntoTavolo[] tavolo =
            {
                new PuntoTavolo(0, 0), new PuntoTavolo(1, 0),
                new PuntoTavolo(1, 1), new PuntoTavolo(0, 1)
            };

            Calibrazione calibrazione = Calibrazione.Calcola(camera, tavolo);

            AsseriPunto(new PuntoTavolo(0.5, 0.5), calibrazione.DaCameraATavolo(new PuntoImmagine(0.5, 0.5)));
            AsseriPunto(new PuntoTavolo(0.2, 0.8), calibrazione.DaCameraATavolo(new PuntoImmagine(0.2, 0.8)));
        }

        [Test]
        public void ScalaETraslazione()
        {
            PuntoImmagine[] camera =
            {
                new PuntoImmagine(0, 0), new PuntoImmagine(640, 0),
                new PuntoImmagine(640, 480), new PuntoImmagine(0, 480)
            };
            PuntoTavolo[] tavolo =
            {
                new PuntoTavolo(0, 0), new PuntoTavolo(1, 0),
                new PuntoTavolo(1, 1), new PuntoTavolo(0, 1)
            };

            Calibrazione calibrazione = Calibrazione.Calcola(camera, tavolo);

            AsseriPunto(new PuntoTavolo(0.5, 0.5), calibrazione.DaCameraATavolo(new PuntoImmagine(320, 240)));
            AsseriPunto(new PuntoTavolo(0.25, 0.25), calibrazione.DaCameraATavolo(new PuntoImmagine(160, 120)));
            AsseriPunto(new PuntoTavolo(1, 1), calibrazione.DaCameraATavolo(new PuntoImmagine(640, 480)));
        }

        [Test]
        public void MenoDiQuattroPunti()
        {
            PuntoImmagine[] camera =
            {
                new PuntoImmagine(0, 0), new PuntoImmagine(640, 0), new PuntoImmagine(640, 480)
            };
            PuntoTavolo[] tavolo =
            {
                new PuntoTavolo(0, 0), new PuntoTavolo(1, 0), new PuntoTavolo(1, 1)
            };

            Assert.That(() => Calibrazione.Calcola(camera, tavolo), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void ArgomentiNulli()
        {
            PuntoTavolo[] tavolo =
            {
                new PuntoTavolo(0, 0), new PuntoTavolo(1, 0), new PuntoTavolo(1, 1), new PuntoTavolo(0, 1)
            };

            Assert.That(() => Calibrazione.Calcola(null, tavolo), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => Calibrazione.Calcola(new PuntoImmagine[4], null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void LunghezzeDiverse()
        {
            PuntoImmagine[] camera =
            {
                new PuntoImmagine(0, 0), new PuntoImmagine(640, 0),
                new PuntoImmagine(640, 480), new PuntoImmagine(0, 480)
            };
            PuntoTavolo[] tavolo =
            {
                new PuntoTavolo(0, 0), new PuntoTavolo(1, 0),
                new PuntoTavolo(1, 1), new PuntoTavolo(0, 1), new PuntoTavolo(0.5, 0.5)
            };

            Assert.That(() => Calibrazione.Calcola(camera, tavolo), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void PuntiCollineari()
        {
            PuntoImmagine[] camera =
            {
                new PuntoImmagine(0, 0), new PuntoImmagine(1, 1),
                new PuntoImmagine(2, 2), new PuntoImmagine(3, 3)
            };
            PuntoTavolo[] tavolo =
            {
                new PuntoTavolo(0, 0), new PuntoTavolo(1, 0),
                new PuntoTavolo(1, 1), new PuntoTavolo(0, 1)
            };

            Assert.That(() => Calibrazione.Calcola(camera, tavolo), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void SalvaECarica()
        {
            PuntoImmagine[] camera =
            {
                new PuntoImmagine(10, 12), new PuntoImmagine(630, 20),
                new PuntoImmagine(620, 470), new PuntoImmagine(15, 460)
            };
            PuntoTavolo[] tavolo =
            {
                new PuntoTavolo(0, 0), new PuntoTavolo(1, 0),
                new PuntoTavolo(1, 1), new PuntoTavolo(0, 1)
            };
            Calibrazione originale = Calibrazione.Calcola(camera, tavolo);
            string percorso = Path.Combine(Path.GetTempPath(), "vmbl-cal-" + Guid.NewGuid().ToString("N") + ".txt");

            try
            {
                originale.Salva(percorso);
                Calibrazione caricata = Calibrazione.Carica(percorso);

                PuntoImmagine prova = new PuntoImmagine(300, 250);
                PuntoTavolo attesoOriginale = originale.DaCameraATavolo(prova);
                AsseriPunto(attesoOriginale, caricata.DaCameraATavolo(prova), 1e-9);
            }
            finally
            {
                if (File.Exists(percorso))
                {
                    File.Delete(percorso);
                }
            }
        }

        [Test]
        public void CaricaMalformato()
        {
            string percorso = Path.Combine(Path.GetTempPath(), "vmbl-cal-" + Guid.NewGuid().ToString("N") + ".txt");

            try
            {
                File.WriteAllText(percorso, "INTESTAZIONE-SBAGLIATA\n1;0;0;0;1;0;0;0;1\n");
                Assert.That(() => Calibrazione.Carica(percorso), Throws.TypeOf<InvalidDataException>());

                File.WriteAllText(percorso, "VMBL-CAL1\n1;0;0;0;1;0;0;0\n");
                Assert.That(() => Calibrazione.Carica(percorso), Throws.TypeOf<InvalidDataException>());

                File.WriteAllText(percorso, "VMBL-CAL1\n1;0;0;0;NaN;0;0;0;1\n");
                Assert.That(() => Calibrazione.Carica(percorso), Throws.TypeOf<InvalidDataException>());
            }
            finally
            {
                if (File.Exists(percorso))
                {
                    File.Delete(percorso);
                }
            }
        }

        private static Calibrazione CalibrazioneDiProva(int larghezza = 0, int altezza = 0)
        {
            PuntoImmagine[] camera =
            {
                new PuntoImmagine(10, 12), new PuntoImmagine(630, 20),
                new PuntoImmagine(620, 470), new PuntoImmagine(15, 460)
            };
            PuntoTavolo[] tavolo =
            {
                new PuntoTavolo(0, 0), new PuntoTavolo(1, 0),
                new PuntoTavolo(1, 1), new PuntoTavolo(0, 1)
            };
            return Calibrazione.Calcola(camera, tavolo, larghezza, altezza);
        }

        [Test]
        public void SalvaECaricaCal2ConservaRisoluzione()
        {
            Calibrazione originale = CalibrazioneDiProva(1280, 720);
            string percorso = PercorsoTemporaneo();

            try
            {
                originale.Salva(percorso);
                Assert.That(File.ReadAllLines(percorso)[0], Is.EqualTo("VMBL-CAL2"));
                Assert.That(File.ReadAllLines(percorso)[2], Is.EqualTo("1280;720"));

                Calibrazione caricata = Calibrazione.Carica(percorso);

                Assert.That(caricata.Larghezza, Is.EqualTo(1280));
                Assert.That(caricata.Altezza, Is.EqualTo(720));
                PuntoImmagine prova = new PuntoImmagine(300, 250);
                AsseriPunto(originale.DaCameraATavolo(prova), caricata.DaCameraATavolo(prova), 1e-9);
            }
            finally
            {
                if (File.Exists(percorso))
                {
                    File.Delete(percorso);
                }
            }
        }

        [Test]
        public void CaricaCal1HaRisoluzioneSconosciuta()
        {
            string percorso = PercorsoTemporaneo();

            try
            {
                File.WriteAllText(percorso, "VMBL-CAL1\n1;0;0;0;1;0;0;0;1\n");

                Calibrazione caricata = Calibrazione.Carica(percorso);

                Assert.That(caricata.Larghezza, Is.EqualTo(0));
                Assert.That(caricata.Altezza, Is.EqualTo(0));
            }
            finally
            {
                if (File.Exists(percorso))
                {
                    File.Delete(percorso);
                }
            }
        }

        [TestCase("VMBL-CAL2\n1;0;0;0;1;0;0;0;1\n")]
        [TestCase("VMBL-CAL2\n1;0;0;0;1;0;0;0;1\n1280\n")]
        [TestCase("VMBL-CAL2\n1;0;0;0;1;0;0;0;1\n1280;abc\n")]
        [TestCase("VMBL-CAL2\n1;0;0;0;1;0;0;0;1\n1280;-720\n")]
        [TestCase("VMBL-CAL2\n1;0;0;0;1;0;0;0;1\n1280;720;5\n")]
        [TestCase("VMBL-CAL2\n1;0;0;0;1;0;0;0\n1280;720\n")]
        [TestCase("VMBL-CAL1\n1;0;0;0;1;0;0;0;1\n1280;720\n")]
        public void CaricaCal2Malformato(string contenuto)
        {
            string percorso = PercorsoTemporaneo();

            try
            {
                File.WriteAllText(percorso, contenuto);

                Assert.That(() => Calibrazione.Carica(percorso), Throws.TypeOf<InvalidDataException>());
            }
            finally
            {
                if (File.Exists(percorso))
                {
                    File.Delete(percorso);
                }
            }
        }

        [Test]
        public void ConfrontoRisoluzione()
        {
            Assert.That(CalibrazioneDiProva(1280, 720).VerificaRisoluzione(1280, 720), Is.EqualTo(EsitoRisoluzione.Compatibile));
            Assert.That(CalibrazioneDiProva(1280, 720).VerificaRisoluzione(640, 480), Is.EqualTo(EsitoRisoluzione.Diversa));
            Assert.That(CalibrazioneDiProva(1280, 720).VerificaRisoluzione(1280, 480), Is.EqualTo(EsitoRisoluzione.Diversa));
            Assert.That(CalibrazioneDiProva().VerificaRisoluzione(1280, 720), Is.EqualTo(EsitoRisoluzione.Sconosciuta));
            // Camera di dimensione ignota: nessun confronto possibile.
            Assert.That(CalibrazioneDiProva(1280, 720).VerificaRisoluzione(0, 0), Is.EqualTo(EsitoRisoluzione.Compatibile));
        }

        [Test]
        public void CaricaFileMancante()
        {
            string percorso = Path.Combine(Path.GetTempPath(), "vmbl-cal-inesistente-" + Guid.NewGuid().ToString("N") + ".txt");

            Assert.That(() => Calibrazione.Carica(percorso), Throws.TypeOf<FileNotFoundException>());
        }

        [Test]
        public void PuntoAllInfinitoLanciaEccezione()
        {
            // Matrice con w = x + 1: il punto camera (-1, 0) ha w = 0.
            string percorso = Path.Combine(Path.GetTempPath(), "vmbl-cal-" + Guid.NewGuid().ToString("N") + ".txt");

            try
            {
                File.WriteAllText(percorso, "VMBL-CAL1\n1;0;0;0;1;0;1;0;1\n");
                Calibrazione calibrazione = Calibrazione.Carica(percorso);

                Assert.That(() => calibrazione.DaCameraATavolo(new PuntoImmagine(-1, 0)),
                    Throws.TypeOf<InvalidOperationException>());
            }
            finally
            {
                if (File.Exists(percorso))
                {
                    File.Delete(percorso);
                }
            }
        }
    }
}
