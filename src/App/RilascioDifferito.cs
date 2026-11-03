using System;
using System.Threading;

namespace VisualMusicBlockLooper.App
{
    /// <summary>La chiusura non libera Mat o decoder ancora posseduti da una chiamata nativa in corso.</summary>
    public sealed class RilascioDifferito : IDisposable
    {
        private readonly Action libera;
        private int utenti, richiesto, liberato;
        public RilascioDifferito(Action libera){this.libera=libera??throw new ArgumentNullException(nameof(libera));}
        public bool InChiusura=>Volatile.Read(ref richiesto)!=0;
        public bool ProvaEntra()
        {
            if(InChiusura)return false;
            Interlocked.Increment(ref utenti);
            if(!InChiusura)return true;
            Esci();return false;
        }
        public void Esci(){if(Interlocked.Decrement(ref utenti)==0&&InChiusura)Libera();}
        public void Dispose(){Volatile.Write(ref richiesto,1);if(Volatile.Read(ref utenti)==0)Libera();}
        private void Libera(){if(Interlocked.Exchange(ref liberato,1)==0)libera();}
    }
}
