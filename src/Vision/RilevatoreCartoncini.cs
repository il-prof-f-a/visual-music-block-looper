using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;
using ZXing;
using ZXing.Common;
using ZXing.Multi.QrCode;

namespace VisualMusicBlockLooper.Vision
{
    /// <summary>
    /// Rileva i cartoncini nel frame della camera.
    /// Modello fisico: il cartoncino e' un rettangolo con un codice QR (su un riquadro bianco con zona quieta)
    /// stampato sull'estremita' SINISTRA, anche a filo del bordo. Il QR contiene tutte le informazioni; il corpo
    /// del cartoncino (di qualsiasi colore, piu' scuro della carta/sfondo) serve solo a misurarne la larghezza,
    /// che codifica la durata della nota. Il colore non viene interpretato.
    /// Cartoncini adiacenti possono toccarsi (note consecutive, righe adiacenti): il contorno della maschera li
    /// fonde, quindi il rettangolo di ciascuno viene ritagliato con i riquadri QR dei vicini (a sinistra: bordo
    /// del proprio riquadro; a destra: bordo del riquadro del vicino; sopra/sotto: punto medio tra i due QR).
    /// Le estensioni sono misurate sulla maschera lungo la fascia del proprio QR (righe per la larghezza,
    /// colonne per l'altezza), cosi' vicini di dimensioni diverse non falsano la misura.
    /// La lettura del QR usa OpenCV; per i cartoncini il cui corpo e' visibile ma il QR non e' stato letto
    /// (piccolo, sfocato, sovraesposto) si ritenta sul ritaglio ingrandito e livellato con ZXing e OpenCV.
    /// </summary>
    public sealed partial class RilevatoreCartoncini : IRilevatoreDiagnostico
    {
        private const double FattoreZonaQuieta = 0.2; // zona quieta ~4 moduli su 21 (frazione del lato del QR)
        private const int TolleranzaContatto = 3;
        private const int LatoNucleoChiusura = 7;
        private const int LatoNucleoApertura = 5;
        private const int LatoFondoLocale = 101;      // finestra per stimare il livello locale della carta
        private const double SogliaCorpo = 0.55;      // corpo = canale minimo sotto questa frazione del fondo locale
        private const int FondoMinimo = 80;           // sotto questo livello l'immagine e' troppo scura per il corpo
        private const int AreaMinimaCorpo = 400;      // pixel: scarta macchie piu' piccole di un cartoncino
        private const int VuotiTollerati = 8;
        private const double AltezzaTargetQr = 320;   // altezza in pixel a cui si ingrandisce il cartoncino per leggere il QR
        private const double AreaMassimaCorpo = 0.25; // frazione del frame: contorni piu' grandi non sono cartoncini
        private const int RestringiTavolo = 6;        // pixel: esclude la cornice dello schermo dal bordo dell'area calibrata

        private readonly IFonteImmagini fonte;
        private readonly Calibrazione calibrazione;
        private readonly ConfigGriglia griglia;

        // QR recuperati con la lettura di riserva nel frame precedente, per corpo: la lettura di riserva e'
        // costosa, quindi un cartoncino fermo non viene riletto a ogni frame.
        private List<RecuperoMemorizzato> recuperati = new List<RecuperoMemorizzato>();
        private long sequenzaFrame;

        private sealed class RecuperoMemorizzato
        {
            public Rect Corpo, Regione;
            public byte[] Aspetto;
            public List<QrTrovato> Qr;
            public long UltimaScansione;
        }

        public RilevatoreCartoncini(IFonteImmagini fonte, Calibrazione calibrazione)
            : this(fonte, calibrazione, new ConfigGriglia { Area = LayoutTavolo.AreaGrigliaDefault }) { }

        public RilevatoreCartoncini(IFonteImmagini fonte, Calibrazione calibrazione, ConfigGriglia griglia)
        {
            this.fonte = fonte;
            this.calibrazione = calibrazione ?? throw new ArgumentNullException(nameof(calibrazione));
            this.griglia = griglia ?? new ConfigGriglia { Area = LayoutTavolo.AreaGrigliaDefault };
        }

        /// <summary>Calibrazione con cui il rilevatore converte la camera nelle coordinate del tavolo.</summary>
        public Calibrazione Calibrazione => calibrazione;

        public IReadOnlyList<CartoncinoRilevato> Rileva() => Rileva(null);

