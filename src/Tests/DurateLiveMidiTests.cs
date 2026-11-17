using System.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.App;
namespace VisualMusicBlockLooper.Tests
{
    public class DurateLiveMidiTests
    {
        [Test]
        public void PubblicazioneRifiutataRiprovaLoStessoFoglioQuandoLaCodaSiLibera()
        {
            var u=new EsecutorePartituraTests.Uscita();
            using(var e=new EsecutoreMidi(u,null,new EsecutorePartituraTests.Mappa(),new ConfigGriglia(),()=>0))
            {
                var p=new PreparatoreFoglio(e);
                StrutturaTavolo.TryCrea(2,1,1,2,out var struttura);
                StatoTavolo Stato(int clip)=>new StatoTavolo(new[]{new CartoncinoNota{Riga=0,Traccia=0,IdClip=clip,DurataStep=8}},null,null,struttura);
                Assert.That(p.PreparaAvvio(Stato(40),1,0));e.Avvia();e.TickInterno();
                var riempitivo=e.Prepara(Stato(40),1,1,0);
                for(int step=1;step<=512;step++)Assert.That(e.PubblicaCella(riempitivo,step));
                Assert.That(p.AggiornaPerCella(Stato(41),true,1,1,513),Is.False);
                Assert.That(p.UltimoErrore,Is.Not.Null);
                for(int tick=0;tick<6;tick++)e.TickInterno();
                Assert.That(p.AggiornaPerCella(Stato(41),true,1,2,513),Is.True);
                Assert.That(p.UltimoErrore,Is.Null);
                Assert.That(p.AggiornaPerCella(Stato(41),true,1,3,513),Is.False,"Successful unchanged observations need no publication.");
                for(int tick=7;tick<=3078;tick++)e.TickInterno();
                Assert.That(e.Stato.PartituraAttiva.CopiaStato().Note.Single().IdClip,Is.EqualTo(41));
            }
        }
        private static void Tick(MotorePartitura m,int n){for(int i=0;i<n;i++)m.TickInterno();}
        [TestCase(5,30)] [TestCase(2,12)]
        public void CellaAggiornaDurataSenzaRipetereAttacco(int durata,int fine)
        {
            var u=new EsecutorePartituraTests.Uscita();var m=new MotorePartitura(u,new EsecutorePartituraTests.Mappa());
            m.Pubblica(EsecutorePartituraTests.Partitura(u,durata:4));m.Avvia();Tick(m,6);
            m.PubblicaCella(EsecutorePartituraTests.Partitura(u,2,durata:durata),1);Tick(m,1);
            Assert.That(m.VersioneAttiva,Is.EqualTo(2));Tick(m,fine-7);
            Assert.That(u.Eventi.Count(e=>e=="On:0:76"),Is.EqualTo(1));
            Assert.That(u.Eventi.Count(e=>e=="Off:0:76"),Is.Zero);
            Tick(m,1);Assert.That(u.Eventi.Count(e=>e=="Off:0:76"),Is.EqualTo(1));
        }
        [Test]
        public void NotaGiaFinitaNonRisorgeConEstensioneTardiva()
        {
            var u=new EsecutorePartituraTests.Uscita();var m=new MotorePartitura(u,new EsecutorePartituraTests.Mappa());
            m.Pubblica(EsecutorePartituraTests.Partitura(u));m.Avvia();Tick(m,12);
            m.PubblicaCella(EsecutorePartituraTests.Partitura(u,2,durata:6),2);Tick(m,25);
            Assert.That(u.Eventi.Count(e=>e=="On:0:76"),Is.EqualTo(1));
            Assert.That(u.Eventi.Count(e=>e=="Off:0:76"),Is.EqualTo(1));
        }
        [Test]
        public void CelleFutureDistinteRestanoInCodaEQuelleTardiveAspettanoIlGiro()
        {
            var u=new EsecutorePartituraTests.Uscita();var m=new MotorePartitura(u,new EsecutorePartituraTests.Mappa());
            m.Pubblica(EsecutorePartituraTests.Partitura(u,durata:8));m.Avvia();Tick(m,7);
            m.PubblicaCella(EsecutorePartituraTests.Partitura(u,2,durata:7),2);
            m.PubblicaCella(EsecutorePartituraTests.Partitura(u,3,durata:6),3);
            Tick(m,6);Assert.That(m.VersioneAttiva,Is.EqualTo(2));
            Tick(m,6);Assert.That(m.VersioneAttiva,Is.EqualTo(3));
            m.PubblicaCella(EsecutorePartituraTests.Partitura(u,4,durata:5),3);
            Tick(m,47);Assert.That(m.VersioneAttiva,Is.EqualTo(3));
            Tick(m,1);Assert.That(m.VersioneAttiva,Is.EqualTo(4));
        }
        [Test]
        public void IdentitaSovrapposteDelloStessoPitchConservanoLeProprieFini()
        {
            var u=new EsecutorePartituraTests.Uscita();var m=new MotorePartitura(u,new EsecutorePartituraTests.Mappa());
            StrutturaTavolo.TryCrea(2,1,1,2,out var struttura);
            PartituraMidiPreparata P(long versione,int durata)
            {
                var note=new[]{new CartoncinoNota{Riga=0,Traccia=0,IdClip=40,DurataStep=durata},
                    new CartoncinoNota{Riga=0,Traccia=0,IdClip=40,StepIniziale=1,DurataStep=1}};
                return PartituraMidiPreparata.Prepara(new CompilatorePartitura(new ConfigGriglia(),new EsecutorePartituraTests.Mappa())
                    .Compila(new StatoTavolo(note,null,null,struttura),versione,1),u);
            }
            m.Pubblica(P(1,6));m.Avvia();Tick(m,12);
            m.PubblicaCella(P(2,4),2);Tick(m,12);
            Assert.That(u.Eventi.Count(e=>e=="Off:0:76"),Is.EqualTo(1),"Only the retrigger has sent an off so far.");
            Tick(m,1);Assert.That(u.Eventi.Count(e=>e=="Off:0:76"),Is.EqualTo(2));
            Assert.That(u.Eventi.Count(e=>e=="On:0:76"),Is.EqualTo(2));
        }
        [Test]
        public void OneShotMantieneLaSuaScadenzaDuranteUnaEstensione()
        {
            var u=new EsecutorePartituraTests.Uscita();var m=new MotorePartitura(u,new EsecutorePartituraTests.Mappa());
            m.Pubblica(EsecutorePartituraTests.Partitura(u,durata:4));m.Avvia();Tick(m,6);m.OneShot(76);
            m.PubblicaCella(EsecutorePartituraTests.Partitura(u,2,durata:6),1);Tick(m,30);
            Assert.That(u.Eventi.Count(e=>e=="Off:0:76"),Is.EqualTo(1));
            Tick(m,1);Assert.That(u.Eventi.Count(e=>e=="Off:0:76"),Is.EqualTo(2));
        }
        [Test]
        public void RimuovereUnaNotaAttivaLaSpegneAllaFrontieraDellaCella()
        {
            var u=new EsecutorePartituraTests.Uscita();var m=new MotorePartitura(u,new EsecutorePartituraTests.Mappa());
            m.Pubblica(EsecutorePartituraTests.Partitura(u,durata:8));m.Avvia();Tick(m,6);
            StrutturaTavolo.TryCrea(2,1,1,2,out var struttura);
            var vuota=PartituraMidiPreparata.Prepara(new CompilatorePartitura(new ConfigGriglia(),new EsecutorePartituraTests.Mappa())
                .Compila(new StatoTavolo(new CartoncinoNota[0],null,null,struttura),2,1),u);
            m.PubblicaCella(vuota,1);Tick(m,1);
            Assert.That(u.Eventi.Count(e=>e=="Off:0:76"),Is.EqualTo(1));
            Tick(m,48);Assert.That(u.Eventi.Count(e=>e=="On:0:76"),Is.EqualTo(1));
            Assert.That(u.Eventi.Count(e=>e=="Off:0:76"),Is.EqualTo(1));
        }
    }
}
