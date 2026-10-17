using System;
using System.Linq;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Midi
{
    /// <summary>
    /// Uscita MIDI verso il DAW tramite DryWetMIDI. Invia solo messaggi:
    /// non richiama mai il motore dall'interno dei metodi di invio.
    /// </summary>
    public class UscitaMidiDryWet : IUscitaMidi, IDisposable
    {
        private readonly OutputDevice dispositivo;
        private bool disposto;

        /// <summary>Apre la porta di uscita indicata.</summary>
        /// <param name="nomePorta">Nome della porta MIDI di uscita.</param>
        /// <exception cref="InvalidOperationException">Se la porta non esiste.</exception>
        public UscitaMidiDryWet(string nomePorta)
        {
            dispositivo = ApriPorta(nomePorta);
        }

        /// <inheritdoc />
        public void NotaOn(int canale, int nota, int velocity)
        {
            NoteOnEvent evento = new NoteOnEvent(
                (SevenBitNumber)ConversioneMidi.Nota(nota),
                (SevenBitNumber)ConversioneMidi.Velocity(velocity))
            {
                Channel = (FourBitNumber)ConversioneMidi.Canale(canale)
            };

            dispositivo.SendEvent(evento);
        }

        /// <inheritdoc />
        public void NotaOff(int canale, int nota)
        {
            NoteOffEvent evento = new NoteOffEvent(
                (SevenBitNumber)ConversioneMidi.Nota(nota),
                (SevenBitNumber)0)
            {
                Channel = (FourBitNumber)ConversioneMidi.Canale(canale)
            };

            dispositivo.SendEvent(evento);
        }

        /// <inheritdoc />
        public void Clock()
        {
            dispositivo.SendEvent(new TimingClockEvent());
        }

        /// <inheritdoc />
        public void Start()
        {
            dispositivo.SendEvent(new StartEvent());
        }

        /// <inheritdoc />
        public void Stop()
        {
            dispositivo.SendEvent(new StopEvent());
        }

        /// <inheritdoc />
        public void Continue()
        {
            dispositivo.SendEvent(new ContinueEvent());
        }

        /// <summary>Rilascia la porta MIDI di uscita.</summary>
        public void Dispose()
        {
            if (disposto)
            {
                return;
            }

            disposto = true;
            dispositivo.Dispose();
        }

        private static OutputDevice ApriPorta(string nomePorta)
        {
            try
            {
                return OutputDevice.GetByName(nomePorta);
            }
            catch (ArgumentException)
            {
                string disponibili = string.Join(", ", OutputDevice.GetAll().Select(d => d.Name));
                if (string.IsNullOrEmpty(disponibili))
                {
                    disponibili = "nessuna";
                }

                throw new InvalidOperationException(
                    $"Porta MIDI di uscita '{nomePorta}' non trovata. Porte disponibili: {disponibili}.");
            }
        }
    }
}