        public IReadOnlyList<CartoncinoRilevato> Rileva(TracciaCartoncini traccia)
        {
            if (traccia != null)
            {
                traccia.CalibrazionePresente = true;
                traccia.EsitoVisione = "Elaborazione";
            }
            var risultato = new List<CartoncinoRilevato>();
            if (fonte == null)
            {
                if (traccia != null) traccia.EsitoVisione = "FonteAssente";
                return risultato;
            }

            using (Mat frame = AcquisisciFrame(traccia))
            {
                if (frame == null || frame.Empty())
                {
                    if (traccia != null) traccia.EsitoVisione = "FrameAssente";
                    return risultato;
                }
                if (traccia != null) { traccia.LarghezzaFrame = frame.Cols; traccia.AltezzaFrame = frame.Rows; }

                Mat convertito = null;
                Mat maschera = null;
                try
                {
                    Mat bgr = frame;
                    if (frame.Channels() != 3)
                    {
                        convertito = new Mat();
                        if (frame.Channels() == 4)
                        {
                            Cv2.CvtColor(frame, convertito, ColorConversionCodes.BGRA2BGR);
                        }
                        else if (frame.Channels() == 1)
                        {
                            Cv2.CvtColor(frame, convertito, ColorConversionCodes.GRAY2BGR);
                        }
                        else
                        {
                            if (traccia != null) traccia.EsitoVisione = "CanaliNonSupportati";
                            return risultato;
                        }

                        bgr = convertito;
                    }

                    sequenzaFrame++;
                    List<QrTrovato> qr = DecodificaQr(bgr, traccia, out List<QrTrovato> localizzati);
                    maschera = MascheraCorpo(bgr, calibrazione, out Rect[] corpi);
                    if (traccia != null)
                        foreach (Rect corpo in corpi)
                            RegistraVisione(traccia, "CorpoCandidato", "Regione del corpo rilevata; identità non ancora nota.", null, corpo);
                    qr.AddRange(RecuperaQrMancanti(bgr, qr, localizzati, corpi, traccia));
                    if (qr.Count == 0)
                    {
                        if (traccia != null) traccia.EsitoVisione = "NessunQrDecodificato";
                        return risultato;
                    }

                    var candidati = new List<Candidato>();
                    foreach (QrTrovato q in qr)
                    {
                        try
                        {
                            if (traccia != null) RegistraQr(traccia, q, "QrDecodificato");
                            Candidato c = Analizza(corpi, q);
                            if (c != null)
                            {
                                candidati.Add(c);
                            }
                            else if (traccia != null) RegistraQr(traccia, q, "GeometriaQrNonValida");
                        }
                        catch (OpenCVException e)
                        {
                            if (traccia != null) RegistraQr(traccia, q, "ErroreAnalisiQr", e.Message);
                            // QR non elaborabile: ignorato.
                        }
                    }

                    foreach (Candidato c in candidati)
                    {
                        CartoncinoRilevato r = Converti(c, candidati, maschera, out Rect areaEffettiva);
                        if (r != null)
                        {
                            risultato.Add(r);
                            if (traccia != null && r.AreaQr.HasValue)
                                traccia.Visione.Add(new EventoDiagnosticoCartoncino("Visione", "AggancioQr", "AggancioQr",
                                    $"Attacco QR x={r.AreaQr.Value.X:0.00000}; fine colore={r.FineEstensioneX:0.00000}.",
                                    c.Testo, areaTavolo: r.AreaQr));
                            if (traccia != null) RegistraVisione(traccia, "CartoncinoRilevato",
                                "QR convertito nelle coordinate del tavolo.", c.Testo, areaEffettiva, areaTavolo: r.Area);
                        }
                        else if (traccia != null) RegistraVisione(traccia, "ConversioneNonRiuscita",
                            "QR letto, ma area del cartoncino non convertibile sul tavolo.", c.Testo, c.Riquadro);
                    }
                    if (traccia != null) traccia.EsitoVisione = risultato.Count > 0 ? "CartonciniRilevati" : "NessunCartoncinoConvertibile";
                }
                catch (OpenCVException e)
                {
                    if (traccia != null)
                    {
                        traccia.EsitoVisione = "ErroreVisione";
                        traccia.Errore = e.GetType().Name + ": " + e.Message;
                    }
                    // Frame non elaborabile: nessun cartoncino.
                    risultato.Clear();
                }
                finally
                {
                    maschera?.Dispose();
                    convertito?.Dispose();
                }
            }

            return risultato;
        }

        private Mat AcquisisciFrame(TracciaCartoncini traccia)
        {
            try { return fonte.Frame(); }
            catch (Exception e)
            {
                if (traccia != null)
                {
                    traccia.EsitoVisione = "ErroreAcquisizione";
                    traccia.Errore = e.GetType().Name + ": " + e.Message;
                }
                throw;
            }
        }

        private void RegistraVisione(TracciaCartoncini traccia, string codice, string dettaglio, string payload,
            Rect area, string origine = null, RettangoloTavolo? areaTavolo = null)
        {
            if (traccia == null) return;
            if (!areaTavolo.HasValue)
            {
                try
                {
                    PuntoTavolo[] punti = calibrazione.BordiDaCameraATavolo(area);
                    double x = punti.Min(p => p.X), y = punti.Min(p => p.Y);
                    areaTavolo = new RettangoloTavolo(x, y, punti.Max(p => p.X) - x, punti.Max(p => p.Y) - y);
                }
                catch (Exception) { /* Area pixel disponibile anche se l'omografia non è applicabile. */ }
            }
            traccia.Visione.Add(new EventoDiagnosticoCartoncino("Visione", codice, codice, dettaglio,
                payload, new RettangoloTavolo(area.X, area.Y, area.Width, area.Height), areaTavolo,
                areaTavolo.HasValue && traccia.Layout != null ? traccia.Layout.ZonaDelCentro(areaTavolo.Value).ToString() : null, origine));
        }

