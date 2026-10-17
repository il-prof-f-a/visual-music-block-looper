using System;

namespace VisualMusicBlockLooper.Midi
{
    /// <summary>
    /// Serializza invii, ricezioni e rilascio di una risorsa MIDI. Dopo il rilascio
    /// le operazioni non vengono più eseguite (invio: eccezione; ricezione: ignorata).
    /// Il rilascio attende il completamento di un'operazione in corso.
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

        /// <summary>True una volta eseguito il rilascio.</summary>
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

        /// <summary>Esegue l'azione se la risorsa non è rilasciata, altrimenti lancia ObjectDisposedException.</summary>
        /// <param name="azione">Operazione da eseguire in modo sincronizzato.</param>
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

        /// <summary>Esegue l'azione solo se la risorsa non è rilasciata.</summary>
        /// <param name="azione">Operazione da eseguire in modo sincronizzato.</param>
        /// <returns>False se la risorsa è già rilasciata (azione non eseguita).</returns>
        public bool EseguiSeAttivo(Action azione)
        {
            lock (blocco)
            {
                if (rilasciato)
                {
                    return false;
                }

                azione();
                return true;
            }
        }

        /// <summary>Esegue il rilascio una sola volta; le chiamate successive non fanno nulla.</summary>
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
                rilascio();
            }
        }
    }
}
