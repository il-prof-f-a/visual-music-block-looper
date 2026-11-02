using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Pubblica il rapporto solo a scrittura conclusa. Nessun accesso al registro durante l'I/O.</summary>
    public static class EsportatoreDiagnosticaCartoncini
    {
        public static string Salva(RapportoDiagnosticaCartoncini rapporto, string cartella)
        {
            if (rapporto == null) throw new ArgumentNullException(nameof(rapporto));
            string nome = "debug-cartoncini-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture)
                + "-" + Guid.NewGuid().ToString("N");
            string finale = Path.Combine(cartella, nome + ".json");
            string temporaneo = Path.Combine(cartella, nome + ".tmp");
            try
            {
                using (var file = new FileStream(temporaneo, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var testo = new StreamWriter(file, new UTF8Encoding(false)))
                using (var json = new JsonTextWriter(testo) { Formatting = Formatting.Indented })
                {
                    JsonSerializer.Create(new JsonSerializerSettings
                    {
                        Culture = CultureInfo.InvariantCulture,
                        NullValueHandling = NullValueHandling.Ignore
                    }).Serialize(json, rapporto);
                }
                File.Move(temporaneo, finale);
                return finale;
            }
            finally
            {
                // Il temporaneo ha nome univoco; non rimuovere mai rapporti già pubblicati.
                try { if (File.Exists(temporaneo)) File.Delete(temporaneo); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