        private void RegistraQr(TracciaCartoncini traccia, QrTrovato qr, string codice, string dettaglio = null)
        {
            if (traccia == null) return;
            try
            {
                if (qr.Angoli.Any(p => float.IsNaN(p.X) || float.IsNaN(p.Y) || float.IsInfinity(p.X) || float.IsInfinity(p.Y)))
                    throw new ArgumentException("Coordinate QR non finite.");
                RegistraVisione(traccia, codice, dettaglio ?? "Payload QR decodificato.", qr.Testo, BoundingBox(qr.Angoli), qr.Origine);
            }
            catch (Exception e)
            {
                traccia.Visione.Add(new EventoDiagnosticoCartoncino("Visione", "GeometriaQrNonValida",
                    "GeometriaQrNonValida", e.Message, qr.Testo, origine: qr.Origine));
            }
        }

        private sealed class QrTrovato
        {
            public string Testo;
            public Point2f[] Angoli;
            public string Origine = "OpenCV";
        }

        private sealed class Candidato
        {
            public string Testo;
            public Point2f[] AngoliQr;
            public Rect Riquadro;   // bounding box del quadrilatero QR
            public Rect Zona;       // riquadro QR + zona quieta
            public Rect? Corpo;     // bounding rect del corpo (eventualmente fuso con i vicini)
        }

        private List<QrTrovato> DecodificaQr(Mat bgr, TracciaCartoncini traccia, out List<QrTrovato> localizzati)
        {
            var trovati = new List<QrTrovato>();
            localizzati = new List<QrTrovato>();
            using (var grigio = new Mat())
            {
                Cv2.CvtColor(bgr, grigio, ColorConversionCodes.BGR2GRAY);
                // MultiDetector propone anche combinazioni spurie di finder appartenenti a codici diversi.
                // Il catalogo globale ZXing contiene solo risultati decodificati; gli altri si cercano nei corpi.
                foreach (var q in DecodificaConZxing(grigio, null, "ZXingGlobale"))
                    if (!trovati.Any(e => Vicini(e, q))) trovati.Add(q);
            }
            using (var rilevatore = new QRCodeDetector())
            {
                try
                {
                    if (rilevatore.DetectMulti(bgr, out Point2f[] punti) && punti != null)
                    {
                        bool riuscita = rilevatore.DecodeMulti(bgr, punti, out string[] testi);
                        if (riuscita && testi != null)
                        {
                            for (int i = 0; i < testi.Length && (i + 1) * 4 <= punti.Length; i++)
                                if (!string.IsNullOrEmpty(testi[i]))
                                {
                                    var q = new QrTrovato { Testo = testi[i], Angoli = punti.Skip(i * 4).Take(4).ToArray() };
                                    if (!trovati.Any(e => Vicini(e, q))) trovati.Add(q);
                                }
                        }
                        for (int i = 0; (i + 1) * 4 <= punti.Length; i++)
                            localizzati.Add(new QrTrovato { Angoli = punti.Skip(i * 4).Take(4).ToArray() });
                        if (traccia != null)
                        {
                            for (int i = 0; (i + 1) * 4 <= punti.Length; i++)
                                if (!riuscita || testi == null || i >= testi.Length || string.IsNullOrEmpty(testi[i]))
                                    RegistraQr(traccia, new QrTrovato { Angoli = punti.Skip(i * 4).Take(4).ToArray() },
                                        "QrLocalizzatoNonDecodificato", "OpenCV ha localizzato un QR ma non ne ha decodificato il testo.");
                        }
                    }
                }
                catch (OpenCVException e)
                {
                    traccia?.Visione.Add(new EventoDiagnosticoCartoncino("Visione", "ErroreDecoder", "ErroreDecoder", e.Message, origine: "OpenCV"));
                }

                if (trovati.Count == 0)
                {
                    try
                    {
                        string testo = rilevatore.DetectAndDecode(bgr, out Point2f[] angoli);
                        if (!string.IsNullOrEmpty(testo) && angoli != null && angoli.Length >= 4)
                            trovati.Add(new QrTrovato { Testo = testo, Angoli = angoli.Take(4).ToArray() });
                        else if (traccia != null && angoli != null && angoli.Length >= 4)
                            RegistraQr(traccia, new QrTrovato { Angoli = angoli.Take(4).ToArray() },
                                "QrLocalizzatoNonDecodificato", "QR localizzato ma testo non decodificato.");
                    }
                    catch (OpenCVException e)
                    {
                        traccia?.Visione.Add(new EventoDiagnosticoCartoncino("Visione", "ErroreDecoder", "ErroreDecoder", e.Message, origine: "OpenCV"));
                    }
                }
            }
            return trovati;
        }

