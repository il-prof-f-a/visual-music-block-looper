namespace VisualMusicBlockLooper.Core
{
    /// <summary>Cartoncino individuato dalla visione: payload grezzo, colore e posizione sul tavolo.</summary>
    public class CartoncinoRilevato
    {
        /// <summary>Testo del payload QR letto dal cartoncino.</summary>
        public string Payload { get; set; }

        /// <summary>Colore rilevato, se disponibile: informativo, non usato dal motore.</summary>
        public ColoreTraccia? Colore { get; set; }

        /// <summary>Area occupata dal cartoncino sul tavolo.</summary>
        public RettangoloTavolo Area { get; set; }

        /// <summary>Quadrilatero QR riportato al tavolo, senza zona bianca o ingombro del corpo.</summary>
        public RettangoloTavolo? AreaQr { get; set; }

        /// <summary>Fine dell'estensione contigua verso destra nel tavolo; null per fonti precedenti.</summary>
        public double? FineEstensioneX { get; set; }
    }
}
