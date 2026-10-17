using System;
using System.IO;
using Newtonsoft.Json;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Midi
{
    /// <summary>Mappatura MIDI letta da una configurazione JSON.</summary>
    public class MappaturaMidiJson : IMappaturaMidi
    {
        private const int CanaleMinimo = 0;
        private const int CanaleMassimo = 15;
        private const int NotaMinima = 0;
        private const int NotaMassima = 127;
        private const int NotaBase = 36;
        private const int VelocityMinima = 1;
        private const int VelocityMassima = 127;

        private readonly ConfigurazioneMidi configurazione;

        /// <summary>Crea la mappatura a partire da una configurazione già in memoria.</summary>
        /// <param name="configurazione">Configurazione da cui leggere canali, note e velocity.</param>
        public MappaturaMidiJson(ConfigurazioneMidi configurazione)
        {
            if (configurazione == null)
            {
                throw new ArgumentNullException(nameof(configurazione));
            }

            this.configurazione = configurazione;
        }

        /// <summary>Carica la mappatura da un file JSON.</summary>
        /// <param name="percorso">Percorso del file di configurazione.</param>
        public static MappaturaMidiJson Carica(string percorso)
        {
            if (string.IsNullOrWhiteSpace(percorso))
            {
                throw new ArgumentException("Il percorso della configurazione MIDI non può essere vuoto.", nameof(percorso));
            }

            if (!File.Exists(percorso))
            {
                throw new FileNotFoundException(
                    "File di configurazione MIDI non trovato: " + percorso, percorso);
            }

            ConfigurazioneMidi configurazione;
            try
            {
                string contenuto = File.ReadAllText(percorso);
                configurazione = JsonConvert.DeserializeObject<ConfigurazioneMidi>(contenuto);
            }
            catch (JsonException eccezione)
            {
                throw new InvalidDataException(
                    "Il file di configurazione MIDI non è un JSON valido: " + percorso, eccezione);
            }

            if (configurazione == null)
            {
                throw new InvalidDataException(
                    "Il file di configurazione MIDI è vuoto o non contiene una configurazione: " + percorso);
            }

            return new MappaturaMidiJson(configurazione);
        }

        /// <inheritdoc />
        public int Canale(int traccia)
        {
            int[] canali = configurazione.CanalePerTraccia;
            int canale = canali != null && traccia >= 0 && traccia < canali.Length
                ? canali[traccia]
                : traccia;

            return Limita(canale, CanaleMinimo, CanaleMassimo);
        }

        /// <inheritdoc />
        public int Nota(int idClip)
        {
            int nota;
            if (configurazione.NotaPerClip == null || !configurazione.NotaPerClip.TryGetValue(idClip, out nota))
            {
                nota = NotaBase + idClip;
            }

            return Limita(nota, NotaMinima, NotaMassima);
        }

        /// <inheritdoc />
        public int Velocity => Limita(configurazione.Velocity, VelocityMinima, VelocityMassima);

        private static int Limita(int valore, int minimo, int massimo)
        {
            return Math.Max(minimo, Math.Min(massimo, valore));
        }
    }
}
