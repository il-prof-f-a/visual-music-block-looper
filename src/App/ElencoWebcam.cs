using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Una webcam di sistema selezionabile dall'operatore.</summary>
    public sealed class InfoWebcam
    {
        private readonly string etichetta;

        /// <param name="indice">Indice di sistema (ordine DirectShow; 0 = la prima).</param>
        /// <param name="nome">Nome del dispositivo come lo vede Windows (null se non noto).</param>
        /// <param name="percorso">Identificativo univoco del dispositivo (DevicePath), null se non noto.</param>
        /// <param name="etichetta">Testo da mostrare al posto del nome (per distinguere nomi duplicati).</param>
        public InfoWebcam(int indice, string nome = null, string percorso = null, string etichetta = null)
        {
            Indice = indice;
            Nome = nome;
            Percorso = percorso;
            this.etichetta = etichetta;
        }

        /// <summary>Indice di sistema della webcam (0 = la prima).</summary>
        public int Indice { get; }

        /// <summary>Nome del dispositivo (null se non noto). È ciò che si salva nel config.</summary>
        public string Nome { get; }

        /// <summary>Identificativo univoco del dispositivo, utile per distinguere webcam dello stesso modello.</summary>
        public string Percorso { get; }

        /// <summary>Testo mostrato nella lista: nome (eventualmente numerato), oppure «Webcam N».</summary>
        public string Descrizione => !string.IsNullOrWhiteSpace(etichetta)
            ? etichetta
            : !string.IsNullOrWhiteSpace(Nome) ? Nome : "Webcam " + Indice;

        /// <summary>Copia con un'etichetta diversa; nome, percorso e indice restano invariati.</summary>
        public InfoWebcam ConEtichetta(string nuovaEtichetta)
        {
            return new InfoWebcam(Indice, Nome, Percorso, nuovaEtichetta);
        }

        public override string ToString()
        {
            return Descrizione;
        }
    }

    /// <summary>Rileva le webcam disponibili: per nome (DirectShow) o, in alternativa, per indici.</summary>
    public static class ElencoWebcam
    {
        /// <summary>Numero massimo di indici provati nella rilevazione per indici.</summary>
        public const int MassimoIndici = 8;

        /// <summary>
        /// Costruisce l'elenco mostrato all'operatore. Usa i dispositivi enumerati dal sistema (con i nomi);
        /// se l'enumerazione è vuota o fallisce ripiega sulla rilevazione per indici («Webcam N»).
        /// </summary>
        /// <param name="enumeratore">Enumera i dispositivi video di sistema, nell'ordine degli indici.</param>
        /// <param name="apribile">Prova ad aprire un indice (solo per il ripiego).</param>
        /// <param name="indiceInUso">Indice della webcam già aperta, oppure -1 (solo per il ripiego).</param>
        public static IReadOnlyList<InfoWebcam> Costruisci(
            Func<IReadOnlyList<InfoWebcam>> enumeratore, Func<int, bool> apribile, int indiceInUso = -1)
        {
            if (enumeratore == null)
            {
                throw new ArgumentNullException(nameof(enumeratore));
            }

            IReadOnlyList<InfoWebcam> dispositivi = null;
            try
            {
                dispositivi = enumeratore();
            }
            catch (Exception)
            {
                // Enumerazione non disponibile: si ripiega sulla rilevazione per indici.
            }

            if (dispositivi != null && dispositivi.Count > 0)
            {
                return AssegnaEtichette(dispositivi);
            }

            return Rileva(apribile, indiceInUso);
        }

        /// <summary>
        /// Distingue nella lista le webcam con lo stesso nome numerandole («Nome (1)», «Nome (2)»). Il
        /// nome vero non cambia: è quello che viene salvato nel config.
        /// </summary>
        public static IReadOnlyList<InfoWebcam> AssegnaEtichette(IReadOnlyList<InfoWebcam> dispositivi)
        {
            if (dispositivi == null)
            {
                throw new ArgumentNullException(nameof(dispositivi));
            }

            Dictionary<string, int> totali = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (InfoWebcam info in dispositivi.Where(i => !string.IsNullOrWhiteSpace(i.Nome)))
            {
                totali[info.Nome] = totali.TryGetValue(info.Nome, out int n) ? n + 1 : 1;
            }

            Dictionary<string, int> progressivi = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            List<InfoWebcam> risultato = new List<InfoWebcam>();
            foreach (InfoWebcam info in dispositivi)
            {
                if (string.IsNullOrWhiteSpace(info.Nome) || totali[info.Nome] < 2)
                {
                    risultato.Add(info);
                    continue;
                }

                int numero = progressivi.TryGetValue(info.Nome, out int corrente) ? corrente + 1 : 1;
                progressivi[info.Nome] = numero;
                risultato.Add(info.ConEtichetta(info.Nome + " (" + numero + ")"));
            }

            return risultato;
        }

        /// <summary>
        /// Rilevazione per indici: prova 0, 1, 2... e si ferma al primo non apribile (gli indici delle
        /// webcam sono contigui). L'indice già in uso non viene riaperto (risulterebbe occupato) ed è
        /// sempre incluso.
        /// </summary>
        /// <param name="apribile">Restituisce true se l'indice si apre; un'eccezione vale "non apribile".</param>
        /// <param name="indiceInUso">Indice della webcam già aperta, oppure -1.</param>
        /// <param name="massimo">Numero massimo di indici da provare.</param>
        public static IReadOnlyList<InfoWebcam> Rileva(
            Func<int, bool> apribile, int indiceInUso = -1, int massimo = MassimoIndici)
        {
            if (apribile == null)
            {
                throw new ArgumentNullException(nameof(apribile));
            }

            List<InfoWebcam> trovate = new List<InfoWebcam>();
            for (int indice = 0; indice < massimo; indice++)
            {
                if (indice != indiceInUso && !Prova(apribile, indice))
                {
                    break;
                }

                trovate.Add(new InfoWebcam(indice));
            }

            // L'indice in uso oltre il limite provato resta comunque selezionabile.
            if (indiceInUso >= massimo)
            {
                trovate.Add(new InfoWebcam(indiceInUso));
            }

            return trovate;
        }

        /// <summary>Rileva le webcam reali del sistema, con i loro nomi.</summary>
        public static IReadOnlyList<InfoWebcam> RilevaReali(int indiceInUso = -1)
        {
            return Costruisci(DispositiviDirectShow.Enumera, ApriEChiudi, indiceInUso);
        }

        private static bool Prova(Func<int, bool> apribile, int indice)
        {
            try
            {
                return apribile(indice);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool ApriEChiudi(int indice)
        {
            using (VideoCapture cattura = new VideoCapture(indice))
            {
                return cattura.IsOpened();
            }
        }
    }
}
