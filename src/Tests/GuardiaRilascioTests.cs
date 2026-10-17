using System;
using System.Threading;
using NUnit.Framework;
using VisualMusicBlockLooper.Midi;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class GuardiaRilascioTests
    {
        [Test]
        public void Esegui_PrimaDelRilascio_EsegueAzione()
        {
            GuardiaRilascio guardia = new GuardiaRilascio("Risorsa di prova");
            int eseguite = 0;

            guardia.Esegui(() => eseguite++);

            Assert.That(eseguite, Is.EqualTo(1));
            Assert.That(guardia.Rilasciato, Is.False);
        }

        [Test]
        public void Esegui_DopoRilascio_LanciaObjectDisposedExceptionItaliana()
        {
            GuardiaRilascio guardia = new GuardiaRilascio("Uscita MIDI");
            guardia.Rilascia(() => { });

            ObjectDisposedException eccezione = Assert.Throws<ObjectDisposedException>(
                () => guardia.Esegui(() => { }));

            Assert.That(eccezione.Message, Does.Contain("Uscita MIDI"));
            Assert.That(eccezione.Message, Does.Contain("rilasciata"));
        }

        [Test]
        public void Rilascia_EseguitoUnaSolaVolta()
        {
            GuardiaRilascio guardia = new GuardiaRilascio("Risorsa di prova");
            int rilasci = 0;

            guardia.Rilascia(() => rilasci++);
            guardia.Rilascia(() => rilasci++);

            Assert.That(rilasci, Is.EqualTo(1));
            Assert.That(guardia.Rilasciato, Is.True);
        }

        [Test]
        public void EseguiSeAttivo_DopoRilascio_NonInvocaAzione()
        {
            GuardiaRilascio guardia = new GuardiaRilascio("Ingresso MIDI");
            int invocazioni = 0;
            guardia.Rilascia(() => { });

            bool eseguita = guardia.EseguiSeAttivo(() => invocazioni++);

            Assert.That(eseguita, Is.False);
            Assert.That(invocazioni, Is.EqualTo(0));
        }

        [Test]
        public void Rilascia_AzioneEseguitaFuoriDalLock_AltroThreadNonBloccato()
        {
            // Se l'azione di rilascio fosse eseguita sotto lock, EseguiSeAttivo da un altro thread
            // resterebbe bloccato e il ManualResetEventSlim scadrebbe (risultato false).
            GuardiaRilascio guardia = new GuardiaRilascio("Risorsa di prova");
            bool completato = false;
            bool risultato = true;

            guardia.Rilascia(() =>
            {
                using (ManualResetEventSlim fine = new ManualResetEventSlim(false))
                {
                    Thread altro = new Thread(() =>
                    {
                        risultato = guardia.EseguiSeAttivo(() => { });
                        fine.Set();
                    });
                    altro.IsBackground = true;
                    altro.Start();
                    completato = fine.Wait(TimeSpan.FromSeconds(5));
                }
            });

            Assert.That(completato, Is.True, "EseguiSeAttivo da un altro thread è rimasto bloccato durante il rilascio");
            Assert.That(risultato, Is.False);
        }

        [Test]
        public void Rilascia_NonBloccatoDaCallbackInCorso()
        {
            // Una callback in corso non deve bloccare Rilascia chiamato da un altro thread.
            GuardiaRilascio guardia = new GuardiaRilascio("Ingresso MIDI");
            using (ManualResetEventSlim dentroCallback = new ManualResetEventSlim(false))
            using (ManualResetEventSlim rilascioFatto = new ManualResetEventSlim(false))
            {
                bool rilascioTerminatoInTempo = false;
                Thread filoCallback = new Thread(() =>
                {
                    guardia.EseguiSeAttivo(() =>
                    {
                        dentroCallback.Set();
                        rilascioTerminatoInTempo = rilascioFatto.Wait(TimeSpan.FromSeconds(5));
                    });
                });
                filoCallback.IsBackground = true;
                filoCallback.Start();

                Assert.That(dentroCallback.Wait(TimeSpan.FromSeconds(5)), Is.True, "La callback non è partita");

                guardia.Rilascia(() => { });
                rilascioFatto.Set();
                filoCallback.Join(TimeSpan.FromSeconds(10));

                Assert.That(rilascioTerminatoInTempo, Is.True, "Rilascia è rimasto bloccato dalla callback in corso");
            }
        }

        [Test]
        public void Rilascia_EccezioneNellAzione_MarcaRilasciataERilancia()
        {
            GuardiaRilascio guardia = new GuardiaRilascio("Risorsa di prova");
            int invocazioni = 0;

            Assert.Throws<InvalidOperationException>(
                () => guardia.Rilascia(() => throw new InvalidOperationException("prova")));

            Assert.That(guardia.Rilasciato, Is.True);
            guardia.Rilascia(() => invocazioni++);
            Assert.That(invocazioni, Is.EqualTo(0));
        }
    }
}