        // Per ogni corpo senza un QR letto vicino, ritenta la lettura sul ritaglio ingrandito e livellato.
        private List<QrTrovato> RecuperaQrMancanti(Mat bgr, List<QrTrovato> gia, List<QrTrovato> localizzati,
            Rect[] corpi, TracciaCartoncini traccia)
        {
            var nuovi = new List<QrTrovato>();
            var attuali = new List<RecuperoMemorizzato>();
            var scansioni = new Dictionary<Rect, List<QrTrovato>>();
            // Tentativi per QR localizzato, anche quando nella stessa regione ne è già stato letto un altro.
            foreach (var candidato in localizzati)
            {
                if (gia.Concat(nuovi).Any(q => Vicini(q, candidato))) continue;
                Rect r = BoundingBox(candidato.Angoli);
                Rect regione = Gonfia(r, (int)Math.Round(Math.Max(r.Width, r.Height) * .5))
                    & new Rect(0, 0, bgr.Cols, bgr.Rows);
                if (regione.Width < 8 || regione.Height < 8) continue;
                foreach (var q in LeggiRitaglio(bgr, regione, gia.Concat(nuovi).ToArray(), true))
                    if (!gia.Concat(nuovi).Any(e => Vicini(e, q))) nuovi.Add(q);
            }
            foreach (Rect corpo in corpi)
            {
                // Il QR sta a un'estremita' del cartoncino (a sinistra, o a destra con camera capovolta):
                // si include un margine laterale pari all'altezza e uno verticale ridotto.
                int h = corpo.Height;
                int margineY = (int)Math.Round(h * 0.3);
                Rect regione = new Rect(corpo.X - h, corpo.Y - margineY, corpo.Width + 2 * h, corpo.Height + 2 * margineY)
                    & new Rect(0, 0, bgr.Cols, bgr.Rows);
                if (regione.Width < 8 || regione.Height < 8)
                {
                    continue;
                }

                // Un componente interamente interno a un QR noto non è un altro corpo da recuperare.
                if (gia.Concat(nuovi).Any(q => Dentro(corpo, BoundingBox(q.Angoli))))
                {
                    continue;
                }

                byte[] aspetto = Impronta(bgr, regione);
                var memoria = recuperati.FirstOrDefault(r => Sovrapposizione(r.Corpo, corpo) >= .9
                    && AspettoSimile(r.Aspetto, aspetto) && sequenzaFrame - r.UltimaScansione < 8);
                List<QrTrovato> letti;
                long scansione;
                if (memoria != null)
                {
                    letti = memoria.Qr.Select(q => new QrTrovato { Testo = q.Testo, Origine = "Memoria",
                        Angoli = q.Angoli.Select(p => new Point2f(
                            regione.X + (p.X-memoria.Regione.X)*regione.Width/memoria.Regione.Width,
                            regione.Y + (p.Y-memoria.Regione.Y)*regione.Height/memoria.Regione.Height)).ToArray() }).ToList();
                    scansione = memoria.UltimaScansione;
                }
                else
                {
                    if (!scansioni.TryGetValue(regione, out letti))
                    {
                        letti = LeggiRitaglio(bgr, regione, gia.Concat(nuovi).ToArray());
                        scansioni[regione] = letti;
                    }
                    scansione = sequenzaFrame;
                }

                if (traccia != null && letti.Count == 0 && !gia.Concat(nuovi).Any(q => CentroDentro(q, regione))) RegistraVisione(traccia, "CorpoSenzaQrDecodificato",
                    "Corpo rilevato, ma nessun QR decodificato nel ritaglio; identità sconosciuta.", null, corpo);

                foreach (QrTrovato q in letti)
                {
                    if (!gia.Concat(nuovi).Any(e => Vicini(e, q)))
                    {
                        nuovi.Add(q);
                    }
                }
                var tutte = new List<QrTrovato>();
                foreach (var q in gia.Concat(nuovi).Where(q => CentroDentro(q, regione)).Concat(letti))
                    if (!tutte.Any(e => Vicini(e, q))) tutte.Add(q);
                // Anche i risultati vuoti hanno una cache; sempre limitata ai corpi del frame corrente.
                attuali.Add(new RecuperoMemorizzato { Corpo = corpo, Regione = regione, Aspetto = aspetto,
                    Qr = tutte, UltimaScansione = scansione });
            }

            recuperati = attuali;
            return nuovi;
        }

        private static bool Dentro(Rect a, Rect b) => a.Left >= b.Left && a.Right <= b.Right
            && a.Top >= b.Top && a.Bottom <= b.Bottom;

        private static byte[] Impronta(Mat bgr, Rect regione)
        {
            using (var ritaglio = new Mat(bgr, regione))
            using (var grigio = new Mat())
            using (var piccolo = new Mat())
            {
                Cv2.CvtColor(ritaglio, grigio, ColorConversionCodes.BGR2GRAY);
                Cv2.Resize(grigio, piccolo, new Size(64, 32), 0, 0, InterpolationFlags.Area);
                var valori = new byte[64*32];
                System.Runtime.InteropServices.Marshal.Copy(piccolo.Data, valori, 0, valori.Length);
                return valori;
            }
        }

        private static bool AspettoSimile(byte[] a, byte[] b)
        {
            if (a == null || a.Length != b.Length) return false;
            int somma = 0, cambiati = 0;
            for (int i = 0; i < a.Length; i++)
            {
                int delta = Math.Abs(a[i]-b[i]); somma += delta;
                if (delta > 12) cambiati++;
            }
            return somma <= a.Length*2.5 && cambiati <= 10;
        }

