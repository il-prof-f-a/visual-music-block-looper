using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenCvSharp;
using VisualMusicBlockLooper.Core;
using VisualMusicBlockLooper.Vision;

namespace VisualMusicBlockLooper.Tests
{
    public class QrScenaCompletaTests
    {
        [TestCase(0, 0, -0.30, 0.30, true, false)]
        [TestCase(3, 2, -0.30, 0.30, true, true)]
        [TestCase(-3, -2, 0.30, -0.30, true, true)]
        public void QuarantottoNoteSpostateInProspettiva_MantengonoPayloadRigheEStep(
            double erroreCalibrazioneX,double erroreCalibrazioneY,double spostamentoQrCelle,double spostamentoQrRighe,
            bool scansioneCompleta,bool scansioneABlocchi)
        {
            const int larghezza = 1920, altezza = 1080;
            var griglia = new ConfigGriglia { Area = LayoutTavolo.AreaGrigliaDefault };
            string[] file = Directory.GetFiles(Path.Combine(GeneratoreCartoncini.CartellaCartoncini(),
                "..", "cartoncini-note"), "nota-*.png").OrderBy(f => f).ToArray();
            Assert.That(file, Has.Length.EqualTo(48));
            var attesi = file.Select(f => "VMBL1;N;" +
                (int.Parse(Path.GetFileName(f).Split('-')[1]) - GeneratoreCartoncini.NotaBase)).ToArray();
            var camera = new[] { new Point2f(35, 30), new Point2f(1870, 12),
                new Point2f(1900, 1040), new Point2f(18, 1060) };
            using (var tavolo = new Mat(altezza, larghezza, MatType.CV_8UC3, Scalar.White))
            using (var frame = new Mat())
            using (var prospettiva = Cv2.GetPerspectiveTransform(new[] { new Point2f(0, 0),
                new Point2f(larghezza, 0), new Point2f(larghezza, altezza), new Point2f(0, altezza) }, camera))
            {
                for (int i = 0; i < file.Length; i++)
                    using (var originale = Cv2.ImRead(file[i]))
                    using (var carta = new Mat())
                    {
                        const int w = 150, h = 100;
                        Cv2.Resize(originale, carta, new Size(w, h));
                        // Il bordo nero del QR nei PNG parte a x=75/709; il centro e' a y=235.5/473.
                        double xQr = griglia.Area.X + (2 * (i % 8) + spostamentoQrCelle) * griglia.Area.Larghezza / 16;
                        double yQr = griglia.Area.Y + (i / 8 + .5 + spostamentoQrRighe) * griglia.Area.Altezza / 6;
                        int x = (int)Math.Round(xQr * larghezza - w * 75.0 / 709);
                        int y = (int)Math.Round(yQr * altezza - h * 235.5 / 473);
                        using (var zona = new Mat(tavolo, new Rect(x, y, w, h))) carta.CopyTo(zona);
                    }
                Cv2.WarpPerspective(tavolo, frame, prospettiva, new Size(larghezza, altezza),
                    InterpolationFlags.Linear, BorderTypes.Constant, Scalar.White);
                // Simula una piccola imprecisione nel posizionamento dei riferimenti di calibrazione.
                var riferimenti = camera.Select(p => new PuntoImmagine(p.X + erroreCalibrazioneX, p.Y + erroreCalibrazioneY)).ToArray();
                var calibrazione = Calibrazione.Calcola(riferimenti,
                    new[] { new PuntoTavolo(0, 0), new PuntoTavolo(1, 0),
                        new PuntoTavolo(1, 1), new PuntoTavolo(0, 1) }, larghezza, altezza);
                if (scansioneCompleta)
                {
                    var traccia = new TracciaCartoncini();
                    var lette = new RilevatoreCartoncini(new NoteContigueVisionTests.Fonte(frame), calibrazione)
                        .Rileva(traccia);
                    Assert.That(lette.Select(n => n.Payload), Is.EquivalentTo(attesi),
                        string.Join("; ", traccia.Visione.Where(e => e.Codice == "QrLocalizzatoNonDecodificato"
                            || e.Codice == "CorpoSenzaQrDecodificato").Select(e => e.Codice + ":" + e.Dettaglio + ":" +
                                (e.AreaCamera.HasValue ? $"{e.AreaCamera.Value.X:0.0000},{e.AreaCamera.Value.Y:0.0000}," +
                                    $"{e.AreaCamera.Value.Larghezza:0.0000},{e.AreaCamera.Value.Altezza:0.0000}" : "senza-area"))));
                    Assert.That(lette.GroupBy(n => n.Payload).All(g => g.Count() == 1), Is.True);
                    var stato = new InterpreteTavolo(new Griglia(griglia), LayoutTavolo.Default).Interpreta(lette);
                    VerificaPartitura(stato);
                }

                // Ripeti la lettura usando i blocchi per riga della scansione anticipata.
                if (scansioneABlocchi)
                {
                    var lettore = new LettoreZone(calibrazione, griglia);
                    IReadOnlyList<CartoncinoRilevato> catalogo = new CartoncinoRilevato[0];
                    double altezzaRiga = griglia.Area.Altezza / griglia.Tracce;
                    // Una ROI di riga deve restare centrata sulla propria corsia: il margine camera
                    // del lettore conserva i bordi; espandere di 0,35 righe sovrappone le sei scansioni.
                    double margineY = 0;
                    double zonaX = Math.Max(0, griglia.Area.X - griglia.Area.Larghezza / griglia.Step * griglia.TolleranzaQrCelle);
                    double destra = Math.Min(griglia.Area.X + griglia.Area.Larghezza, LayoutTavolo.Default.Comandi.X);
                    for (int riga = 0; riga < griglia.Tracce; riga++)
                    {
                        double y = Math.Max(griglia.Area.Y, griglia.Area.Y + riga * altezzaRiga - margineY);
                        double basso = Math.Min(griglia.Area.Y + griglia.Area.Altezza,
                            griglia.Area.Y + (riga + 1) * altezzaRiga + margineY);
                        var zona = new RettangoloTavolo(zonaX, y, destra - zonaX, basso - y);
                        var letturaRiga = lettore.Leggi(frame, zona, catalogo, null, "riga-" + riga);
                        Assert.That(letturaRiga.QrDecodificati, Is.InRange(8, 10),
                            $"Riga {riga}, calibrazione {erroreCalibrazioneX}/{erroreCalibrazioneY}px, " +
                            $"QR {spostamentoQrCelle}/{spostamentoQrRighe} celle/righe: la ROI può includere " +
                            $"fino a due QR vicini nel margine camera, conteggio {letturaRiga.QrDecodificati}; " +
                            $"payload visti: {string.Join(", ", letturaRiga.Catalogo.Select(n => n.Payload))}");
                        Assert.That(letturaRiga.Completa, Is.True, "La riga " + riga + " deve risultare completa.");
                        catalogo = letturaRiga.Catalogo;
                        Assert.That(catalogo.Select(n => n.Payload), Is.EquivalentTo(attesi.Take((riga + 1) * 8)),
                            $"La riga {riga} deve aggiungere solo le sue otto note, senza anticipare o duplicare note vicine.");
                    }
                    Assert.That(catalogo.Select(n => n.Payload), Is.EquivalentTo(attesi),
                        "La lettura anticipata a blocchi deve conservare tutti i payload nella scena prospettica.");
                    Assert.That(catalogo.GroupBy(n => n.Payload).All(g => g.Count() == 1), Is.True,
                        "Le ROI sovrapposte non devono duplicare i cartoncini.");
                    VerificaPartitura(new InterpreteTavolo(new Griglia(griglia), LayoutTavolo.Default).Interpreta(catalogo));
                }
            }
        }

        private static void VerificaPartitura(StatoTavolo stato)
        {
            Assert.That(stato.Errori, Is.Empty);
            Assert.That(stato.Note, Has.Count.EqualTo(48));
            for (int riga = 0; riga < 6; riga++)
                Assert.That(stato.Note.Where(n => n.Riga == riga).Select(n => n.StepIniziale).OrderBy(s => s),
                    Is.EqualTo(new[] { 0, 2, 4, 6, 8, 10, 12, 14 }), "Riga " + riga);
        }
    }
}
