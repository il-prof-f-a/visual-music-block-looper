using System.Collections.Generic;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>Contratto opzionale: osserva lo stesso rilevamento senza acquisire un secondo frame.</summary>
    public interface IRilevatoreDiagnostico : IRilevatoreCartoncini
    {
        IReadOnlyList<CartoncinoRilevato> Rileva(TracciaCartoncini traccia);
    }

    /// <summary>Evento immutabile; le aree camera sono in pixel, quelle tavolo normalizzate.</summary>
    public sealed class EventoDiagnosticoCartoncino
    {
        public EventoDiagnosticoCartoncino(string fase, string esito, string codice, string dettaglio,
            string payload = null, RettangoloTavolo? areaCamera = null, RettangoloTavolo? areaTavolo = null,
            string zona = null, string origine = null)
        {
            Fase = fase; Esito = esito; Codice = codice; Dettaglio = dettaglio;
            Payload = payload; AreaCamera = areaCamera; AreaTavolo = areaTavolo; Zona = zona; Origine = origine;
        }

        public string Fase { get; }
        public string Esito { get; }
        public string Codice { get; }
        public string Dettaglio { get; }
        public string Payload { get; }
        public RettangoloTavolo? AreaCamera { get; }
        public RettangoloTavolo? AreaTavolo { get; }
        public string Zona { get; }
        public string Origine { get; }
        public bool? Accettato => Fase == "Interpretazione" ? (bool?)(Esito == "Accettato") : null;
    }

    /// <summary>Contesto di una singola cattura, posseduto dal solo thread della pipeline.</summary>
    public sealed class TracciaCartoncini
    {
        private readonly Dictionary<PayloadCartoncino, CartoncinoRilevato> originali
            = new Dictionary<PayloadCartoncino, CartoncinoRilevato>();
        public string EsitoVisione { get; set; } = "NonEseguita";
        public int LarghezzaFrame { get; set; }
        public int AltezzaFrame { get; set; }
        public bool? CalibrazionePresente { get; set; }
        public LayoutTavolo Layout { get; set; }
        public List<EventoDiagnosticoCartoncino> Visione { get; } = new List<EventoDiagnosticoCartoncino>();
        public List<EventoDiagnosticoCartoncino> Interpretazione { get; } = new List<EventoDiagnosticoCartoncino>();
        public StrutturaTavolo StrutturaGrezza { get; set; }
        public StrutturaTavolo StrutturaStabile { get; set; }
        public StrutturaTavolo StrutturaInConferma { get; set; }
        public int NoteGrezze { get; set; }
        public int ComandiGrezzi { get; set; }
        public int NoteStabili { get; set; }
        public int ComandiEmessi { get; set; }
        public int ConfermeStruttura { get; set; }
        public int ConfermeRichieste { get; set; }
        public string EsitoStabilizzazione { get; set; } = "NonEseguita";
        public string Errore { get; set; }

        internal void Associa(PayloadCartoncino payload, CartoncinoRilevato cartoncino)
        {
            originali[payload] = cartoncino;
        }

        internal void RegistraImpostazione(PayloadCartoncino payload, RettangoloTavolo area,
            ZonaSpeciale zona, bool accettato, string codice, string dettaglio)
        {
            CartoncinoRilevato cartoncino;
            if (payload == null || !originali.TryGetValue(payload, out cartoncino))
                cartoncino = new CartoncinoRilevato { Area = area };
            RegistraInterpretazione(cartoncino, zona, accettato, codice, dettaglio);
        }

        public void RegistraInterpretazione(CartoncinoRilevato cartoncino, ZonaSpeciale zona,
            bool accettato, string codice, string dettaglio)
        {
            Interpretazione.Add(new EventoDiagnosticoCartoncino("Interpretazione",
                accettato ? "Accettato" : "Scartato", codice, dettaglio, cartoncino.Payload,
                areaTavolo: cartoncino.Area, zona: zona.ToString()));
        }
    }
}
