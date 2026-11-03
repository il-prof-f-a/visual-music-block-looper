using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using Newtonsoft.Json;
using OpenCvSharp;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Midi;
using VisualMusicBlockLooper.Vision;

// Banco isolato: apre esclusivamente "VMBL Test", mai le porte del DAW dell'operatore.
// Senza quella porta misura l'esecutore con uscita in memoria e dichiara il limite nel JSON.
internal static class Program
{
    private sealed class Mappa : IMappaturaMidi
    {public int Canale(int traccia)=>traccia;public int Nota(int clip)=>clip+36;public int Velocity=>100;}
    private sealed class Uscita : IUscitaMidi,IUscitaMidiPreparabile
    {
        private readonly UscitaMidiDryWet driver;
        public long ClockTotali,NoteOn,NoteOff,StartTotali,StopTotali;
        public Uscita(UscitaMidiDryWet driver){this.driver=driver;}
        public void Clock(){Interlocked.Increment(ref ClockTotali);driver?.Clock();}
        public void Start(){Interlocked.Increment(ref StartTotali);driver?.Start();}
        public void Stop(){Interlocked.Increment(ref StopTotali);driver?.Stop();}
        public void Continue(){driver?.Continue();}
        public void NotaOn(int c,int n,int v){Interlocked.Increment(ref NoteOn);driver?.NotaOn(c,n,v);}
        public void NotaOff(int c,int n){Interlocked.Increment(ref NoteOff);driver?.NotaOff(c,n);}
        public Action PreparaClock(){var a=driver?.PreparaClock();return ()=>{Interlocked.Increment(ref ClockTotali);a?.Invoke();};}
        public Action PreparaStart(){var a=driver?.PreparaStart();return ()=>{Interlocked.Increment(ref StartTotali);a?.Invoke();};}
        public Action PreparaStop(){var a=driver?.PreparaStop();return ()=>{Interlocked.Increment(ref StopTotali);a?.Invoke();};}
        public Action PreparaContinue()=>()=>driver?.Continue();
        public Action PreparaNotaOn(int c,int n,int v){var a=driver?.PreparaNotaOn(c,n,v);return ()=>{Interlocked.Increment(ref NoteOn);a?.Invoke();};}
        public Action PreparaNotaOff(int c,int n){var a=driver?.PreparaNotaOff(c,n);return ()=>{Interlocked.Increment(ref NoteOff);a?.Invoke();};}
    }
    private static string Arg(string[] args,string nome,string valore=null)
    {int i=Array.IndexOf(args,nome);return i>=0&&i+1<args.Length?args[i+1]:valore;}
    [STAThread] private static int Main(string[] args)
    {
        try
        {
            int secondi=int.Parse(Arg(args,"--secondi","600"));
            if(secondi<1)throw new ArgumentOutOfRangeException("--secondi");
            string dir=Path.GetFullPath(Arg(args,"--output","TestResults/midi-deterministico/collaudo"));Directory.CreateDirectory(dir);
            string foto=Arg(args,"--immagine"),calFile=Arg(args,"--calibrazione");
            int[] bpm=Arg(args,"--bpm","60,120,180").Split(',').Select(int.Parse).ToArray();
            bool[] carichi=Arg(args,"--carico","entrambi")=="base"?new[]{false}:Arg(args,"--carico")=="visione"?new[]{true}:new[]{false,true};
            string[] ingressi=InputDevice.GetAll().Select(d=>d.Name).ToArray(),uscite=OutputDevice.GetAll().Select(d=>d.Name).ToArray();
            bool loopback=ingressi.Contains("VMBL Test")&&uscite.Contains("VMBL Test");
            File.WriteAllText(Path.Combine(dir,"banco.json"),JsonConvert.SerializeObject(new{IstanteUtc=DateTime.UtcNow,Ingressi=ingressi,Uscite=uscite,Loopback=loopback,SecondiPerScenario=secondi},Formatting.Indented));
            foreach(bool carico in carichi)foreach(int b in bpm)Esegui(secondi,b,carico,loopback,foto,calFile,dir);
            return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static void Esegui(int secondi,int bpm,bool carico,bool loopback,string foto,string calFile,string dir)
    {
        string nome=(carico?"visione-":"base-")+bpm;
        Console.WriteLine(DateTime.UtcNow.ToString("O")+" INIZIO "+nome);
        var tempo=Stopwatch.StartNew();Func<long> ora=()=>tempo.ElapsedMilliseconds;
        var cfg=new ConfigGriglia{Area=LayoutTavolo.AreaGrigliaDefault};
        long[] ricevuti=new long[Math.Max(4096,secondi*130)];int nRicevuti=0;
        using(var input=loopback?InputDevice.GetByName("VMBL Test"):null)
        using(var driver=loopback?new UscitaMidiDryWet("VMBL Test"):null)
        {
            if(input!=null){input.EventReceived+=(s,a)=>{if(a.Event is TimingClockEvent){int n=Interlocked.Increment(ref nRicevuti)-1;if(n<ricevuti.Length)ricevuti[n]=Stopwatch.GetTimestamp();}};input.StartEventsListening();}
            var u=new Uscita(driver);
            using(var e=new EsecutoreMidi(u,null,new Mappa(),cfg,ora,bpm))
            using(var clock=new TickMaster(e,ora))
            using(var registro=new RegistroFotogrammi())
            using(var frame=carico?Cv2.ImRead(foto):null)
            using(var immagine=new Bitmap(800,450))
            using(var grafica=Graphics.FromImage(immagine))
            {
                var note=Enumerable.Range(0,10).Select(i=>new CartoncinoNota{Riga=2+i/5,Traccia=i/5,IdClip=28+i%5,StepIniziale=new[]{0,3,6,11,13}[i%5],DurataStep=2}).ToArray();
                StrutturaTavolo.TryCrea(4,2,1,8,out var struttura);
                var fisso=new StatoTavolo(note,null,null,struttura);
                var preparatore=new PreparatoreFoglio(e);preparatore.Aggiorna(fisso,true);
                Calibrazione cal=carico?Calibrazione.Carica(calFile):null;
                using(var analisi=carico?new AnalizzatoreFoglio(registro,()=>cal,cfg,LayoutTavolo.Default,preparatore,new RegistroCatture(),ora):null)
                using(var comandi=carico?new GestoreComandiRapidi(registro,()=>cal,cfg,LayoutTavolo.Default,e,new RegistroCatture(),ora):null)
                using(var ciclo=carico?new CicloCattura(()=>analisi.Passo(),20):null)
                using(var rapidi=carico?new CicloCattura(()=>comandi.Passo(),20):null)
                {
                    if(carico&&(frame==null||frame.Empty()))throw new InvalidDataException("Immagine del carico assente.");
                    var campioni=new List<CampioneTempoMidi>();long ultimo=0,prossimaCopia=0,prossimaRilettura=15000;
                    int framePubblicati=0,disegni=0;var render=new RenderGuida(cfg,LayoutTavolo.Default);
                    int gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);
                    e.Avvia();clock.Avvia();ciclo?.Avvia();rapidi?.Avvia();long inizio=ora();
                    while(ora()-inizio<secondi*1000L)
                    {
                        if(carico)
                        {
                            registro.Pubblica(frame,ora());framePubblicati++;
                            // Ogni 15 s una rilettura completa sollecita anche il decoder nativo senza cambiare la scena.
                            if(ora()-inizio>=prossimaRilettura){analisi.Rileggi();prossimaRilettura+=15000;}
                            render.Disegna(grafica,immagine.Size,analisi.UltimoStato,e.Posizione,e.InEsecuzione);disegni++;
                        }
                        if(ora()-inizio>=prossimaCopia)
                        {Copia(clock.RegistroTempi,campioni,ref ultimo);prossimaCopia+=1000;}
                        Thread.Sleep(carico?33:100);
                    }
                    bool ancoraSuona=e.InEsecuzione;clock.Ferma();Copia(clock.RegistroTempi,campioni,ref ultimo);
                    ciclo?.Ferma();rapidi?.Ferma();input?.StopEventsListening();
                    double f=1000.0/Stopwatch.Frequency,periodo=60000.0/(bpm*24);
                    var ritardi=campioni.Select(c=>Math.Max(0,c.InizioInvio-c.Previsto)*f).OrderBy(x=>x).ToArray();
                    var intervalli=campioni.Skip(1).Select((c,i)=>Math.Abs((c.InizioInvio-campioni[i].InizioInvio)*f-periodo)).OrderBy(x=>x).ToArray();
                    Func<double[],double,double> p=(v,q)=>v.Length==0?0:v[Math.Max(0,(int)Math.Ceiling(v.Length*q)-1)];
                    double deriva=campioni.Count<2?0:(campioni.Last().InizioInvio-campioni[0].InizioInvio)*f-(campioni.Count-1)*periodo;
                    var rapporto=new{Scenario=nome,Bpm=bpm,Secondi=secondi,SecondiEffettivi=(ora()-inizio)/1000.0,TipoUscita=loopback?"loopback VMBL Test":"memoria: loopback non disponibile",
                        Clock=u.ClockTotali,Campioni=campioni.Count,CampioniMancanti=clock.RegistroTempi.TotaleCampioni-campioni.Count,ClockRicevuti=nRicevuti,
                        ClockDifferenzaRicevuti=loopback?u.ClockTotali-nRicevuti:(long?)null,
                        NoteOn=u.NoteOn,NoteOff=u.NoteOff,Start=u.StartTotali,Stop=u.StopTotali,AncoraInEsecuzione=ancoraSuona,
                        Errore=e.UltimoErrore??clock.UltimoErrore,Mmcss=clock.MmcssAttiva,
                        RitardoP50Ms=p(ritardi,.50),RitardoP95Ms=p(ritardi,.95),RitardoP99Ms=p(ritardi,.99),RitardoMassimoMs=p(ritardi,1),
                        IntervalloP99Ms=p(intervalli,.99),IntervalloMassimoMs=p(intervalli,1),DerivaMs=deriva,
                        InvioMassimoMs=campioni.Count==0?0:campioni.Max(c=>(c.FineInvio-c.InizioInvio)*f),
                        FramePubblicati=framePubblicati,DisegniOffscreen=disegni,LettureZone=analisi?.NumeroLettureZone??0,Compilazioni=preparatore.NumeroCompilazioni,
                        Gc0=GC.CollectionCount(0)-gc0,Gc1=GC.CollectionCount(1)-gc1,Gc2=GC.CollectionCount(2)-gc2,
                        TargetRaggiunto=p(ritardi,.99)<=2&&p(ritardi,1)<=10&&ancoraSuona&&clock.RegistroTempi.TotaleCampioni==campioni.Count};
                    File.WriteAllText(Path.Combine(dir,nome+".json"),JsonConvert.SerializeObject(rapporto,Formatting.Indented));
                    File.WriteAllText(Path.Combine(dir,nome+"-campioni.json"),JsonConvert.SerializeObject(new{FrequenzaQpc=Stopwatch.Frequency,Campioni=campioni,Ricevuti=ricevuti.Take(Math.Min(nRicevuti,ricevuti.Length))}));
                    Console.WriteLine(JsonConvert.SerializeObject(rapporto));
                }
            }
        }
    }
    private static void Copia(RegistroTempiMidi registro,List<CampioneTempoMidi> campioni,ref long ultimo)
    {foreach(var c in registro.Istantanea())if(c.Tick>ultimo){campioni.Add(c);ultimo=c.Tick;}}
}
