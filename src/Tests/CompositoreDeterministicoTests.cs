using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class CompositoreDeterministicoTests
    {
        private sealed class Fonte : IRilevatoreCartoncini
        {
            public IReadOnlyList<CartoncinoRilevato> Rileva()=>new[]{new CartoncinoRilevato
            {Payload="VMBL1;N;40",Area=new RettangoloTavolo(.16,.06,.03,.06),AreaQr=new RettangoloTavolo(.16,.06,.02,.04),FineEstensioneX=.19}};
        }
        [Test]
        public void RuntimeNuovoPreparaSenzaInviareFincheNonPassaIlProprietarioMidi()
        {
            var u=new EsecutorePartituraTests.Uscita();
            using(var c=new Compositore(new Fonte(),u,null,new EsecutorePartituraTests.Mappa(),
                new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault},LayoutTavolo.Default,()=>0,esecuzioneDeterministica:true))
            {
                Assert.That(c.Motore,Is.TypeOf<EsecutoreMidi>());
                c.Passo();c.Passo();c.Passo();c.Motore.Avvia();
                Assert.That(u.Eventi,Is.Empty);
                c.Motore.TickInterno();
                Assert.That(u.Eventi.Take(3),Is.EqualTo(new[]{"Start","Clock","On:0:76"}));
            }
        }
    }
}