        // Intersezione su unione dei due rettangoli (0..1).
        private static double Sovrapposizione(Rect a, Rect b)
        {
            Rect inter = a & b;
            double area = inter.Width * inter.Height;
            double unione = a.Width * a.Height + b.Width * b.Height - area;
            return unione <= 0 ? 0 : area / unione;
        }

        private static bool CentroDentro(QrTrovato q, Rect regione)
        {
            float cx = q.Angoli.Average(p => p.X), cy = q.Angoli.Average(p => p.Y);
            return cx >= regione.Left && cx < regione.Right && cy >= regione.Top && cy < regione.Bottom;
        }

        private static bool Vicini(QrTrovato a, QrTrovato b)
        {
            float dx = a.Angoli.Average(p => p.X) - b.Angoli.Average(p => p.X);
            float dy = a.Angoli.Average(p => p.Y) - b.Angoli.Average(p => p.Y);
            double lato = Math.Max(1, Math.Min(BoundingBox(a.Angoli).Width, BoundingBox(b.Angoli).Width));
            return Math.Sqrt(dx * dx + dy * dy) < .35 * lato;
        }

        private static List<QrTrovato> LeggiRitaglio(Mat bgr, Rect regione, IReadOnlyList<QrTrovato> noti = null,
            bool qrLocalizzato = false)
        {
            var trovati = new List<QrTrovato>();
            double scala = Math.Max(2.0, Math.Min(6.0, AltezzaTargetQr / Math.Max(1.0, regione.Height / 1.6)));
            using (var ritaglio = new Mat(bgr, regione))
            using (var grigio = new Mat())
            using (var ingrandito = new Mat())
            {
                Cv2.CvtColor(ritaglio, grigio, ColorConversionCodes.BGR2GRAY);
                Cv2.Resize(grigio, ingrandito, new Size(), scala, scala, InterpolationFlags.Cubic);
                OscuraQrLetti(ingrandito, noti ?? new QrTrovato[0], regione, scala);

                foreach (Mat variante in Varianti(ingrandito))
                {
                    using (variante)
                    {
                        var candidati = new List<QrTrovato>();
                        var zxing = DecodificaConZxing(variante, candidati, "ZXingRitaglio").ToArray();
                        foreach (QrTrovato q in zxing)
                        {
                            Point2f[] angoli = q.Angoli
                                .Select(p => new Point2f((float)(p.X / scala + regione.X), (float)(p.Y / scala + regione.Y)))
                                .ToArray();
                            var letto = new QrTrovato { Testo = q.Testo, Angoli = angoli, Origine = q.Origine };
                            if (!trovati.Any(e => Vicini(e, letto))) trovati.Add(letto);
                        }
                        // Il decoder singolo vede solo i simboli ancora da recuperare. Eliminare i QR
                        // già letti evita di ripetere scansioni costose e di fermarsi sempre sul primo.
                        using (var mancanti = variante.Clone())
                        {
                            OscuraQrLetti(mancanti, (noti ?? new QrTrovato[0]).Concat(trovati), regione, scala);
                            var nativi = qrLocalizzato || candidati.Any(c => !zxing.Any(q => Vicini(c, q)))
                                ? DecodificaConOpenCv(mancanti) : Enumerable.Empty<QrTrovato>();
                            foreach (QrTrovato q in nativi)
                            {
                                var letto = new QrTrovato { Testo = q.Testo, Origine = q.Origine,
                                    Angoli = q.Angoli.Select(p => new Point2f((float)(p.X/scala+regione.X), (float)(p.Y/scala+regione.Y))).ToArray() };
                                if (!trovati.Any(e => Vicini(e, letto))) trovati.Add(letto);
                            }
                        }
                    }

                    // Una variante può leggere soltanto parte dei QR presenti nella stessa regione.
                    OscuraQrLetti(ingrandito, trovati, regione, scala);
                }
            }

            return trovati;
        }

        private static void OscuraQrLetti(Mat immagine, IEnumerable<QrTrovato> qr, Rect regione, double scala)
        {
            foreach (var q in qr)
            {
                var poligono = q.Angoli.Select(p => new Point((int)Math.Round((p.X-regione.X)*scala),
                    (int)Math.Round((p.Y-regione.Y)*scala))).ToArray();
                Cv2.FillConvexPoly(immagine, poligono, Scalar.All(255));
            }
        }

        // Grigio, livellato (divide per lo sfondo sfocato: toglie bagliori e gradienti) e contrasto adattivo.
        private static IEnumerable<Mat> Varianti(Mat grigio)
        {
            yield return grigio.Clone();

            Mat livellato = new Mat();
            using (var sfondo = new Mat())
            {
                int sigma = Math.Max(5, grigio.Rows / 12);
                Cv2.GaussianBlur(grigio, sfondo, new Size(), sigma);
                Cv2.Divide(grigio, sfondo, livellato, 255);
            }

            yield return livellato;

            using (var clahe = Cv2.CreateCLAHE(2.0, new Size(8, 8)))
            {
                var contrasto = new Mat();
                clahe.Apply(grigio, contrasto);
                yield return contrasto;
            }
        }

