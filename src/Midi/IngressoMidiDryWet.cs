using System;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Midi
{
    /// <summary>
    /// Ingresso MIDI dal DAW tramite DryWetMIDI. Gli eventi vengono sollevati
    /// sul thread del dispositivo. Dopo Dispose nessun subscriber viene più invocato.
    /// </summary>
    public class IngressoMidiDryWet : IIngressoMidi, IDisposable
    {
        private readonly InputDevice dispositivo;
        private readonly GuardiaRilascio guardia = new GuardiaRilascio("Ingresso MIDI");

        /// <summary>Apre la porta di ingresso indicata e avvia l'ascolto.</summary>
        /// <param name="nomePorta">Nome della porta MIDI di ingresso.</param>
        /// <exception cref="InvalidOperationException">Se la porta non esiste.</exception>
        public IngressoMidiDryWet(string nomePorta)
        {
            dispositivo = ApriPorta(nomePorta);

            try
            {
                dispositivo.EventReceived += GestisciEvento;
                dispositivo.StartEventsListening();
            }
            catch
            {
                // Evita di perdere l'handle se l'avvio dell'ascolto fallisce.
                dispositivo.EventReceived -= GestisciEvento;
                dispositivo.Dispose();
                throw;
            }
        }

        /// <inheritdoc />
        public event Action ClockRicevuto;

        /// <inheritdoc />
        public event Action StartRicevuto;

        /// <inheritdoc />
        public event Action StopRicevuto;

        /// <inheritdoc />
        public event Action ContinueRicevuto;

        /// <summary>Interrompe l'ascolto e rilascia la porta MIDI di ingresso.</summary>
        public void Dispose()
        {
            guardia.Rilascia(() =>
            {
                dispositivo.EventReceived -= GestisciEvento;
                dispositivo.StopEventsListening();
                dispositivo.Dispose();
            });
        }

        private void GestisciEvento(object mittente, MidiEventReceivedEventArgs argomenti)
        {
            // Dopo il rilascio gli eventi residui vengono ignorati.
            guardia.EseguiSeAttivo(() =>
            {
                switch (argomenti.Event)
                {
                    case TimingClockEvent _:
                        Solleva(ClockRicevuto);
                        break;
                    case StartEvent _:
                        Solleva(StartRicevuto);
                        break;
                    case StopEvent _:
                        Solleva(StopRicevuto);
                        break;
                    case ContinueEvent _:
                        Solleva(ContinueRicevuto);
                        break;
                }
            });
        }

        private static void Solleva(Action evento)
        {
            if (evento == null)
            {
                return;
            }

            foreach (Action gestore in evento.GetInvocationList())
            {
                try
                {
                    gestore();
                }
                catch (Exception)
                {
                    // Un subscriber che lancia non deve far morire il thread del dispositivo.
                }
            }
        }

        private static InputDevice ApriPorta(string nomePorta)
        {
            try
            {
                return InputDevice.GetByName(nomePorta);
            }
            catch (ArgumentException)
            {
                string disponibili = string.Join(", ", InputDevice.GetAll().Select(d => d.Name));
                if (string.IsNullOrEmpty(disponibili))
                {
                    disponibili = "nessuna";
                }

                throw new InvalidOperationException(
                    $"Porta MIDI di ingresso '{nomePorta}' non trovata. Porte disponibili: {disponibili}.");
            }
        }
    }
}
