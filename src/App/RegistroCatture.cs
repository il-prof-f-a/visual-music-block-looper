using System;
using System.Collections.Generic;
using System.Diagnostics;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.App
{
    /// <summary>Contesto monouso. Soltanto il produttore modifica Traccia prima del completamento.</summary>
    public sealed class CatturaInCorso
    {
        internal CatturaInCorso(RegistroCatture proprietario, long generazione, long sequenza)
        {
            Proprietario = proprietario; Generazione = generazione; Sequenza = sequenza;
            TimestampUtc = DateTimeOffset.UtcNow;
        }

        internal RegistroCatture Proprietario { get; }
        internal long Generazione { get; }
        internal bool Completata { get; set; }
        internal Stopwatch Orologio { get; } = Stopwatch.StartNew();
        public long Sequenza { get; }
        public DateTimeOffset TimestampUtc { get; }
        public TracciaCartoncini Traccia { get; } = new TracciaCartoncini();
    }

    /// <summary>Copia immutabile dei soli metadati: non trattiene immagini o oggetti nativi.</summary>
    public sealed class CatturaDiagnostica
    {
        internal CatturaDiagnostica(CatturaInCorso cattura)
        {
            TracciaCartoncini t = cattura.Traccia;
            Sequenza = cattura.Sequenza; TimestampUtc = cattura.TimestampUtc;
            DurataMs = cattura.Orologio.Elapsed.TotalMilliseconds;
            EsitoVisione = t.EsitoVisione; LarghezzaFrame = t.LarghezzaFrame; AltezzaFrame = t.AltezzaFrame;
            CalibrazionePresente = t.CalibrazionePresente;
            Layout = t.Layout;
            Visione = new List<EventoDiagnosticoCartoncino>(t.Visione).AsReadOnly();
            Interpretazione = new List<EventoDiagnosticoCartoncino>(t.Interpretazione).AsReadOnly();
            StrutturaGrezza = t.StrutturaGrezza; StrutturaStabile = t.StrutturaStabile;
            StrutturaInConferma = t.StrutturaInConferma;
            NoteGrezze = t.NoteGrezze; ComandiGrezzi = t.ComandiGrezzi;
            NoteStabili = t.NoteStabili; ComandiEmessi = t.ComandiEmessi;
            ConfermeStruttura = t.ConfermeStruttura; ConfermeRichieste = t.ConfermeRichieste;
            EsitoStabilizzazione = t.EsitoStabilizzazione; Errore = t.Errore;
        }

        public long Sequenza { get; }
        public DateTimeOffset TimestampUtc { get; }
        public double DurataMs { get; }
        public string EsitoVisione { get; }
        public int LarghezzaFrame { get; }
        public int AltezzaFrame { get; }
        public bool? CalibrazionePresente { get; }
        public LayoutTavolo Layout { get; }
        public IReadOnlyList<EventoDiagnosticoCartoncino> Visione { get; }
        public IReadOnlyList<EventoDiagnosticoCartoncino> Interpretazione { get; }
        public StrutturaTavolo StrutturaGrezza { get; }
        public StrutturaTavolo StrutturaStabile { get; }
        public StrutturaTavolo StrutturaInConferma { get; }
        public int NoteGrezze { get; }
        public int ComandiGrezzi { get; }
        public int NoteStabili { get; }
        public int ComandiEmessi { get; }
        public int ConfermeStruttura { get; }
        public int ConfermeRichieste { get; }
        public string EsitoStabilizzazione { get; }
        public string Errore { get; }
    }

    public sealed class RapportoDiagnosticaCartoncini
    {
        internal RapportoDiagnosticaCartoncini(long totale, CatturaDiagnostica[] catture)
        {
            TotaleCatture = totale; Catture = Array.AsReadOnly(catture);
        }
        public int VersioneFormato => 1;
        public DateTimeOffset EsportatoUtc { get; } = DateTimeOffset.UtcNow;
        public int Capacita => RegistroCatture.Capacita;
        public long TotaleCatture { get; }
        public long CattureOmesse => Math.Max(0, TotaleCatture - Catture.Count);
        public IReadOnlyList<CatturaDiagnostica> Catture { get; }
    }

    /// <summary>Un produttore (cattura), lettori concorrenti (UI/export). Inserimento e scarto O(1).</summary>
    public sealed class RegistroCatture
    {
        public const int Capacita = 50;
        private readonly object blocco = new object();
        private readonly CatturaDiagnostica[] elementi = new CatturaDiagnostica[Capacita];
        private bool attivo;
        private long generazione, sequenza, totale;
        private int prossimo, numero;

        public bool Attivo
        {
            get { lock (blocco) return attivo; }
            set
            {
                lock (blocco)
                {
                    if (attivo == value) return;
                    attivo = value;
                    generazione++;
                    if (!value) return;
                    Array.Clear(elementi, 0, elementi.Length);
                    prossimo = numero = 0; sequenza = totale = 0;
                }
            }
        }

        public int NumeroCatture { get { lock (blocco) return numero; } }

        public CatturaInCorso IniziaCattura()
        {
            lock (blocco) return attivo ? new CatturaInCorso(this, generazione, ++sequenza) : null;
        }

        public void CompletaCattura(CatturaInCorso cattura)
        {
            if (cattura == null || cattura.Proprietario != this) return;
            // Il contesto appartiene al produttore; la copia non blocca UI/export.
            var copia = new CatturaDiagnostica(cattura);
            lock (blocco)
            {
                if (!attivo || cattura.Generazione != generazione || cattura.Completata) return;
                cattura.Completata = true;
                elementi[prossimo] = copia;
                prossimo = (prossimo + 1) % Capacita;
                numero = Math.Min(numero + 1, Capacita);
                totale++;
            }
        }

        public RapportoDiagnosticaCartoncini Istantanea()
        {
            lock (blocco)
            {
                var copia = new CatturaDiagnostica[numero];
                int primo = (prossimo - numero + Capacita) % Capacita;
                for (int i = 0; i < numero; i++) copia[i] = elementi[(primo + i) % Capacita];
                return new RapportoDiagnosticaCartoncini(totale, copia);
            }
        }
    }
}
