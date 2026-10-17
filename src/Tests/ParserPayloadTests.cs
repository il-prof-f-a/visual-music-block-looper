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

        [TestCase("VMBL1;C;ONESHOT;0", 0)]
        [TestCase("VMBL1;C;ONESHOT;127", 127)]
        public void OneShotLimitiAccettati(string testo, int atteso)
        {
            bool ok = ParserPayload.TryParse(testo, out PayloadCartoncino payload);

            Assert.That(ok, Is.True);
            Assert.That(payload.Comando, Is.EqualTo(TipoComando.OneShot));
            Assert.That(payload.Parametro, Is.EqualTo(atteso));
        }

        [TestCase("VMBL1;C;ONESHOT;128")]
        [TestCase("VMBL1;C;ONESHOT;-1")]
        [TestCase("VMBL1;C;ONESHOT;1000")]
        public void OneShotFuoriLimiteRifiutato(string testo)
        {
            bool ok = true;

            Assert.DoesNotThrow(() => ok = ParserPayload.TryParse(testo, out _));
            Assert.That(ok, Is.False);
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
        public void Misura_Valida()
        {
            bool ok = ParserPayload.TryParse("VMBL1;S;MISURA;3", out PayloadCartoncino payload);

            Assert.That(ok, Is.True);
            Assert.That(payload.Tipo, Is.EqualTo(TipoCartoncino.Impostazione));
            Assert.That(payload.Impostazione, Is.EqualTo(TipoImpostazione.Misura));
            Assert.That(payload.Valore, Is.EqualTo(3));
        }

        [Test]
        public void Link_Valido()
        {
            bool ok = ParserPayload.TryParse("VMBL1;S;LINK;6", out PayloadCartoncino payload);

            Assert.That(ok, Is.True);
            Assert.That(payload.Tipo, Is.EqualTo(TipoCartoncino.Impostazione));
            Assert.That(payload.Impostazione, Is.EqualTo(TipoImpostazione.Collegamento));
            Assert.That(payload.Valore, Is.EqualTo(6));
        }

        [Test]
        public void Ab_Valido()
        {
            bool ok = ParserPayload.TryParse("VMBL1;S;AB;2;5", out PayloadCartoncino payload);

            Assert.That(ok, Is.True);
            Assert.That(payload.Tipo, Is.EqualTo(TipoCartoncino.Impostazione));
            Assert.That(payload.Impostazione, Is.EqualTo(TipoImpostazione.IntervalloAb));
            Assert.That(payload.Valore, Is.EqualTo(2));
            Assert.That(payload.ValoreB, Is.EqualTo(5));
        }

        [Test]
        public void ChiaveCaseInsensitive()
        {
            bool ok = ParserPayload.TryParse("VMBL1;S;misura;4", out PayloadCartoncino payload);

            Assert.That(ok, Is.True);
            Assert.That(payload.Impostazione, Is.EqualTo(TipoImpostazione.Misura));
            Assert.That(payload.Valore, Is.EqualTo(4));
        }

        [TestCase("VMBL1;S")]
        [TestCase("VMBL1;S;MISURA")]
        [TestCase("VMBL1;S;MISURA;")]
        [TestCase("VMBL1;S;MISURA;-1")]
        [TestCase("VMBL1;S;MISURA;4;4")]
        [TestCase("VMBL1;S;AB;2")]
        [TestCase("VMBL1;S;AB;2;x")]
        [TestCase("VMBL1;S;AB;1;2;3")]
        [TestCase("VMBL1;S;COSA;1")]
        [TestCase("VMBL1;S;LINK;١")]
        public void RifiutatiSpeciali(string testo)
        {
            bool ok = true;

            Assert.DoesNotThrow(() => ok = ParserPayload.TryParse(testo, out _));
            Assert.That(ok, Is.False);
        }

        [Test]
        public void Fuzz_NonLanciaMai()
        {
            const string alfabeto = "VMBL1NCS;-+ 0123456789abcdefONESHOTTAP١٢\t\n";
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