        private static IEnumerable<QrTrovato> DecodificaConOpenCv(Mat grigio)
        {
            var trovati = new List<QrTrovato>();
            using (var rilevatore = new QRCodeDetector())
            {
                try
                {
                    string testo = rilevatore.DetectAndDecode(grigio, out Point2f[] angoli);
                    if (!string.IsNullOrEmpty(testo) && angoli != null && angoli.Length >= 4)
                    {
                        trovati.Add(new QrTrovato { Testo = testo, Angoli = angoli.Take(4).ToArray(), Origine = "OpenCVRitaglio" });
                    }
                }
                catch (OpenCVException)
                {
                }
            }

            return trovati;
        }

        private static IEnumerable<QrTrovato> DecodificaConZxing(Mat grigio) => DecodificaConZxing(grigio, null, "ZXingRitaglio");

        private static IEnumerable<QrTrovato> DecodificaConZxing(Mat grigio, List<QrTrovato> localizzati, string origine)
        {
            var trovati = new List<QrTrovato>();
            try
            {
                byte[] byteGrigi = new byte[grigio.Rows * grigio.Cols];
                System.Runtime.InteropServices.Marshal.Copy(grigio.Data, byteGrigi, 0, byteGrigi.Length);
                var luminanza = new RGBLuminanceSource(byteGrigi, grigio.Cols, grigio.Rows, RGBLuminanceSource.BitmapFormat.Gray8);
                var bitmap = new BinaryBitmap(new HybridBinarizer(luminanza));
                var suggerimenti = new Dictionary<DecodeHintType, object>
                {
                    { DecodeHintType.TRY_HARDER, true },
                    { DecodeHintType.POSSIBLE_FORMATS, new List<BarcodeFormat> { BarcodeFormat.QR_CODE } },
                };

                if (localizzati != null)
                {
                    try
                    {
                        var detector = new ZXing.Multi.QrCode.Internal.MultiDetector(bitmap.BlackMatrix);
                        var candidati = detector.detectMulti(suggerimenti);
                        if (candidati != null)
                            foreach (var candidato in candidati)
                            {
                                var angoli = AngoliDaPunti(candidato.Points);
                                if (angoli != null) localizzati.Add(new QrTrovato { Angoli = angoli, Origine = origine });
                            }
                    }
                    catch (Exception) { /* Un errore del localizzatore non impedisce il tentativo del decoder. */ }
                }
                Result[] risultati = new QRCodeMultiReader().decodeMultiple(bitmap, suggerimenti);
                if (risultati != null)
                {
                    foreach (Result r in risultati)
                    {
                        Point2f[] angoli = AngoliDaPunti(r.ResultPoints);
                        if (!string.IsNullOrEmpty(r.Text) && angoli != null)
                        {
                            trovati.Add(new QrTrovato { Testo = r.Text, Angoli = angoli, Origine = origine });
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Lettura non riuscita su questa variante: si passa alla successiva.
            }

            return trovati;
        }

        // ZXing restituisce i centri dei tre riquadri di posizione (basso-sinistra, alto-sinistra, alto-destra)
        // e, se presente, il quarto punto di allineamento: si ricostruiscono i quattro angoli del QR.
        private static Point2f[] AngoliDaPunti(ResultPoint[] punti)
        {
            if (punti == null || punti.Length < 3)
            {
                return null;
            }

            var bl = new Point2f(punti[0].X, punti[0].Y);
            var tl = new Point2f(punti[1].X, punti[1].Y);
            var tr = new Point2f(punti[2].X, punti[2].Y);
            double lunghezza = Distanza(tl, tr);
            double altezza = Distanza(tl, bl);
            if (lunghezza < 1 || altezza < 1)
            {
                return null;
            }

            double moduli = punti.Length >= 4 ? 18.0 : 14.0; // distanza tra i centri dei riquadri, in moduli
            double modulo = lunghezza / moduli;
            var u = new Point2f((float)((tr.X - tl.X) / lunghezza), (float)((tr.Y - tl.Y) / lunghezza));
            var v = new Point2f((float)((bl.X - tl.X) / altezza), (float)((bl.Y - tl.Y) / altezza));
            float m = (float)(3.5 * modulo);

            Point2f Angolo(Point2f p, float su, float sv)
            {
                return new Point2f(p.X + m * (su * u.X + sv * v.X), p.Y + m * (su * u.Y + sv * v.Y));
            }

            var br = new Point2f(tr.X + bl.X - tl.X, tr.Y + bl.Y - tl.Y);
            return new[] { Angolo(tl, -1, -1), Angolo(tr, 1, -1), Angolo(br, 1, 1), Angolo(bl, -1, 1) };
        }

        private static double Distanza(Point2f a, Point2f b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // Maschera del corpo: pixel il cui canale minimo e' molto sotto il livello locale della carta/sfondo
        // (qualsiasi colore pieno o grigio scuro; linee sottili e la griglia chiara sono escluse).
        private static Mat MascheraCorpo(Mat bgr, Calibrazione calibrazione, out Rect[] rettangoli)
            => MascheraCorpoInterna(bgr, calibrazione, out rettangoli, true, AreaMassimaCorpo * bgr.Cols * bgr.Rows);

        private static Mat MascheraCorpoInterna(Mat bgr, Calibrazione calibrazione, out Rect[] rettangoli,
            bool limitaAlTavolo, double areaMassima)
        {
            var maschera = new Mat();
            try
            {
                Mat[] canali = Cv2.Split(bgr);
                using (var minimo = new Mat())
                using (var fondo = new Mat())
                using (var soglia = new Mat())
                using (var abbastanzaChiaro = new Mat())
                {
                    try
                    {
                        Cv2.Min(canali[0], canali[1], minimo);
                        Cv2.Min(minimo, canali[2], minimo);
                    }
                    finally
                    {
                        foreach (Mat c in canali)
                        {
                            c.Dispose();
                        }
                    }

                    using (var finestra = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(LatoFondoLocale, LatoFondoLocale)))
                    {
                        Cv2.Dilate(minimo, fondo, finestra);
                    }

                    Cv2.Multiply(fondo, Scalar.All(SogliaCorpo), soglia);
                    Cv2.Compare(minimo, soglia, maschera, CmpType.LT);
                    Cv2.Threshold(fondo, abbastanzaChiaro, FondoMinimo, 255, ThresholdTypes.Binary);
                    Cv2.BitwiseAnd(maschera, abbastanzaChiaro, maschera);
                }

                // Fuori dall'area calibrata (cornice dello schermo, mani, tavolo) non ci sono cartoncini.
                if (limitaAlTavolo)
                {
                    using (Mat tavolo = calibrazione.MascheraTavolo(bgr.Cols, bgr.Rows, RestringiTavolo))
                        Cv2.BitwiseAnd(maschera, tavolo, maschera);
                }

                using (var apertura = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(LatoNucleoApertura, LatoNucleoApertura)))
                using (var chiusura = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(LatoNucleoChiusura, LatoNucleoChiusura)))
                {
                    Cv2.MorphologyEx(maschera, maschera, MorphTypes.Open, apertura);
                    Cv2.MorphologyEx(maschera, maschera, MorphTypes.Close, chiusura);
                }

                // FindContours puo' modificare l'immagine in ingresso: lavora su una copia.
                using (Mat copia = maschera.Clone())
                {
                    Cv2.FindContours(copia, out Point[][] contorni, out HierarchyIndex[] _,
                        RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                    rettangoli = contorni
                        .Select(Cv2.BoundingRect)
                        .Where(r => r.Width * r.Height >= AreaMinimaCorpo
                            && r.Width * r.Height <= areaMassima)
                        .ToArray();
                }

                return maschera;
            }
            catch
            {
                maschera.Dispose();
                throw;
            }
        }

        // Associa a un QR il corpo con maggiore sovrapposizione con la zona del QR (contatto incluso).
        private static Candidato Analizza(Rect[] corpi, QrTrovato qr)
        {
            Point2f[] a = qr.Angoli;
            if (a.Any(p => float.IsNaN(p.X) || float.IsNaN(p.Y) || float.IsInfinity(p.X) || float.IsInfinity(p.Y)))
            {
                return null;
            }

            Rect riquadro = BoundingBox(a);
            int margine = (int)Math.Round(Math.Max(riquadro.Width, riquadro.Height) * FattoreZonaQuieta);
            Rect zona = Gonfia(riquadro, margine);
            Rect zonaContatto = Gonfia(zona, TolleranzaContatto);

            var c = new Candidato { Testo = qr.Testo, AngoliQr = qr.Angoli, Riquadro = riquadro, Zona = zona };
            Rect migliore = default(Rect);
            int areaMigliore = 0;
            foreach (Rect r in corpi)
            {
                Rect inter = r & zonaContatto;
                int area = inter.Width * inter.Height;
                if (inter.Width > 0 && inter.Height > 0 && area > areaMigliore)
                {
                    areaMigliore = area;
                    migliore = r;
                }
            }

            if (areaMigliore > 0)
            {
                c.Corpo = migliore;
            }

            return c;
        }

        private CartoncinoRilevato Converti(Candidato c, List<Candidato> tutti, Mat maschera, out Rect areaCamera)
        {
            Rect rettangolo = c.Corpo.HasValue ? Ritaglia(c, tutti, maschera) : c.Riquadro;
            areaCamera = rettangolo;

            try
            {
                PuntoTavolo[] punti = calibrazione.BordiDaCameraATavolo(rettangolo);

                double minX = punti.Min(p => p.X), maxX = punti.Max(p => p.X);
                double minY = punti.Min(p => p.Y), maxY = punti.Max(p => p.Y);
                PuntoTavolo[] puntiQr = c.AngoliQr.Select(p => calibrazione.DaCameraATavolo(new PuntoImmagine(p.X, p.Y))).ToArray();
                double qx = puntiQr.Min(p => p.X), qy = puntiQr.Min(p => p.Y);
                var areaQr = new RettangoloTavolo(qx, qy, puntiQr.Max(p => p.X) - qx, puntiQr.Max(p => p.Y) - qy);
                return new CartoncinoRilevato
                {
                    Payload = c.Testo,
                    Area = new RettangoloTavolo(minX, minY, maxX - minX, maxY - minY),
                    AreaQr = areaQr,
                    FineEstensioneX = MisuratoreEstensioneNota.Misura(maschera, calibrazione, areaQr, griglia),
                };
            }
            catch (Exception)
            {
                // Conversione non possibile (es. punto sul piano all'infinito): cartoncino scartato.
                return null;
            }
        }

        // Unisce il corpo con la zona del QR e lo ritaglia con i vicini.
        private static Rect Ritaglia(Candidato c, List<Candidato> tutti, Mat maschera)
        {
            // Estensione lungo la fascia del QR: righe del riquadro per sinistra/destra, colonne per alto/basso.
            // La zona del QR (riquadro bianco) conta come piena anche se fuori maschera.
            Rect z = c.Zona;
            int sinistra = z.Left, destra = z.Right, alto = z.Top, basso = z.Bottom;
            sinistra = Espandi(sinistra, -1, 0, maschera.Cols, x => ColonnaPiena(maschera, x, c.Riquadro.Top, c.Riquadro.Bottom));
            destra = Espandi(destra - 1, +1, 0, maschera.Cols, x => ColonnaPiena(maschera, x, c.Riquadro.Top, c.Riquadro.Bottom)) + 1;
            alto = Espandi(alto, -1, 0, maschera.Rows, y => RigaPiena(maschera, y, c.Riquadro.Left, c.Riquadro.Right));
            basso = Espandi(basso - 1, +1, 0, maschera.Rows, y => RigaPiena(maschera, y, c.Riquadro.Left, c.Riquadro.Right)) + 1;

            foreach (Candidato n in tutti)
            {
                if (ReferenceEquals(n, c))
                {
                    continue;
                }

                bool sovrappostoY = Sovrappone(n.Riquadro.Top, n.Riquadro.Bottom, c.Riquadro.Top, c.Riquadro.Bottom);
                bool sovrappostoX = Sovrappone(n.Riquadro.Left, n.Riquadro.Right, c.Riquadro.Left, c.Riquadro.Right);
                if (sovrappostoY && n.Riquadro.Left > c.Riquadro.Left)
                {
                    destra = Math.Min(destra, n.Zona.Left);
                }
                else if (sovrappostoY && n.Riquadro.Left < c.Riquadro.Left)
                {
                    sinistra = Math.Max(sinistra, c.Zona.Left);
                }
                else if (sovrappostoX && n.Riquadro.Top < c.Riquadro.Top)
                {
                    alto = Math.Max(alto, (n.Riquadro.Bottom + c.Riquadro.Top) / 2);
                }
                else if (sovrappostoX && n.Riquadro.Top > c.Riquadro.Top)
                {
                    basso = Math.Min(basso, (c.Riquadro.Bottom + n.Riquadro.Top) / 2);
                }
            }

            return new Rect(sinistra, alto, Math.Max(1, destra - sinistra), Math.Max(1, basso - alto));
        }

        // Parte da "inizio" e avanza nel verso dato finche' trova posizioni piene; tollera piccoli vuoti
        // (imprecisione del quadrilatero QR rispetto al riquadro bianco). Restituisce l'ultima posizione piena.
        private static int Espandi(int inizio, int verso, int minimo, int massimoEscluso, Func<int, bool> piena)
        {
            int ultimo = inizio;
            int vuoti = 0;
            for (int p = inizio + verso; p >= minimo && p < massimoEscluso && vuoti <= VuotiTollerati; p += verso)
            {
                if (piena(p))
                {
                    ultimo = p;
                    vuoti = 0;
                }
                else
                {
                    vuoti++;
                }
            }

            return ultimo;
        }

        private static bool ColonnaPiena(Mat m, int x, int y0, int y1)
        {
            for (int y = Math.Max(0, y0); y < Math.Min(m.Rows, y1); y++)
            {
                if (m.At<byte>(y, x) != 0) return true;
            }

            return false;
        }

        private static bool RigaPiena(Mat m, int y, int x0, int x1)
        {
            for (int x = Math.Max(0, x0); x < Math.Min(m.Cols, x1); x++)
            {
                if (m.At<byte>(y, x) != 0) return true;
            }

            return false;
        }

        private static bool Sovrappone(int a0, int a1, int b0, int b1)
        {
            return a0 < b1 && b0 < a1;
        }

        private static Rect Gonfia(Rect r, int m)
        {
            return new Rect(r.X - m, r.Y - m, r.Width + 2 * m, r.Height + 2 * m);
        }

        private static Rect BoundingBox(Point2f[] a)
        {
            int x0 = (int)Math.Floor(a.Min(p => p.X)), x1 = (int)Math.Ceiling(a.Max(p => p.X));
            int y0 = (int)Math.Floor(a.Min(p => p.Y)), y1 = (int)Math.Ceiling(a.Max(p => p.Y));
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }
    }
}
