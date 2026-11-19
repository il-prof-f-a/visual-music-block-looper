using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class CalibrazioneCurvaTests
    {
        private ParametriCorrezioneBordo parametriPrecedenti;

        [SetUp]
        public void PrimaDiOgniTest()
        {
            parametriPrecedenti = Calibrazione.ParametriBordo;
            Calibrazione.ParametriBordo = new ParametriCorrezioneBordo();
        }

        [TearDown]
        public void DopoOgniTest()
        {
            Calibrazione.ParametriBordo = parametriPrecedenti;
        }

        // Distorsione radiale indipendente dal modello di interpolazione usato in produzione.
        internal static PuntoImmagine Camera(PuntoTavolo p, bool capovolta = false, double coefficiente = -0.3)
        {
            double x = p.X - 0.5, y = p.Y - 0.5;
            double fattore = 1 + coefficiente * (x * x + y * y);
            double u = 0.5 + x * fattore, v = 0.5 + y * fattore;
            double w = 1 + 0.12 * u + 0.06 * v;
            double cx = (100 + 1000 * u + 80 * v) / w;
            double cy = (60 + 25 * u + 650 * v) / w;
            return capovolta ? new PuntoImmagine(1280 - cx, 720 - cy) : new PuntoImmagine(cx, cy);
        }

        // Caso reale osservato: linee interne giÃ  dritte e bordo proiettato curvo.
        // L'omografia governa il centro; il TPS corregge solo la fascia perimetrale.
        internal static PuntoImmagine CameraBordo(PuntoTavolo p)
        {
            double x = p.X, y = p.Y;
            double w = 1 + 0.15 * x + 0.08 * y;
            double cx = (80 + 950 * x + 120 * y) / w;
            double cy = (60 + 30 * x + 620 * y) / w;
            double distanza = Math.Min(Math.Min(x, 1 - x), Math.Min(y, 1 - y));
            if (distanza < 0.10)
            {
                double f = (0.10 - distanza) * (0.10 - distanza) / 0.01;
                cx += f * 60 * (x - 0.5);
                cy += f * 40 * (y - 0.5);
            }
            return new PuntoImmagine(cx, cy);
        }

        internal static PuntoImmagine CameraSferica(PuntoTavolo p)
        {
            const double focale = 700, cx = 640, cy = 360;
            double ix = 900 * p.X + 20 * p.Y + 100;
            double iy = 15 * p.X + 640 * p.Y + 50;
            double dx = ix - cx, dy = iy - cy;
            double r = Math.Sqrt(dx * dx + dy * dy);
            if (r < 1e-12) return new PuntoImmagine(cx, cy);
            double theta = Math.Atan2(r, focale);
            double rd = 2 * focale * Math.Sin(theta / 2);
            return new PuntoImmagine(cx + dx * rd / r, cy + dy * rd / r);
        }

        private static double Distanza(PuntoTavolo p, PuntoTavolo q) =>
            Math.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y));

        private static double ScostamentoDaRetta(double[] ascisse, double[] ordinate, out double pendenza)
        {
            double mediaX = ascisse.Average(), mediaY = ordinate.Average();
            double numeratore = ascisse.Zip(ordinate, (x, y) => (x - mediaX) * (y - mediaY)).Sum();
            double denominatore = ascisse.Sum(x => (x - mediaX) * (x - mediaX));
            double coefficiente = Math.Abs(denominatore) < 1e-15 ? 0 : numeratore / denominatore;
            double intercetta = mediaY - coefficiente * mediaX;
            pendenza = Math.Abs(coefficiente);
            return ascisse.Zip(ordinate, (x, y) => Math.Abs(y - (coefficiente * x + intercetta))).Max();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Curvatura_PassaPerTuttiIDodiciRiferimenti(bool capovolta)
        {
            var punti = SchemaCalibrazione.Punti.Select(p => Camera(p, capovolta)).ToArray();
            var curva = SchemaCalibrazione.Calcola(punti, 1280, 720, out _);
            var omografia = Calibrazione.Calcola(punti, SchemaCalibrazione.Punti.ToArray(), 1280, 720);
            double erroreVecchio = punti.Select((p, i) => Distanza(omografia.DaCameraATavolo(p), SchemaCalibrazione.Punti[i])).Max();
            Assert.That(erroreVecchio, Is.GreaterThan(0.01), "Il caso deve riprodurre il difetto originale");
            for (int i = 0; i < punti.Length; i++)
                Assert.That(Distanza(curva.DaCameraATavolo(punti[i]), SchemaCalibrazione.Punti[i]),
                    Is.LessThan(1e-7), "Riferimento " + (i + 1));
        }

        [Test]
        public void FasciaBordoConfigurabileCambiaLaCorrezioneDelBordo()
        {
            var punti = SchemaCalibrazione.Punti.Select(CameraBordo).ToArray();
            Calibrazione.BordoEsterno = 0.12;
            var stretta = SchemaCalibrazione.Calcola(punti, 1280, 720, out _);
            Calibrazione.BordoEsterno = 0.24;
            var larga = SchemaCalibrazione.Calcola(punti, 1280, 720, out _);

            var p = new PuntoTavolo(0.10, 0.10);
            double differenza = Distanza(stretta.DaCameraATavolo(CameraBordo(p)),
                larga.DaCameraATavolo(CameraBordo(p)));
            Assert.That(differenza, Is.GreaterThan(1e-6));
        }

        [Test]
        public void ModelloSmussato_RispettaIDodiciPuntiEInversa()
        {
            Calibrazione.ParametriBordo = new ParametriCorrezioneBordo
            {
                Modello = ModelloCorrezioneBordo.BordoSmussato,
                Pieno = 0.04,
                Esterno = 0.24,
                FattoreX = 0.8,
                FattoreY = 1.2,
                Smussamento = 20
            };
            var punti = SchemaCalibrazione.Punti.Select(CameraBordo).ToArray();
            var curva = SchemaCalibrazione.Calcola(punti, 1280, 720, out _);

            for (int i = 0; i < punti.Length; i++)
                Assert.That(Distanza(curva.DaCameraATavolo(punti[i]), SchemaCalibrazione.Punti[i]),
                    Is.LessThan(1e-7), "Riferimento " + (i + 1));

            foreach (double x in new[] { 0.06, 0.15, 0.35, 0.65, 0.85, 0.94 })
                foreach (double y in new[] { 0.06, 0.15, 0.35, 0.65, 0.85, 0.94 })
                {
                    var p = new PuntoTavolo(x, y);
                    PuntoImmagine camera = curva.DaTavoloACamera(p);
                    Assert.That(Distanza(curva.DaCameraATavolo(camera), p), Is.LessThan(1e-6),
                        "Inversa " + x + "," + y);
                }
        }

        [Test]
        public void CopiaConParametriBordo_CambiaLaMappaSenzaPerdereIRiferimenti()
        {
            var punti = SchemaCalibrazione.Punti.Select(CameraBordo).ToArray();
            var originale = SchemaCalibrazione.Calcola(punti, 1280, 720, out _);
            var nuova = originale.ConParametriBordo(new ParametriCorrezioneBordo
            {
                Modello = ModelloCorrezioneBordo.BordoSmussato,
                Pieno = 0.04,
                Esterno = 0.24,
                FattoreX = 0.8,
                FattoreY = 1.2,
                Smussamento = 20
            });

            Assert.That(nuova, Is.Not.SameAs(originale));
            for (int i = 0; i < punti.Length; i++)
                Assert.That(Distanza(nuova.DaCameraATavolo(punti[i]), SchemaCalibrazione.Punti[i]),
                    Is.LessThan(1e-7), "Riferimento " + (i + 1));

            var prova = new PuntoTavolo(0.10, 0.10);
            Assert.That(Distanza(originale.DaCameraATavolo(CameraBordo(prova)),
                nuova.DaCameraATavolo(CameraBordo(prova))), Is.GreaterThan(1e-6));
        }

        [Test]
        public void ModelloSferico_RicostruisceLaProiezioneEquisolida()
        {
            Calibrazione.ParametriBordo = new ParametriCorrezioneBordo
            {
                Modello = ModelloCorrezioneBordo.Sferico,
                TipoSferico = TipoProiezioneSferica.Equisolido,
                Focale = 700,
                CentroX = 640,
                CentroY = 360
            };
            var camera = SchemaCalibrazione.Punti.Select(CameraSferica).ToArray();
            var curva = Calibrazione.CalcolaCurva(camera, SchemaCalibrazione.Punti.ToArray(), 1280, 720);

            Assert.That(curva.ModelloCorrente, Is.EqualTo(ModelloCorrezioneBordo.Sferico));
            for (int i = 0; i < camera.Length; i++)
                Assert.That(Distanza(curva.DaCameraATavolo(camera[i]), SchemaCalibrazione.Punti[i]),
                    Is.LessThan(1e-6), "Riferimento " + (i + 1));

            foreach (double x in new[] { 0.10, 0.30, 0.50, 0.70, 0.90 })
                foreach (double y in new[] { 0.10, 0.30, 0.50, 0.70, 0.90 })
                {
                    var p = new PuntoTavolo(x, y);
                    Assert.That(Distanza(curva.DaCameraATavolo(CameraSferica(p)), p),
                        Is.LessThan(1e-6), "Inversa " + x + "," + y);
                }
        }

        [Test]
        public void BordoCurvo_LineeInterneDritteE12PuntiRispettati()
        {
            var punti = SchemaCalibrazione.Punti.Select(CameraBordo).ToArray();
            var curva = SchemaCalibrazione.Calcola(punti, 1280, 720, out _);

            // Il bordo resta agganciato ai dodici click.
            for (int i = 0; i < punti.Length; i++)
                Assert.That(Distanza(curva.DaCameraATavolo(punti[i]), SchemaCalibrazione.Punti[i]),
                    Is.LessThan(1e-7), "Riferimento " + (i + 1));

            // Al centro la mappa Ã¨ omografica: le linee interne restano rette anche
            // se l'omografia stimata dai click puÃ² traslarle o ruotarle leggermente.
            foreach (double y in new[] { 0.25, 0.35, 0.50, 0.65, 0.75 })
            {
                var ascisse = new double[21];
                var ordinate = new double[21];
                for (int i = 0; i <= 20; i++)
                {
                    var atteso = new PuntoTavolo(0.15 + 0.70 * i / 20.0, y);
                    PuntoTavolo letto = curva.DaCameraATavolo(CameraBordo(atteso));
                    ascisse[i] = letto.X;
                    ordinate[i] = letto.Y;
                }
                double pendenza;
                double scostamento = ScostamentoDaRetta(ascisse, ordinate, out pendenza);
                Assert.That(scostamento, Is.LessThan(1e-4), "Linea orizzontale y=" + y);
                Assert.That(pendenza, Is.LessThan(0.02), "Pendenza linea orizzontale y=" + y);
            }

            foreach (double x in new[] { 0.25, 0.35, 0.50, 0.65, 0.75 })
            {
                var ascisse = new double[21];
                var ordinate = new double[21];
                for (int i = 0; i <= 20; i++)
                {
                    var atteso = new PuntoTavolo(x, 0.15 + 0.70 * i / 20.0);
                    PuntoTavolo letto = curva.DaCameraATavolo(CameraBordo(atteso));
                    ascisse[i] = letto.Y;
                    ordinate[i] = letto.X;
                }
                double pendenza;
                double scostamento = ScostamentoDaRetta(ascisse, ordinate, out pendenza);
                Assert.That(scostamento, Is.LessThan(1e-4), "Linea verticale x=" + x);
                Assert.That(pendenza, Is.LessThan(0.02), "Pendenza linea verticale x=" + x);
            }
        }

        [Test]
        public void Salvataggio_ConservaLaCurvaAlRiavvio()
        {
            string file = Path.Combine(Path.GetTempPath(), "vmbl-curva-" + Guid.NewGuid().ToString("N"));
            try
            {
                var punti = SchemaCalibrazione.Punti.Select(p => Camera(p)).ToArray();
                var curva = SchemaCalibrazione.Calcola(punti, 1280, 720, out _);
                curva.Salva(file);
                Assert.That(File.ReadAllText(file), Does.StartWith("VMBL-CAL3"));
                var riletta = Calibrazione.Carica(file);
                foreach (var p in punti)
                    Assert.That(Distanza(riletta.DaCameraATavolo(p), curva.DaCameraATavolo(p)), Is.LessThan(1e-10));
            }
            finally { if (File.Exists(file)) File.Delete(file); }
        }

        private static Calibrazione Crea() => SchemaCalibrazione.Calcola(
            SchemaCalibrazione.Punti.Select(p => Camera(p)).ToArray(), 1280, 720, out _);

        [Test]
        public void GrigliaDensa_InversaCoerenteEContinuaAncheSulMargine()
        {
            var curva = Crea();
            Assert.That(curva.Curva, Is.True);
            double massimo = 0;
            for (int y = 0; y <= 40; y++)
                for (int x = 0; x <= 40; x++)
                {
                    var p = new PuntoTavolo(x / 40.0, y / 40.0);
                    var c = curva.DaTavoloACamera(p);
                    massimo = Math.Max(massimo, Distanza(curva.DaCameraATavolo(c), p));
                }
            Assert.That(massimo, Is.LessThan(1e-8));
        }

        [Test]
        public void Remap_RaddrizzaIRiferimentiEDopoResizeNonRiusaIlFrameVecchio()
        {
            var curva = Crea();
            using (var immagine = new Mat(720, 1280, MatType.CV_8UC1, Scalar.All(0)))
            {
                foreach (var p in SchemaCalibrazione.Punti)
                {
                    var c = Camera(p);
                    Cv2.Circle(immagine, new Point((int)Math.Round(c.X), (int)Math.Round(c.Y)), 5, Scalar.All(240), -1);
                }
                var tempi = Stopwatch.StartNew();
                foreach (var dimensione in new[] { new Size(600, 338), new Size(900, 507), new Size(600, 338) })
                    using (var raddrizzata = curva.Raddrizza(immagine, dimensione.Width, dimensione.Height))
                        foreach (var p in SchemaCalibrazione.Punti)
                            Assert.That(raddrizzata.At<byte>((int)Math.Round(p.Y * dimensione.Height),
                                (int)Math.Round(p.X * dimensione.Width)), Is.GreaterThan(180));
                TestContext.WriteLine("Preparazione tre mappe/resize: " + tempi.ElapsedMilliseconds + " ms");
                immagine.SetTo(Scalar.All(71));
                tempi.Restart();
                for (int i = 0; i < 5; i++)
                    using (var raddrizzata = curva.Raddrizza(immagine, 600, 338))
                        Assert.That(raddrizzata.At<byte>(169, 300), Is.EqualTo(71));
                TestContext.WriteLine("Riuso mappa, media: " + tempi.Elapsed.TotalMilliseconds / 5 + " ms");
            }
        }

        [Test]
        public void RaddrizzaZonaCurva_CoincideConIlRitaglioDellaScenaCompleta()
        {
            var curva = SchemaCalibrazione.Calcola(
                SchemaCalibrazione.Punti.Select(p => Camera(p)).ToArray(), 1280, 720, out _);
            var area = new Rect(500, 20, 91, 331);
            using (var frame = new Mat(720, 1280, MatType.CV_8UC1, Scalar.All(17)))
            {
                Cv2.Rectangle(frame, new Rect(230, 80, 810, 520), Scalar.All(211), -1);
                Cv2.Line(frame, new Point(100, 600), new Point(1180, 120), Scalar.All(95), 7);
                using (var completa = curva.Raddrizza(frame, 640, 360))
                using (var attesa = new Mat(completa, area))
                using (var ottenuta = curva.RaddrizzaZona(frame, 640, 360, area))
                {
                    Assert.That(ottenuta.Size(), Is.EqualTo(attesa.Size()));
                    int massimo = 0;
                    for (int y = 0; y < area.Height; y++)
                        for (int x = 0; x < area.Width; x++)
                            massimo = System.Math.Max(massimo,
                                System.Math.Abs(attesa.At<byte>(y, x) - ottenuta.At<byte>(y, x)));
                    Assert.That(massimo, Is.LessThanOrEqualTo(1));
                }

                for (int i = 0; i < 3; i++)
                {
                    using (curva.Raddrizza(frame, 640, 360)) { }
                    using (curva.RaddrizzaZona(frame, 640, 360, area)) { }
                }
                const int iterazioni = 12;
                var cronometro = Stopwatch.StartNew();
                for (int i = 0; i < iterazioni; i++)
                    using (curva.Raddrizza(frame, 640, 360)) { }
                double scenaMs = cronometro.Elapsed.TotalMilliseconds;
                cronometro.Restart();
                for (int i = 0; i < iterazioni; i++)
                    using (curva.RaddrizzaZona(frame, 640, 360, area)) { }
                double zonaMs = cronometro.Elapsed.TotalMilliseconds;
                TestContext.WriteLine($"Raddrizzamento curvo {iterazioni}x: scena intera={scenaMs:0.00} ms, sola zona={zonaMs:0.00} ms, rapporto={scenaMs / Math.Max(zonaMs, 0.01):0.0}x.");
            }
        }

        [Test]
        public void AnteprimaEAnalizzatoreAlternatiNonRicostruisconoLaMappaOgniVolta()
        {
            var c=Crea();
            using(var f=new Mat(720,1280,MatType.CV_8UC1,Scalar.All(0)))
            {
                using(var a=c.Raddrizza(f,640,360)){}
                using(var b=c.Raddrizza(f,520,293)){}
                f.SetTo(Scalar.All(71));
                AppDomain.MonitoringIsEnabled=true;
                long prima=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                using(var a=c.Raddrizza(f,640,360))
                    Assert.That(a.At<byte>(180,320),Is.EqualTo(71),"La cache riguarda la geometria, non il contenuto del frame.");
                long allocate=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-prima;
                // Resta molto sotto una mappa ricostruita (640x360x4x2 = 1,8 MB): copre solo
                // le piccole allocazioni dei wrapper Mat/Remap, non nuovi array per pixel.
                Assert.That(allocate,Is.LessThan(250000),"Il formato giÃƒÂ  preparato deve evitare nuove matrici gestite per ogni pixel.");
            }
        }

        [Test]
        public void Maschera_CoincideConLeCoordinateCurveENonCondivideIlBuffer()
        {
            var curva = Crea();
            using (var maschera = curva.MascheraTavolo(1280, 720))
            {
                for (int y = 10; y < 710; y += 11)
                    for (int x = 10; x < 1270; x += 13)
                    {
                        var p = curva.DaCameraATavolo(new PuntoImmagine(x, y));
                        if (Math.Min(Math.Min(Math.Abs(p.X), Math.Abs(p.X - 1)), Math.Min(Math.Abs(p.Y), Math.Abs(p.Y - 1))) < 0.004) continue;
                        bool dentro = p.X >= 0 && p.X <= 1 && p.Y >= 0 && p.Y <= 1;
                        Assert.That(maschera.At<byte>(y, x) != 0, Is.EqualTo(dentro), "Pixel " + x + "," + y);
                    }
                int totale = Cv2.CountNonZero(maschera);
                maschera.SetTo(Scalar.All(0));
                using (var copia = curva.MascheraTavolo(1280, 720))
                    Assert.That(Cv2.CountNonZero(copia), Is.EqualTo(totale));
                using (var ristretta = curva.MascheraTavolo(1280, 720, 6))
                    Assert.That(Cv2.CountNonZero(ristretta), Is.LessThan(totale).And.GreaterThan(100000));
            }
        }

        [Test]
        public void BordiCurvi_IncludonoEstremiFraGliAngoli()
        {
            // Con il cuscinetto, l'estremo del bordo raddrizzato puÃƒÂ² essere a metÃƒÂ  del lato.
            var curva = SchemaCalibrazione.Calcola(SchemaCalibrazione.Punti.Select(p => Camera(p, false, 0.3)).ToArray(),
                1280, 720, out _);
            var r = new Rect(200, 200, 800, 200);
            var bordo = curva.BordiDaCameraATavolo(r);
            var angoli = new[] { new PuntoImmagine(r.Left, r.Top), new PuntoImmagine(r.Right, r.Top),
                new PuntoImmagine(r.Right, r.Bottom), new PuntoImmagine(r.Left, r.Bottom) }.Select(curva.DaCameraATavolo).ToArray();
            double differenza = new[] { angoli.Min(p => p.X) - bordo.Min(p => p.X),
                bordo.Max(p => p.X) - angoli.Max(p => p.X), angoli.Min(p => p.Y) - bordo.Min(p => p.Y),
                bordo.Max(p => p.Y) - angoli.Max(p => p.Y) }.Max();
            TestContext.WriteLine("Estensione fra gli angoli: " + differenza);
            Assert.That(differenza, Is.GreaterThan(0.001));
            for (int i = 0; i <= 1000; i++)
            {
                double t = i / 1000.0;
                var pixel = new[] { new PuntoImmagine(r.Left + r.Width * t, r.Top), new PuntoImmagine(r.Right, r.Top + r.Height * t),
                    new PuntoImmagine(r.Right - r.Width * t, r.Bottom), new PuntoImmagine(r.Left, r.Bottom - r.Height * t) };
                foreach (var c in pixel)
                {
                    var p = curva.DaCameraATavolo(c);
                    Assert.That(p.X, Is.InRange(bordo.Min(q => q.X) - 0.0001, bordo.Max(q => q.X) + 0.0001));
                    Assert.That(p.Y, Is.InRange(bordo.Min(q => q.Y) - 0.0001, bordo.Max(q => q.Y) + 0.0001));
                }
            }
        }

        [Test]
        public void Mappe_CondivisibiliFraThreadConFormatiDiversi()
        {
            var curva = Crea();
            Parallel.For(0, 4, i =>
            {
                using (var frame = new Mat(720, 1280, MatType.CV_8UC1, Scalar.All(40 + i)))
                using (var r = curva.Raddrizza(frame, 200 + 20 * i, 120 + 10 * i))
                using (var m = curva.MascheraTavolo(1280, 720, i))
                {
                    Assert.That(r.At<byte>(r.Rows / 2, r.Cols / 2), Is.EqualTo(40 + i));
                    Assert.That(m.At<byte>(350, 600), Is.EqualTo(255));
                }
            });
        }

        [TestCase("nan")]
        [TestCase("duplicato")]
        [TestCase("ordine")]
        [TestCase("risoluzione")]
        [TestCase("conteggio")]
        [TestCase("matrice")]
        public void Cal3_CorrottoRifiutato(string caso)
        {
            string file = Path.Combine(Path.GetTempPath(), "vmbl-curva-" + Guid.NewGuid().ToString("N"));
            try
            {
                Crea().Salva(file);
                var righe = File.ReadAllLines(file);
                switch (caso)
                {
                    case "nan": righe[4] = "NaN;20;0.3;0.03"; break;
                    case "duplicato": righe[4] = righe[3]; break;
                    case "ordine": var tmp = righe[4]; righe[4] = righe[8]; righe[8] = tmp; break;
                    case "risoluzione": righe[2] = "0;0"; break;
                    case "conteggio": righe = righe.Take(14).ToArray(); break;
                    case "matrice": righe[1] = "0;0;0;0;0;0;0;0;0"; break;
                }
                File.WriteAllLines(file, righe);
                Assert.That(() => Calibrazione.Carica(file), Throws.TypeOf<InvalidDataException>());
            }
            finally { if (File.Exists(file)) File.Delete(file); }
        }

        [Test]
        public void DeformazioneConPiega_RifiutataAncheSeIClickNonSiIncrociano()
        {
            var tavolo = SchemaCalibrazione.Punti.ToArray();
            var camera = tavolo.Select(p => new PuntoImmagine(p.X * 1200, p.Y * 700)).ToArray();
            camera[1] = new PuntoImmagine(camera[1].X, 550);
            camera[2] = new PuntoImmagine(camera[2].X, 550);
            Assert.That(() => Calibrazione.CalcolaCurva(camera, tavolo, 1280, 720), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Argomenti_ModificatiDopoIlCalcoloNonMutanoLaCalibrazione()
        {
            var tavolo = SchemaCalibrazione.Punti.ToArray(); var camera = tavolo.Select(p => Camera(p)).ToArray();
            var curva = Calibrazione.CalcolaCurva(camera, tavolo, 1280, 720);
            var prova = camera[1]; var atteso = tavolo[1];
            camera[1] = new PuntoImmagine(0, 0); tavolo[1] = new PuntoTavolo(0, 0);
            Assert.That(Distanza(curva.DaCameraATavolo(prova), atteso), Is.LessThan(1e-8));
        }
    }
}

