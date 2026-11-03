using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Snapshot e statistiche fuori dal thread MIDI; pubblicazione atomica del rapporto locale.</summary>
    public static class EsportatoreTempiMidi
    {
        public static string Salva(RegistroTempiMidi registro,string cartella,bool mmcss,string errore,long versioneOsservata,long versioneAttiva)
        {
            if(registro==null)throw new ArgumentNullException(nameof(registro));
            var campioni=registro.Istantanea();
            double unita=1000.0/Stopwatch.Frequency;
            var ritardi=campioni.Select(c=>Math.Max(0,c.InizioInvio-c.Previsto)*unita).OrderBy(x=>x).ToArray();
            Func<double,double> percentile=p=>ritardi.Length==0?0:ritardi[Math.Max(0,(int)Math.Ceiling(p*ritardi.Length)-1)];
            var rapporto=new
            {
                Formato="VMBL-tempi-MIDI-1",IstanteUtc=DateTime.UtcNow,FrequenzaQpc=Stopwatch.Frequency,
                MmcssAttiva=mmcss,Errore=errore,VersioneOsservata=versioneOsservata,VersioneAttiva=versioneAttiva,
                TotaleCampioni=registro.TotaleCampioni,
                RitardoInvioMs=new{P50=percentile(.50),P95=percentile(.95),P99=percentile(.99),Massimo=percentile(1)},
                DurataInvioMassimaMs=campioni.Count==0?0:campioni.Max(c=>(c.FineInvio-c.InizioInvio)*unita),Campioni=campioni
            };
            string finale=Path.Combine(cartella,"tempi-midi-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N")+".json");
            string temporaneo=finale+".tmp";
            try
            {
                File.WriteAllText(temporaneo,JsonConvert.SerializeObject(rapporto,Formatting.Indented),new UTF8Encoding(false));
                File.Move(temporaneo,finale);return finale;
            }
            finally { if(File.Exists(temporaneo))File.Delete(temporaneo); }
        }
    }
}
