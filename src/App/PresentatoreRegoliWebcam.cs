using System;
using System.Globalization;

namespace VisualMusicBlockLooper.App
{
    /// <summary>
    /// Logica pura dei regoli della webcam (risoluzione, fuoco, esposizione) e delle etichette:
    /// conversioni regolo/valore, configurazioni parziali da inviare alla webcam, fusione nella
    /// configurazione da salvare e testi mostrati. Nessuna dipendenza da WinForms.
    /// </summary>
    public static class PresentatoreRegoliWebcam
    {
        /// <summary>Posizione massima del regolo del fuoco (255 / passo 5).</summary>
        public const int RegoloFuocoMax = ImpostazioniCamera.FuocoMax / ImpostazioniCamera.FuocoPasso;

        /// <summary>Posizione minima del regolo dell'esposizione (mezzi passi: -13 * 2).</summary>
        public const int RegoloEsposizioneMin = ImpostazioniCamera.EsposizioneMin * 2;

        /// <summary>Posizione massima del regolo dell'esposizione.</summary>
        public const int RegoloEsposizioneMax = ImpostazioniCamera.EsposizioneMax * 2;

        /// <summary>Esposizione di partenza del regolo quando né il file né la webcam ne indicano una.</summary>
        private const double EsposizioneNeutra = -6;

        private static readonly CultureInfo Italiano = CultureInfo.GetCultureInfo("it-IT");

        // ---------- Risoluzione ----------

        /// <summary>Larghezza e altezza del preset indicato dal regolo (indice limitato all'elenco).</summary>
        public static (int Larghezza, int Altezza) RisoluzioneDaRegolo(int indice)
        {
            int limitato = Math.Min(PresetRisoluzione.Elenco.Count - 1, Math.Max(0, indice));
            return PresetRisoluzione.Elenco[limitato];
        }

        /// <summary>
        /// Posizione iniziale del regolo della risoluzione: quella del file di configurazione se presente,
        /// altrimenti quella reale della webcam; il preset è il più vicino (1280x720 se entrambe ignote).
        /// </summary>
        public static int RegoloRisoluzione(int larghezzaConfig, int altezzaConfig, int larghezzaWebcam, int altezzaWebcam)
        {
            if (larghezzaConfig > 0 && altezzaConfig > 0)
            {
                return PresetRisoluzione.IndiceVicino(larghezzaConfig, altezzaConfig);
            }

            return PresetRisoluzione.IndiceVicino(larghezzaWebcam, altezzaWebcam);
        }

        /// <summary>Etichetta del regolo, es. «Risoluzione: 1280x720».</summary>
        public static string EtichettaRisoluzione(int indice)
        {
            int limitato = Math.Min(PresetRisoluzione.Elenco.Count - 1, Math.Max(0, indice));
            return "Risoluzione: " + PresetRisoluzione.Etichetta(limitato);
        }

        // ---------- Fuoco ----------

        /// <summary>Valore del fuoco (0..255, a passi di 5) per la posizione del regolo.</summary>
        public static int FuocoDaRegolo(int regolo)
        {
            int limitato = Math.Min(RegoloFuocoMax, Math.Max(0, regolo));
            return ImpostazioniCamera.LimitaFuoco(limitato * ImpostazioniCamera.FuocoPasso);
        }

        /// <summary>Posizione del regolo (0..51) più vicina al valore del fuoco.</summary>
        public static int RegoloDaFuoco(int fuoco)
        {
            int limitato = ImpostazioniCamera.LimitaFuoco(fuoco);
            return (int)Math.Round(limitato / (double)ImpostazioniCamera.FuocoPasso, MidpointRounding.AwayFromZero);
        }

        // ---------- Esposizione ----------

        /// <summary>Esposizione in log2 dei secondi (-13..0, a mezzi passi) per la posizione del regolo.</summary>
        public static double EsposizioneDaRegolo(int regolo)
        {
            int limitato = Math.Min(RegoloEsposizioneMax, Math.Max(RegoloEsposizioneMin, regolo));
            return limitato / 2.0;
        }

        /// <summary>Posizione del regolo (-26..0) più vicina all'esposizione.</summary>
        public static int RegoloDaEsposizione(double esposizione)
        {
            double limitata = ImpostazioniCamera.LimitaEsposizione(esposizione);
            return (int)Math.Round(limitata * 2, MidpointRounding.AwayFromZero);
        }

        // ---------- Posizioni iniziali ----------

        /// <summary>Casella «Auto» del fuoco: dal file, altrimenti dalla webcam, altrimenti automatico.</summary>
        public static bool FuocoAutomaticoIniziale(ConfigurazioneWebcam config, StatoImpostazioniWebcam stato)
        {
            return config?.FuocoAutomatico ?? stato?.FuocoAutomatico ?? true;
        }

        /// <summary>Regolo del fuoco: dal file, altrimenti dal valore letto dalla webcam, altrimenti 0.</summary>
        public static int RegoloFuocoIniziale(ConfigurazioneWebcam config, StatoImpostazioniWebcam stato)
        {
            return RegoloDaFuoco(config?.Fuoco ?? stato?.Fuoco ?? 0);
        }

