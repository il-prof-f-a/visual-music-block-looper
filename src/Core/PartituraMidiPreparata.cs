using System;
using System.Threading;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>Il driver può preparare messaggi immutabili senza costruirli durante l'invio.</summary>
    public interface IUscitaMidiPreparabile
    {
        Action PreparaNotaOn(int canale, int nota, int velocity);
        Action PreparaNotaOff(int canale, int nota);
        Action PreparaClock();
        Action PreparaStart();
        Action PreparaStop();
        Action PreparaContinue();
    }

    public sealed class PartituraMidiPreparata
    {
        internal readonly struct EventoPronto
        {
            public EventoPronto(EventoMidiProgrammato dati, Action invio, Action off, int fineTick)
            { Dati = dati; Invio = invio; Off = off; FineTick = fineTick; }
            public readonly EventoMidiProgrammato Dati;
            public readonly Action Invio, Off;
            public readonly int FineTick;
        }
        private PartituraMidiPreparata(PartituraMidi dati, EventoPronto[] eventi)
        { Dati = dati; Eventi = eventi; }
        public PartituraMidi Dati { get; }
        internal EventoPronto[] Eventi { get; }
        private int pubblicati;
        private string errore;
        private int consumato;
        public int EventiPubblicati=>Volatile.Read(ref pubblicati);
        public string Errore=>Volatile.Read(ref errore);
        internal bool Consumato=>Volatile.Read(ref consumato)!=0;
        internal void MarcaConsumato()=>Interlocked.Exchange(ref consumato,1);
        public static PartituraMidiPreparata Prepara(PartituraMidi dati, IUscitaMidi uscita)
            =>PreparaProgressiva(dati,uscita,null);
        public static PartituraMidiPreparata PreparaProgressiva(PartituraMidi dati,IUscitaMidi uscita,Action<PartituraMidiPreparata> primoGruppo)
        {
            if (dati == null) throw new ArgumentNullException(nameof(dati));
            if (uscita == null) throw new ArgumentNullException(nameof(uscita));
            var eventi = new EventoPronto[dati.Eventi.Count];
            var fini = new int[eventi.Length];
            for(int i=0;i<fini.Length;i++)fini[i]=dati.TickGiro;
            var aperti=new System.Collections.Generic.Dictionary<int,System.Collections.Generic.Queue<int>>();
            for(int i=0;i<dati.Eventi.Count;i++)
            {
                var e=dati.Eventi[i];int chiave=e.Canale*128+e.Nota;
                if(e.NoteOn)
                {
                    if(!aperti.TryGetValue(chiave,out var coda)){coda=new System.Collections.Generic.Queue<int>();aperti.Add(chiave,coda);}
                    coda.Enqueue(i);
                }
                else if(aperti.TryGetValue(chiave,out var codaOff)&&codaOff.Count>0)fini[codaOff.Dequeue()]=e.Tick;
            }
            var pronta=new PartituraMidiPreparata(dati,eventi);bool notificato=false;
            try
            {
                for(int i=0;i<eventi.Length;)
                {
                    int tick=dati.Eventi[i].Tick;
                    do
                    {
                        var e=dati.Eventi[i];Action off=PreparaOff(uscita,e.Canale,e.Nota);
                        eventi[i]=new EventoPronto(e,e.NoteOn?PreparaOn(uscita,e.Canale,e.Nota,e.Velocity):off,off,e.FineTick>=0?e.FineTick:fini[i]);i++;
                    }while(i<eventi.Length&&dati.Eventi[i].Tick==tick);
                    Volatile.Write(ref pronta.pubblicati,i);
                    if(!notificato){notificato=true;primoGruppo?.Invoke(pronta);}
                }
                if(!notificato)primoGruppo?.Invoke(pronta);
                return pronta;
            }
            catch(Exception e){Volatile.Write(ref pronta.errore,"Preparazione MIDI fallita: "+e.Message);throw;}
        }
        internal static Action PreparaOn(IUscitaMidi u, int c, int n, int v) => u is IUscitaMidiPreparabile p ? p.PreparaNotaOn(c, n, v) : (Action)(() => u.NotaOn(c, n, v));
        internal static Action PreparaOff(IUscitaMidi u, int c, int n) => u is IUscitaMidiPreparabile p ? p.PreparaNotaOff(c, n) : (Action)(() => u.NotaOff(c, n));
    }
}
