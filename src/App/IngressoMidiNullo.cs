using System;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Ingresso MIDI che non solleva mai eventi; usato quando la porta reale non è disponibile.</summary>
    public sealed class IngressoMidiNullo : IIngressoMidi
    {
#pragma warning disable CS0067 // Eventi mai sollevati per costruzione.
        public event Action ClockRicevuto;

        public event Action StartRicevuto;

        public event Action StopRicevuto;

        public event Action ContinueRicevuto;
#pragma warning restore CS0067
    }
}
