using System;
using System.Text;
using NUnit.Framework;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class ParserPayloadTests
    {
        [Test]
        public void Nota_Valida()
        {
            bool ok = ParserPayload.TryParse("VMBL1;N;12", out PayloadCartoncino payload);

            Assert.That(ok, Is.True);
            Assert.That(payload.Tipo, Is.EqualTo(TipoCartoncino.Nota));
            Assert.That(payload.IdClip, Is.EqualTo(12));
        }

        [Test]
        public void ComandoSenzaParametro()
        {
            bool ok = ParserPayload.TryParse("VMBL1;C;TAP", out PayloadCartoncino payload);

            Assert.That(ok, Is.True);
            Assert.That(payload.Tipo, Is.EqualTo(TipoCartoncino.Comando));
            Assert.That(payload.Comando, Is.EqualTo(TipoComando.Tap));
        }

        [Test]
        public void OneShotConParametro()
        {
            bool ok = ParserPayload.TryParse("VMBL1;C;ONESHOT;60", out PayloadCartoncino payload);

            Assert.That(ok, Is.True);
            Assert.That(payload.Tipo, Is.EqualTo(TipoCartoncino.Comando));
            Assert.That(payload.Comando, Is.EqualTo(TipoComando.OneShot));
            Assert.That(payload.Parametro, Is.EqualTo(60));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("VMBL2;N;1")]
        [TestCase("VMBL1;N;")]
        [TestCase("VMBL1;N;-3")]
        [TestCase("VMBL1;N;abc")]
        [TestCase("VMBL1;C;BOH")]
        [TestCase("VMBL1;C;ONESHOT")]
        [TestCase("VMBL1;N;1;2;3")]
        [TestCase("VMBL1;N;١٢")]
        public void Rifiutati(string testo)
        {
            bool ok = true;

            Assert.DoesNotThrow(() => ok = ParserPayload.TryParse(testo, out _));
            Assert.That(ok, Is.False);
        }

        [Test]
        public void Fuzz_NonLanciaMai()
        {
            const string alfabeto = "VMBL1NC;-+ 0123456789abcdefONESHOTTAP١٢\t\n";
            var random = new Random(20261017);
            var sb = new StringBuilder();

            for (int i = 0; i < 2000; i++)
            {
                int lunghezza = random.Next(0, 24);
                sb.Clear();
                for (int j = 0; j < lunghezza; j++)
                {
                    sb.Append(alfabeto[random.Next(alfabeto.Length)]);
                }

                string testo = sb.ToString();
                Assert.DoesNotThrow(() => ParserPayload.TryParse(testo, out _), "Input: \"" + testo + "\"");
            }
        }
    }
}
