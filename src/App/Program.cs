using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Midi;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    internal static class Program
    {
        private const string NomeMappatura = "mappatura-default.json";
        private const string NomeCalibrazione = "calibrazione.txt";

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string cartella = AppDomain.CurrentDomain.BaseDirectory;
            List<string> avvisi = new List<string>();
            List<IDisposable> risorse = new List<IDisposable>();

            ConfigGriglia config = new ConfigGriglia { Area = LayoutTavolo.AreaGrigliaDefault };
            Stopwatch cronometro = Stopwatch.StartNew();
            Func<long> orologioMs = () => cronometro.ElapsedMilliseconds;

            MappaturaMidiJson mappatura = CaricaMappatura(cartella, avvisi);
            ConfigurazioneMidi configMidi = mappatura.Configurazione;
            IUscitaMidi uscita = ApriUscita(configMidi.PortaUscita, avvisi, risorse);
            IIngressoMidi ingresso = ApriIngresso(configMidi.PortaIngresso, avvisi, risorse);

            string percorsoConfigurazione = Path.Combine(cartella, ConfigurazioneApplicazione.NomeFile);
            ConfigurazioneApplicazione configurazione = CaricaConfigurazione(percorsoConfigurazione, avvisi);

            // L'elenco si rileva prima di aprire la webcam configurata: una webcam aperta risulterebbe occupata.
            IReadOnlyList<InfoWebcam> elencoWebcam = ElencoWebcam.RilevaReali();
            GestoreWebcam webcam = new GestoreWebcam(indice => WebcamFonte.Apri(indice, configurazione.Webcam.Larghezza, configurazione.Webcam.Altezza));
            ApriWebcamConfigurata(webcam, elencoWebcam, configurazione.Webcam, avvisi);

            RilevatoreCommutabile rilevatore = new RilevatoreCommutabile();
            string percorsoCalibrazione = Path.Combine(cartella, NomeCalibrazione);
            bool calibrata = ApplicazioneCalibrazione.TentaCaricamento(
                percorsoCalibrazione, webcam, rilevatore, avvisi);

            Compositore compositore = new Compositore(
                rilevatore, uscita, ingresso, mappatura, config, LayoutTavolo.Default, orologioMs);
            FormProiettore proiettore = new FormProiettore(config, LayoutTavolo.Default);
            string testoPorte = $"MIDI uscita: {configMidi.PortaUscita}  |  ingresso: {configMidi.PortaIngresso}"
                + (avvisi.Exists(a => a.StartsWith("Porta MIDI")) ? "  (non disponibile)" : string.Empty);

            using (FormControllo controllo = new FormControllo(
                compositore, proiettore, webcam, rilevatore, percorsoCalibrazione,
                configurazione, percorsoConfigurazione, elencoWebcam,
                orologioMs, testoPorte, avvisi, risorse, calibrata))
            {
                Application.Run(controllo);
            }
        }

        private static MappaturaMidiJson CaricaMappatura(string cartella, List<string> avvisi)
        {
            try
            {
                return MappaturaMidiJson.Carica(Path.Combine(cartella, NomeMappatura));
            }
            catch (Exception eccezione) when (eccezione is IOException || eccezione is InvalidDataException || eccezione is ArgumentException)
            {
                avvisi.Add("Mappatura MIDI non caricata (" + eccezione.Message + "): uso i valori di default.");
                return new MappaturaMidiJson(new ConfigurazioneMidi());
            }
        }

        private static IUscitaMidi ApriUscita(string porta, List<string> avvisi, List<IDisposable> risorse)
        {
            try
            {
                UscitaMidiDryWet uscita = new UscitaMidiDryWet(porta);
                risorse.Add(uscita);
                return uscita;
            }
            catch (InvalidOperationException eccezione)
            {
                avvisi.Add("Porta MIDI di uscita non disponibile: " + eccezione.Message);
                return new UscitaMidiSilenziosa();
            }
        }

        private static IIngressoMidi ApriIngresso(string porta, List<string> avvisi, List<IDisposable> risorse)
        {
            try
            {
                IngressoMidiDryWet ingresso = new IngressoMidiDryWet(porta);
                risorse.Add(ingresso);
                return ingresso;
            }
            catch (InvalidOperationException eccezione)
            {
                avvisi.Add("Porta MIDI di ingresso non disponibile: " + eccezione.Message);
                return new IngressoMidiNullo();
            }
        }

        /// <summary>
        /// Apre la webcam indicata nel config cercandola per identità (percorso, poi nome) tra quelle
        /// presenti. Se non c'è, resta la modalità simulazione con un avviso: non si apre un'altra webcam.
        /// </summary>
        private static void ApriWebcamConfigurata(
            GestoreWebcam webcam, IReadOnlyList<InfoWebcam> elenco, ConfigurazioneWebcam salvata, List<string> avvisi)
        {
            string descrizione = !string.IsNullOrWhiteSpace(salvata.Nome)
                ? "«" + salvata.Nome + "»"
                : "con indice " + salvata.Indice;

            InfoWebcam trovata = AbbinamentoWebcam.Trova(elenco, salvata);
            if (trovata == null)
            {
                avvisi.Add("Webcam " + descrizione + " (da configurazione) non trovata: scegli una webcam dalla lista.");
                return;
            }

            if (!webcam.Apri(trovata))
            {
                avvisi.Add("Webcam " + descrizione + " (da configurazione) non apribile: scegli una webcam dalla lista.");
            }
        }

        private static ConfigurazioneApplicazione CaricaConfigurazione(string percorso, List<string> avvisi)
        {
            try
            {
                return ConfigurazioneApplicazione.Carica(percorso);
            }
            catch (Exception eccezione) when (eccezione is IOException || eccezione is InvalidDataException || eccezione is ArgumentException)
            {
                avvisi.Add("Configurazione non caricata (" + eccezione.Message + "): uso i valori di default.");
                return new ConfigurazioneApplicazione();
            }
        }
    }
}
