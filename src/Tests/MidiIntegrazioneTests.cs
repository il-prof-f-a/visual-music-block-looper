using System;
using System.Linq;
using System.Threading;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using NUnit.Framework;
using VisualMusicBlockLooper.Midi;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class MidiIntegrazioneTests
    {
        private const string PortaLoopback = "VMBL Test";
        private const string PortaInesistente = "VMBL-porta-che-non-esiste";

        [Test]
        public void Uscita_PortaInesistente_LanciaEccezioneChiara()
        {
            InvalidOperationException eccezione = Assert.Throws<InvalidOperationException>(
                () => new UscitaMidiDryWet(PortaInesistente).Dispose());

            Assert.That(eccezione.Message, Does.Contain(PortaInesistente));
        }

        [Test]
        public void Ingresso_PortaInesistente_LanciaEccezioneChiara()
        {
            InvalidOperationException eccezione = Assert.Throws<InvalidOperationException>(
                () => new IngressoMidiDryWet(PortaInesistente).Dispose());

            Assert.That(eccezione.Message, Does.Contain(PortaInesistente));
        }

        [Test]
        [Category("Hardware")]
        public void Uscita_InviaNoteSuLoopback()
        {
            VerificaPortaLoopback();

            using (ManualResetEventSlim ricevuto = new ManualResetEventSlim(false))
            using (InputDevice osservatore = InputDevice.GetByName(PortaLoopback))
            using (UscitaMidiDryWet uscita = new UscitaMidiDryWet(PortaLoopback))
            {
                NoteOnEvent notaRicevuta = null;
                EventHandler<MidiEventReceivedEventArgs> gestore = (mittente, argomenti) =>
                {
                    if (argomenti.Event is NoteOnEvent noteOn && noteOn.NoteNumber == (SevenBitNumber)60)
                    {
                        notaRicevuta = noteOn;
                        ricevuto.Set();
                    }
                };
                osservatore.EventReceived += gestore;
                osservatore.StartEventsListening();

                uscita.NotaOn(0, 60, 100);

                bool arrivato = ricevuto.Wait(TimeSpan.FromSeconds(3));
                osservatore.EventReceived -= gestore;
                osservatore.StopEventsListening();

                Assert.That(arrivato, Is.True, "Nota-on non ricevuta sul loopback");
                Assert.That(notaRicevuta.Channel, Is.EqualTo((FourBitNumber)0));
                Assert.That(notaRicevuta.Velocity, Is.EqualTo((SevenBitNumber)100));
            }
        }

        [Test]
        [Category("Hardware")]
        public void Ingresso_RiceveClockDaLoopback()
        {
            VerificaPortaLoopback();

            using (ManualResetEventSlim ricevuto = new ManualResetEventSlim(false))
            using (OutputDevice mittente = OutputDevice.GetByName(PortaLoopback))
            using (IngressoMidiDryWet ingresso = new IngressoMidiDryWet(PortaLoopback))
            {
                Action gestore = () => ricevuto.Set();
                ingresso.ClockRicevuto += gestore;

                mittente.SendEvent(new TimingClockEvent());

                bool arrivato = ricevuto.Wait(TimeSpan.FromSeconds(3));
                ingresso.ClockRicevuto -= gestore;

                Assert.That(arrivato, Is.True, "Clock non ricevuto dal loopback");
            }
        }

        private static void VerificaPortaLoopback()
        {
            bool uscitaPresente = OutputDevice.GetAll().Any(d => d.Name == PortaLoopback);
            bool ingressoPresente = InputDevice.GetAll().Any(d => d.Name == PortaLoopback);

            if (!uscitaPresente || !ingressoPresente)
            {
                Assert.Ignore("Porta MIDI 'VMBL Test' non presente (loopMIDI)");
            }
        }
    }
}
