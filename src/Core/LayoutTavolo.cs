namespace VisualMusicBlockLooper.Core
{
    /// <summary>Zona speciale del tavolo in cui può cadere il centro di un cartoncino.</summary>
    public enum ZonaSpeciale
    {
        Nessuna,
        Misura,
        Collegamento,
        Ab,
        Comandi,
    }

    /// <summary>Disposizione normalizzata delle zone speciali del tavolo e dell'area della griglia.</summary>
    public sealed class LayoutTavolo
    {
        private static readonly RettangoloTavolo ZonaVuota = new RettangoloTavolo(-1, -1, 0, 0);

        public LayoutTavolo(RettangoloTavolo misura, RettangoloTavolo collegamento, RettangoloTavolo ab, RettangoloTavolo comandi)
        {
            Misura = misura;
            Collegamento = collegamento;
            Ab = ab;
            Comandi = comandi;
        }

        /// <summary>Layout di default dei Global Constraints.</summary>
        public static LayoutTavolo Default { get; } = new LayoutTavolo(
            new RettangoloTavolo(0.01, 0.04, 0.12, 0.28),
            new RettangoloTavolo(0.01, 0.36, 0.12, 0.28),
            new RettangoloTavolo(0.01, 0.68, 0.12, 0.28),
            new RettangoloTavolo(0.84, 0.04, 0.14, 0.92));

        /// <summary>Area di default della griglia musicale.</summary>
        public static RettangoloTavolo AreaGrigliaDefault { get; } = new RettangoloTavolo(0.15, 0.04, 0.67, 0.92);

        /// <summary>
        /// Layout con le sole zone comandi: le tre zone speciali restano vuote e non raggiungibili.
        /// Il sentinella (-1, -1, 0, 0) assume coordinate normalizzate (0..1): un centro di cartoncino
        /// (sempre >= 0) non può cadere in una zona vuota con quelle coordinate.
        /// </summary>
        public static LayoutTavolo ConSoloComandi(RettangoloTavolo comandi)
        {
            return new LayoutTavolo(ZonaVuota, ZonaVuota, ZonaVuota, comandi);
        }

        public RettangoloTavolo Misura { get; }

        public RettangoloTavolo Collegamento { get; }

        public RettangoloTavolo Ab { get; }

        public RettangoloTavolo Comandi { get; }

        /// <summary>
        /// Zona che contiene il centro di <paramref name="area"/> (estremi inclusi).
        /// Ordine di controllo: Misura, Collegamento, Ab, Comandi; altrimenti Nessuna.
        /// </summary>
        public ZonaSpeciale ZonaDelCentro(RettangoloTavolo area)
        {
            double cx = area.X + area.Larghezza / 2;
            double cy = area.Y + area.Altezza / 2;

            if (Contiene(Misura, cx, cy)) return ZonaSpeciale.Misura;
            if (Contiene(Collegamento, cx, cy)) return ZonaSpeciale.Collegamento;
            if (Contiene(Ab, cx, cy)) return ZonaSpeciale.Ab;
            if (Contiene(Comandi, cx, cy)) return ZonaSpeciale.Comandi;
            return ZonaSpeciale.Nessuna;
        }

        private static bool Contiene(RettangoloTavolo r, double x, double y)
        {
            return x >= r.X && x <= r.X + r.Larghezza && y >= r.Y && y <= r.Y + r.Altezza;
        }
    }
}