        /// <summary>Casella «Auto» dell'esposizione: dal file, altrimenti dalla webcam, altrimenti automatica.</summary>
        public static bool EsposizioneAutomaticaIniziale(ConfigurazioneWebcam config, StatoImpostazioniWebcam stato)
        {
            return config?.EsposizioneAutomatica ?? stato?.EsposizioneAutomatica ?? true;
        }

        /// <summary>Regolo dell'esposizione: dal file, altrimenti dalla webcam, altrimenti un valore neutro (-6).</summary>
        public static int RegoloEsposizioneIniziale(ConfigurazioneWebcam config, StatoImpostazioniWebcam stato)
        {
            return RegoloDaEsposizione(config?.Esposizione ?? stato?.Esposizione ?? EsposizioneNeutra);
        }

        // ---------- Configurazioni parziali e fusione ----------

        /// <summary>Configurazione parziale con la sola risoluzione del preset indicato.</summary>
        public static ConfigurazioneWebcam ParzialeRisoluzione(int indice)
        {
            (int larghezza, int altezza) = RisoluzioneDaRegolo(indice);
            return new ConfigurazioneWebcam { Larghezza = larghezza, Altezza = altezza };
        }

        /// <summary>
        /// Configurazione parziale del fuoco da inviare alla webcam: con «Auto» attiva solo il flag,
        /// altrimenti il flag spento e il valore del regolo.
        /// </summary>
        public static ConfigurazioneWebcam ParzialeFuoco(bool automatico, int regolo)
        {
            return new ConfigurazioneWebcam
            {
                FuocoAutomatico = automatico,
                Fuoco = automatico ? (int?)null : FuocoDaRegolo(regolo)
            };
        }

        /// <summary>
        /// Configurazione parziale dell'esposizione da inviare alla webcam: con «Auto» attiva solo il flag,
        /// altrimenti il flag spento e il valore del regolo.
        /// </summary>
        public static ConfigurazioneWebcam ParzialeEsposizione(bool automatica, int regolo)
        {
            return new ConfigurazioneWebcam
            {
                EsposizioneAutomatica = automatica,
                Esposizione = automatica ? (double?)null : EsposizioneDaRegolo(regolo)
            };
        }

        /// <summary>
        /// Fonde in <paramref name="destinazione"/> i soli campi presenti in <paramref name="parziale"/>
        /// (fuoco ed esposizione se non null; risoluzione se entrambe le dimensioni sono positive).
        /// Non tocca mai l'identità della webcam.
        /// </summary>
        public static void Unisci(ConfigurazioneWebcam destinazione, ConfigurazioneWebcam parziale)
        {
            if (destinazione == null)
            {
                throw new ArgumentNullException(nameof(destinazione));
            }

            if (parziale == null)
            {
                return;
            }

            if (parziale.Larghezza > 0 && parziale.Altezza > 0)
            {
                destinazione.Larghezza = parziale.Larghezza;
                destinazione.Altezza = parziale.Altezza;
            }

            destinazione.FuocoAutomatico = parziale.FuocoAutomatico ?? destinazione.FuocoAutomatico;
            destinazione.Fuoco = parziale.Fuoco ?? destinazione.Fuoco;
            destinazione.EsposizioneAutomatica = parziale.EsposizioneAutomatica ?? destinazione.EsposizioneAutomatica;
            destinazione.Esposizione = parziale.Esposizione ?? destinazione.Esposizione;
        }

        // ---------- Etichette ----------

        /// <summary>«Fuoco: automatico», «Fuoco: 120» oppure «Fuoco: non supportato».</summary>
        public static string EtichettaFuoco(bool supportato, bool automatico, int regolo)
        {
            if (!supportato)
            {
                return "Fuoco: non supportato";
            }

            return automatico
                ? "Fuoco: automatico"
                : "Fuoco: " + ImpostazioniCamera.EtichettaFuoco(FuocoDaRegolo(regolo));
        }

        /// <summary>«Esposizione: automatica», «Esposizione: 7,8 ms» oppure «Esposizione: non supportata».</summary>
        public static string EtichettaEsposizione(bool supportata, bool automatica, int regolo)
        {
            if (!supportata)
            {
                return "Esposizione: non supportata";
            }

            return automatica
                ? "Esposizione: automatica"
                : "Esposizione: " + ImpostazioniCamera.EtichettaEsposizione(EsposizioneDaRegolo(regolo));
        }

        /// <summary>
        /// Testo dei frame al secondo, es. «Webcam: 29,8 fps (dichiarati 30) | Elaborazione: 14,9 fps | 1280x720».
        /// Senza webcam (dimensioni non positive) restituisce «Webcam non attiva»; con fps dichiarati ignoti «dichiarati n/d».
        /// </summary>
        public static string TestoFps(double fpsWebcam, double fpsDichiarati, double fpsElaborazione, int larghezza, int altezza)
        {
            if (larghezza <= 0 || altezza <= 0)
            {
                return "Webcam non attiva";
            }

            string dichiarati = fpsDichiarati > 0
                ? fpsDichiarati.ToString("0.##", Italiano)
                : "n/d";
            return string.Format(
                CultureInfo.InvariantCulture,
                "Webcam: {0} fps (dichiarati {1}) | Elaborazione: {2} fps | {3}x{4}",
                fpsWebcam.ToString("0.0", Italiano),
                dichiarati,
                fpsElaborazione.ToString("0.0", Italiano),
                larghezza,
                altezza);
        }
    }
}
