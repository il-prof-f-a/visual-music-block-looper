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

    /// <summary>DTO JSON della correzione di bordo; la logica vive in <see cref="ParametriCorrezioneBordo"/>.</summary>
    public class ConfigurazioneBordo
    {
        /// <summary>«Bordo» (smoothstep) oppure «BordoSmussato» (softmin + anisotropia).</summary>
        public string Modello { get; set; } = "Bordo";

        /// <summary>Distanza dal bordo entro cui vale il TPS al 100% (default 0,04).</summary>
        public double Pieno { get; set; } = 0.04;

        /// <summary>Distanza dal bordo oltre cui vale la sola omografia (default 0,22).</summary>
        public double Esterno { get; set; } = 0.22;

        /// <summary>Fattore di anisotropia orizzontale per la modalità smussata (default 1).</summary>
        public double FattoreX { get; set; } = 1.0;

        /// <summary>Fattore di anisotropia verticale per la modalità smussata (default 1).</summary>
        public double FattoreY { get; set; } = 1.0;

        /// <summary>Intensità della softmin; 0 usa il minimo netto (default 0).</summary>
        public double Smussamento { get; set; } = 0.0;

        /// <summary>«Equisolido», «Equidistante» o «Stereografico» (usato con Modello = Sferico).</summary>
        public string TipoSferico { get; set; } = "Equisolido";

        /// <summary>Focale in pixel; 0 = stimata dai dodici punti.</summary>
        public double Focale { get; set; } = 0.0;

        /// <summary>Centro ottico X in pixel; 0 = centro dell'immagine.</summary>
        public double CentroX { get; set; } = 0.0;

        /// <summary>Centro ottico Y in pixel; 0 = centro dell'immagine.</summary>
        public double CentroY { get; set; } = 0.0;

        public ParametriCorrezioneBordo Converti()
        {
            var parametri = new ParametriCorrezioneBordo
            {
                Modello = string.Equals(Modello, nameof(ModelloCorrezioneBordo.Sferico), StringComparison.OrdinalIgnoreCase)
                    ? ModelloCorrezioneBordo.Sferico
                    : string.Equals(Modello, nameof(ModelloCorrezioneBordo.BordoSmussato), StringComparison.OrdinalIgnoreCase)
                        ? ModelloCorrezioneBordo.BordoSmussato
                        : ModelloCorrezioneBordo.Bordo,
                Pieno = Pieno,
                Esterno = Esterno,
                FattoreX = FattoreX,
                FattoreY = FattoreY,
                Smussamento = Smussamento,
                TipoSferico = string.Equals(TipoSferico, nameof(TipoProiezioneSferica.Equidistante), StringComparison.OrdinalIgnoreCase)
                    ? TipoProiezioneSferica.Equidistante
                    : string.Equals(TipoSferico, nameof(TipoProiezioneSferica.Stereografico), StringComparison.OrdinalIgnoreCase)
                        ? TipoProiezioneSferica.Stereografico
                        : TipoProiezioneSferica.Equisolido,
                Focale = Focale,
                CentroX = CentroX,
                CentroY = CentroY
            };
            return parametri.Normalizzati();
        }

        public void Imposta(ParametriCorrezioneBordo parametri)
        {
            ParametriCorrezioneBordo p = (parametri ?? new ParametriCorrezioneBordo()).Clona().Normalizzati();
            Modello = p.Modello.ToString();
            Pieno = p.Pieno;
            Esterno = p.Esterno;
            FattoreX = p.FattoreX;
            FattoreY = p.FattoreY;
            Smussamento = p.Smussamento;
            TipoSferico = p.TipoSferico.ToString();
            Focale = p.Focale;
            CentroX = p.CentroX;
            CentroY = p.CentroY;
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

        /// <summary>Fascia di transizione della correzione di bordo (CAL3).</summary>
        public ConfigurazioneBordo CorrezioneBordo { get; set; } = new ConfigurazioneBordo();

        /// <summary>0: una osservazione per misura; altrimenti intervallo in pulsazioni.</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int PulsazioniPerOsservazione { get; set; }

        /// <summary>BPM Master iniziale, salvato anche dopo modifiche manuali o Tap Tempo (default applicativo 100).</summary>
        public double BpmIniziale { get; set; } = 100;

        /// <summary>Se false legge tutta la scena a ogni osservazione; se true usa blocchi anticipati o differenze, secondo la modalità selezionata.</summary>
        public bool AnalizzaSoloSeCambia { get; set; } = true;

        /// <summary>Con AnalizzaSoloSeCambia attiva, legge blocchi anticipati durante il play e, a riproduzione ferma, una zona per fotogramma.</summary>
        public bool ScansioneABlocchiAnticipata { get; set; } = true;

        public static bool AnticipoScansioneValido(int valore)=>valore==1||valore==2||valore==4||valore==8||valore==16;
        /// <summary>Anticipo live dell’app in celle: 1, 2, 4, 8 o 16 (default 4). Nome JSON storico conservato.</summary>
        public int MisureAnticipoScansione { get; set; } = 4;

        /// <summary>Scarto massimo del QR dai riferimenti della griglia, come frazione di cella (0..0,49).</summary>
        public double TolleranzaQrCelle { get; set; } = 0.35;

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

            if (configurazione.CorrezioneBordo == null)
            {
                configurazione.CorrezioneBordo = new ConfigurazioneBordo();
            }
            configurazione.CorrezioneBordo.Imposta(configurazione.CorrezioneBordo.Converti());

            if(configurazione.AnalisiVariazioni==null)
                configurazione.AnalisiVariazioni=new ParametriRilevatoreVariazioni();
            configurazione.AnalisiVariazioni=configurazione.AnalisiVariazioni.Normalizzati();
            configurazione.BpmIniziale=double.IsNaN(configurazione.BpmIniziale)||double.IsInfinity(configurazione.BpmIniziale)
                ?100:Tempo.Limita(configurazione.BpmIniziale);
            if(!AnticipoScansioneValido(configurazione.MisureAnticipoScansione))
                configurazione.MisureAnticipoScansione=4;
            if(double.IsNaN(configurazione.TolleranzaQrCelle)||double.IsInfinity(configurazione.TolleranzaQrCelle)
                ||configurazione.TolleranzaQrCelle<0||configurazione.TolleranzaQrCelle>=0.5)
                configurazione.TolleranzaQrCelle=0.35;
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
