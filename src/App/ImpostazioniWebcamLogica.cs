using System;

namespace VisualMusicBlockLooper.App
{
    /// <summary>
    /// Applica e rilegge fuoco ed esposizione su una webcam. I driver DirectShow spesso dichiarano di aver
    /// accettato un valore che poi ignorano, quindi dopo ogni impostazione si rilegge: una proprietà è
    /// «supportata» solo se l'impostazione è riuscita E il valore riletto è coerente con quello richiesto.
    /// Non lancia mai eccezioni: gli errori diventano «non supportato».
    /// </summary>
    public sealed class ImpostazioniWebcamLogica
    {
        private const double ToleranzaFuoco = 3;
        private const double ToleranzaEsposizione = 0.5;
        private const double ToleranzaFlag = 0.01;

        /// <summary>
        /// Codifiche del flag di auto-esposizione {automatica, manuale}. I driver DirectShow di OpenCV non sono
        /// uniformi: alcuni usano 0.75/0.25 (valori del menu DirectShow), altri 1/0. Si prova la prima e, se la
        /// rilettura non la conferma, la seconda; quella che funziona viene ricordata.
        /// </summary>
        private static readonly double[][] CodificheAutoEsposizione =
        {
            new[] { 0.75, 0.25 },
            new[] { 1.0, 0.0 }
        };

        private enum Esito { Sconosciuto, Si, No }

        private readonly IProprietaCamera camera;
        private Esito autoFuoco = Esito.Sconosciuto;
        private Esito fuoco = Esito.Sconosciuto;
        private Esito autoEsposizione = Esito.Sconosciuto;
        private Esito esposizione = Esito.Sconosciuto;
        private int codificaNota = -1;

        public ImpostazioniWebcamLogica(IProprietaCamera camera)
        {
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
        }

        /// <summary>Applica solo i campi non null di fuoco ed esposizione (la risoluzione non si cambia qui).</summary>
        public void Applica(ConfigurazioneWebcam impostazioni)
        {
            if (impostazioni == null)
            {
                return;
            }

            try
            {
                if (impostazioni.FuocoAutomatico.HasValue)
                {
                    autoFuoco = Prova(ProprietaCamera.FuocoAutomatico, impostazioni.FuocoAutomatico.Value ? 1 : 0, ToleranzaFlag);
                }

                if (impostazioni.Fuoco.HasValue && impostazioni.FuocoAutomatico != true)
                {
                    fuoco = Prova(ProprietaCamera.Fuoco, ImpostazioniCamera.LimitaFuoco(impostazioni.Fuoco.Value), ToleranzaFuoco);
                }

                if (impostazioni.EsposizioneAutomatica.HasValue)
                {
                    autoEsposizione = ImpostaAutoEsposizione(impostazioni.EsposizioneAutomatica.Value);
                }

                if (impostazioni.Esposizione.HasValue && impostazioni.EsposizioneAutomatica != true)
                {
                    esposizione = Prova(
                        ProprietaCamera.Esposizione,
                        ImpostazioniCamera.LimitaEsposizione(impostazioni.Esposizione.Value),
                        ToleranzaEsposizione);
                }
            }
            catch (Exception)
            {
                // Nessun errore verso l'alto: ciò che non è stato confermato resta com'era.
            }
        }

