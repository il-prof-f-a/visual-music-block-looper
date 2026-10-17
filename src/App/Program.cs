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

            ConfigGriglia config = new ConfigGriglia { Area = new RettangoloTavolo(0.02, 0.04, 0.80, 0.92) };
            Stopwatch cronometro = Stopwatch.StartNew();
            Func<long> orologioMs = () => cronometro.ElapsedMilliseconds;

            MappaturaMidiJson mappatura = CaricaMappatura(cartella, avvisi);
            ConfigurazioneMidi configMidi = mappatura.Configurazione;
            IUscitaMidi uscita = ApriUscita(configMidi.PortaUscita, avvisi, risorse);
            IIngressoMidi ingresso = ApriIngresso(configMidi.PortaIngresso, avvisi, risorse);

            WebcamFonte webcam = WebcamFonte.Apri(0);
            RilevatoreCommutabile rilevatore = new RilevatoreCommutabile();
            string percorsoCalibrazione = Path.Combine(cartella, NomeCalibrazione);
            bool calibrata = CaricaCalibrazione(webcam, rilevatore, percorsoCalibrazione, avvisi);

            Compositore compositore = new Compositore(
                rilevatore, uscita, ingresso, mappatura, config, null, orologioMs);
            FormProiettore proiettore = new FormProiettore(config);
            string testoPorte = $"MIDI uscita: {configMidi.PortaUscita}  |  ingresso: {configMidi.PortaIngresso}"
                + (avvisi.Exists(a => a.StartsWith("Porta MIDI")) ? "  (non disponibile)" : string.Empty);

            using (FormControllo controllo = new FormControllo(
                compositore, proiettore, webcam, rilevatore, percorsoCalibrazione,
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

        private static bool CaricaCalibrazione(
            WebcamFonte webcam, RilevatoreCommutabile rilevatore, string percorso, List<string> avvisi)
        {
            if (webcam == null || !File.Exists(percorso))
            {
                return false;
            }

            try
            {
                rilevatore.Imposta(new RilevatoreCartoncini(webcam, Calibrazione.Carica(percorso)));
                return true;
            }
            catch (Exception eccezione)
            {
                avvisi.Add("Calibrazione salvata non valida (" + eccezione.Message + "): ripetere la calibrazione.");
                return false;
            }
        }
    }
}
