using System;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>Ingresso MIDI dal DAW: eventi di clock e di trasporto ricevuti.</summary>
    public interface IIngressoMidi
    {
        /// <summary>Sollevato a ogni clock MIDI ricevuto.</summary>
        event Action ClockRicevuto;

        /// <summary>Sollevato alla ricezione di start.</summary>
        event Action StartRicevuto;

        /// <summary>Sollevato alla ricezione di stop.</summary>
        event Action StopRicevuto;

        /// <summary>Sollevato alla ricezione di continue.</summary>
        event Action ContinueRicevuto;
    }
}
