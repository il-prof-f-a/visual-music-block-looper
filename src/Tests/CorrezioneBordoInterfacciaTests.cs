using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture, Apartment(ApartmentState.STA)]
    public class CorrezioneBordoInterfacciaTests
    {
        private static FormCorrezioneBordo Crea(List<ParametriCorrezioneBordo> applicati)
            => new FormCorrezioneBordo(new ParametriCorrezioneBordo(),
                p => applicati.Add(p), p => applicati.Add(p));

        [Test]
        public void Costruzione_MostraModelloECinqueRegoli()
        {
            var applicati = new List<ParametriCorrezioneBordo>();
            using (var form = Crea(applicati))
            {
                Assert.That(form.Controls.OfType<ComboBox>().Count(), Is.EqualTo(1));
                Assert.That(form.Controls.OfType<TrackBar>().Count(), Is.EqualTo(5));
                Assert.That(form.Text, Is.EqualTo("Correzione bordo"));
            }
        }

        [Test]
        public void ModelloBordo_DisabilitaAnisotropiaESmussamento()
        {
            var applicati = new List<ParametriCorrezioneBordo>();
            using (var form = Crea(applicati))
            {
                var combo = form.Controls.OfType<ComboBox>().Single();
                combo.SelectedIndex = 0;
                TrackBar[] regoli = form.Controls.OfType<TrackBar>().OrderBy(r => r.Top).ToArray();
                Assert.That(regoli[2].Enabled, Is.False, "Fattore X");
                Assert.That(regoli[3].Enabled, Is.False, "Fattore Y");
                Assert.That(regoli[4].Enabled, Is.False, "Smussamento");
            }
        }

        [Test]
        public void ModelloSmussato_AbilitaFattoriESmussamento()
        {
            var applicati = new List<ParametriCorrezioneBordo>();
            using (var form = Crea(applicati))
            {
                var combo = form.Controls.OfType<ComboBox>().Single();
                combo.SelectedIndex = 1;
                TrackBar[] regoli = form.Controls.OfType<TrackBar>().OrderBy(r => r.Top).ToArray();
                Assert.That(regoli[2].Enabled, Is.True, "Fattore X");
                Assert.That(regoli[3].Enabled, Is.True, "Fattore Y");
                Assert.That(regoli[4].Enabled, Is.True, "Smussamento");
            }
        }
    }
}
