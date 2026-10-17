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
        public void Rilascio_NonAvviene_MaiDuranteUnaOperazioneInCorso()
        {
            GuardiaRilascio guardia = new GuardiaRilascio("Risorsa di prova");
            int violazioni = 0;
            bool rilasciata = false;

            Thread[] fili = new Thread[4];
            for (int i = 0; i < fili.Length; i++)
            {
                fili[i] = new Thread(() =>
                {
                    for (int j = 0; j < 2000; j++)
                    {
                        guardia.EseguiSeAttivo(() =>
                        {
                            if (Volatile.Read(ref rilasciata))
                            {
                                Interlocked.Increment(ref violazioni);
                            }
                        });
                    }
                });
                fili[i].Start();
            }

            Thread.Sleep(5);
            guardia.Rilascia(() => Volatile.Write(ref rilasciata, true));

            foreach (Thread filo in fili)
            {
                filo.Join();
            }

            Assert.That(Volatile.Read(ref violazioni), Is.EqualTo(0));
        }
    }
}
