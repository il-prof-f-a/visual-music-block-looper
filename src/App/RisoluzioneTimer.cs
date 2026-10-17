using System;
using System.Runtime.InteropServices;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Alza la risoluzione del timer di Windows a 1 ms (winmm); se non disponibile non fa nulla.</summary>
    public static class RisoluzioneTimer
    {
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint periodo);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint periodo);

        /// <summary>Richiede 1 ms; restituisce true se la richiesta è andata a buon fine (da bilanciare con <see cref="Rilascia"/>).</summary>
        public static bool Richiedi()
        {
            try
            {
                return TimeBeginPeriod(1) == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Rilascia una richiesta ottenuta con <see cref="Richiedi"/>.</summary>
        public static void Rilascia()
        {
            try
            {
                TimeEndPeriod(1);
            }
            catch (Exception)
            {
                // winmm non disponibile: nulla da rilasciare.
            }
        }
    }
}
