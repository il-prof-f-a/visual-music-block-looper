using System;
using System.Diagnostics;
using System.Threading;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>Esecuzione a singolo proprietario. Solo Pubblica e ImpostaGenerazione sono ingressi concorrenti.</summary>
    public sealed class MotorePartitura
    {
        private readonly Action clock, start, stop;
        private readonly Action[] shotOn = new Action[128], shotOff = new Action[128];
        private readonly Action[] spegnimenti = new Action[16 * 128];
        private readonly int[] sonanti = new int[16 * 128], shotRimanenti = new int[128];
        private readonly struct NoteOffPendente
        {
            public NoteOffPendente(long tick,int indice,Action invio){Tick=tick;Indice=indice;Invio=invio;}
            public readonly long Tick;
            public readonly int Indice;
            public readonly Action Invio;
        }
        private readonly NoteOffPendente[] noteOffPendenti=new NoteOffPendente[4096];
        private int numeroNoteOffPendenti;
        private readonly int canaleShot;
        private readonly Func<long> orologioMs;
        private readonly TapTempo tap = new TapTempo();
        private readonly ScambioPartiture scambio = new ScambioPartiture();
        private PartituraMidiPreparata attiva;
        private PosizioneGiro posizione = StrutturaTavolo.Default.Posizione(0);
        private volatile bool inEsecuzione, richiestaPlay;
        private bool stopPendente;
        private string ultimoErrore;
        public string UltimoErrore=>Volatile.Read(ref ultimoErrore);
        private volatile ModoSincronia modo;
        private double bpm, bpmPendente;
        private int tickNelGiro, indiceEvento;
        private long giro, tickTempo, ultimoClock, segnaleOsservazione, segnalePreparazione;
        private long ultimaMisuraDestinazione=-1,ultimaMisuraApplicazione=-1;
        private int ultimoAggiornamentoRinviato;
        private IstantaneaTrasporto trasporto;
        private int revisioneTrasporto;
        private long esecuzione;

        public MotorePartitura(IUscitaMidi uscita, IMappaturaMidi mappatura, double bpm = 120, Func<long> orologioMs = null)
        {
            if (uscita == null) throw new ArgumentNullException(nameof(uscita));
            if (mappatura == null) throw new ArgumentNullException(nameof(mappatura));
            this.bpm = Tempo.Limita(bpm);
            if (orologioMs == null) { var t = Stopwatch.StartNew(); orologioMs = () => t.ElapsedMilliseconds; }
            this.orologioMs = orologioMs;
            if (uscita is IUscitaMidiPreparabile p)
            { clock = p.PreparaClock(); start = p.PreparaStart(); stop = p.PreparaStop(); }
            else { clock = uscita.Clock; start = uscita.Start; stop = uscita.Stop; }
            canaleShot = mappatura.Canale(0);
            for (int n = 0; n < 128; n++)
            { shotOn[n] = PartituraMidiPreparata.PreparaOn(uscita, canaleShot, n, mappatura.Velocity); shotOff[n] = PartituraMidiPreparata.PreparaOff(uscita, canaleShot, n); }
        }
        public ModoSincronia Modo => modo;
        public bool InEsecuzione => inEsecuzione;
        public bool InPreparazione => richiestaPlay && !inEsecuzione;
        public double Bpm => Volatile.Read(ref bpm);
        public PartituraMidi PartituraAttiva => Volatile.Read(ref attiva)?.Dati;
        public long VersioneAttiva => PartituraAttiva?.VersioneFoglio ?? 0;
        public long VersionePronta => scambio.VersionePronta;
        public long Giro => Volatile.Read(ref giro);
        public long TickTempo => Volatile.Read(ref tickTempo);
        public long SegnaleOsservazione => Volatile.Read(ref segnaleOsservazione);
        public long MisuraAssoluta => Math.Max(0,SegnaleOsservazione-1);
        public long UltimaMisuraDestinazione => Volatile.Read(ref ultimaMisuraDestinazione);
        public long UltimaMisuraApplicazione => Volatile.Read(ref ultimaMisuraApplicazione);
        public bool UltimoAggiornamentoRinviato => Volatile.Read(ref ultimoAggiornamentoRinviato)!=0;
        public long SegnalePreparazione => Volatile.Read(ref segnalePreparazione);
        public IstantaneaTrasporto Trasporto
        {
            get
            {
                // Un solo scrittore. Il clock non attende mai i lettori e pubblica solo valori.
                while(true)
                {
                    int prima=Volatile.Read(ref revisioneTrasporto);
                    if((prima&1)!=0){Thread.SpinWait(1);continue;}
                    var copia=trasporto;
                    Thread.MemoryBarrier();
                    if(prima==Volatile.Read(ref revisioneTrasporto))return copia;
                }
            }
        }
        private void PubblicaTrasporto(IstantaneaTrasporto valore)
        {
            Interlocked.Increment(ref revisioneTrasporto);
            trasporto=valore;
            Interlocked.Increment(ref revisioneTrasporto);
        }
        public PosizioneGiro Posizione => Volatile.Read(ref posizione);
        public int StepCorrente => Posizione.StepInMisura;
        public StrutturaTavolo Struttura => PartituraAttiva?.Struttura ?? StrutturaTavolo.Default;
        public bool Pubblica(PartituraMidiPreparata p) => scambio.Pubblica(p);
        public void ImpostaGenerazione(long g) => scambio.ImpostaGenerazione(g);
        public void Avvia()
        {
            if (modo != ModoSincronia.Master || inEsecuzione) return;
            Volatile.Write(ref ultimoErrore,null);richiestaPlay = true; TentaAvvio(true);
        }
        private void TentaAvvio(bool inviaStart)
        {
            if (!richiestaPlay || inEsecuzione) return;
            // Un nuovo Start apre una nuova esecuzione: una candidata già pronta non deve
            // restare vincolata al numero di frontiera del trasporto precedente.
            var pronta = scambio.Ultima;
            if(pronta?.Errore!=null){Volatile.Write(ref ultimoErrore,pronta.Errore);richiestaPlay=false;return;}
            if (pronta != null && pronta.Dati.Generazione != scambio.Generazione) pronta = null;
            if (pronta != null) Volatile.Write(ref attiva, pronta);
            if (attiva == null || attiva.Dati.Generazione != scambio.Generazione) return;
            scambio.SvuotaProgrammiMisura();
            tickNelGiro = 0; indiceEvento = 0; Interlocked.Exchange(ref tickTempo, 0);
            Interlocked.Exchange(ref segnaleOsservazione,0);
            Volatile.Write(ref ultimaMisuraDestinazione,-1);Volatile.Write(ref ultimaMisuraApplicazione,-1);
            Volatile.Write(ref ultimoAggiornamentoRinviato,0);
            richiestaPlay = false; inEsecuzione = true;
            esecuzione++;
            PubblicaTrasporto(new IstantaneaTrasporto(attiva.Dati.Struttura,esecuzione,attiva.Dati.Generazione,
                true,0,0,0,orologioMs(),0,attiva.Dati));
            if (inviaStart) start();
        }
        public void TickInterno()
        {
            if (modo != ModoSincronia.Master) return;
            TentaAvvio(true);
            if (bpmPendente > 0 && tickTempo % 24 == 0) { Volatile.Write(ref bpm, bpmPendente); bpmPendente = 0; }
            long istante=orologioMs();clock(); Avanza(istante,Tempo.IntervalloTickMs(bpm));
        }
        private void Avanza(long istante,double periodoTickMs)
        {
            if (inEsecuzione)
            {
                if (tickNelGiro == attiva.Dati.TickGiro)
                {
                    if(!EseguiEventi())return;
                    Interlocked.Increment(ref giro);
                    var pronta = scambio.Leggi(giro);
                    if (pronta != null && pronta.Dati.VersioneFoglio>=attiva.Dati.VersioneFoglio)
                        Volatile.Write(ref attiva, pronta);
                    tickNelGiro = 0; indiceEvento = 0;
                }
                var pos = attiva.Dati.PosizioneAlTick(tickNelGiro);
                Volatile.Write(ref posizione, pos);
                if (tickNelGiro == 0 || (tickNelGiro % 6 == 0 && pos.StepInMisura == 0))
                {
                    long misura=Interlocked.Increment(ref segnaleOsservazione)-1;
                    var aggiornata=scambio.LeggiMisura(misura,attiva.Dati.Struttura);
                    if(aggiornata!=null&&aggiornata.Dati.VersioneFoglio>attiva.Dati.VersioneFoglio)
                    {
                        Volatile.Write(ref attiva,aggiornata);
                        Volatile.Write(ref ultimaMisuraDestinazione,aggiornata.Dati.PrimaMisuraAssoluta);
                        Volatile.Write(ref ultimaMisuraApplicazione,misura);
                        Volatile.Write(ref ultimoAggiornamentoRinviato,
                            misura>aggiornata.Dati.PrimaMisuraAssoluta?1:0);
                        indiceEvento=PrimoEventoAlTick(aggiornata.Eventi,tickNelGiro);
                        pos=attiva.Dati.PosizioneAlTick(tickNelGiro);
                        Volatile.Write(ref posizione,pos);
                    }
                }
                int inizio = (attiva.Dati.Struttura.AInizio - 1) * 4;
                int ultimoInizioMisura = ((attiva.Dati.Struttura.AFine - 1) * 4 / attiva.Dati.Struttura.StepPerMisura) * attiva.Dati.Struttura.StepPerMisura;
                if (tickNelGiro == (Math.Max(inizio, ultimoInizioMisura) - inizio) * 6) Interlocked.Increment(ref segnalePreparazione);
                if(!EseguiEventi())return;
                PubblicaTrasporto(new IstantaneaTrasporto(attiva.Dati.Struttura,esecuzione,attiva.Dati.Generazione,true,
                    Math.Max(0,segnaleOsservazione-1),attiva.Dati.Struttura.IndiceMisuraAlTick(tickNelGiro),tickNelGiro,istante,periodoTickMs,attiva.Dati));
                tickNelGiro++;
            }
            for (int n = 0; n < 128; n++)
                if (shotRimanenti[n] > 0 && --shotRimanenti[n] == 0) Spegni(canaleShot * 128 + n, shotOff[n]);
            Interlocked.Increment(ref tickTempo);
        }
        private bool EseguiEventi()
        {
            ScaricaNoteOffScadute();
            var eventi = attiva.Eventi;
            int disponibili=attiva.EventiPubblicati;
            if(attiva.Errore!=null||(indiceEvento>=disponibili&&indiceEvento<eventi.Length&&attiva.Dati.Eventi[indiceEvento].Tick<=tickNelGiro))
            {Volatile.Write(ref ultimoErrore,attiva.Errore??"Buffer del produttore non pronto: arresto controllato.");Arresta();return false;}
            while (indiceEvento < disponibili && eventi[indiceEvento].Dati.Tick == tickNelGiro)
            {
                var e = eventi[indiceEvento++]; int indice = e.Dati.Canale * 128 + e.Dati.Nota;
                if (e.Dati.NoteOn)
                {
                    long distanza=Math.Max(1,e.FineTick-tickNelGiro);
                    if(!InserisciNoteOff(tickTempo+distanza,indice,e.Off))
                    {
                        Volatile.Write(ref ultimoErrore,"Numero massimo di NoteOff pendenti raggiunto.");
                        Arresta();return false;
                    }
                    spegnimenti[indice] = e.Off;
                    if (sonanti[indice] > 0) e.Off();
                    sonanti[indice]++; e.Invio();
                }
            }
            return true;
        }
        private bool InserisciNoteOff(long tick,int indice,Action invio)
        {
            if(numeroNoteOffPendenti>=noteOffPendenti.Length)return false;
            int i=numeroNoteOffPendenti++;
            while(i>0)
            {
                int padre=(i-1)/2;
                if(noteOffPendenti[padre].Tick<=tick)break;
                noteOffPendenti[i]=noteOffPendenti[padre];i=padre;
            }
            noteOffPendenti[i]=new NoteOffPendente(tick,indice,invio);return true;
        }
        private void ScaricaNoteOffScadute()
        {
            while(numeroNoteOffPendenti>0&&noteOffPendenti[0].Tick<=tickTempo)
            {
                var scaduta=noteOffPendenti[0];var ultima=noteOffPendenti[--numeroNoteOffPendenti];
                if(numeroNoteOffPendenti>0)
                {
                    int i=0;
                    while(true)
                    {
                        int sinistro=i*2+1;if(sinistro>=numeroNoteOffPendenti)break;
                        int destro=sinistro+1,figlio=destro<numeroNoteOffPendenti
                            &&noteOffPendenti[destro].Tick<noteOffPendenti[sinistro].Tick?destro:sinistro;
                        if(noteOffPendenti[figlio].Tick>=ultima.Tick)break;
                        noteOffPendenti[i]=noteOffPendenti[figlio];i=figlio;
                    }
                    noteOffPendenti[i]=ultima;
                }
                Spegni(scaduta.Indice,scaduta.Invio);
            }
        }
        private static int PrimoEventoAlTick(PartituraMidiPreparata.EventoPronto[] eventi,int tick)
        {
            int basso=0,alto=eventi.Length;
            while(basso<alto)
            {
                int medio=basso+(alto-basso)/2;
                if(eventi[medio].Dati.Tick<tick)basso=medio+1;else alto=medio;
            }
            return basso;
        }
        public PosizioneGiro PosizioneDopoMisure(int anticipo,out long misuraAssoluta)
        {
            if(anticipo<0)throw new ArgumentOutOfRangeException(nameof(anticipo));
            var t=Trasporto;
            if(t.Struttura==null)throw new InvalidOperationException("La partitura non è ancora attiva.");
            misuraAssoluta=t.MisuraAssoluta+anticipo;
            if(anticipo==0)return t.Partitura.PosizioneAlTick(t.TickNelGiro);
            return t.Struttura.PosizioneMisuraGiro((t.IndiceMisura+anticipo)%t.Struttura.MisurePerGiro);
        }
        private void Spegni(int indice, Action off)
        {
            if (sonanti[indice] == 0) return;
            if (sonanti[indice] > 1) { sonanti[indice]--; return; }
            off(); // Se il driver fallisce, il proprietario conserva il debito di spegnimento per il cleanup.
            sonanti[indice] = 0;
        }
        public void OneShot(int nota)
        {
            if (nota < 0 || nota > 127) return;
            int indice = canaleShot * 128 + nota;
            spegnimenti[indice] = shotOff[nota];
            if (sonanti[indice] > 0) shotOff[nota]();
            if (shotRimanenti[nota] == 0) sonanti[indice]++;
            shotRimanenti[nota] = 6; shotOn[nota]();
        }
        public void Tap(long istante)
        {
            if (modo != ModoSincronia.Master) return;
            var nuovo = tap.Tap(istante);
            if (!nuovo.HasValue) return;
            if (inEsecuzione) bpmPendente = nuovo.Value;
            else {bpmPendente=0;Volatile.Write(ref bpm, nuovo.Value);}
        }
        public double BpmRichiesto {get {double p=Volatile.Read(ref bpmPendente);return p>0?p:Bpm;}}
        public void ImpostaBpm(double nuovo)
        {
            if(modo!=ModoSincronia.Master)return;
            tap.Azzera();nuovo=Tempo.Limita(nuovo);
            if(inEsecuzione)Volatile.Write(ref bpmPendente,nuovo);
            else{bpmPendente=0;Volatile.Write(ref bpm,nuovo);}
        }
        public void Arresta()
        {
            if (modo == ModoSincronia.Master && inEsecuzione) stopPendente = true;
            Exception errore = null;
            try { Silenzia(); } catch (Exception e) { errore = e; }
            if (stopPendente)
            {
                try { stop(); stopPendente = false; }
                catch (Exception e) { if (errore == null) errore = e; }
            }
            if (errore != null) throw errore;
        }
        public void Silenzia()
        {
            inEsecuzione = false; richiestaPlay = false;
            var t=trasporto;
            PubblicaTrasporto(new IstantaneaTrasporto(t.Struttura,t.Esecuzione,t.Generazione,false,
                t.MisuraAssoluta,t.IndiceMisura,t.TickNelGiro,t.IstanteMs,0,t.Partitura));
            Exception errore = null;
            for (int i = 0; i < sonanti.Length; i++) if (sonanti[i] > 0)
            { try { spegnimenti[i]?.Invoke(); sonanti[i] = 0; } catch (Exception e) { errore = e; } }
            Array.Clear(shotRimanenti, 0, shotRimanenti.Length);
            Array.Clear(noteOffPendenti,0,numeroNoteOffPendenti);numeroNoteOffPendenti=0;
            tickNelGiro = 0; indiceEvento = 0;
            if (errore != null) throw errore;
        }
        public void CambiaModo()
        {
            Arresta(); modo = modo == ModoSincronia.Master ? ModoSincronia.Slave : ModoSincronia.Master;
            ultimoClock = orologioMs();
        }
        public void RiceviStart()
        {
            if (modo != ModoSincronia.Slave) return;
            Silenzia(); tickTempo = 0; richiestaPlay = true; ultimoClock = orologioMs(); TentaAvvio(false);
        }
        public void RiceviStop() { if (modo == ModoSincronia.Slave) Silenzia(); }
        public void RiceviContinue()
        {
            if (modo != ModoSincronia.Slave) return;
            richiestaPlay = true; ultimoClock = orologioMs(); TentaAvvio(false);
        }
        public void RiceviClock(long istante,double periodoTickMs=0)
        {
            if (modo != ModoSincronia.Slave) return;
            ultimoClock = istante;
            var pronta = scambio.Ultima;
            if (pronta != null && tickTempo % pronta.Dati.TickGiro == 0) TentaAvvio(false);
            Avanza(istante,periodoTickMs>0?periodoTickMs:Tempo.IntervalloTickMs(bpm));
        }
        public void ControllaTimeoutClock(long adesso)
        { if (modo == ModoSincronia.Slave && adesso - ultimoClock > 500) Silenzia(); }
    }
}
