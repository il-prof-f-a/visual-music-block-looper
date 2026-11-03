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
        internal EsitoVariazioni(bool affidabile, IEnumerable<RettangoloTavolo> regioni, double frazione)
        { Affidabile = affidabile; Regioni = Array.AsReadOnly(regioni.ToArray()); FrazioneCambiata = frazione; }
        public bool Affidabile { get; }
        public IReadOnlyList<RettangoloTavolo> Regioni { get; }
        public double FrazioneCambiata { get; }
    }
    /// <summary>Firma leggera del foglio raddrizzato. Solo Conferma avanza il riferimento delle zone risolte.</summary>
    public sealed class RilevatoreVariazioni : IDisposable
    {
        private const int W = 640, H = 360;
        private readonly byte[] bgr = new byte[W * H * 3], attuale = new byte[W * H], precedente = new byte[W * H], differenze = new byte[W * H];
        private readonly int[] istogramma = new int[256];
        private byte[] riferimento;
        private bool ultimoAffidabile, haPrecedente;
        private readonly Mat piccola = new Mat(), convertita = new Mat(), maschera = new Mat(H, W, MatType.CV_8UC1);
        public EsitoVariazioni Confronta(Mat fotogramma)
        {
            if (fotogramma == null || fotogramma.Empty()) return new EsitoVariazioni(false, new RettangoloTavolo[0], 0);
            Mat colore = fotogramma;
            if (fotogramma.Channels() == 1) { Cv2.CvtColor(fotogramma, convertita, ColorConversionCodes.GRAY2BGR); colore = convertita; }
            else if (fotogramma.Channels() == 4) { Cv2.CvtColor(fotogramma, convertita, ColorConversionCodes.BGRA2BGR); colore = convertita; }
            Cv2.Resize(colore, piccola, new Size(W, H), 0, 0, InterpolationFlags.Area);
            Marshal.Copy(piccola.Data, bgr, 0, bgr.Length); Array.Clear(istogramma, 0, istogramma.Length);
            for (int i = 0; i < attuale.Length; i++) istogramma[(bgr[i*3]*29 + bgr[i*3+1]*150 + bgr[i*3+2]*77) >> 8]++;
            int bianco = 255, somma = 0;
            for (int i = 0; i < 256; i++) { somma += istogramma[i]; if (somma >= attuale.Length * .85) { bianco = i; break; } }
            int cambiati = 0, fraOsservazioni = 0;
            for (int i = 0; i < attuale.Length; i++)
            {
                int b = bgr[i*3], g = bgr[i*3+1], r = bgr[i*3+2];
                int grigio = (b*29+g*150+r*77) >> 8;
                byte firma = (byte)((grigio < bianco*.55 ? 1 : 0) | (Math.Min(b, Math.Min(g,r)) < bianco*.30 ? 2 : 0));
                int minimo=Math.Min(b,Math.Min(g,r)),massimo=Math.Max(b,Math.Max(g,r));
                if(firma!=0&&massimo-minimo>bianco*.25)
                {
                    double soglia=minimo+(massimo-minimo)*.6;
                    firma|=(byte)((r>soglia?4:0)|(g>soglia?8:0)|(b>soglia?16:0));
                }
                if (firma != precedente[i]) fraOsservazioni++;
                attuale[i] = firma;
                bool cambiato = riferimento != null && firma != riferimento[i];
                differenze[i] = cambiato ? (byte)255 : (byte)0;
                if (cambiato) cambiati++;
            }
            bool globaleInstabile = riferimento != null && cambiati > attuale.Length*.45 && (!haPrecedente || fraOsservazioni > attuale.Length*.02);
            Array.Copy(attuale, precedente, attuale.Length); haPrecedente = true;
            ultimoAffidabile = bianco >= 80 && !globaleInstabile;
            if (!ultimoAffidabile) return new EsitoVariazioni(false, new RettangoloTavolo[0], (double)cambiati/attuale.Length);
            if (riferimento == null) return new EsitoVariazioni(true, new[] { new RettangoloTavolo(0,0,1,1) }, 1);
            if (cambiati == 0) return new EsitoVariazioni(true, new RettangoloTavolo[0], 0);
            Marshal.Copy(differenze, 0, maschera.Data, differenze.Length);
            using (var nucleo = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(9,9)))
                Cv2.Dilate(maschera, maschera, nucleo);
            Cv2.FindContours(maschera, out Point[][] contorni, out HierarchyIndex[] _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            var regioni = contorni.Select(Cv2.BoundingRect).Where(a => a.Width*a.Height >= 30).Select(a =>
            {
                int x = Math.Max(0, a.X-12), y = Math.Max(0, a.Y-12);
                int x1 = Math.Min(W, a.Right+12), y1 = Math.Min(H, a.Bottom+12);
                return new RettangoloTavolo((double)x/W,(double)y/H,(double)(x1-x)/W,(double)(y1-y)/H);
            }).ToArray();
            return new EsitoVariazioni(true, regioni, (double)cambiati/attuale.Length);
        }
        public void Conferma(IReadOnlyList<RettangoloTavolo> regioni)
        {
            if (!ultimoAffidabile || regioni == null || regioni.Count == 0) return;
            if (riferimento == null) riferimento = new byte[attuale.Length];
            foreach (var r in regioni)
            {
                int x = Math.Max(0, (int)Math.Floor(r.X*W)), y = Math.Max(0, (int)Math.Floor(r.Y*H));
                int x1 = Math.Min(W,(int)Math.Ceiling((r.X+r.Larghezza)*W)), y1 = Math.Min(H,(int)Math.Ceiling((r.Y+r.Altezza)*H));
                for (int cy = y; cy < y1; cy++) Array.Copy(attuale,cy*W+x,riferimento,cy*W+x,x1-x);
            }
        }
        public void Dispose() { piccola.Dispose(); convertita.Dispose(); maschera.Dispose(); }
    }
}
