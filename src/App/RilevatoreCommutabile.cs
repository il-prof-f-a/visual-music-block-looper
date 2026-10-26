using System.Collections.Generic;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    /// <summary>
    /// Rilevatore sostituibile a caldo: finché non ne viene impostato uno restituisce un tavolo vuoto.
    /// Permette di completare la calibrazione senza ricreare il compositore.
    /// </summary>
    public sealed class RilevatoreCommutabile : IRilevatoreDiagnostico
    {
        private static readonly IReadOnlyList<CartoncinoRilevato> Vuoto = new List<CartoncinoRilevato>();

        private volatile IRilevatoreCartoncini corrente;

        /// <summary>True se è stato impostato un rilevatore reale.</summary>
        public bool Attivo => corrente != null;

        /// <summary>Calibrazione del rilevatore attivo (serve a raddrizzare l'anteprima), o null.</summary>
        public Calibrazione CalibrazioneAttiva => (corrente as RilevatoreCartoncini)?.Calibrazione;

        /// <summary>Imposta il rilevatore da usare; null torna al tavolo vuoto.</summary>
        public void Imposta(IRilevatoreCartoncini rilevatore)
        {
            corrente = rilevatore;
        }

        public IReadOnlyList<CartoncinoRilevato> Rileva(TracciaCartoncini traccia)
        {
            IRilevatoreCartoncini rilevatore = corrente;
            if (rilevatore == null)
            {
                if (traccia != null) { traccia.CalibrazionePresente = false; traccia.EsitoVisione = "CalibrazioneAssente"; }
                return Vuoto;
            }
            if (rilevatore is IRilevatoreDiagnostico diagnostico) return diagnostico.Rileva(traccia);
            if (traccia != null) traccia.EsitoVisione = "RilevatoreNonDiagnostico";
            return rilevatore.Rileva();
        }

        public IReadOnlyList<CartoncinoRilevato> Rileva()
        {
            IRilevatoreCartoncini rilevatore = corrente;
            return rilevatore == null ? Vuoto : rilevatore.Rileva();
        }
    }
}
