using System.Collections.Generic;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>Sorgente dei cartoncini individuati sul tavolo in un dato istante.</summary>
    public interface IRilevatoreCartoncini
    {
        /// <summary>Rileva i cartoncini presenti nell'ultimo frame disponibile.</summary>
        IReadOnlyList<CartoncinoRilevato> Rileva();
    }
}
