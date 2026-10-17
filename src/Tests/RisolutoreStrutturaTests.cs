using System.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class RisolutoreStrutturaTests
    {
        private static PayloadCartoncino Imp(TipoImpostazione tipo, int valore, int valoreB = 0)
        {
            return new PayloadCartoncino
            {
                Tipo = TipoCartoncino.Impostazione,
                Impostazione = tipo,
                Valore = valore,
                ValoreB = valoreB,
            };
        }

        private static RettangoloTavolo Area(double x, double y)
        {
            return new RettangoloTavolo(x, y, 0.1, 0.1);
        }

        private static RisoluzioneStruttura Una(PayloadCartoncino payload, ZonaSpeciale zona)
        {
            return RisolutoreStruttura.Risolvi(new[] { (payload, Area(0.01, 0.04), zona) });
        }

        [Test]
        public void Vuoto_Default()
        {
            var risultato = RisolutoreStruttura.Risolvi(new (PayloadCartoncino, RettangoloTavolo, ZonaSpeciale)[0]);

            Assert.That(risultato.Struttura, Is.EqualTo(StrutturaTavolo.Default));
            Assert.That(risultato.Errori, Is.Empty);
        }

        [Test]
        public void Null_Default()
        {
            var risultato = RisolutoreStruttura.Risolvi(null);

            Assert.That(risultato.Struttura, Is.EqualTo(StrutturaTavolo.Default));
            Assert.That(risultato.Errori, Is.Empty);
        }

        [Test]
        public void MisuraTre()
        {
            var risultato = Una(Imp(TipoImpostazione.Misura, 3), ZonaSpeciale.Misura);

            Assert.That(risultato.Struttura.BattitiPerMisura, Is.EqualTo(3));
            Assert.That(risultato.Errori, Is.Empty);
        }

        [Test]
        public void LinkDue()
        {
            var risultato = Una(Imp(TipoImpostazione.Collegamento, 2), ZonaSpeciale.Collegamento);

            Assert.That(risultato.Struttura.RigheCollegate, Is.EqualTo(2));
            Assert.That(risultato.Struttura.AInizio, Is.EqualTo(1));
            Assert.That(risultato.Struttura.AFine, Is.EqualTo(2));
            Assert.That(risultato.Errori, Is.Empty);
        }

        [Test]
        public void AbDueTre_ConLinkTre()
        {
            var risultato = RisolutoreStruttura.Risolvi(new[]
            {
                (Imp(TipoImpostazione.Collegamento, 3), Area(0.01, 0.36), ZonaSpeciale.Collegamento),
                (Imp(TipoImpostazione.IntervalloAb, 2, 3), Area(0.01, 0.68), ZonaSpeciale.Ab),
            });

            Assert.That(risultato.Struttura.RigheCollegate, Is.EqualTo(3));
            Assert.That(risultato.Struttura.AInizio, Is.EqualTo(2));
            Assert.That(risultato.Struttura.AFine, Is.EqualTo(3));
            Assert.That(risultato.Errori, Is.Empty);
        }

        [Test]
        public void ChiaveSbagliataPerZona()
        {
            var risultato = Una(Imp(TipoImpostazione.Collegamento, 2), ZonaSpeciale.Misura);

            Assert.That(risultato.Errori.Count, Is.EqualTo(1));
            Assert.That(risultato.Errori[0].Tipo, Is.EqualTo(TipoErrore.ImpostazioneNonValida));
            Assert.That(risultato.Struttura, Is.EqualTo(StrutturaTavolo.Default));
        }

        [Test]
        public void ValoreFuoriInsieme()
        {
            var misura = Una(Imp(TipoImpostazione.Misura, 5), ZonaSpeciale.Misura);
            Assert.That(misura.Struttura.BattitiPerMisura, Is.EqualTo(StrutturaTavolo.Default.BattitiPerMisura));
            Assert.That(misura.Errori.Single().Tipo, Is.EqualTo(TipoErrore.ImpostazioneNonValida));

            var link = Una(Imp(TipoImpostazione.Collegamento, 4), ZonaSpeciale.Collegamento);
            Assert.That(link.Struttura.RigheCollegate, Is.EqualTo(StrutturaTavolo.Default.RigheCollegate));
            Assert.That(link.Errori.Single().Tipo, Is.EqualTo(TipoErrore.ImpostazioneNonValida));

            var abUguali = Una(Imp(TipoImpostazione.IntervalloAb, 3, 3), ZonaSpeciale.Ab);
            Assert.That(abUguali.Struttura, Is.EqualTo(StrutturaTavolo.Default));
            Assert.That(abUguali.Errori.Single().Tipo, Is.EqualTo(TipoErrore.ImpostazioneNonValida));

            var abInvertiti = Una(Imp(TipoImpostazione.IntervalloAb, 4, 2), ZonaSpeciale.Ab);
            Assert.That(abInvertiti.Struttura, Is.EqualTo(StrutturaTavolo.Default));
            Assert.That(abInvertiti.Errori.Single().Tipo, Is.EqualTo(TipoErrore.ImpostazioneNonValida));
        }

        [Test]
        public void AbOltreIlGruppo()
        {
            var risultato = RisolutoreStruttura.Risolvi(new[]
            {
                (Imp(TipoImpostazione.Collegamento, 2), Area(0.01, 0.36), ZonaSpeciale.Collegamento),
                (Imp(TipoImpostazione.IntervalloAb, 1, 5), Area(0.01, 0.68), ZonaSpeciale.Ab),
            });

            Assert.That(risultato.Errori.Single().Tipo, Is.EqualTo(TipoErrore.ImpostazioneNonValida));
            Assert.That(risultato.Struttura.RigheCollegate, Is.EqualTo(2));
            Assert.That(risultato.Struttura.AInizio, Is.EqualTo(1));
            Assert.That(risultato.Struttura.AFine, Is.EqualTo(2));
        }

        [Test]
        public void AbConUnaRiga()
        {
            var risultato = RisolutoreStruttura.Risolvi(new[]
            {
                (Imp(TipoImpostazione.Collegamento, 1), Area(0.01, 0.36), ZonaSpeciale.Collegamento),
                (Imp(TipoImpostazione.IntervalloAb, 1, 2), Area(0.01, 0.68), ZonaSpeciale.Ab),
            });

            Assert.That(risultato.Errori.Single().Tipo, Is.EqualTo(TipoErrore.ImpostazioneNonValida));
            Assert.That(risultato.Struttura.RigheCollegate, Is.EqualTo(1));
            Assert.That(risultato.Struttura.AInizio, Is.EqualTo(1));
            Assert.That(risultato.Struttura.AFine, Is.EqualTo(1));
        }

        [Test]
        public void DueCartoniNellaStessaZona()
        {
            var inAlto = Area(0.01, 0.04);
            var inBasso = Area(0.01, 0.20);

            var risultato = RisolutoreStruttura.Risolvi(new[]
            {
                (Imp(TipoImpostazione.Misura, 3), inBasso, ZonaSpeciale.Misura),
                (Imp(TipoImpostazione.Misura, 2), inAlto, ZonaSpeciale.Misura),
            });

            Assert.That(risultato.Struttura.BattitiPerMisura, Is.EqualTo(2));
            Assert.That(risultato.Errori.Count, Is.EqualTo(1));
            Assert.That(risultato.Errori[0].Tipo, Is.EqualTo(TipoErrore.Sovrapposto));
            Assert.That(risultato.Errori[0].Area.Y, Is.EqualTo(inBasso.Y));
            Assert.That(risultato.Errori[0].Area.X, Is.EqualTo(inBasso.X));
        }

        [Test]
        public void ZonaComandiIgnorata()
        {
            var risultato = Una(Imp(TipoImpostazione.Misura, 3), ZonaSpeciale.Comandi);

            Assert.That(risultato.Struttura, Is.EqualTo(StrutturaTavolo.Default));
            Assert.That(risultato.Errori, Is.Empty);
        }

        [Test]
        public void CombinazioneCompleta()
        {
            var risultato = RisolutoreStruttura.Risolvi(new[]
            {
                (Imp(TipoImpostazione.Misura, 2), Area(0.01, 0.04), ZonaSpeciale.Misura),
                (Imp(TipoImpostazione.Collegamento, 3), Area(0.01, 0.36), ZonaSpeciale.Collegamento),
                (Imp(TipoImpostazione.IntervalloAb, 2, 3), Area(0.01, 0.68), ZonaSpeciale.Ab),
            });

            Assert.That(risultato.Struttura.BattitiPerMisura, Is.EqualTo(2));
            Assert.That(risultato.Struttura.RigheCollegate, Is.EqualTo(3));
            Assert.That(risultato.Struttura.AInizio, Is.EqualTo(2));
            Assert.That(risultato.Struttura.AFine, Is.EqualTo(3));
            Assert.That(risultato.Errori, Is.Empty);
        }
    }
}
