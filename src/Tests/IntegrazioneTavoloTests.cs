using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.App;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    /// <summary>
    /// Prova di integrazione del tavolo con i cartoncini speciali: <see cref="Compositore"/> con il layout di default,
    /// rilevatore, uscita MIDI e mappatura finti. Le posizioni attese sono scritte a mano dalle tabelle della spec
    /// (misura 4/4 = 96 tick, step = 6 tick, collegamento e A-B), non calcolate con il codice di produzione.
    /// </summary>
    [TestFixture]
    public class IntegrazioneTavoloTests
    {
        private sealed class UscitaRegistro : IUscitaMidi
        {
            public List<string> Registro { get; } = new List<string>();

            public void NotaOn(int canale, int nota, int velocity) => Registro.Add($"NotaOn:{canale}:{nota}:{velocity}");

            public void NotaOff(int canale, int nota) => Registro.Add($"NotaOff:{canale}:{nota}");

            public void Clock() => Registro.Add("Clock");

            public void Start() => Registro.Add("Start");

            public void Stop() => Registro.Add("Stop");

            public void Continue() => Registro.Add("Continue");
        }

        /// <summary>Mappatura della prova: canale = colore (traccia), nota = 36 + IdClip.</summary>
        private sealed class MappaturaDiretta : IMappaturaMidi
        {
            public int Canale(int traccia) => traccia;

            public int Nota(int idClip) => 36 + idClip;

            public int Velocity => 100;
        }

        private sealed class RilevatoreFisso : IRilevatoreCartoncini
        {
            public IReadOnlyList<CartoncinoRilevato> Cartoncini { get; set; } = new List<CartoncinoRilevato>();

            public IReadOnlyList<CartoncinoRilevato> Rileva() => Cartoncini;
        }

        /// <summary>
        /// Banco di prova: esegue Passo e tick e registra, per ogni evento di note-on e note-off,
        /// il tick (0-based, il primo TickInterno è il tick 0) in cui è stato prodotto.
        /// </summary>
        private sealed class Banco
        {
            private readonly UscitaRegistro uscita;
            private readonly Compositore compositore;
            private int prossimoTick;

            public Banco(UscitaRegistro uscita, Compositore compositore)
            {
                this.uscita = uscita;
                this.compositore = compositore;
            }

            public List<string> Eventi { get; } = new List<string>();

            public int ProssimoTick => prossimoTick;

            public Compositore Compositore => compositore;

            public void Passi(int quanti)
            {
                for (int i = 0; i < quanti; i++)
                {
                    compositore.Passo();
                }
            }

            public void Tick(int quanti)
            {
                for (int i = 0; i < quanti; i++)
                {
                    int prima = uscita.Registro.Count;
                    compositore.Motore.TickInterno();
                    Raccogli(prima);
                    prossimoTick++;
                }
            }

            public void Arresta()
            {
                int prima = uscita.Registro.Count;
                compositore.Motore.Arresta();
                Raccogli(prima);
            }

            private void Raccogli(int daIndice)
            {
                for (int j = daIndice; j < uscita.Registro.Count; j++)
                {
                    string evento = uscita.Registro[j];
                    if (evento.StartsWith("NotaOn") || evento.StartsWith("NotaOff"))
                    {
                        Eventi.Add($"{prossimoTick} {evento}");
                    }
                }
            }
        }

        /// <summary>Colore della riga: riga 0 Rosso, 1 Arancione, 2 Verde, 3 Blu, 4 Viola, 5 Giallo (canali 0,1,3,4,5,2).</summary>
        private static readonly ColoreTraccia[] ColoriRighe =
        {
            ColoreTraccia.Rosso,
            ColoreTraccia.Arancione,
            ColoreTraccia.Verde,
            ColoreTraccia.Blu,
            ColoreTraccia.Viola,
            ColoreTraccia.Giallo,
        };

        private static ConfigGriglia Config() => new ConfigGriglia { Area = LayoutTavolo.AreaGrigliaDefault };

        /// <summary>Cartoncino nota all'inizio della riga indicata (step 0), con durata in step e IdClip = riga.</summary>
        private static CartoncinoRilevato Nota(int riga, int durataStep = 2)
        {
            RettangoloTavolo g = LayoutTavolo.AreaGrigliaDefault;
            double colonna = g.Larghezza / 16;
            double altezzaRiga = g.Altezza / 6;
            return new CartoncinoRilevato
            {
                Payload = "VMBL1;N;" + riga,
                Colore = ColoriRighe[riga],
                Area = new RettangoloTavolo(
                    g.X + 0.005,
                    g.Y + riga * altezzaRiga + 0.02,
                    durataStep * colonna - 0.01,
                    altezzaRiga - 0.04)
            };
        }

        /// <summary>Cartoncino speciale posto al centro della zona indicata.</summary>
        private static CartoncinoRilevato NelCentro(RettangoloTavolo zona, string payload)
        {
            double cx = zona.X + zona.Larghezza / 2;
            double cy = zona.Y + zona.Altezza / 2;
            return new CartoncinoRilevato
            {
                Payload = payload,
                Area = new RettangoloTavolo(cx - 0.04, cy - 0.03, 0.08, 0.06)
            };
        }

        private static CartoncinoRilevato Comando(string nome)
        {
            return NelCentro(LayoutTavolo.Default.Comandi, "VMBL1;C;" + nome);
        }

        private static Banco NuovoBanco(RilevatoreFisso rilevatore)
        {
            var uscita = new UscitaRegistro();
            var compositore = new Compositore(rilevatore, uscita, null, new MappaturaDiretta(), Config(), LayoutTavolo.Default, () => 0);
            return new Banco(uscita, compositore);
        }

        [Test]
        public void TreCoppie_ContemporaneeEInSequenza()
        {
            var rilevatore = new RilevatoreFisso
            {
                Cartoncini = new[]
                {
                    NelCentro(LayoutTavolo.Default.Collegamento, "VMBL1;S;LINK;2"),
                    Comando("PLAY"),
                    Nota(0), Nota(1), Nota(2), Nota(3), Nota(4), Nota(5),
                }
            };
            Banco banco = NuovoBanco(rilevatore);

            banco.Passi(4);
            banco.Tick(205);

            // Righe 0,2,4 nella prima misura (canali 0,3,5); righe 1,3,5 nella seconda (canali 1,2,4).
            // Ogni nota dura 2 step = 12 tick e termina 12 tick dopo l'inizio.
            var attesi = new List<string>
            {
                "0 NotaOn:0:36:100",
                "0 NotaOn:3:38:100",
                "0 NotaOn:5:40:100",
                "12 NotaOff:0:36",
                "12 NotaOff:3:38",
                "12 NotaOff:5:40",
                "96 NotaOn:1:37:100",
                "96 NotaOn:2:41:100",
                "96 NotaOn:4:39:100",
                "108 NotaOff:1:37",
                "108 NotaOff:2:41",
                "108 NotaOff:4:39",
                "192 NotaOn:0:36:100",
                "192 NotaOn:3:38:100",
                "192 NotaOn:5:40:100",
                "204 NotaOff:0:36",
                "204 NotaOff:3:38",
                "204 NotaOff:5:40",
            };
            Assert.That(banco.Eventi, Is.EqualTo(attesi));
        }

        [Test]
        public void AbRipeteSoloLaParteScelta()
        {
            var rilevatore = new RilevatoreFisso
            {
                Cartoncini = new[]
                {
                    NelCentro(LayoutTavolo.Default.Collegamento, "VMBL1;S;LINK;3"),
                    NelCentro(LayoutTavolo.Default.Ab, "VMBL1;S;AB;2;3"),
                    Comando("PLAY"),
                    Nota(0), Nota(1), Nota(2), Nota(3), Nota(4), Nota(5),
                }
            };
            Banco banco = NuovoBanco(rilevatore);

            banco.Passi(4);
            banco.Tick(576);

            // Due gruppi di tre righe, A-B = [2,3] (1-based): il giro dura 2 misure = 192 tick.
            // Misura 1 del giro: righe 1 e 4 (0-based). Misura 2: righe 2 e 5 (0-based).
            var attesi = new List<string>();
            for (int giro = 0; giro < 3; giro++)
            {
                int base0 = giro * 192;
                attesi.Add($"{base0} NotaOn:1:37:100");
                attesi.Add($"{base0} NotaOn:5:40:100");
                attesi.Add($"{base0 + 12} NotaOff:1:37");
                attesi.Add($"{base0 + 12} NotaOff:5:40");
                attesi.Add($"{base0 + 96} NotaOn:2:41:100");
                attesi.Add($"{base0 + 96} NotaOn:3:38:100");
                attesi.Add($"{base0 + 108} NotaOff:2:41");
                attesi.Add($"{base0 + 108} NotaOff:3:38");
            }

            Assert.That(banco.Eventi, Is.EqualTo(attesi));
            Assert.That(banco.Eventi.Any(e => e.Contains("NotaOn:0:36") || e.Contains("NotaOn:4:39")), Is.False,
                "Le righe 0 e 3 (0-based) non devono mai suonare con A-B = [2,3].");
        }

        [Test]
        public void RimuoviCartoncinoSpeciale_TornaAlDefaultSenzaNoteAppese()
        {
            var rilevatore = new RilevatoreFisso
            {
                Cartoncini = new[]
                {
                    NelCentro(LayoutTavolo.Default.Collegamento, "VMBL1;S;LINK;1"),
                    Comando("PLAY"),
                    Nota(0, 16), Nota(1, 16), Nota(2, 16),
                }
            };
            Banco banco = NuovoBanco(rilevatore);
            banco.Passi(4);
            banco.Tick(100);

            // Il cartoncino LINK viene tolto e PLAY non viene più presentato: restano solo le note.
            rilevatore.Cartoncini = new[] { Nota(0, 16), Nota(1, 16), Nota(2, 16) };
            int tickCambio = -1;
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    if (i < 4)
                    {
                        banco.Passi(1);
                        if (tickCambio < 0 && banco.Compositore.Motore.Struttura.Equals(StrutturaTavolo.Default))
                        {
                            tickCambio = banco.ProssimoTick;
                        }
                    }

                    banco.Tick(1);
                }

                banco.Arresta();
            });

            Assert.That(tickCambio, Is.GreaterThanOrEqualTo(0), "La struttura non è tornata al default.");

            // Ogni note-on è chiuso da uno e un solo note-off; nessuna nota resta appesa; nessun note-off doppio.
            // Formato evento: "<tick> NotaOn:<canale>:<nota>:<velocity>" oppure "<tick> NotaOff:<canale>:<nota>".
            var aperte = new HashSet<string>();
            foreach (string evento in banco.Eventi)
            {
                string[] parti = evento.Substring(evento.IndexOf(' ') + 1).Split(':');
                string chiave = parti[1] + ":" + parti[2];
                if (parti[0] == "NotaOn")
                {
                    Assert.That(aperte.Add(chiave), Is.True, $"Note-on doppio senza note-off: {evento}");
                }
                else
                {
                    Assert.That(aperte.Remove(chiave), Is.True, $"Note-off senza note-on: {evento}");
                }
            }

            Assert.That(aperte, Is.Empty, "Note rimaste accese dopo l'arresto.");

            // Con il default (6 righe in sequenza) le righe 1 e 2 (0-based) suonano solo nella loro misura:
            // misura m del giro = (tick / 96) mod 6, le note iniziano sempre allo step 0 (tick multipli di 96).
            var noteDopoCambio = banco.Eventi
                .Select(e => (Tick: int.Parse(e.Substring(0, e.IndexOf(' '))), Corpo: e.Substring(e.IndexOf(' ') + 1)))
                .Where(e => e.Tick >= tickCambio && e.Corpo.StartsWith("NotaOn:"))
                .ToList();

            var riga1 = noteDopoCambio.Where(e => e.Corpo.StartsWith("NotaOn:1:37")).ToList();
            var riga2 = noteDopoCambio.Where(e => e.Corpo.StartsWith("NotaOn:3:38")).ToList();
            Assert.That(riga1, Is.Not.Empty);
            Assert.That(riga2, Is.Not.Empty);
            Assert.That(riga1.All(e => e.Tick % 96 == 0 && (e.Tick / 96) % 6 == 1), Is.True,
                "La riga 1 non deve suonare prima della sua misura nel giro di 6.");
            Assert.That(riga2.All(e => e.Tick % 96 == 0 && (e.Tick / 96) % 6 == 2), Is.True,
                "La riga 2 non deve suonare prima della sua misura nel giro di 6.");
        }

        [Test]
        public void CambioDiStruttura_SpegneSubitoLeNoteInCorso()
        {
            // Nota di 16 step (96 tick) con LINK 1: senza cambio finirebbe al tick 96.
            var rilevatore = new RilevatoreFisso
            {
                Cartoncini = new[]
                {
                    NelCentro(LayoutTavolo.Default.Collegamento, "VMBL1;S;LINK;1"),
                    Comando("PLAY"),
                    Nota(0, 16),
                }
            };
            Banco banco = NuovoBanco(rilevatore);
            banco.Passi(4);
            banco.Tick(10);

            // Il LINK viene tolto: la struttura diventa Default al terzo Passo (stabilizzazione a 3 frame).
            rilevatore.Cartoncini = new[] { Nota(0, 16) };
            banco.Passi(1);
            banco.Tick(1);
            banco.Passi(1);
            banco.Tick(1);
            banco.Passi(1);
            Assert.That(banco.Compositore.Motore.Struttura, Is.EqualTo(StrutturaTavolo.Default));

            // Il tick 12 è il primo con la nuova struttura: la nota in corso si spegne lì, non al tick 96.
            banco.Tick(1);
            string primoOff = banco.Eventi.First(e => e.Contains("NotaOff:0:36"));
            Assert.That(primoOff, Is.EqualTo("12 NotaOff:0:36"));
        }

        [Test]
        public void Misura34_TroncaLeNoteLunghe()
        {
            var rilevatore = new RilevatoreFisso
            {
                Cartoncini = new[]
                {
                    NelCentro(LayoutTavolo.Default.Misura, "VMBL1;S;MISURA;3"),
                    Comando("PLAY"),
                    Nota(0, 16),
                }
            };
            Banco banco = NuovoBanco(rilevatore);

            banco.Passi(4);
            banco.Tick(100);

            // 3/4 = 12 step = 72 tick: la nota di 16 step (dall'inizio) finisce alla fine della misura.
            var attesi = new List<string>
            {
                "0 NotaOn:0:36:100",
                "72 NotaOff:0:36",
            };
            Assert.That(banco.Eventi, Is.EqualTo(attesi));
        }
    }
}
