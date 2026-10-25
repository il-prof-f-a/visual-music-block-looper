using System;
using System.Collections.Generic;

namespace VisualMusicBlockLooper.App
{
    /// <summary>
    /// Misura i frame al secondo su una finestra mobile di istanti registrati.
    /// Thread-safe: <see cref="Registra"/> può arrivare da un thread in background, <see cref="Fps"/> dal thread UI.
    /// </summary>
    public sealed class MisuraFps
    {
        /// <summary>Durata minima della finestra, in millisecondi.</summary>
        private const int FinestraMinimaMs = 100;

        private readonly Func<long> _orologioMs;
        private readonly int _finestraMs;
        private readonly List<long> _istanti = new List<long>();
        private readonly object _blocco = new object();

        /// <summary>Crea una misura con l'orologio indicato (millisecondi) e la finestra di calcolo.</summary>
        /// <exception cref="ArgumentNullException">L'orologio è nullo.</exception>
        /// <exception cref="ArgumentOutOfRangeException">La finestra è inferiore a 100 ms.</exception>
        public MisuraFps(Func<long> orologioMs, int finestraMs = 1000)
        {
            if (orologioMs == null)
            {
                throw new ArgumentNullException(nameof(orologioMs));
            }

            if (finestraMs < FinestraMinimaMs)
            {
                throw new ArgumentOutOfRangeException(nameof(finestraMs), finestraMs, "La finestra deve essere di almeno 100 ms.");
            }

            _orologioMs = orologioMs;
            _finestraMs = finestraMs;
        }

        /// <summary>Frame al secondo calcolati sugli eventi della finestra; 0 se ce ne sono meno di due.</summary>
        public double Fps
        {
            get
            {
                lock (_blocco)
                {
                    ScartaVecchi(_orologioMs());

                    int n = _istanti.Count;
                    if (n < 2)
                    {
                        return 0;
                    }

                    long primo = _istanti[0];
                    long ultimo = _istanti[n - 1];
                    if (ultimo == primo)
                    {
                        return 0;
                    }

                    return (n - 1) / ((ultimo - primo) / 1000.0);
                }
            }
        }

        /// <summary>Registra un evento (un frame) all'istante corrente dell'orologio.</summary>
        public void Registra()
        {
            lock (_blocco)
            {
                long ora = _orologioMs();
                _istanti.Add(ora);
                ScartaVecchi(ora);
            }
        }

        /// <summary>Elimina tutti gli eventi registrati.</summary>
        public void Azzera()
        {
            lock (_blocco)
            {
                _istanti.Clear();
            }
        }

        /// <summary>Scarta gli eventi non più dentro la finestra (timestamp &lt;= ora - finestra). Va chiamato con il lock.</summary>
        private void ScartaVecchi(long ora)
        {
            long limite = ora - _finestraMs;
            _istanti.RemoveAll(istante => istante <= limite);
        }
    }
}
