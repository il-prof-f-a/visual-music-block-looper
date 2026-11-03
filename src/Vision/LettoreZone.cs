using System;
using System.Collections.Generic;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Vision
{
    public sealed class LetturaZona
    {
        internal LetturaZona(IReadOnlyList<CartoncinoRilevato> catalogo, bool completa, int qr, Rect camera)
        { Catalogo=catalogo;Completa=completa;QrDecodificati=qr;RegioneCamera=camera; }
        public IReadOnlyList<CartoncinoRilevato> Catalogo { get; }
        public bool Completa { get; }
        public int QrDecodificati { get; }
        public Rect RegioneCamera { get; }
    }
    public sealed class LettoreZone
    {
        private readonly Calibrazione calibrazione;
        private readonly ConfigGriglia griglia;
        public LettoreZone(Calibrazione calibrazione,ConfigGriglia griglia)
        { this.calibrazione=calibrazione??throw new ArgumentNullException(nameof(calibrazione));this.griglia=griglia??throw new ArgumentNullException(nameof(griglia)); }
        public Rect UltimaRegioneCamera { get; private set; }
        public LetturaZona Leggi(Mat frame,RettangoloTavolo zona,IReadOnlyList<CartoncinoRilevato> noti,TracciaCartoncini traccia=null)
        {
            // Ogni lettura ha stato proprio: nessuna cache di decodifica condivisa fra note e comandi.
            var r=new RilevatoreCartoncini(null,calibrazione,griglia).LeggiZona(frame,zona,noti,traccia);
            UltimaRegioneCamera=r.RegioneCamera;return r;
        }
    }
}
