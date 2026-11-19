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
                Assert.That(form.Controls.OfType<ComboBox>().Count(), Is.EqualTo(2));
                Assert.That(form.Controls.OfType<TrackBar>().Count(), Is.EqualTo(8));
                Assert.That(form.Text, Is.EqualTo("Correzione bordo"));
            }
        }

        [Test]
        public void ModelloBordo_DisabilitaAnisotropiaESmussamento()
        {
            var applicati = new List<ParametriCorrezioneBordo>();
            using (var form = Crea(applicati))
            {
                var combo = form.Controls.OfType<ComboBox>().OrderBy(c => c.Top).First();
                combo.SelectedIndex = 0;
                TrackBar[] regoli = form.Controls.OfType<TrackBar>().OrderBy(r => r.Top).ToArray();
                Assert.That(regoli[2].Enabled, Is.False, "Fattore X");
                Assert.That(regoli[3].Enabled, Is.False, "Fattore Y");
                Assert.That(regoli[4].Enabled, Is.False, "Smussamento");
                Assert.That(regoli[5].Enabled, Is.False, "Focale");
                Assert.That(regoli[6].Enabled, Is.False, "Centro X");
                Assert.That(regoli[7].Enabled, Is.False, "Centro Y");
            }
        }

        [Test]
        public void ModelloSmussato_AbilitaFattoriESmussamento()
        {
            var applicati = new List<ParametriCorrezioneBordo>();
            using (var form = Crea(applicati))
            {
                var combo = form.Controls.OfType<ComboBox>().OrderBy(c => c.Top).First();
                combo.SelectedIndex = 1;
                TrackBar[] regoli = form.Controls.OfType<TrackBar>().OrderBy(r => r.Top).ToArray();
                Assert.That(regoli[2].Enabled, Is.True, "Fattore X");
                Assert.That(regoli[3].Enabled, Is.True, "Fattore Y");
                Assert.That(regoli[4].Enabled, Is.True, "Smussamento");
                Assert.That(regoli[5].Enabled, Is.False, "Focale");
            }
        }

        [Test]
        public void ModelloSferico_AbilitaSoloControlliSferici()
        {
            var applicati = new List<ParametriCorrezioneBordo>();
            using (var form = Crea(applicati))
            {
                var combo = form.Controls.OfType<ComboBox>().OrderBy(c => c.Top).First();
                combo.SelectedIndex = 2;
                TrackBar[] regoli = form.Controls.OfType<TrackBar>().OrderBy(r => r.Top).ToArray();
                Assert.That(regoli[0].Enabled, Is.False, "Pieno");
                Assert.That(regoli[1].Enabled, Is.False, "Esterno");
                Assert.That(regoli[5].Enabled, Is.True, "Focale");
                Assert.That(regoli[6].Enabled, Is.True, "Centro X");
                Assert.That(regoli[7].Enabled, Is.True, "Centro Y");
            }
        }
    }
}

