using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Uscita MIDI che scarta ogni messaggio; usata quando la porta reale non è disponibile.</summary>
    public sealed class UscitaMidiSilenziosa : IUscitaMidi
    {
        public void NotaOn(int canale, int nota, int velocity)
        {
        }

        public void NotaOff(int canale, int nota)
        {
        }

        public void Clock()
        {
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void Continue()
        {
        }
    }
}
