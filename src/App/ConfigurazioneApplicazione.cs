using System;
using System.IO;
using Newtonsoft.Json;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Impostazioni della webcam salvate nel file di configurazione dell'applicazione.</summary>
    public class ConfigurazioneWebcam
    {
        /// <summary>Nome della webcam come lo vede Windows. È l'identità principale: ordine e numero possono cambiare.</summary>
        public string Nome { get; set; }

        /// <summary>Percorso univoco del dispositivo: distingue due webcam dello stesso modello.</summary>
        public string Percorso { get; set; }

        /// <summary>
        /// Indice di sistema all'ultimo salvataggio. Si usa solo se nel file non ci sono nome né percorso
        /// (configurazione di una versione precedente) o per informazione.
        /// </summary>
        public int Indice { get; set; }

        /// <summary>
        /// Larghezza di cattura richiesta in pixel (0 = predefinita, 1280). Più pixel rendono leggibili QR piccoli;
        /// cambiando risoluzione la calibrazione va rifatta.
        /// </summary>
        public int Larghezza { get; set; }

        /// <summary>Altezza di cattura richiesta in pixel (0 = predefinita, 720).</summary>
        public int Altezza { get; set; }

        /// <summary>
        /// Messa a fuoco automatica. Null = non modificare il valore impostato nel driver della webcam.
        /// </summary>
        public bool? FuocoAutomatico { get; set; }

        /// <summary>
        /// Posizione del fuoco manuale (0..255). Null = non modificare il valore impostato nel driver della webcam.
        /// </summary>
        public int? Fuoco { get; set; }

        /// <summary>
        /// Esposizione automatica. Null = non modificare il valore impostato nel driver della webcam.
        /// </summary>
        public bool? EsposizioneAutomatica { get; set; }

        /// <summary>
        /// Esposizione manuale come log2 dei secondi (tipicamente -13..0, come in DirectShow). Null = non modificare
        /// il valore impostato nel driver della webcam.
        /// </summary>
        public double? Esposizione { get; set; }

        /// <summary>Copia nella configurazione l'identità della webcam in uso.</summary>
        public void Imposta(InfoWebcam webcam)
        {
            if (webcam == null)
            {
                throw new ArgumentNullException(nameof(webcam));
            }

            Nome = webcam.Nome;
            Percorso = webcam.Percorso;
            Indice = webcam.Indice;
        }

        /// <summary>True se la webcam data è quella salvata (stesse regole di <see cref="AbbinamentoWebcam"/>).</summary>
        public bool Corrisponde(InfoWebcam webcam)
        {
            if (webcam == null)
            {
                return false;
            }

            return AbbinamentoWebcam.Trova(new[] { webcam }, this) != null;
        }
    }

    /// <summary>
    /// Configurazione dell'applicazione (file JSON accanto all'eseguibile). Oggi contiene la webcam; viene
    /// riscritta dopo ogni calibrazione riuscita e riletta all'avvio insieme alla calibrazione.
    /// </summary>
    public class ConfigurazioneApplicazione
    {
        /// <summary>Nome del file di configurazione.</summary>
        public const string NomeFile = "configurazione.json";

        /// <summary>Webcam da usare.</summary>
        public ConfigurazioneWebcam Webcam { get; set; } = new ConfigurazioneWebcam();

        /// <summary>0: una osservazione per misura; altrimenti intervallo in pulsazioni.</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int PulsazioniPerOsservazione { get; set; }

        /// <summary>BPM Master iniziale, salvato anche dopo modifiche manuali o Tap Tempo (default applicativo 100).</summary>
        public double BpmIniziale { get; set; } = 100;

        /// <summary>Se false legge tutta la scena a ogni osservazione; se true usa blocchi anticipati o differenze, secondo la modalità selezionata.</summary>
        public bool AnalizzaSoloSeCambia { get; set; } = true;

        /// <summary>Con AnalizzaSoloSeCambia attiva, legge blocchi anticipati durante il play e, a riproduzione ferma, una zona per fotogramma.</summary>
        public bool ScansioneABlocchiAnticipata { get; set; } = true;

        /// <summary>Misure future incluse nella finestra di scansione, limitate a 1..5 (default 2 per confermare prima dell'attacco).</summary>
        public int MisureAnticipoScansione { get; set; } = 2;

        /// <summary>Scarto massimo del QR dai riferimenti della griglia, come frazione di cella (0..0,49).</summary>
        public double TolleranzaQrCelle { get; set; } = 0.25;

        /// <summary>Massimo di worker OpenCV paralleli; riduce la contesa CPU con il thread MIDI.</summary>
        public int NumeroThreadOpenCv { get; set; } = 2;

        /// <summary>Soglie e dimensioni del rilevatore delle differenze, modificabili nel file JSON.</summary>
        public ParametriRilevatoreVariazioni AnalisiVariazioni { get; set; } = new ParametriRilevatoreVariazioni();

        /// <summary>
        /// Carica la configurazione. Se il file non esiste restituisce i valori di default; se non è un
        /// JSON valido lancia <see cref="InvalidDataException"/>.
        /// </summary>
        public static ConfigurazioneApplicazione Carica(string percorso)
        {
            if (string.IsNullOrWhiteSpace(percorso))
            {
                throw new ArgumentException("Il percorso della configurazione non può essere vuoto.", nameof(percorso));
            }

            if (!File.Exists(percorso))
            {
                return new ConfigurazioneApplicazione();
            }

            ConfigurazioneApplicazione configurazione;
            try
            {
                configurazione = JsonConvert.DeserializeObject<ConfigurazioneApplicazione>(File.ReadAllText(percorso));
            }
            catch (JsonException eccezione)
            {
                throw new InvalidDataException(
                    "Il file di configurazione non è un JSON valido: " + percorso, eccezione);
            }

            if (configurazione == null)
            {
                return new ConfigurazioneApplicazione();
            }

            if (configurazione.Webcam == null)
            {
                configurazione.Webcam = new ConfigurazioneWebcam();
            }

            if(configurazione.AnalisiVariazioni==null)
                configurazione.AnalisiVariazioni=new ParametriRilevatoreVariazioni();
            configurazione.AnalisiVariazioni=configurazione.AnalisiVariazioni.Normalizzati();
            configurazione.BpmIniziale=double.IsNaN(configurazione.BpmIniziale)||double.IsInfinity(configurazione.BpmIniziale)
                ?100:Tempo.Limita(configurazione.BpmIniziale);
            if(configurazione.MisureAnticipoScansione<1||configurazione.MisureAnticipoScansione>5)
                configurazione.MisureAnticipoScansione=2;
            if(double.IsNaN(configurazione.TolleranzaQrCelle)||double.IsInfinity(configurazione.TolleranzaQrCelle)
                ||configurazione.TolleranzaQrCelle<0||configurazione.TolleranzaQrCelle>=0.5)
                configurazione.TolleranzaQrCelle=0.25;
            if(configurazione.NumeroThreadOpenCv<1||configurazione.NumeroThreadOpenCv>4)
                configurazione.NumeroThreadOpenCv=2;

            if (configurazione.Webcam.Indice < 0)
            {
                configurazione.Webcam.Indice = 0;
            }

            return configurazione;
        }

        /// <summary>Scrive la configurazione su file (JSON indentato).</summary>
        public void Salva(string percorso)
        {
            if (string.IsNullOrWhiteSpace(percorso))
            {
                throw new ArgumentException("Il percorso della configurazione non può essere vuoto.", nameof(percorso));
            }

            File.WriteAllText(percorso, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
