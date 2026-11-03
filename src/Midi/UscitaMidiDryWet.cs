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
    /// Invii e rilascio sono sincronizzati: thread diversi possono inviare
    /// in sicurezza, e dopo Dispose ogni invio lancia ObjectDisposedException.
    /// </summary>
    public class UscitaMidiDryWet : IUscitaMidi, IUscitaMidiPreparabile, IDisposable
    {
        private readonly OutputDevice dispositivo;
        private readonly GuardiaRilascio guardia = new GuardiaRilascio("Uscita MIDI");

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

            Invia(evento);
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

            Invia(evento);
        }

        /// <inheritdoc />
        public void Clock()
        {
            Invia(new TimingClockEvent());
        }

        /// <inheritdoc />
        public void Start()
        {
            Invia(new StartEvent());
        }

        /// <inheritdoc />
        public void Stop()
        {
            Invia(new StopEvent());
        }

        /// <inheritdoc />
        public void Continue()
        {
            Invia(new ContinueEvent());
        }

        /// <summary>Rilascia la porta MIDI di uscita. Attende l'eventuale invio in corso.</summary>
        public void Dispose()
        {
            guardia.Rilascia(() => dispositivo.Dispose());
        }

        private void Invia(MidiEvent evento)
        {
            guardia.Esegui(() => dispositivo.SendEvent(evento));
        }

        // Delegate e messaggio sono costruiti prima della pubblicazione della partitura.
        private Action PreparaInvio(MidiEvent evento)
        {
            Action invio = () => dispositivo.SendEvent(evento);
            return () => guardia.Esegui(invio);
        }
        public Action PreparaNotaOn(int canale, int nota, int velocity) => PreparaInvio(new NoteOnEvent(
            (SevenBitNumber)ConversioneMidi.Nota(nota), (SevenBitNumber)ConversioneMidi.Velocity(velocity))
            { Channel = (FourBitNumber)ConversioneMidi.Canale(canale) });
        public Action PreparaNotaOff(int canale, int nota) => PreparaInvio(new NoteOffEvent(
            (SevenBitNumber)ConversioneMidi.Nota(nota), (SevenBitNumber)0) { Channel = (FourBitNumber)ConversioneMidi.Canale(canale) });
        public Action PreparaClock() => PreparaInvio(new TimingClockEvent());
        public Action PreparaStart() => PreparaInvio(new StartEvent());
        public Action PreparaStop() => PreparaInvio(new StopEvent());
        public Action PreparaContinue() => PreparaInvio(new ContinueEvent());

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
