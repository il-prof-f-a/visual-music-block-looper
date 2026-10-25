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

            return false;
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

                default:
                    return false;
            }
        }

        private static bool TryParseNota(string[] campi, out PayloadCartoncino payload)
        {
            payload = null;

            if (campi.Length != 3 || !TryParseNumero(campi[2], out int idClip))
            {
                return false;
            }

            payload = new PayloadCartoncino { Tipo = TipoCartoncino.Nota, IdClip = idClip };
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
