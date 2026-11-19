using System;
using System.IO;
using Newtonsoft.Json;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Midi
{
    /// <summary>Mappatura MIDI letta da una configurazione JSON.</summary>
    public class MappaturaMidiJson : IMappaturaMidi, IMappaturaCanaliRiga
    {
        private const int CanaleMinimo = 0;
        private const int CanaleMassimo = 15;
        private const int NotaMinima = 0;
        private const int NotaMassima = 127;
        private const int NotaBase = 36;
        private const int VelocityMinima = 1;
        private const int VelocityMassima = 127;

        private readonly ConfigurazioneMidi configurazione;

        // Override per riga: null = usa la mappatura normale della traccia.
        private readonly int?[] canaliRiga = new int?[StrutturaTavolo.RigheTotali];

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

        /// <summary>Configurazione da cui la mappatura è derivata (nomi delle porte compresi).</summary>
        public ConfigurazioneMidi Configurazione => configurazione;

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

        /// <summary>Imposta (o rimuove con -1) l'override di canale di una riga.</summary>
        public void ImpostaCanaleRiga(int riga, int canale)
        {
            if (riga < 0 || riga >= canaliRiga.Length)
            {
                return;
            }

            canaliRiga[riga] = canale < 0 ? (int?)null : Limita(canale, CanaleMinimo, CanaleMassimo);
        }

        /// <summary>Override corrente per riga: -1 dove la riga usa la mappatura normale.</summary>
        public int[] CanaliRiga()
        {
            int[] copia = new int[canaliRiga.Length];
            for (int i = 0; i < canaliRiga.Length; i++)
            {
                copia[i] = canaliRiga[i] ?? -1;
            }

            return copia;
        }

        /// <inheritdoc />
        public bool TryCanaleRiga(int riga, out int canale)
        {
            canale = -1;
            if (riga < 0 || riga >= canaliRiga.Length || !canaliRiga[riga].HasValue)
            {
                return false;
            }

            canale = canaliRiga[riga].Value;
            return true;
        }

        private static int Limita(int valore, int minimo, int massimo)
        {
            return Math.Max(minimo, Math.Min(massimo, valore));
        }
    }
}
