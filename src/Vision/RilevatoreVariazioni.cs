using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Vision
{
    public sealed class EsitoVariazioni
    {
        internal EsitoVariazioni(bool affidabile, IEnumerable<RettangoloTavolo> regioni, double frazione,int livelloSfondo)
        { Affidabile = affidabile; Regioni = Array.AsReadOnly(regioni.ToArray()); FrazioneCambiata = frazione; LivelloSfondo=livelloSfondo; }
        public bool Affidabile { get; }
        public IReadOnlyList<RettangoloTavolo> Regioni { get; }
        public double FrazioneCambiata { get; }
        public int LivelloSfondo { get; }
    }

    /// <summary>Firma leggera del foglio raddrizzato. Solo Conferma avanza il riferimento delle zone risolte.</summary>
    public sealed class RilevatoreVariazioni : IDisposable
    {
        private readonly ParametriRilevatoreVariazioni parametri;
        private readonly int larghezza, altezza, pixel;
        private readonly byte[] bgr, attuale, precedente, differenze;
        private readonly int[] istogramma = new int[256];
        private byte[] riferimento;
        private bool ultimoAffidabile, haPrecedente;
        private readonly Mat piccola, convertita = new Mat(), maschera;

        public RilevatoreVariazioni() : this(null) { }

        public RilevatoreVariazioni(ParametriRilevatoreVariazioni parametri)
        {
            this.parametri = (parametri ?? new ParametriRilevatoreVariazioni()).Normalizzati();
            larghezza = this.parametri.LarghezzaAnalisi;
            altezza = this.parametri.AltezzaAnalisi;
            pixel = larghezza * altezza;
            bgr = new byte[pixel * 3];
            attuale = new byte[pixel];
            precedente = new byte[pixel];
            differenze = new byte[pixel];
            piccola = new Mat();
            maschera = new Mat(altezza, larghezza, MatType.CV_8UC1);
        }

        public EsitoVariazioni Confronta(Mat fotogramma)
        {
            if (fotogramma == null || fotogramma.Empty())
                return new EsitoVariazioni(false, new RettangoloTavolo[0], 0, 0);

            Mat colore = fotogramma;
            if (fotogramma.Channels() == 1)
            { Cv2.CvtColor(fotogramma, convertita, ColorConversionCodes.GRAY2BGR); colore = convertita; }
            else if (fotogramma.Channels() == 4)
            { Cv2.CvtColor(fotogramma, convertita, ColorConversionCodes.BGRA2BGR); colore = convertita; }

            Cv2.Resize(colore, piccola, new Size(larghezza, altezza), 0, 0, InterpolationFlags.Area);
            Marshal.Copy(piccola.Data, bgr, 0, bgr.Length);
            Array.Clear(istogramma, 0, istogramma.Length);
            for (int i = 0; i < pixel; i++)
                istogramma[(bgr[i * 3] * 29 + bgr[i * 3 + 1] * 150 + bgr[i * 3 + 2] * 77) >> 8]++;

            int bianco = 255, somma = 0;
            int obiettivoSfondo = (int)Math.Ceiling(pixel * parametri.PercentileSfondo);
            for (int i = 0; i < 256; i++)
            { somma += istogramma[i]; if (somma >= obiettivoSfondo) { bianco = i; break; } }

            int cambiati = 0, fraOsservazioni = 0;
            for (int i = 0; i < pixel; i++)
            {
                int b = bgr[i * 3], g = bgr[i * 3 + 1], r = bgr[i * 3 + 2];
                int grigio = (b * 29 + g * 150 + r * 77) >> 8;
                byte firma = (byte)((grigio < bianco * parametri.SogliaGrigioRelativa ? 1 : 0)
                    | (Math.Min(b, Math.Min(g, r)) < bianco * parametri.SogliaNeroRelativa ? 2 : 0));
                int minimo = Math.Min(b, Math.Min(g, r)), massimo = Math.Max(b, Math.Max(g, r));
                if (firma != 0 && massimo - minimo > bianco * parametri.SogliaColoreRelativa)
                {
                    double soglia = minimo + (massimo - minimo) * parametri.SogliaDominanzaColore;
                    firma |= (byte)((r > soglia ? 4 : 0) | (g > soglia ? 8 : 0) | (b > soglia ? 16 : 0));
                }
                if (firma != precedente[i]) fraOsservazioni++;
                attuale[i] = firma;
                bool cambiato = riferimento != null && firma != riferimento[i];
                differenze[i] = cambiato ? (byte)255 : (byte)0;
                if (cambiato) cambiati++;
            }

            bool globaleInstabile = riferimento != null
                && cambiati > pixel * parametri.SogliaInstabilitaGlobale
                && (!haPrecedente || fraOsservazioni > pixel * parametri.SogliaVariazioneFrame);
            Array.Copy(attuale, precedente, pixel);
            haPrecedente = true;
            ultimoAffidabile = bianco >= parametri.LuminositaMinimaSfondo && !globaleInstabile;
            if (!ultimoAffidabile)
                return new EsitoVariazioni(false, new RettangoloTavolo[0], (double)cambiati / pixel, bianco);
            if (riferimento == null)
                return new EsitoVariazioni(true, new[] { new RettangoloTavolo(0, 0, 1, 1) }, 1, bianco);
            if (cambiati == 0)
                return new EsitoVariazioni(true, new RettangoloTavolo[0], 0, bianco);

            Marshal.Copy(differenze, 0, maschera.Data, differenze.Length);
            using (var nucleo = Cv2.GetStructuringElement(MorphShapes.Rect,
                new Size(parametri.DimensioneDilatazione, parametri.DimensioneDilatazione)))
                Cv2.Dilate(maschera, maschera, nucleo);
            Cv2.FindContours(maschera, out Point[][] contorni, out HierarchyIndex[] _,
                RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            var regioni = contorni.Select(Cv2.BoundingRect)
                .Where(a => a.Width * a.Height >= parametri.AreaMinimaContorno)
                .Select(a =>
                {
                    int x = Math.Max(0, a.X - parametri.MargineRegione);
                    int y = Math.Max(0, a.Y - parametri.MargineRegione);
                    int x1 = Math.Min(larghezza, a.Right + parametri.MargineRegione);
                    int y1 = Math.Min(altezza, a.Bottom + parametri.MargineRegione);
                    return new RettangoloTavolo((double)x / larghezza, (double)y / altezza,
                        (double)(x1 - x) / larghezza, (double)(y1 - y) / altezza);
                }).ToArray();
            return new EsitoVariazioni(true, regioni, (double)cambiati / pixel, bianco);
        }

        public void Conferma(IReadOnlyList<RettangoloTavolo> regioni)
        {
            if (!ultimoAffidabile || regioni == null || regioni.Count == 0) return;
            if (riferimento == null) riferimento = new byte[pixel];
            foreach (var r in regioni)
            {
                int x = Math.Max(0, (int)Math.Floor(r.X * larghezza));
                int y = Math.Max(0, (int)Math.Floor(r.Y * altezza));
                int x1 = Math.Min(larghezza, (int)Math.Ceiling((r.X + r.Larghezza) * larghezza));
                int y1 = Math.Min(altezza, (int)Math.Ceiling((r.Y + r.Altezza) * altezza));
                for (int cy = y; cy < y1; cy++)
                    Array.Copy(attuale, cy * larghezza + x, riferimento, cy * larghezza + x, x1 - x);
            }
        }

        public void Dispose() { piccola.Dispose(); convertita.Dispose(); maschera.Dispose(); }
    }
}
