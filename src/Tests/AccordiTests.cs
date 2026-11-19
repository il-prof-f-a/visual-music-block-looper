using System.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class AccordiTests
    {
        [TestCase(0, TipoAccordo.Maggiore, new[] { 48, 52, 55 })]          // Do+
        [TestCase(0, TipoAccordo.Minore, new[] { 48, 51, 55 })]           // Do-
        [TestCase(0, TipoAccordo.Settima, new[] { 48, 52, 55, 58 })]      // Do7
        [TestCase(0, TipoAccordo.MinoreSettima, new[] { 48, 51, 55, 58 })] // Do-7
        [TestCase(0, TipoAccordo.MaggioreSettima, new[] { 48, 52, 55, 59 })] // Do+7
        [TestCase(0, TipoAccordo.Diminuito, new[] { 48, 51, 54 })]        // Dodim
        [TestCase(0, TipoAccordo.DiminuitoSettima, new[] { 48, 51, 54, 57 })]
        [TestCase(2, TipoAccordo.Maggiore, new[] { 50, 54, 57 })]         // Re+
        [TestCase(11, TipoAccordo.Maggiore, new[] { 59, 63, 66 })]        // Si+
        public void VoceParteDaDo3(int fondamentale, TipoAccordo tipo, int[] attese)
        {
            Assert.That(Accordi.Note(fondamentale, tipo), Is.EqualTo(attese));
        }

        [Test]
        public void TutteLeQualitaHannoUnCodiceDistinto()
        {
            var codici = System.Enum.GetValues(typeof(TipoAccordo)).Cast<TipoAccordo>()
                .Select(Accordi.Codice).ToArray();
            Assert.That(codici.Distinct().Count(), Is.EqualTo(codici.Length));
        }

        [Test]
        public void CodiciSonoCompatti()
        {
            foreach (TipoAccordo tipo in System.Enum.GetValues(typeof(TipoAccordo)))
            {
                Assert.That(Accordi.Codice(tipo).Length, Is.LessThanOrEqualTo(4));
            }
        }
    }
}
