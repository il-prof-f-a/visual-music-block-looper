using System;
using System.Collections.Generic;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>Qualità di un accordo codificata sul cartoncino.</summary>
    public enum TipoAccordo
    {
        Maggiore,
        Minore,
        Settima,
        MinoreSettima,
        MaggioreSettima,
        Diminuito,
        DiminuitoSettima,
        Aumentato,
        SospesaSeconda,
        SospesaQuarta,
        Sesta,
        MinoreSesta
    }

    /// <summary>
    /// Accordi del tavolo. Il fondamentale è una classe di altezza 0..11 e la voce parte da Do3
    /// (MIDI 48): tutti i cartoncini di accordo usano quindi il colore del terzo registro.
    /// </summary>
    public static class Accordi
    {
        /// <summary>MIDI del Do3, base di tutte le voci di accordo.</summary>
        public const int MidiBase = 48;

        private static readonly Dictionary<TipoAccordo, int[]> Intervalli = new Dictionary<TipoAccordo, int[]>
        {
            { TipoAccordo.Maggiore, new[] { 0, 4, 7 } },
            { TipoAccordo.Minore, new[] { 0, 3, 7 } },
            { TipoAccordo.Settima, new[] { 0, 4, 7, 10 } },
            { TipoAccordo.MinoreSettima, new[] { 0, 3, 7, 10 } },
            { TipoAccordo.MaggioreSettima, new[] { 0, 4, 7, 11 } },
            { TipoAccordo.Diminuito, new[] { 0, 3, 6 } },
            { TipoAccordo.DiminuitoSettima, new[] { 0, 3, 6, 9 } },
            { TipoAccordo.Aumentato, new[] { 0, 4, 8 } },
            { TipoAccordo.SospesaSeconda, new[] { 0, 2, 7 } },
            { TipoAccordo.SospesaQuarta, new[] { 0, 5, 7 } },
            { TipoAccordo.Sesta, new[] { 0, 4, 7, 9 } },
            { TipoAccordo.MinoreSesta, new[] { 0, 3, 7, 9 } }
        };

        /// <summary>Note MIDI dell'accordo, a partire da Do3 + classe di altezza.</summary>
        public static IReadOnlyList<int> Note(int fondamentale, TipoAccordo tipo)
        {
            int pc = ((fondamentale % 12) + 12) % 12;
            int[] intervalli = Intervalli[tipo];
            int[] note = new int[intervalli.Length];
            for (int i = 0; i < intervalli.Length; i++)
            {
                note[i] = Math.Min(127, MidiBase + pc + intervalli[i]);
            }

            return note;
        }

        /// <summary>Codice compatto stampato nel QR: +, -, 7, m7, M7, dim, dim7, aug, sus2, sus4, 6, m6.</summary>
        public static string Codice(TipoAccordo tipo)
        {
            switch (tipo)
            {
                case TipoAccordo.Maggiore: return "+";
                case TipoAccordo.Minore: return "-";
                case TipoAccordo.Settima: return "7";
                case TipoAccordo.MinoreSettima: return "m7";
                case TipoAccordo.MaggioreSettima: return "M7";
                case TipoAccordo.Diminuito: return "dim";
                case TipoAccordo.DiminuitoSettima: return "dim7";
                case TipoAccordo.Aumentato: return "aug";
                case TipoAccordo.SospesaSeconda: return "sus2";
                case TipoAccordo.SospesaQuarta: return "sus4";
                case TipoAccordo.Sesta: return "6";
                default: return "m6";
            }
        }

        /// <summary>Simbolo leggibile per l'etichetta del cartoncino.</summary>
        public static string Simbolo(TipoAccordo tipo)
        {
            switch (tipo)
            {
                case TipoAccordo.Maggiore: return "+";
                case TipoAccordo.Minore: return "-";
                case TipoAccordo.Settima: return "7";
                case TipoAccordo.MinoreSettima: return "-7";
                case TipoAccordo.MaggioreSettima: return "+7";
                case TipoAccordo.Diminuito: return "dim";
                case TipoAccordo.DiminuitoSettima: return "dim7";
                case TipoAccordo.Aumentato: return "aug";
                case TipoAccordo.SospesaSeconda: return "sus2";
                case TipoAccordo.SospesaQuarta: return "sus4";
                case TipoAccordo.Sesta: return "6";
                default: return "-6";
            }
        }
    }
}
