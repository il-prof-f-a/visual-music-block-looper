using System;
using System.Drawing;
using OpenCvSharp;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.App
{
    /// <summary>
    /// Possiede la webcam corrente e permette di cambiarla a caldo. Fa da sorgente di immagini stabile per
    /// il rilevatore: dopo un cambio di webcam non serve ricreare nulla.
    /// </summary>
    public sealed class GestoreWebcam : IFonteImmagini, IDisposable
    {
        private readonly Func<int, int, int, ISorgenteWebcam> apri;
        private readonly object blocco = new object();
        private volatile ISorgenteWebcam corrente;
        private int indiceCorrente = -1;
        private InfoWebcam infoCorrente;
        private bool eliminato;
        private int larghezzaRichiesta;
        private int altezzaRichiesta;
        private ConfigurazioneWebcam impostazioni = new ConfigurazioneWebcam();

        /// <param name="apri">Apre la webcam con l'indice dato; restituisce null se non è disponibile.</param>
        public GestoreWebcam(Func<int, ISorgenteWebcam> apri)
        {
            if (apri == null)
            {
                throw new ArgumentNullException(nameof(apri));
            }

            // Questo costruttore ignora le dimensioni richieste.
            this.apri = (indice, larghezza, altezza) => apri(indice);
        }

        /// <param name="apri">
        /// Apre la webcam con indice, larghezza e altezza richieste (0, 0 = predefinite); restituisce null se non è disponibile.
        /// </param>
        public GestoreWebcam(Func<int, int, int, ISorgenteWebcam> apri)
        {
            this.apri = apri ?? throw new ArgumentNullException(nameof(apri));
        }

        /// <summary>True se una webcam è aperta.</summary>
        public bool Disponibile => corrente != null;

        /// <summary>Indice della webcam aperta, oppure -1.</summary>
        public int IndiceCorrente
        {
            get
            {
                lock (blocco)
                {
                    return corrente == null ? -1 : indiceCorrente;
                }
            }
        }

        /// <summary>Identità (nome, percorso, indice) della webcam aperta, oppure null.</summary>
        public InfoWebcam Info
        {
            get
            {
                lock (blocco)
                {
                    return corrente == null ? null : infoCorrente;
                }
            }
        }

        /// <summary>Larghezza dei frame della webcam corrente (0 se assente).</summary>
        public int Larghezza => corrente?.Larghezza ?? 0;

        /// <summary>Altezza dei frame della webcam corrente (0 se assente).</summary>
        public int Altezza => corrente?.Altezza ?? 0;

        /// <summary>
        /// Apre la webcam con l'indice dato e sostituisce quella corrente. Se l'apertura fallisce la webcam
        /// precedente resta attiva. Riaprire l'indice già in uso non fa nulla.
        /// </summary>
        /// <returns>True se la webcam richiesta è ora quella corrente.</returns>
        public bool Apri(int indice)
        {
            return Apri(new InfoWebcam(indice));
        }

        /// <summary>Come <see cref="Apri(int)"/> ma conserva nome e percorso della webcam scelta.</summary>
        public bool Apri(InfoWebcam webcam)
        {
            if (webcam == null)
            {
                throw new ArgumentNullException(nameof(webcam));
            }

            int indice = webcam.Indice;
            lock (blocco)
            {
                if (eliminato)
                {
                    return false;
                }

                if (corrente != null && indiceCorrente == indice)
                {
                    infoCorrente = webcam;
                    return true;
                }

                ISorgenteWebcam nuova;
                try
                {
                    nuova = ApriSorgente(indice, larghezzaRichiesta, altezzaRichiesta);
                }
                catch (Exception)
                {
                    nuova = null;
                }

                if (nuova == null)
                {
                    return false;
                }

                ISorgenteWebcam vecchia = corrente;
                corrente = nuova;
                indiceCorrente = indice;
                infoCorrente = webcam;
                vecchia?.Dispose();
                return true;
            }
        }

        /// <summary>
        /// Memorizza risoluzione richiesta e impostazioni di fuoco ed esposizione: valgono a ogni apertura
        /// successiva e vengono applicate alla sorgente appena aperta. Non tocca la webcam già aperta.
        /// </summary>
        public void ImpostaConfigurazione(ConfigurazioneWebcam configurazione)
        {
            if (configurazione == null)
            {
                throw new ArgumentNullException(nameof(configurazione));
            }

            lock (blocco)
            {
                larghezzaRichiesta = configurazione.Larghezza;
                altezzaRichiesta = configurazione.Altezza;
                impostazioni = new ConfigurazioneWebcam();
                Unisci(impostazioni, configurazione);
            }
        }

        /// <summary>
        /// Riapre la webcam corrente con le nuove dimensioni, mantenendone l'identità. Le webcam DirectShow non
        /// si aprono due volte, quindi la sorgente precedente viene rilasciata prima; se la nuova apertura
        /// fallisce si riapre la precedente con le dimensioni di prima e le nuove non vengono memorizzate.
        /// Senza webcam aperta memorizza soltanto le dimensioni.
        /// </summary>
        /// <returns>True se la webcam è stata riaperta con le nuove dimensioni.</returns>
        public bool CambiaRisoluzione(int larghezza, int altezza)
        {
            lock (blocco)
            {
                if (eliminato)
                {
                    return false;
                }

                int larghezzaPrima = larghezzaRichiesta;
                int altezzaPrima = altezzaRichiesta;
                if (corrente == null)
                {
                    larghezzaRichiesta = larghezza;
                    altezzaRichiesta = altezza;
                    return false;
                }

                ISorgenteWebcam vecchia = corrente;
                corrente = null;
                vecchia.Dispose();

                ISorgenteWebcam nuova = ApriSorgente(indiceCorrente, larghezza, altezza);
                if (nuova != null)
                {
                    larghezzaRichiesta = larghezza;
                    altezzaRichiesta = altezza;
                    corrente = nuova;
                    return true;
                }

                // Ripristino: la webcam precedente, con le dimensioni di prima.
                ISorgenteWebcam ripristinata = ApriSorgente(indiceCorrente, larghezzaPrima, altezzaPrima);
                if (ripristinata != null)
                {
                    corrente = ripristinata;
                }
                else
                {
                    indiceCorrente = -1;
                    infoCorrente = null;
                }

                return false;
            }
        }

        /// <summary>Applica le impostazioni alla webcam corrente (campi non null) e le memorizza per le aperture successive.</summary>
        public void ApplicaImpostazioni(ConfigurazioneWebcam configurazione)
        {
            if (configurazione == null)
            {
                throw new ArgumentNullException(nameof(configurazione));
            }

            lock (blocco)
            {
                Unisci(impostazioni, configurazione);
                corrente?.ApplicaImpostazioni(configurazione);
            }
        }

        /// <summary>Valori effettivi riletti dalla webcam corrente; stato «non supportato» se non c'è webcam.</summary>
        public StatoImpostazioniWebcam LeggiImpostazioni()
        {
            ISorgenteWebcam sorgente = corrente;
            return sorgente?.LeggiImpostazioni() ?? StatoImpostazioniWebcam.NonSupportato();
        }

        /// <summary>Frame al secondo realmente consegnati dalla webcam corrente (0 se assente).</summary>
        public double FpsCatturati => corrente?.FpsCatturati ?? 0;

        /// <summary>Apre una sorgente e le applica le impostazioni memorizzate; null se non disponibile. Va chiamato con il lock.</summary>
        private ISorgenteWebcam ApriSorgente(int indice, int larghezza, int altezza)
        {
            ISorgenteWebcam sorgente;
            try
            {
                sorgente = apri(indice, larghezza, altezza);
            }
            catch (Exception)
            {
                return null;
            }

            if (sorgente != null)
            {
                try
                {
                    sorgente.ApplicaImpostazioni(impostazioni);
                }
                catch (Exception)
                {
                    // Le impostazioni sono facoltative: la webcam resta utilizzabile.
                }
            }

            return sorgente;
        }

        /// <summary>Copia in destinazione i soli campi di fuoco ed esposizione non null.</summary>
        private static void Unisci(ConfigurazioneWebcam destinazione, ConfigurazioneWebcam origine)
        {
            destinazione.FuocoAutomatico = origine.FuocoAutomatico ?? destinazione.FuocoAutomatico;
            destinazione.Fuoco = origine.Fuoco ?? destinazione.Fuoco;
            destinazione.EsposizioneAutomatica = origine.EsposizioneAutomatica ?? destinazione.EsposizioneAutomatica;
            destinazione.Esposizione = origine.Esposizione ?? destinazione.Esposizione;
        }

        /// <summary>Legge un frame dalla webcam corrente (null se assente o non disponibile).</summary>
        public Mat Frame()
        {
            return corrente?.Frame();
        }

        /// <summary>Bitmap dell'ultimo frame della webcam corrente, o null; il chiamante la rilascia.</summary>
        public Bitmap Anteprima()
        {
            return corrente?.Anteprima();
        }

        /// <summary>Bitmap dell'ultimo frame raddrizzato sull'area del tavolo, o null; il chiamante la rilascia.</summary>
        public Bitmap AnteprimaRaddrizzata(Calibrazione calibrazione, int larghezza, int altezza)
        {
            return corrente?.AnteprimaRaddrizzata(calibrazione, larghezza, altezza);
        }

        public void Dispose()
        {
            lock (blocco)
            {
                if (eliminato)
                {
                    return;
                }

                eliminato = true;
                ISorgenteWebcam vecchia = corrente;
                corrente = null;
                indiceCorrente = -1;
                infoCorrente = null;
                vecchia?.Dispose();
            }
        }
    }
}
