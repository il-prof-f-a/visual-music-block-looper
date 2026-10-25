using System.Linq;
using NUnit.Framework;
using VisualMusicBlockLooper.Core;

namespace VisualMusicBlockLooper.Tests
{
    [TestFixture]
    public class InterpreteTavoloTests
    {
        // Griglia nell'area (0,0,1,0.8) con 6 tracce e 16 step; la zona comandi occupa la fascia sotto.
        private static readonly RettangoloTavolo ZonaComandi = new RettangoloTavolo(0, 0.8, 1, 0.2);
        private const double AltezzaTraccia = 0.8 / 6;
        private const double LarghezzaStep = 1.0 / 16;
        private const double AltezzaCartoncino = 0.05;

        [Test]
        public void NotaValida()
        {
            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;N;12", ColoreTraccia.Verde, Cella(3, 2))
            });

            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Comandi, Is.Empty);
            Assert.That(stato.Note, Has.Count.EqualTo(1));

            CartoncinoNota nota = stato.Note[0];
            Assert.That(nota.Traccia, Is.EqualTo(0), "Con il default (6 righe collegate) c'e' un solo strumento");
            Assert.That(nota.StepIniziale, Is.EqualTo(2));
            Assert.That(nota.DurataStep, Is.EqualTo(1));
            Assert.That(nota.IdClip, Is.EqualTo(12));
            Assert.That(nota.Colore, Is.EqualTo(ColoreTraccia.Verde));
            Assert.That(nota.Riga, Is.EqualTo(3));
            Assert.That(nota.Chiave, Is.EqualTo("12:0:3:2"));
        }

        [Test]
        public void ColoriMistiNellaStessaRiga_StessoStrumento()
        {
            StatoTavolo stato = CreaInterpreteCompleto().Interpreta(new[]
            {
                Carta(3, 2, 1, ColoreTraccia.Rosso, "VMBL1;N;1"),
                Carta(3, 5, 1, ColoreTraccia.Verde, "VMBL1;N;2")
            });

            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Note, Has.Count.EqualTo(2));
            Assert.That(stato.Note.Select(n => n.Traccia), Is.All.EqualTo(0));
            Assert.That(stato.Note.Select(n => n.Riga), Is.All.EqualTo(3));
        }

        [Test]
        public void TracciaIndipendenteDalColore()
        {
            StatoTavolo stato = CreaInterpreteCompleto().Interpreta(new[]
            {
                Carta(1, 4, 1, ColoreTraccia.Verde, "VMBL1;N;7")
            });

            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Note, Has.Count.EqualTo(1));
            Assert.That(stato.Note[0].Riga, Is.EqualTo(1));
            Assert.That(stato.Note[0].Traccia, Is.EqualTo(0));
            Assert.That(stato.Note[0].StepIniziale, Is.EqualTo(4));
            Assert.That(stato.Note[0].Chiave, Is.EqualTo("7:0:1:4"));
        }

        [TestCase(1, new[] { 0, 1, 2, 3, 4, 5 })]
        [TestCase(2, new[] { 0, 0, 1, 1, 2, 2 })]
        [TestCase(3, new[] { 0, 0, 0, 1, 1, 1 })]
        [TestCase(6, new[] { 0, 0, 0, 0, 0, 0 })]
        public void StrumentoPerGruppoDiRigheCollegate(int righeCollegate, int[] tracceAttese)
        {
            var cartoncini = new System.Collections.Generic.List<CartoncinoRilevato>
            {
                NellaZona(ZonaSpeciale.Collegamento, "VMBL1;S;LINK;" + righeCollegate)
            };
            for (int riga = 0; riga < 6; riga++)
            {
                cartoncini.Add(Carta(riga, 0, 1, ColoreTraccia.Rosso, "VMBL1;N;" + riga));
            }

            StatoTavolo stato = CreaInterpreteCompleto().Interpreta(cartoncini);

            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Note.OrderBy(n => n.Riga).Select(n => n.Traccia), Is.EqualTo(tracceAttese));
        }

        [Test]
        public void StessoColoreSovrappostoNellaRiga()
        {
            StatoTavolo stato = CreaInterpreteCompleto().Interpreta(new[]
            {
                Carta(2, 4, 2, ColoreTraccia.Blu, "VMBL1;N;1"),
                Carta(2, 5, 1, ColoreTraccia.Blu, "VMBL1;N;2")
            });

            Assert.That(stato.Note, Has.Count.EqualTo(1));
            Assert.That(stato.Errori, Has.Count.EqualTo(1));
            Assert.That(stato.Errori[0].Tipo, Is.EqualTo(TipoErrore.Sovrapposto));
        }

        [Test]
        public void ColoriDiversiSovrapposti_StessaRiga_Errore()
        {
            StatoTavolo stato = CreaInterpreteCompleto().Interpreta(new[]
            {
                Carta(2, 4, 2, ColoreTraccia.Blu, "VMBL1;N;1"),
                Carta(2, 4, 2, ColoreTraccia.Viola, "VMBL1;N;2")
            });

            // Il colore non distingue gli strumenti: due note sovrapposte nella stessa riga sono in conflitto.
            Assert.That(stato.Note, Has.Count.EqualTo(1));
            Assert.That(stato.Errori, Has.Count.EqualTo(1));
            Assert.That(stato.Errori[0].Tipo, Is.EqualTo(TipoErrore.Sovrapposto));
        }

        [Test]
        public void StrutturaDaiCartoniSpeciali()
        {
            StatoTavolo stato = CreaInterpreteCompleto().Interpreta(new[]
            {
                NellaZona(ZonaSpeciale.Misura, "VMBL1;S;MISURA;3"),
                NellaZona(ZonaSpeciale.Collegamento, "VMBL1;S;LINK;2")
            });

            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Struttura.BattitiPerMisura, Is.EqualTo(3));
            Assert.That(stato.Struttura.RigheCollegate, Is.EqualTo(2));
            Assert.That(stato.Struttura.AInizio, Is.EqualTo(1));
            Assert.That(stato.Struttura.AFine, Is.EqualTo(6), "Default: tutte le battute (2 righe x 3 battiti)");
        }

        [Test]
        public void SenzaCartoniSpeciali_StrutturaDefault()
        {
            StatoTavolo stato = CreaInterpreteCompleto().Interpreta(new[]
            {
                Carta(0, 0, 1, ColoreTraccia.Rosso, "VMBL1;N;1")
            });

            Assert.That(stato.Struttura, Is.EqualTo(StrutturaTavolo.Default));
        }

        [Test]
        public void MisuraTreQuarti_NotaAlloStep14_FuoriMisura()
        {
            StatoTavolo stato = CreaInterpreteCompleto().Interpreta(new[]
            {
                NellaZona(ZonaSpeciale.Misura, "VMBL1;S;MISURA;3"),
                Carta(0, 14, 1, ColoreTraccia.Rosso, "VMBL1;N;1")
            });

            Assert.That(stato.Note, Is.Empty);
            Assert.That(stato.Errori, Has.Count.EqualTo(1));
            Assert.That(stato.Errori[0].Tipo, Is.EqualTo(TipoErrore.FuoriMisura));
        }

        [Test]
        public void MisuraTreQuarti_DurataTroncata()
        {
            StatoTavolo stato = CreaInterpreteCompleto().Interpreta(new[]
            {
                NellaZona(ZonaSpeciale.Misura, "VMBL1;S;MISURA;3"),
                Carta(0, 8, 6, ColoreTraccia.Rosso, "VMBL1;N;1")
            });

            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Note, Has.Count.EqualTo(1));
            Assert.That(stato.Note[0].StepIniziale, Is.EqualTo(8));
            Assert.That(stato.Note[0].DurataStep, Is.EqualTo(4));
        }

        [Test]
        public void ZonaSpecialeConNota_FuoriGriglia()
        {
            StatoTavolo stato = CreaInterpreteCompleto().Interpreta(new[]
            {
                NellaZona(ZonaSpeciale.Misura, "VMBL1;N;1")
            });

            Assert.That(stato.Note, Is.Empty);
            Assert.That(stato.Errori, Has.Count.EqualTo(1));
            Assert.That(stato.Errori[0].Tipo, Is.EqualTo(TipoErrore.FuoriGriglia));
        }

        [Test]
        public void ComandoInZonaSpeciale_PayloadNonValido()
        {
            StatoTavolo stato = CreaInterpreteCompleto().Interpreta(new[]
            {
                NellaZona(ZonaSpeciale.Misura, "VMBL1;C;PLAY")
            });

            Assert.That(stato.Comandi, Is.Empty);
            Assert.That(stato.Errori, Has.Count.EqualTo(1));
            Assert.That(stato.Errori[0].Tipo, Is.EqualTo(TipoErrore.PayloadNonValido));
        }

        [Test]
        public void ImpostazioneFuoriZone_PayloadNonValido()
        {
            StatoTavolo stato = CreaInterpreteCompleto().Interpreta(new[]
            {
                Carta(0, 2, 1, null, "VMBL1;S;MISURA;3")
            });

            Assert.That(stato.Note, Is.Empty);
            Assert.That(stato.Struttura, Is.EqualTo(StrutturaTavolo.Default));
            Assert.That(stato.Errori, Has.Count.EqualTo(1));
            Assert.That(stato.Errori[0].Tipo, Is.EqualTo(TipoErrore.PayloadNonValido));
        }

        [Test]
        public void OverloadSenzaZoneSpeciali_NessunaZonaSpeciale()
        {
            // Con il solo overload della zona comandi, una nota nella vecchia posizione della zona Misura è una nota.
            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;N;1", ColoreTraccia.Rosso, new RettangoloTavolo(0.01, 0.04, 0.01, 0.05))
            });

            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Note, Has.Count.EqualTo(1));
            Assert.That(stato.Struttura, Is.EqualTo(StrutturaTavolo.Default));
        }

        [Test]
        public void LayoutNull_ArgumentNullException()
        {
            var griglia = new Griglia(new ConfigGriglia());

            Assert.Throws<System.ArgumentNullException>(() => new InterpreteTavolo(griglia, (LayoutTavolo)null));
        }

        [Test]
        public void GrigliaNull_ArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() => new InterpreteTavolo(null, LayoutTavolo.Default));
        }

        [Test]
        public void SenzaColore_NotaValida()
        {
            RettangoloTavolo area = Cella(3, 2);

            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;N;12", null, area)
            });

            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Note, Has.Count.EqualTo(1));
            Assert.That(stato.Note[0].Traccia, Is.EqualTo(0));
            Assert.That(stato.Note[0].IdClip, Is.EqualTo(12));
        }

        [Test]
        public void ComandoInZonaComandi()
        {
            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;C;PLAY", null, AreaInZonaComandi())
            });

            Assert.That(stato.Note, Is.Empty);
            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Comandi, Has.Count.EqualTo(1));
            Assert.That(stato.Comandi[0].Tipo, Is.EqualTo(TipoComando.Play));
            Assert.That(stato.Comandi[0].Parametro, Is.EqualTo(0));
        }

        [Test]
        public void NotaInZonaComandiRifiutata()
        {
            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;N;12", ColoreTraccia.Verde, AreaInZonaComandi())
            });

            Assert.That(stato.Note, Is.Empty);
            Assert.That(stato.Comandi, Is.Empty);
            Assert.That(stato.Errori, Has.Count.EqualTo(1));
            Assert.That(stato.Errori[0].Tipo, Is.EqualTo(TipoErrore.PayloadNonValido));
        }

        [Test]
        public void ComandoFuoriZonaComandiRifiutato()
        {
            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;C;PLAY", null, Cella(3, 2))
            });

            Assert.That(stato.Note, Is.Empty);
            Assert.That(stato.Comandi, Is.Empty);
            Assert.That(stato.Errori, Has.Count.EqualTo(1));
            Assert.That(stato.Errori[0].Tipo, Is.EqualTo(TipoErrore.PayloadNonValido));
        }

        [Test]
        public void PayloadNonValido()
        {
            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;N;abc", ColoreTraccia.Verde, Cella(3, 2))
            });

            Assert.That(stato.Note, Is.Empty);
            Assert.That(stato.Comandi, Is.Empty);
            Assert.That(stato.Errori, Has.Count.EqualTo(1));
            Assert.That(stato.Errori[0].Tipo, Is.EqualTo(TipoErrore.PayloadNonValido));
        }

        [Test]
        public void SovrappostoStessaCella()
        {
            // La prima per Area.X (step 4-5) resta; la seconda (step 5) si sovrappone e va in errore.
            RettangoloTavolo primaArea = Cella(2, 4, 2);
            RettangoloTavolo secondaArea = Cella(2, 5, 1);

            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;N;2", ColoreTraccia.Giallo, secondaArea),
                Rilevato("VMBL1;N;1", ColoreTraccia.Giallo, primaArea)
            });

            Assert.That(stato.Comandi, Is.Empty);
            Assert.That(stato.Note, Has.Count.EqualTo(1));
            Assert.That(stato.Note[0].IdClip, Is.EqualTo(1));
            Assert.That(stato.Note[0].StepIniziale, Is.EqualTo(4));
            Assert.That(stato.Note[0].DurataStep, Is.EqualTo(2));
            Assert.That(stato.Errori, Has.Count.EqualTo(1));
            Assert.That(stato.Errori[0].Tipo, Is.EqualTo(TipoErrore.Sovrapposto));
            Assert.That(stato.Errori[0].Area.X, Is.EqualTo(secondaArea.X));
        }

        [Test]
        public void FuoriGriglia()
        {
            RettangoloTavolo area = new RettangoloTavolo(1.2, Cella(3, 0).Y, LarghezzaStep, AltezzaCartoncino);

            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;N;12", ColoreTraccia.Verde, area)
            });

            Assert.That(stato.Note, Is.Empty);
            Assert.That(stato.Comandi, Is.Empty);
            Assert.That(stato.Errori, Has.Count.EqualTo(1));
            Assert.That(stato.Errori[0].Tipo, Is.EqualTo(TipoErrore.FuoriGriglia));
            Assert.That(stato.Errori[0].Area.X, Is.EqualTo(area.X));
        }

        [Test]
        public void InputNull_StatoVuoto()
        {
            StatoTavolo stato = null;

            Assert.DoesNotThrow(() => stato = CreaInterprete().Interpreta(null));

            Assert.That(stato.Note, Is.Not.Null.And.Empty);
            Assert.That(stato.Comandi, Is.Not.Null.And.Empty);
            Assert.That(stato.Errori, Is.Not.Null.And.Empty);
            Assert.That(stato.Struttura, Is.EqualTo(StrutturaTavolo.Default));
        }

        [Test]
        public void ElementoNull_Ignorato()
        {
            StatoTavolo stato = null;

            Assert.DoesNotThrow(() => stato = CreaInterprete().Interpreta(new CartoncinoRilevato[]
            {
                null,
                Rilevato("VMBL1;N;12", ColoreTraccia.Verde, Cella(3, 2)),
                null
            }));

            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Comandi, Is.Empty);
            Assert.That(stato.Note, Has.Count.EqualTo(1));
            Assert.That(stato.Note[0].IdClip, Is.EqualTo(12));
        }

        [Test]
        public void SovrapposizioneParziale()
        {
            // Nota [2,5) e nota [4,7) sulla stessa traccia: la seconda si sovrappone solo in parte.
            RettangoloTavolo primaArea = Cella(2, 2, 3);
            RettangoloTavolo secondaArea = Cella(2, 4, 3);

            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;N;1", ColoreTraccia.Giallo, primaArea),
                Rilevato("VMBL1;N;2", ColoreTraccia.Giallo, secondaArea)
            });

            Assert.That(stato.Note, Has.Count.EqualTo(1));
            Assert.That(stato.Note[0].IdClip, Is.EqualTo(1));
            Assert.That(stato.Errori, Has.Count.EqualTo(1));
            Assert.That(stato.Errori[0].Tipo, Is.EqualTo(TipoErrore.Sovrapposto));
            Assert.That(stato.Errori[0].Area.X, Is.EqualTo(secondaArea.X));
        }

        [Test]
        public void StessiStepSuRigheDiverse_Accettati()
        {
            StatoTavolo stato = CreaInterprete().Interpreta(new[]
            {
                Rilevato("VMBL1;N;1", ColoreTraccia.Arancione, Cella(1, 4)),
                Rilevato("VMBL1;N;2", ColoreTraccia.Blu, Cella(4, 4))
            });

            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Note, Has.Count.EqualTo(2));
            Assert.That(stato.Note[0].Traccia, Is.EqualTo(0));
            Assert.That(stato.Note[0].StepIniziale, Is.EqualTo(4));
            Assert.That(stato.Note[1].Traccia, Is.EqualTo(0));
            Assert.That(stato.Note[1].StepIniziale, Is.EqualTo(4));
        }

        private static InterpreteTavolo CreaInterprete()
        {
            var config = new ConfigGriglia { Area = new RettangoloTavolo(0, 0, 1, 0.8) };
            return new InterpreteTavolo(new Griglia(config), ZonaComandi);
        }

        private static RettangoloTavolo Cella(int traccia, int step, int durataStep = 1)
        {
            return new RettangoloTavolo(
                step * LarghezzaStep,
                (traccia + 0.5) * AltezzaTraccia - AltezzaCartoncino / 2,
                durataStep * LarghezzaStep,
                AltezzaCartoncino);
        }

        private static RettangoloTavolo AreaInZonaComandi()
        {
            return new RettangoloTavolo(0.1, 0.85, LarghezzaStep, AltezzaCartoncino);
        }

        private static CartoncinoRilevato Rilevato(string payload, ColoreTraccia? colore, RettangoloTavolo area)
        {
            return new CartoncinoRilevato { Payload = payload, Colore = colore, Area = area };
        }

        private static InterpreteTavolo CreaInterpreteCompleto()
        {
            var config = new ConfigGriglia { Area = LayoutTavolo.AreaGrigliaDefault };
            return new InterpreteTavolo(new Griglia(config), LayoutTavolo.Default);
        }

        // Cartoncino nella griglia di default: colonna = Larghezza/16, riga = Altezza/6.
        // Il rettangolo resta dentro la propria cella (margini di un decimo di colonna).
        private static CartoncinoRilevato Carta(int riga, int step, int larghezzaStep, ColoreTraccia? colore, string payload)
        {
            RettangoloTavolo g = LayoutTavolo.AreaGrigliaDefault;
            double colonna = g.Larghezza / 16.0;
            double altezzaRiga = g.Altezza / 6.0;
            double margine = colonna * 0.1;

            var area = new RettangoloTavolo(
                g.X + step * colonna + margine,
                g.Y + riga * altezzaRiga + altezzaRiga * 0.25,
                larghezzaStep * colonna - 2 * margine,
                altezzaRiga * 0.5);

            return Rilevato(payload, colore, area);
        }

        // Cartoncino piccolo al centro di una zona speciale del layout di default, senza colore.
        private static CartoncinoRilevato NellaZona(ZonaSpeciale zona, string payload)
        {
            RettangoloTavolo z;
            switch (zona)
            {
                case ZonaSpeciale.Misura: z = LayoutTavolo.Default.Misura; break;
                case ZonaSpeciale.Collegamento: z = LayoutTavolo.Default.Collegamento; break;
                case ZonaSpeciale.Ab: z = LayoutTavolo.Default.Ab; break;
                case ZonaSpeciale.Comandi: z = LayoutTavolo.Default.Comandi; break;
                default: throw new System.ArgumentOutOfRangeException(nameof(zona));
            }

            double cx = z.X + z.Larghezza / 2;
            double cy = z.Y + z.Altezza / 2;
            return Rilevato(payload, null, new RettangoloTavolo(cx - 0.005, cy - 0.005, 0.01, 0.01));
        }
    }
}