        /// <summary>Rilegge i valori effettivi; ciò che non è mai stato provato viene verificato rimpostando il valore corrente.</summary>
        public StatoImpostazioniWebcam Leggi()
        {
            StatoImpostazioniWebcam stato = new StatoImpostazioniWebcam();
            try
            {
                if (autoFuoco == Esito.Sconosciuto)
                {
                    double v = Rileggi(ProprietaCamera.FuocoAutomatico);
                    autoFuoco = IsFinito(v) ? Prova(ProprietaCamera.FuocoAutomatico, v >= 0.5 ? 1 : 0, ToleranzaFlag) : Esito.No;
                }

                if (fuoco == Esito.Sconosciuto)
                {
                    double v = Rileggi(ProprietaCamera.Fuoco);
                    fuoco = IsFinito(v) ? Prova(ProprietaCamera.Fuoco, v, ToleranzaFuoco) : Esito.No;
                }

                if (autoEsposizione == Esito.Sconosciuto)
                {
                    autoEsposizione = SondaAutoEsposizione();
                }

                if (esposizione == Esito.Sconosciuto)
                {
                    double v = Rileggi(ProprietaCamera.Esposizione);
                    esposizione = IsFinito(v) ? Prova(ProprietaCamera.Esposizione, v, ToleranzaEsposizione) : Esito.No;
                }

                stato.FuocoSupportato = autoFuoco == Esito.Si || fuoco == Esito.Si;
                stato.EsposizioneSupportata = autoEsposizione == Esito.Si || esposizione == Esito.Si;

                if (autoFuoco == Esito.Si)
                {
                    stato.FuocoAutomatico = Rileggi(ProprietaCamera.FuocoAutomatico) >= 0.5;
                }

                if (fuoco == Esito.Si)
                {
                    stato.Fuoco = (int)Math.Round(Rileggi(ProprietaCamera.Fuoco));
                }

                if (autoEsposizione == Esito.Si && codificaNota >= 0)
                {
                    double valoreAuto = CodificheAutoEsposizione[codificaNota][0];
                    stato.EsposizioneAutomatica = Math.Abs(Rileggi(ProprietaCamera.EsposizioneAutomatica) - valoreAuto) < ToleranzaFlag;
                }

                if (esposizione == Esito.Si)
                {
                    stato.Esposizione = Rileggi(ProprietaCamera.Esposizione);
                }

                double fps = Rileggi(ProprietaCamera.Fps);
                stato.FpsDichiarati = IsFinito(fps) && fps > 0 ? fps : 0;
            }
            catch (Exception)
            {
                return StatoImpostazioniWebcam.NonSupportato();
            }

            return stato;
        }

        private Esito ImpostaAutoEsposizione(bool automatica)
        {
            int indiceValore = automatica ? 0 : 1;
            foreach (int c in OrdineCodifiche())
            {
                if (ProvaEsito(ProprietaCamera.EsposizioneAutomatica, CodificheAutoEsposizione[c][indiceValore], ToleranzaFlag))
                {
                    codificaNota = c;
                    return Esito.Si;
                }
            }

            return Esito.No;
        }

        private Esito SondaAutoEsposizione()
        {
            double v = Rileggi(ProprietaCamera.EsposizioneAutomatica);
            if (!IsFinito(v))
            {
                return Esito.No;
            }

            for (int c = 0; c < CodificheAutoEsposizione.Length; c++)
            {
                foreach (double candidato in CodificheAutoEsposizione[c])
                {
                    if (Math.Abs(v - candidato) < ToleranzaFlag
                        && ProvaEsito(ProprietaCamera.EsposizioneAutomatica, candidato, ToleranzaFlag))
                    {
                        codificaNota = c;
                        return Esito.Si;
                    }
                }
            }

            return Esito.No;
        }

        private int[] OrdineCodifiche()
        {
            return codificaNota == 1 ? new[] { 1, 0 } : new[] { 0, 1 };
        }

        private Esito Prova(ProprietaCamera proprieta, double valore, double tolleranza)
        {
            return ProvaEsito(proprieta, valore, tolleranza) ? Esito.Si : Esito.No;
        }

        private bool ProvaEsito(ProprietaCamera proprieta, double valore, double tolleranza)
        {
            if (valore == 0 && (proprieta == ProprietaCamera.FuocoAutomatico || proprieta == ProprietaCamera.EsposizioneAutomatica))
            {
                // Uno 0 riletto coincide col valore di un driver che ignora tutto: lo si conferma accendendo
                // e rispegnendo il flag, altrimenti «manuale» sembrerebbe sempre riuscito.
                return ProvaSemplice(proprieta, 1, tolleranza) && ProvaSemplice(proprieta, 0, tolleranza);
            }

            return ProvaSemplice(proprieta, valore, tolleranza);
        }

        private bool ProvaSemplice(ProprietaCamera proprieta, double valore, double tolleranza)
        {
            try
            {
                if (!camera.Imposta(proprieta, valore))
                {
                    return false;
                }

                double riletto = camera.Leggi(proprieta);
                return IsFinito(riletto) && Math.Abs(riletto - valore) <= tolleranza;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private double Rileggi(ProprietaCamera proprieta)
        {
            try
            {
                return camera.Leggi(proprieta);
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        private static bool IsFinito(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v);
        }
    }
}
