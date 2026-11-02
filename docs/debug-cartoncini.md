# Debug dei cartoncini

La finestra principale contiene, in basso, **Debug cartoncini**, **Salva debug** e il numero di catture conservate.
Il debug parte spento a ogni avvio dell'applicazione.

## Registrare il problema A-B 1-2

1. Avviare l'app aggiornata e verificare webcam e calibrazione come al solito.
2. Selezionare **Debug cartoncini**. Ogni nuova attivazione inizia una raccolta vuota.
3. Posare il cartoncino A-B 1-2 nella zona A-B e lasciarlo fermo qualche secondo.
4. Deselezionare **Debug cartoncini** per congelare le ultime catture.
5. Premere **Salva debug**. Il JSON viene scritto accanto all'eseguibile con nome `debug-cartoncini-<data-ora UTC>-<id>.json`.
6. Il percorso completo è disponibile passando il mouse sul messaggio di salvataggio. Conservare il JSON per l'analisi.

La coda mantiene al massimo **50 catture**, incluse quelle senza cartoncini e quelle fallite; quando è piena sostituisce la più vecchia.
Sono metadati, non fotografie. Se serve anche l'immagine, usare separatamente **Salva frame**: il fotogramma salvato così non è necessariamente quello dell'ultima voce del JSON.
Disattivare il debug non elimina i dati; riattivarlo li azzera. Il salvataggio non svuota il buffer e può essere ripetuto.

## Leggere gli esiti

Il rapporto, formato versione 1, contiene `Catture` in ordine cronologico, `TotaleCatture`, `CattureOmesse` e capacità 50.
Ogni cattura include timestamp UTC, durata, dimensioni del frame, layout effettivo, eventi `Visione` e `Interpretazione`, struttura grezza/stabile ed eventuale errore.

| Dato o codice | Significato |
| --- | --- |
| `WebcamAssente` / `FrameAssente` | Webcam non disponibile oppure nessuna immagine consegnata. Non è uno scarto di A-B. |
| `CalibrazioneAssente` | Immagine disponibile, ma rilevamento del tavolo non attivo per mancanza di calibrazione. |
| `NessunQrDecodificato` | Nessun testo QR ottenuto dalla cattura. Non identifica quale cartoncino fosse presente. |
| `CorpoCandidato` / `CorpoSenzaQrDecodificato` | Regione del corpo individuata; nel secondo caso la lettura di riserva non ha ottenuto un QR. L'identità resta sconosciuta. |
| `QrLocalizzatoNonDecodificato` | Il decoder ha fornito una posizione QR senza riuscire a leggerne il testo. |
| `QrDecodificato` | Il testo compare in `Payload`; `Origine` distingue OpenCV, lettura sul ritaglio e `Memoria`. Quest'ultima riusa una lettura precedente, senza nuova conferma ottica. |
| `PayloadNonValido` | Il testo è stato letto, ma non rispetta il protocollo `VMBL1`. |
| `ZonaIncompatibile` / `ImpostazioneInZonaErrata` | Payload riconosciuto, ma cartoncino nella zona sbagliata. `Zona`, aree e `Layout` mostrano la classificazione effettiva. |
| `IntervalloAbNonValido` | Il rapporto mostra A/B richiesti e MISURA/LINK effettivi, con il limite valido. |
| `Sovrapposto` | Più impostazioni nella stessa zona, oppure note sovrapposte. Il dettaglio spiega la decisione. |
| `AbAccettato` | Il vincolo A-B è valido nello stato grezzo; può dover ancora superare la stabilizzazione. |
| `InAttesa` | La struttura ha meno delle conferme consecutive richieste; confrontare `ConfermeStruttura` e `ConfermeRichieste`. |
| `Applicata` / `GiaStabile` | Nuova struttura applicata in quella cattura, oppure struttura già attiva. |
| `NonAggiornata` | La pipeline non ha avanzato lo stabilizzatore. Lo stato stabile noto rimane riportato. |

`AreaCamera` usa pixel; `AreaTavolo` usa coordinate normalizzate. Per l'evento `CartoncinoRilevato` le due aree descrivono lo stesso corpo ritagliato; l'evento QR descrive invece il suo riquadro.
Gli errori di acquisizione/elaborazione riportano tipo e messaggio, senza trasformarsi in falsi rifiuti dei vincoli.

Nel modello corrente A-B è contato in battute applicative di quattro step. **A-B 1-2 è valido con tutte le MISURA/LINK previste**, quando il QR è letto correttamente e il cartoncino è nella zona A-B. L'applicazione richiede normalmente tre catture consecutive.
La presenza del solo `CorpoCandidato`, o l'assenza di un payload A-B, non prova quale cartoncino fosse ripreso: l'analisi deve usare posizione e letture effettivamente disponibili.

## Prestazioni e salvataggio

Il buffer usa un array circolare: inserimento e rimozione del più vecchio hanno costo costante.
Con debug spento non vengono creati contesti/rapporti, serializzati dati o scritti file di debug.
Il salvataggio copia un'istantanea immutabile del buffer; serializzazione e scrittura avvengono fuori dal thread di cattura e dai lock del registro.
Un errore di scrittura viene mostrato nella finestra senza perdere le catture raccolte. Il rapporto finale viene pubblicato solo a scrittura completata.

I test automatici usano immagini sintetiche e webcam simulate. La causa della mancata lettura del cartoncino reale A-B 1-2 richiede il rapporto prodotto sul tavolo.
