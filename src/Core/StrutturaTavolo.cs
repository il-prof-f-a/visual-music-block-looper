using System;
using System.Collections.Generic;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>Posizione del giro al passo assoluto dato: misura nel giro, passo nella misura e righe attive.</summary>
    public sealed class PosizioneGiro
    {
        public PosizioneGiro(int stepInMisura, int misuraNelGiro, IReadOnlyList<int> righeAttive)
        {
            StepInMisura = stepInMisura;
            MisuraNelGiro = misuraNelGiro;
            RigheAttive = righeAttive;
        }

        /// <summary>Passo (sedicesimo) all'interno della misura corrente.</summary>
        public int StepInMisura { get; }

        /// <summary>Indice della riga nel gruppo (la misura in cui si trova il giro), 0-based.</summary>
        public int MisuraNelGiro { get; }

        /// <summary>Righe attive (0-based), una per gruppo.</summary>
        public IReadOnlyList<int> RigheAttive { get; }
    }

    /// <summary>
    /// Struttura del tavolo: misura (battiti per misura), collegamento delle righe in gruppi e
    /// intervallo A-B di ripetizione. Valore immutabile.
    /// </summary>
    public sealed class StrutturaTavolo : IEquatable<StrutturaTavolo>
    {
        /// <summary>Numero di righe del tavolo.</summary>
        public const int RigheTotali = 6;

        private static readonly int[] BattitiAmmessi = { 2, 3, 4 };
        private static readonly int[] RigheCollegateAmmesse = { 1, 2, 3, 6 };

        private StrutturaTavolo(int battiti, int righeCollegate, int aInizio, int aFine)
        {
            BattitiPerMisura = battiti;
            RigheCollegate = righeCollegate;
            AInizio = aInizio;
            AFine = aFine;
        }

        /// <summary>Step (sedicesimi) in una battuta.</summary>
        public const int StepPerBattuta = 4;

        /// <summary>Struttura di default: 4/4, sei righe collegate, A-B = tutte le battute [1, 24].</summary>
        public static StrutturaTavolo Default { get; } = new StrutturaTavolo(4, RigheTotali, 1, RigheTotali * 4);

        /// <summary>
        /// Crea una struttura se i valori sono validi. A-B è un intervallo di battute (estremi inclusi) lungo
        /// le righe collegate del gruppo: deve valere 1 ≤ a &lt; b ≤ righeCollegate × battiti.
        /// </summary>
        public static bool TryCrea(int battiti, int righeCollegate, int aInizio, int aFine, out StrutturaTavolo struttura)
        {
            struttura = null;
            if (Array.IndexOf(BattitiAmmessi, battiti) < 0 || Array.IndexOf(RigheCollegateAmmesse, righeCollegate) < 0)
            {
                return false;
            }

            bool abValido = aInizio >= 1 && aInizio < aFine && aFine <= righeCollegate * battiti;
            if (!abValido)
            {
                return false;
            }

            struttura = new StrutturaTavolo(battiti, righeCollegate, aInizio, aFine);
            return true;
        }

        /// <summary>Battiti per misura (2, 3 o 4).</summary>
        public int BattitiPerMisura { get; }

        /// <summary>Righe per gruppo (1, 2, 3 o 6).</summary>
        public int RigheCollegate { get; }

        /// <summary>Prima battuta (1-based) dell'intervallo A-B, contata lungo le righe collegate del gruppo.</summary>
        public int AInizio { get; }

        /// <summary>Ultima battuta (1-based, inclusa) dell'intervallo A-B.</summary>
        public int AFine { get; }

        /// <summary>Battute del gruppo di righe collegate: righe collegate × battiti per misura.</summary>
        public int BattuteGruppo => RigheCollegate * BattitiPerMisura;

        /// <summary>Passi (sedicesimi) per misura: 4 × battiti.</summary>
        public int StepPerMisura => 4 * BattitiPerMisura;

        /// <summary>Numero di gruppi di righe collegate.</summary>
        public int NumeroGruppi => RigheTotali / RigheCollegate;

        /// <summary>Lunghezza del giro in step: le battute di A-B per 4 step.</summary>
        public int StepGiro => (AFine - AInizio + 1) * StepPerBattuta;

        /// <summary>Posizione del giro allo step assoluto dato (sedicesimi dall'avvio).</summary>
        public PosizioneGiro Posizione(long stepAssoluto)
        {
            if (stepAssoluto < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stepAssoluto), "Lo step assoluto non può essere negativo.");
            }

            // Posizione nella sequenza delle righe collegate (in step dall'inizio del gruppo): il giro parte
            // dalla battuta A, dura fino alla battuta B inclusa e poi si ripete.
            int posizioneNelGruppo = (AInizio - 1) * StepPerBattuta + (int)(stepAssoluto % StepGiro);
            int misuraNelGiro = posizioneNelGruppo / StepPerMisura;
            int stepInMisura = posizioneNelGruppo % StepPerMisura;

            var righeAttive = new int[NumeroGruppi];
            for (int gruppo = 0; gruppo < NumeroGruppi; gruppo++)
            {
                righeAttive[gruppo] = gruppo * RigheCollegate + misuraNelGiro;
            }

            return new PosizioneGiro(stepInMisura, misuraNelGiro, righeAttive);
        }

        public bool Equals(StrutturaTavolo altra)
        {
            if (ReferenceEquals(altra, null))
            {
                return false;
            }

            return BattitiPerMisura == altra.BattitiPerMisura
                && RigheCollegate == altra.RigheCollegate
                && AInizio == altra.AInizio
                && AFine == altra.AFine;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrutturaTavolo);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + BattitiPerMisura;
                hash = hash * 31 + RigheCollegate;
                hash = hash * 31 + AInizio;
                hash = hash * 31 + AFine;
                return hash;
            }
        }
    }
}
