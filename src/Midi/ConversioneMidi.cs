namespace VisualMusicBlockLooper.Midi
{
    /// <summary>Conversioni pure verso i range MIDI validi, testabili senza hardware.</summary>
    public static class ConversioneMidi
    {
        /// <summary>Limita un canale MIDI al range 0..15.</summary>
        public static int Canale(int canale)
        {
            return Limita(canale, 0, 15);
        }

        /// <summary>Limita un numero di nota MIDI al range 0..127.</summary>
        public static int Nota(int nota)
        {
            return Limita(nota, 0, 127);
        }

        /// <summary>Limita una velocity MIDI al range 0..127.</summary>
        public static int Velocity(int velocity)
        {
            return Limita(velocity, 0, 127);
        }

        private static int Limita(int valore, int minimo, int massimo)
        {
            if (valore < minimo)
            {
                return minimo;
            }

            return valore > massimo ? massimo : valore;
        }
    }
}
