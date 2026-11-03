using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.App;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class EsportazioneTempiMidiTests
    {
        [Test]
        public void RapportoContieneUnitaQpcCampioniEVersioniENonLasciaTemporanei()
        {
            string dir=Path.Combine(Path.GetTempPath(),"vmbl-tempi-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            string file=null;
            try
            {
                var r=new RegistroTempiMidi(2);r.Registra(1,100,101,102);r.Registra(2,200,202,204);
                file=EsportatoreTempiMidi.Salva(r,dir,true,null,8,7);
                var j=JObject.Parse(File.ReadAllText(file));
                Assert.That((long)j["FrequenzaQpc"],Is.GreaterThan(0));
                Assert.That(((JArray)j["Campioni"]).Count,Is.EqualTo(2));
                Assert.That((long)j["VersioneOsservata"],Is.EqualTo(8));
                Assert.That((long)j["VersioneAttiva"],Is.EqualTo(7));
                Assert.That(Directory.GetFiles(dir,"*.tmp"),Is.Empty);
            }
            finally{if(file!=null&&File.Exists(file))File.Delete(file);Directory.Delete(dir);}
        }
    }
}
