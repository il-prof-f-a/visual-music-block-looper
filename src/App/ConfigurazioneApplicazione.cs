using System;
using System.IO;
using Newtonsoft.Json;

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
