# Prova hardware

Procedura per la prova con il banco reale: proiettore, webcam, loopMIDI e Ableton Live. Serve a verificare il percorso completo: cartoncino -> webcam -> riconoscimento -> note MIDI -> Ableton, con clock Master e Slave e tap-tempo.

Il materiale stampabile è in `assets/cartoncini/` (vedi `assets/cartoncini/LEGGIMI.md`).

## 1. Requisiti

| Elemento | Note |
|---|---|
| Windows 10/11 a 64 bit | L'app è net472. Nei progetti non è impostato `PlatformTarget` (compilazione AnyCPU di default). |
| loopMIDI | Driver MIDI virtuale. Creare due porte con nome esatto **`VMBL`** (usata dall'app in uscita e in ingresso) e, solo per i test automatici, **`VMBL Test`**. |
| Ableton Live | Con preferenze MIDI accessibili (sezione Link/Tempo/MIDI). |
| Webcam | Indice 0 (la prima camera di sistema). Non deve essere usata da altre app (Teams, OBS, Zoom). |
| Proiettore | Su secondo schermo, meglio in modalità "solo secondo schermo" (estendi). |
| Cartoncini | Stampati a 100% da `assets/cartoncini/` (6 colori x 8 clip, 5 comandi). |

## 2. Installazione loopMIDI

1. Installare loopMIDI.
2. Creare la porta **`VMBL`**. Il nome deve essere esattamente `VMBL`: l'app lo cerca con `GetByName` di DryWetMIDI (primo dispositivo con quel nome). Se non lo trova, l'avviso elenca le porte disponibili.
3. Facoltativo, solo per i test automatici di hardware: creare la porta **`VMBL Test`**. I test di categoria `Hardware` (`MidiIntegrazioneTests`) vengono saltati se questa porta manca (messaggio: "Porta MIDI 'VMBL Test' non presente").
4. Lasciare loopMIDI aperto durante tutta la prova.

## 3. Setup del proiettore e della webcam

1. Collegare il proiettore come secondo schermo. Lo schermo "non principale" viene selezionato automaticamente nel menu "Monitor proiettore" della finestra di controllo.
2. Mostrare sul proiettore una pagina **bianca a schermo intero**. La zona bianca proiettata è l'area che la calibrazione definisce come tavolo (0..1 nelle due direzioni).
3. Inquadrare la webcam in modo che:
   - l'area bianca proiettata occupi gran parte del fotogramma;
   - i cartoncini siano a fuoco e ben illuminati;
   - non ci siano riflessi diretti del proiettore sulla superficie.
4. Ridurre la luce ambiente, se possibile. Vedi anche la sezione 9 per i colori.

## 4. Avvio dell'app

1. Compilare la soluzione (o usare l'eseguibile già compilato). L'eseguibile è `VisualMusicBlockLooper.App.exe` nella cartella di output di `src/App` (es. `src/App/bin/Debug/net472/`).
2. Tenere nella stessa cartella dell'exe il file `mappatura-default.json` (copiato automaticamente dal build). Il file `calibrazione.txt` viene salvato nella stessa cartella.
3. Avviare l'exe. Nella finestra di controllo leggere la riga delle porte:
   - `MIDI uscita: VMBL | ingresso: VMBL` senza "(non disponibile)": porte aperte.
   - Riga di avviso "Webcam non trovata: modalità simulazione": la camera non è stata aperta (vedi sezione 10).

## 5. Calibrazione (4 click)

1. Premere **Calibra**.
2. Nell'anteprima della webcam cliccare, in quest'ordine, i quattro angoli dell'area bianca proiettata:
   1. alto-sinistra
   2. alto-destra
   3. basso-destra
   4. basso-sinistra

   Il testo nell'area avvisi in basso nella finestra di controllo indica l'angolo atteso ("clicca l'angolo 2/4 ..."). La riga di stato in alto mostra invece BPM, modo e stato di esecuzione.
3. Dopo il quarto click la calibrazione viene calcolata e salvata in `calibrazione.txt`, accanto all'exe. Il messaggio "Calibrazione mancante" scompare.
4. Se i click sono sbagliati, premere di nuovo **Calibra** e ripetere.
5. Controllo: appoggiare un cartoncino nero o colorato su una zona nota e verificare che la guida proiettata lo riconosca nella cella giusta (vedi sezione 7).

## 6. Mappatura in Ableton

La mappatura di default è in `assets/config/mappatura-default.json`:

- Porte: uscita e ingresso **`VMBL`**.
- Velocity **100**.
- Canale MIDI per traccia: traccia 0 (Rosso) = canale 0 (in Ableton canale **1**), traccia 1 (Arancione) = canale 2, ..., traccia 5 (Viola) = canale 6. Il file usa canali 0-based, Ableton li mostra 1-based.
- Nota per clip: **36 + idClip** (clip 0 = nota 36 = C1, clip 7 = nota 43). In `NotaPerClip` ci sono override per i clip 0, 1 e 9 con gli stessi valori di default.

Passi in Ableton:

1. Preferenze -> MIDI: abilitare l'input **VMBL** con Track e Remote attivi (riceve le note inviate dall'app).
2. Creare 6 tracce MIDI (o 6 gruppi di clip), una per colore.
3. Su ogni clip assegnare la nota del clip, con **MIDI Map** (Ctrl+M su Windows): cliccare sul clip, poi inviare la nota dal tavolo (cartoncino di nota) oppure premere la nota sulla tastiera virtuale del loopMIDI.
4. Verificare che il canale del clip coincida con quello della traccia, e che la nota corrisponda al clip.
5. Uscire da MIDI Map mode (Ctrl+M).

## 7. Verifica nota on/off per traccia e clip

Riferimenti per il controllo, tutti sulla guida proiettata:

- La griglia è di **6 tracce x 16 step**; la traccia è determinata dalla riga (dal centro verticale del cartoncino) e il colore del cartoncino deve corrispondere alla riga.
- Lo step iniziale è determinato dal bordo sinistro del cartoncino. La durata è la larghezza in passi (30 mm per passo).
- La nota viene riconosciuta (verde sulla guida) dopo circa 3 frame consecutivi e rimossa dopo circa 5 frame di assenza.

Procedura:

1. Posare un cartoncino rosso `nota-rosso-0.png` nel primo step della riga 1. La guida lo mostra verde.
2. Premere **Avvia** (modo Master). Ad ogni passaggio del playhead la nota viene inviata: in Ableton il clip deve partire e la nota MIDI 36 canale 1 deve apparire nel monitor MIDI.
3. Rimuovere il cartoncino: la nota si spegne (note-off) alla fine della sua durata.
4. Ripetere la verifica per ciascuno dei sei colori (una traccia per colore).
5. Errori: un cartoncino non valido sulla griglia è segnato in rosso sulla guida (vedi sezione 9).

## 8. Modo Master e Slave

### Master (l'app guida il clock)

1. Verificare che la riga di stato mostri **Master**. Il pulsante **Master / Slave** alterna i modi.
2. In Ableton, preferenze MIDI: abilitare la **sincronizzazione** sull'input **VMBL** (Sync On, Track On). Ableton segue il clock dell'app.
3. Premere **Avvia** nell'app (o il comando `VMBL1;C;PLAY` con il cartoncino nella zona comandi): l'app invia MIDI Start e Ableton parte.
4. Premere **Ferma** (o `VMBL1;C;STOP`): invia MIDI Stop, Ableton si ferma e le note in corso vengono spente.
5. Osservare il BPM nella riga di stato: il clock è inviato in continuo anche da fermo.

Nota: lo stesso nome di porta è usato in uscita e in ingresso, quindi ciò che l'app invia rientra nel suo ingresso. In Master il clock ricevuto è ignorato (nessun doppio tick). L'ingresso MIDI dell'app gestisce solo clock, Start, Stop e Continue: le note ricevute non hanno effetto sul motore.

### Slave (Ableton guida il clock)

1. Premere una volta **Master / Slave**: è un pulsante unico che alterna i due modi. Verificare che la riga di stato mostri **Slave** (il cambio di modo ferma la riproduzione).
2. In Ableton, preferenze MIDI: abilitare l'**uscita** verso **VMBL** con sincronizzazione (Sync On). Ableton invia clock e Start/Stop alla porta.
3. Avviare la riproduzione in Ableton: l'app riceve Start e segue i tick (6 tick = 1 step).
4. Fermare la riproduzione in Ableton: l'app riceve Stop e spegne le note.
5. Se il clock si interrompe per più di 500 ms, l'app ferma la riproduzione e spegne le note (timeout). Verificarlo togliendo il clock in Ableton a metà.
6. In Slave il pulsante Tap è disabilitato e il cartoncino `TAP` non ha effetto: il BPM è quello di Ableton.

## 9. Tap-tempo, comandi e colori

### Tap-tempo (solo Master)

1. Premere **Tap** almeno 2 volte al ritmo voluto: dalla seconda pressione l'app calcola il BPM come media degli ultimi 4 intervalli (limite 30-300 BPM).
2. Una pausa superiore a 2 secondi azzera il calcolo: il tap successivo ricomincia da zero.
3. In alternativa, appoggiare nella zona comandi il cartoncino `comando-tap.png`.
4. Verificare che il BPM della riga di stato cambi e che il playhead proceda al tempo tappato.

### Comandi dalla zona comandi

- La zona comandi è la fascia destra del tavolo (circa 14% della larghezza, da x 0,84 a x 0,98 del tavolo). Un cartoncino è un comando se il suo centro cade in questa zona. La guida proiettata **non disegna** questa zona: è un riferimento fisico da tenere sul tavolo (ad esempio con un nastro).
- Ogni comando viene eseguito una volta sola, quando diventa stabile: tenere fermo il cartoncino non ripete il comando.
- `comando-oneshot-36.png` suona la nota 36 sul canale della traccia 0 per circa 6 tick di clock.

### Errori e colori (guida proiettata)

La guida segnala in rosso (contorno e fondo chiaro) i cartoncini scartati. Il motivo dello scarto è determinato così:

| Errore | Causa tipica | Cosa fare |
|---|---|---|
| Payload non valido | QR non leggibile o payload non previsto (nota nella zona comandi, comando nella griglia). | Verificare il cartoncino e la sua posizione. |
| Colore mancante | Il colore non è abbastanza saturo (saturazione < 35%) o troppo scuro (valore < 25%). Succede con il proiettore sovraesposto. | Ridurre la luce ambiente, abbassare la luminosità del proiettore, usare cartoncini stampati più saturi. |
| Colore incoerente | Il colore non corrisponde alla riga del cartoncino. | Spostare il cartoncino nella riga del suo colore. |
| Sovrapposto | Due note sulla stessa traccia si sovrappongono nei passi. | Spostare o accorciare una delle due note. |
| Fuori griglia | Cartoncino fuori dalla griglia (ad esempio oltre la riga 6 o prima del primo step). | Riposizionare il cartoncino. |

Colori di riferimento (corpo, RGB): Rosso 220/40/40, Arancione 255/140/0, Giallo 250/210/0, Verde 40/160/70, Blu 30/90/200, Viola 130/50/170. La classificazione è per tonalità HSV (intervalli in `ClassificatoreColore`): prima della prova completa, verificare il riconoscimento con un cartoncino per colore, sotto la luce reale del proiettore.

## 10. Latenza

Latenza attesa, dalla struttura del codice (da verificare sul banco):

- Ciclo di cattura: circa 66 ms per fotogramma (circa 15 fps).
- Stabilizzazione: una nota diventa stabile dopo 3 frame consecutivi (circa 200 ms), e resta attiva finché non è assente per 5 frame.
- Clock Master: un tick ogni 60000 / (BPM x 24) ms. A 120 BPM un passo (6 tick) dura circa 125 ms.

Misura suggerita (con registrazione audio):

1. Registrare contemporaneamente con uno smartphone il tavolo (per il momento di ingresso del cartoncino) e l'uscita audio di Ableton (per il momento del suono).
2. Calcolare la differenza tra il primo fotogramma in cui il cartoncino è verde sulla guida (o il primo frame in cui appare nel video) e il suono del clip.
3. Ripetere 5 volte e annotare la mediana. Il ritardo strutturale del riconoscimento è di circa 200 ms più il ciclo di cattura; valori molto superiori al totale stimato indicano un problema da indagare (uscita audio, latenza della webcam, buffer MIDI).

## 11. Risoluzione dei problemi

| Sintomo | Causa probabile | Azione |
|---|---|---|
| "Porta MIDI di uscita non disponibile" | Porta `VMBL` assente in loopMIDI o nome diverso. In questo caso l'app gira in modalità silenziosa. | Creare/rinominare la porta `VMBL` in loopMIDI, riavviare l'app. |
| "Porta MIDI di ingresso non disponibile" | Porta `VMBL` in ingresso assente. | Come sopra. |
| "Webcam non trovata: modalità simulazione" | Camera non aperta (indice 0). Usata da un'altra app o scollegata. | Chiudere le altre app che usano la camera, ricollegare, riavviare. |
| "Calibrazione mancante: premi Calibra" | Nessun file `calibrazione.txt` accanto all'exe. | Eseguire la calibrazione (sezione 5). |
| "Calibrazione salvata non valida ... ripetere" | `calibrazione.txt` corrotto o con valori non validi. | Eliminare il file e ripetere la calibrazione. |
| "Mappatura MIDI non caricata ... uso i valori di default" | `mappatura-default.json` assente o non valido accanto all'exe. | Copiare il file da `assets/config/`. |
| Il proiettore non ha l'area bianca corretta | La calibrazione è stata fatta con il proiettore spostato. | Ripetere la calibrazione dopo aver fissato il proiettore. |
| Cartoncini riconosciuti a intermittenza | Luce ambiente o riflessi, messa a fuoco. | Ridurre la luce, correggere la messa a fuoco, ripetere la prova. |
| Colore sbagliato o non riconosciuto | Vedi sezione 9 (proiettore sovraesposto, saturazione bassa). | Vedi tabella della sezione 9. |
| Ableton non parte o non segue il clock | Sync non abilitato sulla porta `VMBL`. | Verificare le preferenze MIDI di Ableton (sezione 8). |

## Esito della prova

> **Sezione per l'operatore.** Da compilare durante la prova sul banco reale. Non compilare in anticipo: lasciare vuoti i campi fino all'esecuzione.

| Prova | Esito (OK / KO / parziale) | Note | Data |
|---|---|---|---|
| Porte `VMBL` aperte, nessun avviso MIDI | | | |
| Webcam aperta (nessun avviso di simulazione) | | | |
| Calibrazione con 4 click, file `calibrazione.txt` creato | | | |
| Nota on/off per ciascuna delle 6 tracce (rosso, arancione, giallo, verde, blu, viola) | | | |
| Nota on/off per clip (almeno clip 0, 3 e 7) | | | |
| Mappatura clip in Ableton (MIDI Map) | | | |
| Clock Master: Ableton segue il BPM dell'app | | | |
| Master: Start/Stop da app (pulsante e cartoncini PLAY/STOP) | | | |
| Slave: Ableton come master di clock, app segue i tick | | | |
| Slave: timeout clock (500 ms) ferma la riproduzione | | | |
| Tap-tempo (pulsante e cartoncino TAP) | | | |
| Comando ONESHOT (nota 36, circa 6 tick) | | | |
| Errori segnalati in rosso sulla guida (payload, colore, sovrapposizione) | | | |
| Latenza misurata (mediana di 5 prove, in ms) | | | |

## Correzioni emerse

> **Sezione per l'operatore.** Annotare qui le correzioni a codice, configurazione, materiale o procedura emerse durante la prova, con riferimento alla prova che le ha generate.

| # | Prova di riferimento | Problema osservato | Correzione proposta | Stato |
|---|---|---|---|---|
| 1 | | | | |
