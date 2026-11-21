using System;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>Parser del payload testuale dei cartoncini QR (formato VMBL1;...).</summary>
    public static class ParserPayload
    {
        private const string Prefisso = "VMBL1";
        private const int NotaMinima = 0;
        private const int NotaMassima = 127;

        /// <summary>
        /// Tenta di decodificare il payload. Non lancia mai eccezioni: restituisce false
        /// per input nulli, vuoti, malformati o fuori intervallo.
        /// </summary>
        public static bool TryParse(string testo, out PayloadCartoncino payload)
        {
            payload = null;

            if (string.IsNullOrWhiteSpace(testo))
            {
                return false;
            }

            string[] campi = testo.Trim().Split(';');
            if (campi.Length < 3 || campi[0] != Prefisso)
            {
                return false;
            }

            string tipo = campi[1];
            if (string.Equals(tipo, "N", StringComparison.Ordinal))
            {
                return TryParseNota(campi, out payload);
            }

            if (string.Equals(tipo, "C", StringComparison.Ordinal))
            {
                return TryParseComando(campi, out payload);
            }

            if (string.Equals(tipo, "S", StringComparison.Ordinal))
            {
                return TryParseImpostazione(campi, out payload);
            }

            if (string.Equals(tipo, "A", StringComparison.Ordinal))
            {
                return TryParseAccordo(campi, out payload);
            }

            return false;
        }

        private static bool TryParseAccordo(string[] campi, out PayloadCartoncino payload)
        {
            payload = null;
            if ((campi.Length != 4 && campi.Length != 5)
                || !TryParseNumero(campi[2], out int fondamentale)
                || fondamentale < 0 || fondamentale > 11
                || !TryParseTipoAccordo(campi[3], out TipoAccordo tipo))
            {
                return false;
            }

            int trasposizioneOttave = 0;
            if (campi.Length == 5
                && (!TryParseNumero(campi[4], out trasposizioneOttave)
                    || trasposizioneOttave < 0 || trasposizioneOttave > 1))
            {
                return false;
            }

            payload = new PayloadCartoncino
            {
                Tipo = TipoCartoncino.Accordo,
                Fondamentale = fondamentale,
                Accordo = tipo,
                TrasposizioneOttave = trasposizioneOttave
            };
            return true;
        }

        private static bool TryParseTipoAccordo(string testo, out TipoAccordo tipo)
        {
            // I simboli distinguono maiuscole e minuscole: M7 è maggiore, m7 è minore.
            switch (testo)
            {
                case "+": tipo = TipoAccordo.Maggiore; return true;
                case "-": tipo = TipoAccordo.Minore; return true;
                case "7": tipo = TipoAccordo.Settima; return true;
                case "m7": tipo = TipoAccordo.MinoreSettima; return true;
                case "M7": tipo = TipoAccordo.MaggioreSettima; return true;
                case "dim": tipo = TipoAccordo.Diminuito; return true;
                case "dim7": tipo = TipoAccordo.DiminuitoSettima; return true;
                case "aug": tipo = TipoAccordo.Aumentato; return true;
                case "sus2": tipo = TipoAccordo.SospesaSeconda; return true;
                case "sus4": tipo = TipoAccordo.SospesaQuarta; return true;
                case "6": tipo = TipoAccordo.Sesta; return true;
                case "m6": tipo = TipoAccordo.MinoreSesta; return true;
            }

            switch (testo.ToLowerInvariant())
            {
                case "maj":
                case "major":
                case "maggiore": tipo = TipoAccordo.Maggiore; return true;
                case "min":
                case "minor":
                case "minore": tipo = TipoAccordo.Minore; return true;
                case "dom7":
                case "settima": tipo = TipoAccordo.Settima; return true;
                case "min7":
                case "minoresettima": tipo = TipoAccordo.MinoreSettima; return true;
                case "maj7":
                case "magioresettima": tipo = TipoAccordo.MaggioreSettima; return true;
                case "diminuito": tipo = TipoAccordo.Diminuito; return true;
                case "diminuitosettima": tipo = TipoAccordo.DiminuitoSettima; return true;
                case "aumentato":
                case "augmented": tipo = TipoAccordo.Aumentato; return true;
                case "sospesaseconda": tipo = TipoAccordo.SospesaSeconda; return true;
                case "sospesaquarta": tipo = TipoAccordo.SospesaQuarta; return true;
                case "sesta": tipo = TipoAccordo.Sesta; return true;
                case "minoresesta": tipo = TipoAccordo.MinoreSesta; return true;
                default: tipo = TipoAccordo.Maggiore; return false;
            }
        }

        private static bool TryParseImpostazione(string[] campi, out PayloadCartoncino payload)
        {
            payload = null;

            string chiave = campi[2].ToUpperInvariant();
            switch (chiave)
            {
                case "MISURA":
                case "LINK":
                    if (campi.Length != 4 || !TryParseNumero(campi[3], out int valore))
                    {
                        return false;
                    }

                    payload = new PayloadCartoncino
                    {
                        Tipo = TipoCartoncino.Impostazione,
                        Impostazione = chiave == "MISURA"
                            ? TipoImpostazione.Misura
                            : TipoImpostazione.Collegamento,
                        Valore = valore
                    };
                    return true;

                case "AB":
                    if (campi.Length != 5
                        || !TryParseNumero(campi[3], out int primo)
                        || !TryParseNumero(campi[4], out int secondo))
                    {
                        return false;
                    }

                    payload = new PayloadCartoncino
                    {
                        Tipo = TipoCartoncino.Impostazione,
                        Impostazione = TipoImpostazione.IntervalloAb,
                        Valore = primo,
                        ValoreB = secondo
                    };
                    return true;

                case "CANALE":
                case "CH":
                    if (campi.Length != 4
                        || !TryParseNumero(campi[3], out int canale)
                        || canale < 1 || canale > 16)
                    {
                        return false;
                    }

                    payload = new PayloadCartoncino
                    {
                        Tipo = TipoCartoncino.Impostazione,
                        Impostazione = TipoImpostazione.Canale,
                        Valore = canale
                    };
                    return true;

                case "STRUMENTO":
                case "ST":
                    string strumento = campi.Length == 4 ? campi[3].Trim() : "";
                    if (strumento.Length < 1 || strumento.Length > 16)
                    {
                        return false;
                    }

                    payload = new PayloadCartoncino
                    {
                        Tipo = TipoCartoncino.Impostazione,
                        Impostazione = TipoImpostazione.Strumento,
                        Strumento = strumento
                    };
                    return true;

                default:
                    return false;
            }
        }

        private static bool TryParseNota(string[] campi, out PayloadCartoncino payload)
        {
            payload = null;

            if ((campi.Length != 3 && campi.Length != 4)
                || !TryParseNumero(campi[2], out int idClip))
            {
                return false;
            }

            int nota = -1;
            if (campi.Length == 4
                && (!TryParseNumero(campi[3], out nota) || nota < NotaMinima || nota > NotaMassima))
            {
                return false;
            }

            payload = new PayloadCartoncino { Tipo = TipoCartoncino.Nota, IdClip = idClip, NotaMidi = nota };
            return true;
        }

        private static bool TryParseComando(string[] campi, out PayloadCartoncino payload)
        {
            payload = null;

            string nome = campi[2].ToUpperInvariant();
            switch (nome)
            {
                case "PLAY":
                case "STOP":
                case "TAP":
                case "MODO":
                    if (campi.Length != 3)
                    {
                        return false;
                    }

                    payload = new PayloadCartoncino
                    {
                        Tipo = TipoCartoncino.Comando,
                        Comando = MappaComandoSenzaParametro(nome)
                    };
                    return true;

                case "ONESHOT":
                    if (campi.Length != 4
                        || !TryParseNumero(campi[3], out int nota)
                        || nota < NotaMinima
                        || nota > NotaMassima)
                    {
                        return false;
                    }

                    payload = new PayloadCartoncino
                    {
                        Tipo = TipoCartoncino.Comando,
                        Comando = TipoComando.OneShot,
                        Parametro = nota
                    };
                    return true;

                default:
                    return false;
            }
        }

        private static TipoComando MappaComandoSenzaParametro(string nome)
        {
            switch (nome)
            {
                case "PLAY":
                    return TipoComando.Play;
                case "STOP":
                    return TipoComando.Stop;
                case "TAP":
                    return TipoComando.Tap;
                default:
                    return TipoComando.Modo;
            }
        }

        /// <summary>Accetta solo cifre ASCII '0'-'9', senza segno, entro il range di int.</summary>
        private static bool TryParseNumero(string testo, out int valore)
        {
            valore = 0;

            if (string.IsNullOrEmpty(testo))
            {
                return false;
            }

            foreach (char c in testo)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }

            return int.TryParse(testo, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out valore);
        }
    }
}
