namespace VisualMusicBlockLooper.Core
{
    /// <summary>Superficie di controllo comune; il runtime prioritario accoda le richieste dei produttori.</summary>
    public interface IMotoreMusicale
    {
        ModoSincronia Modo { get; }
        bool InEsecuzione { get; }
        double Bpm { get; }
        int StepCorrente { get; }
        PosizioneGiro Posizione { get; }
        StrutturaTavolo Struttura { get; }
        void AggiornaTavolo(StatoTavolo stato);
        void TickInterno();
        void Tap(long istante);
        void ImpostaBpm(double bpm);
        void ControllaTimeoutClock(long adesso);
        void Avvia();
        void Arresta();
        void Silenzia();
        void CambiaModo();
    }
}
