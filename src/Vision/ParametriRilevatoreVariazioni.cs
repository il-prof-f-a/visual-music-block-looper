using System;

namespace VisualMusicBlockLooper.Vision
{
    /// <summary>Parametri regolabili per il confronto della scena e la conferma della rimozione dei QR.</summary>
    public sealed class ParametriRilevatoreVariazioni
    {
        public int LarghezzaAnalisi { get; set; } = 640;
        public int AltezzaAnalisi { get; set; } = 360;
        public double PercentileSfondo { get; set; } = .85;
        public int LuminositaMinimaSfondo { get; set; } = 80;
        public double SogliaGrigioRelativa { get; set; } = .55;
        public double SogliaNeroRelativa { get; set; } = .30;
        public double SogliaColoreRelativa { get; set; } = .25;
        public double SogliaDominanzaColore { get; set; } = .60;
        public double SogliaInstabilitaGlobale { get; set; } = .45;
        public double SogliaVariazioneFrame { get; set; } = .02;
        public int DimensioneDilatazione { get; set; } = 9;
        public int AreaMinimaContorno { get; set; } = 30;
        public int MargineRegione { get; set; } = 12;
        public double FrazioneQrVicinoAlFondo { get; set; } = .72;

        /// <summary>Restituisce una copia limitata a valori con cui la geometria e gli array restano validi.</summary>
        public ParametriRilevatoreVariazioni Normalizzati()
        {
            int larghezza=Limita(LarghezzaAnalisi,160,1920,640);
            int altezza=Limita(AltezzaAnalisi,90,1080,360);
            int dilatazione=Limita(DimensioneDilatazione,1,31,9);
            if(dilatazione%2==0)dilatazione=dilatazione==31?31:dilatazione+1;
            return new ParametriRilevatoreVariazioni
            {
                LarghezzaAnalisi=larghezza,
                AltezzaAnalisi=altezza,
                PercentileSfondo=Limita(PercentileSfondo,.50,.99,.85),
                LuminositaMinimaSfondo=Limita(LuminositaMinimaSfondo,0,255,80),
                SogliaGrigioRelativa=Limita(SogliaGrigioRelativa,0,1,.55),
                SogliaNeroRelativa=Limita(SogliaNeroRelativa,0,1,.30),
                SogliaColoreRelativa=Limita(SogliaColoreRelativa,0,1,.25),
                SogliaDominanzaColore=Limita(SogliaDominanzaColore,0,1,.60),
                SogliaInstabilitaGlobale=Limita(SogliaInstabilitaGlobale,.01,1,.45),
                SogliaVariazioneFrame=Limita(SogliaVariazioneFrame,0,1,.02),
                DimensioneDilatazione=dilatazione,
                AreaMinimaContorno=Limita(AreaMinimaContorno,1,larghezza*altezza,30),
                MargineRegione=Limita(MargineRegione,0,128,12),
                FrazioneQrVicinoAlFondo=Limita(FrazioneQrVicinoAlFondo,0,1,.72)
            };
        }

        private static int Limita(int valore,int minimo,int massimo,int predefinito)
            =>valore<minimo||valore>massimo?predefinito:valore;
        private static double Limita(double valore,double minimo,double massimo,double predefinito)
            =>double.IsNaN(valore)||double.IsInfinity(valore)||valore<minimo||valore>massimo?predefinito:valore;
    }
}
