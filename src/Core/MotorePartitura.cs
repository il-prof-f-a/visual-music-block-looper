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
        public long VersionePronta => scambio.Ultima?.Dati.VersioneFoglio ?? 0;
        public long Giro => Volatile.Read(ref giro);
        public long TickTempo => Volatile.Read(ref tickTempo);
        public long SegnaleOsservazione => Volatile.Read(ref segnaleOsservazione);
        public long SegnalePreparazione => Volatile.Read(ref segnalePreparazione);
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
            tickNelGiro = 0; indiceEvento = 0; Interlocked.Exchange(ref tickTempo, 0);
            richiestaPlay = false; inEsecuzione = true;
            if (inviaStart) start();
        }
        public void TickInterno()
        {
            if (modo != ModoSincronia.Master) return;
            TentaAvvio(true);
            if (bpmPendente > 0 && tickTempo % 24 == 0) { Volatile.Write(ref bpm, bpmPendente); bpmPendente = 0; }
            clock(); Avanza();
        }
        private void Avanza()
        {
            if (inEsecuzione)
            {
                if (tickNelGiro == attiva.Dati.TickGiro)
                {
                    if(!EseguiEventi())return;
                    Interlocked.Increment(ref giro);
                    var pronta = scambio.Leggi(giro);
                    if (pronta != null) Volatile.Write(ref attiva, pronta);
                    tickNelGiro = 0; indiceEvento = 0;
                }
                var pos = attiva.Dati.PosizioneAlTick(tickNelGiro);
                Volatile.Write(ref posizione, pos);
                if (tickNelGiro == 0 || (tickNelGiro % 6 == 0 && pos.StepInMisura == 0)) Interlocked.Increment(ref segnaleOsservazione);
                int inizio = (attiva.Dati.Struttura.AInizio - 1) * 4;
                int ultimoInizioMisura = ((attiva.Dati.Struttura.AFine - 1) * 4 / attiva.Dati.Struttura.StepPerMisura) * attiva.Dati.Struttura.StepPerMisura;
                if (tickNelGiro == (Math.Max(inizio, ultimoInizioMisura) - inizio) * 6) Interlocked.Increment(ref segnalePreparazione);
                if(!EseguiEventi())return;tickNelGiro++;
            }
            for (int n = 0; n < 128; n++)
                if (shotRimanenti[n] > 0 && --shotRimanenti[n] == 0) Spegni(canaleShot * 128 + n, shotOff[n]);
            Interlocked.Increment(ref tickTempo);
        }
        private bool EseguiEventi()
        {
            var eventi = attiva.Eventi;
            int disponibili=attiva.EventiPubblicati;
            if(attiva.Errore!=null||(indiceEvento>=disponibili&&indiceEvento<eventi.Length&&attiva.Dati.Eventi[indiceEvento].Tick<=tickNelGiro))
            {Volatile.Write(ref ultimoErrore,attiva.Errore??"Buffer del produttore non pronto: arresto controllato.");Arresta();return false;}
            while (indiceEvento < disponibili && eventi[indiceEvento].Dati.Tick == tickNelGiro)
            {
                var e = eventi[indiceEvento++]; int indice = e.Dati.Canale * 128 + e.Dati.Nota;
                if (e.Dati.NoteOn)
                {
                    spegnimenti[indice] = e.Off;
                    if (sonanti[indice] > 0) e.Off();
                    sonanti[indice]++; e.Invio();
                }
                else Spegni(indice, e.Off);
            }
            return true;
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
            Exception errore = null;
            for (int i = 0; i < sonanti.Length; i++) if (sonanti[i] > 0)
            { try { spegnimenti[i]?.Invoke(); sonanti[i] = 0; } catch (Exception e) { errore = e; } }
            Array.Clear(shotRimanenti, 0, shotRimanenti.Length);
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
        public void RiceviClock(long istante)
        {
            if (modo != ModoSincronia.Slave) return;
            ultimoClock = istante;
            var pronta = scambio.Ultima;
            if (pronta != null && tickTempo % pronta.Dati.TickGiro == 0) TentaAvvio(false);
            Avanza();
        }
        public void ControllaTimeoutClock(long adesso)
        { if (modo == ModoSincronia.Slave && adesso - ultimoClock > 500) Silenzia(); }
    }
}
