using System;
using System.Threading;

namespace VisualMusicBlockLooper.Core
{
    /// <summary>Uno slot versionato. La pubblicazione non aspetta il lettore e non accumula candidati obsoleti.</summary>
    public sealed class ScambioPartiture
    {
        private const int CapacitaProgrammiMisura = 64;
        private PartituraMidiPreparata ultima;
        private PartituraMidiPreparata[] programmiMisura = new PartituraMidiPreparata[0];
        private long generazione = 1;
        public long Generazione => Volatile.Read(ref generazione);
        public PartituraMidiPreparata Ultima => Volatile.Read(ref ultima);
        public long VersionePronta
        {
            get
            {
                long versione=Ultima?.Dati.VersioneFoglio??0;
                foreach(var p in Volatile.Read(ref programmiMisura))
                    if(!p.Consumato&&p.Errore==null&&p.EventiPubblicati==p.Eventi.Length
                        &&p.Dati.Generazione==Generazione&&p.Dati.VersioneFoglio>versione)versione=p.Dati.VersioneFoglio;
                return versione;
            }
        }
        public bool Pubblica(PartituraMidiPreparata nuova)
        {
            if (nuova == null) throw new ArgumentNullException(nameof(nuova));
            if(nuova.Dati.PrimaMisuraAssoluta>=0)return PubblicaMisura(nuova);
            while (nuova.Dati.Generazione == Generazione)
            {
                var prima = Ultima;
                if (prima != null && prima.Dati.Generazione == nuova.Dati.Generazione && prima.Dati.VersioneFoglio >= nuova.Dati.VersioneFoglio) return false;
                if(prima!=null&&!prima.Dati.Struttura.Equals(nuova.Dati.Struttura))SvuotaProgrammiMisura();
                if (Interlocked.CompareExchange(ref ultima, nuova, prima) == prima) return nuova.Dati.Generazione == Generazione;
            }
            return false;
        }
        private bool PubblicaMisura(PartituraMidiPreparata nuova)
        {
            while(nuova.Dati.Generazione==Generazione)
            {
                var prima=Volatile.Read(ref programmiMisura);
                var mantenuti=new System.Collections.Generic.List<PartituraMidiPreparata>(prima.Length+1);
                bool duplicato=false;
                foreach(var p in prima)
                {
                    if(p.Consumato||p.Dati.Generazione!=Generazione)continue;
                    if(p.Dati.PrimaMisuraAssoluta==nuova.Dati.PrimaMisuraAssoluta)
                    {
                        duplicato=true;
                        if(p.Dati.VersioneFoglio>=nuova.Dati.VersioneFoglio)return false;
                        continue;
                    }
                    mantenuti.Add(p);
                }
                if(mantenuti.Count>=CapacitaProgrammiMisura)return false;
                mantenuti.Add(nuova);
                mantenuti.Sort((a,b)=>
                {
                    int ordine=a.Dati.PrimaMisuraAssoluta.CompareTo(b.Dati.PrimaMisuraAssoluta);
                    return ordine!=0?ordine:a.Dati.VersioneFoglio.CompareTo(b.Dati.VersioneFoglio);
                });
                var dopo=mantenuti.ToArray();
                if(Interlocked.CompareExchange(ref programmiMisura,dopo,prima)==prima)return nuova.Dati.Generazione==Generazione;
                if(duplicato)continue;
            }
            return false;
        }
        public void ImpostaGenerazione(long nuova)
        {
            if (nuova < Generazione) throw new ArgumentOutOfRangeException(nameof(nuova));
            Interlocked.Exchange(ref generazione, nuova);
            SvuotaProgrammiMisura();
            while (true)
            {
                var prima = Ultima;
                if (prima == null || prima.Dati.Generazione == nuova) return;
                if (Interlocked.CompareExchange(ref ultima, null, prima) == prima) return;
            }
        }
        public PartituraMidiPreparata Leggi(long frontiera)
        {
            var p = Ultima;
            return p != null && p.Dati.Generazione == Generazione && p.Dati.PrimaFrontiera <= frontiera ? p : null;
        }
        public PartituraMidiPreparata LeggiMisura(long misuraAssoluta,StrutturaTavolo struttura)
        {
            var programmi=Volatile.Read(ref programmiMisura);
            PartituraMidiPreparata scelta=null;
            foreach(var p in programmi)
            {
                if(p.Consumato||p.Errore!=null||p.EventiPubblicati!=p.Eventi.Length
                    ||p.Dati.Generazione!=Generazione||!p.Dati.Struttura.Equals(struttura))continue;
                long scarto=misuraAssoluta-p.Dati.PrimaMisuraAssoluta;
                int periodo=p.Dati.Struttura.MisurePerGiro;
                if(scarto<0||periodo<1||scarto%periodo!=0)continue;
                if(scelta==null||p.Dati.VersioneFoglio>scelta.Dati.VersioneFoglio
                    ||(p.Dati.VersioneFoglio==scelta.Dati.VersioneFoglio
                        &&p.Dati.PrimaMisuraAssoluta>scelta.Dati.PrimaMisuraAssoluta))scelta=p;
            }
            if(scelta==null)return null;
            foreach(var p in programmi)
            {
                if(p.Dati.Generazione!=Generazione||!p.Dati.Struttura.Equals(struttura))continue;
                if(p.Errore!=null){p.MarcaConsumato();continue;}
                if(p.EventiPubblicati!=p.Eventi.Length)continue;
                long scarto=misuraAssoluta-p.Dati.PrimaMisuraAssoluta;
                int periodo=p.Dati.Struttura.MisurePerGiro;
                if(scarto>=0&&periodo>0&&scarto%periodo==0)p.MarcaConsumato();
            }
            Promuovi(scelta);
            return scelta;
        }
        private void Promuovi(PartituraMidiPreparata nuova)
        {
            while(nuova.Dati.Generazione==Generazione)
            {
                var prima=Ultima;
                if(prima!=null&&prima.Dati.Generazione==nuova.Dati.Generazione
                    &&prima.Dati.VersioneFoglio>=nuova.Dati.VersioneFoglio)return;
                if(Interlocked.CompareExchange(ref ultima,nuova,prima)==prima)return;
            }
        }
        public void SvuotaProgrammiMisura()=>Interlocked.Exchange(ref programmiMisura,new PartituraMidiPreparata[0]);
    }
}
