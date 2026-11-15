using System;
using System.Collections.Generic;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Vision
{
    public sealed class LetturaZona
    {
        internal LetturaZona(IReadOnlyList<CartoncinoRilevato> catalogo, bool completa, int qr, Rect camera)
        { Catalogo=catalogo;Completa=completa;QrDecodificati=qr;RegioneCamera=camera; }
        public IReadOnlyList<CartoncinoRilevato> Catalogo { get; }
        public bool Completa { get; }
        public int QrDecodificati { get; }
        public Rect RegioneCamera { get; }
        public Rect RegioneMaschera { get; internal set; }
    }
    public sealed class LettoreZone
    {
        private const int NumeroMassimoZoneMemorizzate=16;
        private sealed class CacheZona
        {
            public CacheZona(RilevatoreCartoncini rilevatore,LinkedListNode<ChiaveZona> nodo)
            {Rilevatore=rilevatore;Nodo=nodo;}
            public readonly RilevatoreCartoncini Rilevatore;
            public readonly object Sincronizzazione=new object();
            public readonly LinkedListNode<ChiaveZona> Nodo;
        }
        private struct ChiaveZona : IEquatable<ChiaveZona>
        {
            private readonly string nome;
            private readonly double x,y,larghezza,altezza;
            public ChiaveZona(string nome,RettangoloTavolo zona)
            {this.nome=nome;x=zona.X;y=zona.Y;larghezza=zona.Larghezza;altezza=zona.Altezza;}
            public bool Equals(ChiaveZona altra)=>StringComparer.Ordinal.Equals(nome,altra.nome)
                &&x.Equals(altra.x)&&y.Equals(altra.y)
                &&larghezza.Equals(altra.larghezza)&&altezza.Equals(altra.altezza);
            public override bool Equals(object obj)=>obj is ChiaveZona altra&&Equals(altra);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash=StringComparer.Ordinal.GetHashCode(nome);hash=hash*397^x.GetHashCode();hash=hash*397^y.GetHashCode();
                    hash=hash*397^larghezza.GetHashCode();return hash*397^altezza.GetHashCode();
                }
            }
        }
        private readonly Calibrazione calibrazione;
        private readonly ConfigGriglia griglia;
        private readonly object sincronizzazioneCache=new object();
        private readonly Dictionary<ChiaveZona,CacheZona> cache=new Dictionary<ChiaveZona,CacheZona>();
        private readonly LinkedList<ChiaveZona> usateDiRecente=new LinkedList<ChiaveZona>();
        public LettoreZone(Calibrazione calibrazione,ConfigGriglia griglia)
        { this.calibrazione=calibrazione??throw new ArgumentNullException(nameof(calibrazione));this.griglia=griglia??throw new ArgumentNullException(nameof(griglia)); }
        public Rect UltimaRegioneCamera { get; private set; }
        public LetturaZona Leggi(Mat frame,RettangoloTavolo zona,IReadOnlyList<CartoncinoRilevato> noti,
            TracciaCartoncini traccia=null,string chiaveCache=null)
        {
            LetturaZona r;
            if(string.IsNullOrWhiteSpace(chiaveCache))
                r=new RilevatoreCartoncini(null,calibrazione,griglia).LeggiZona(frame,zona,noti,traccia);
            else
            {
                var stato=Ottieni(new ChiaveZona(chiaveCache,zona));
                lock(stato.Sincronizzazione)r=stato.Rilevatore.LeggiZona(frame,zona,noti,traccia);
            }
            UltimaRegioneCamera=r.RegioneCamera;return r;
        }
        private CacheZona Ottieni(ChiaveZona chiave)
        {
            lock(sincronizzazioneCache)
            {
                if(cache.TryGetValue(chiave,out var esistente))
                {
                    usateDiRecente.Remove(esistente.Nodo);usateDiRecente.AddFirst(esistente.Nodo);
                    return esistente;
                }
                var nodo=usateDiRecente.AddFirst(chiave);
                var nuovo=new CacheZona(new RilevatoreCartoncini(null,calibrazione,griglia),nodo);
                cache.Add(chiave,nuovo);
                if(cache.Count>NumeroMassimoZoneMemorizzate)
                {
                    var menoRecente=usateDiRecente.Last;
                    cache.Remove(menoRecente.Value);usateDiRecente.RemoveLast();
                }
                return nuovo;
            }
        }
    }
}
