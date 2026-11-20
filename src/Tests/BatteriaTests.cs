using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class BatteriaTests
    {
        [Test]
        public void MappaCopreTuttoIlKitSenzaBuchi()
        {
            int[] midi = GeneratoreBatteria.Voci.Select(v => v.Midi).ToArray();

            Assert.That(midi.Length, Is.EqualTo(47));
            Assert.That(midi, Is.EqualTo(Enumerable.Range(35, 47)));
            Assert.That(GeneratoreBatteria.Voci.Select(v => v.Slug).Distinct().Count(),
                Is.EqualTo(GeneratoreBatteria.Voci.Count));
        }

        [Test]
        public void OgniPayloadUsaIlQrDelleNoteConNotaEsplicita()
        {
            foreach (GeneratoreBatteria.Voce voce in GeneratoreBatteria.Voci)
            {
                Assert.That(voce.Payload, Does.StartWith("VMBL1;N;"));
                Assert.That(ParserPayload.TryParse(voce.Payload, out PayloadCartoncino payload), Is.True,
                    voce.Payload);
                Assert.That(payload.Tipo, Is.EqualTo(TipoCartoncino.Nota));
                Assert.That(payload.NotaMidi, Is.EqualTo(voce.Midi), voce.Payload);
            }
        }

        [Test]
        public void NotaEsplicitaSullaGriglia_ArrivaAlMotore()
        {
            var config = new ConfigGriglia { Area = LayoutTavolo.AreaGrigliaDefault };
            var interprete = new InterpreteTavolo(new Griglia(config), LayoutTavolo.Default);
            RettangoloTavolo g = LayoutTavolo.AreaGrigliaDefault;
            double colonna = g.Larghezza / 16.0, altezzaRiga = g.Altezza / 6.0;
            var area = new RettangoloTavolo(g.X + 2 * colonna + colonna * 0.1,
                g.Y + 1 * altezzaRiga + altezzaRiga * 0.25, colonna * 0.8, altezzaRiga * 0.5);

            StatoTavolo stato = interprete.Interpreta(new[]
            {
                new CartoncinoRilevato { Payload = "VMBL1;N;0;35", Area = area }
            });

            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Note, Has.Count.EqualTo(1));
            Assert.That(stato.Note[0].NoteMidi, Is.EqualTo(new[] { 35 }));
        }
    }
}
