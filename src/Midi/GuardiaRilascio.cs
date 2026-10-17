using System;

namespace VisualMusicBlockLooper.Midi
{
    /// <summary>
    /// Gestisce invii, ricezioni e rilascio di una risorsa MIDI.
    /// <para>
    /// Il lock protegge solo il flag di rilascio: l'invio (<see cref="Esegui"/>) è sincrono
    /// e viene eseguito sotto lock, mentre le callback di ricezione (<see cref="EseguiSeAttivo"/>)
    /// e l'azione di rilascio (<see cref="Rilascia"/>) vengono eseguite FUORI dal lock, per evitare
    /// deadlock con il thread del dispositivo. Un evento che arriva durante il rilascio
    /// può quindi essere consegnato una sola volta dopo l'avvio di Dispose.
    /// </para>
    /// </summary>
    public sealed class GuardiaRilascio
    {
        private readonly object blocco = new object();
        private readonly string nomeRisorsa;
        private bool rilasciato;

        /// <summary>Crea una guardia per la risorsa indicata.</summary>
        /// <param name="nomeRisorsa">Nome della risorsa, usato nei messaggi di errore.</param>
        public GuardiaRilascio(string nomeRisorsa)
        {
            this.nomeRisorsa = nomeRisorsa;
        }

        /// <summary>True una volta avviato il rilascio.</summary>
        public bool Rilasciato
        {
            get
            {
                lock (blocco)
                {
                    return rilasciato;
                }
            }
        }

        /// <summary>
        /// Esegue l'azione sotto lock se la risorsa non è rilasciata, altrimenti lancia
        /// ObjectDisposedException. Il lock garantisce che l'azione non sia in corso durante il rilascio.
        /// </summary>
        /// <param name="azione">Operazione di invio, sincrona e senza callback verso il motore.</param>
        public void Esegui(Action azione)
        {
            lock (blocco)
            {
                if (rilasciato)
                {
                    throw new ObjectDisposedException(
                        nomeRisorsa,
                        $"{nomeRisorsa} è già stata rilasciata e non può più essere usata.");
                }

                azione();
            }
        }

        /// <summary>
        /// Controlla il flag sotto lock e, se la risorsa è attiva, esegue l'azione FUORI dal lock.
        /// </summary>
        /// <param name="azione">Callback da eseguire.</param>
        /// <returns>False se la risorsa è già rilasciata (azione non eseguita).</returns>
        public bool EseguiSeAttivo(Action azione)
        {
            bool attiva;
            lock (blocco)
            {
                attiva = !rilasciato;
            }

            if (!attiva)
            {
                return false;
            }

            azione();
            return true;
        }

        /// <summary>
        /// Segna la risorsa come rilasciata sotto lock, poi esegue l'azione di rilascio FUORI dal lock.
        /// Le chiamate successive non fanno nulla. Se l'azione lancia, l'eccezione viene propagata
        /// e la risorsa resta marcata come rilasciata.
        /// </summary>
        /// <param name="rilascio">Operazione di rilascio della risorsa.</param>
        public void Rilascia(Action rilascio)
        {
            lock (blocco)
            {
                if (rilasciato)
                {
                    return;
                }

                rilasciato = true;
            }

            rilascio();
        }
    }
}
