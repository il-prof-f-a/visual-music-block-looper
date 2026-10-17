# Prova hardware

Procedura per la prova con il banco reale: proiettore, webcam, loopMIDI e Ableton Live. Serve a verificare il percorso completo: cartoncino -> webcam -> riconoscimento -> note MIDI -> Ableton, con clock Master e Slave e tap-tempo.

Il materiale stampabile è in `assets/cartoncini/` (vedi `assets/cartoncini/LEGGIMI.md`).

## 1. Requisiti

| Elemento | Note |
|---|---|
| Windows 10/11 a 64 bit | L'app è net472. Nei progetti non è impostato `PlatformTarget` (compilazione AnyCPU di default). |
| loopMIDI | Driver MIDI virtuale. Creare due porte con nome esatto **`VMBL`** (usata dall'app in uscita e in ingresso) e, solo per i test automatici, **`VMBL Test`**. |
| Ableton Live | Con preferenze MIDI accessibili (sezione Link/Tempo/MIDI). |
| Webcam | All'avvio si apre la webcam indicata in `configurazione.json` (la prima, se il file non c'è). Si cambia dalla lista "Webcam" della finestra di controllo. Non deve essere usata da altre app (Teams, OBS, Zoom). |
| Proiettore | Su secondo schermo, meglio in modalità "solo secondo schermo" (estendi). |
| Cartoncini | Stampati a 100% da `assets/cartoncini/` (6 colori x 8 clip, 5 comandi, 22 speciali per misura, collegamento e A-B). |

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

### Scelta della webcam e `configurazione.json`

- La lista **Webcam** della finestra di controllo mostra le webcam con il **nome** che usa Windows (per esempio `USB2.0 HD UVC WebCam`). Due webcam con lo stesso nome compaiono come `Nome (1)` e `Nome (2)`. **Aggiorna elenco** le rileva di nuovo. Se Windows non fornisce i nomi, la lista ripiega su `Webcam 0`, `Webcam 1`, ...
- Le webcam si aprono con il backend DirectShow, il cui indice segue lo stesso ordine dei nomi.
- Scegliendo una webcam dalla lista l'app la apre subito; se non si apre resta attiva la precedente e compare un avviso.
- Cambiare webcam spegne il riconoscimento: la calibrazione è legata alla webcam e alla sua risoluzione, quindi va rifatta (viene ricaricata da sola solo se era stata salvata con quella stessa webcam).
- **Dopo una calibrazione riuscita** l'app scrive `configurazione.json` accanto all'exe con l'**identità** della webcam in uso: nome, percorso univoco del dispositivo e indice. All'avvio rilegge il file e cerca la webcam **per percorso, poi per nome**, non per posizione: se cambiano ordine o numero delle webcam viene riaperta la stessa. L'indice è usato solo per i file scritti dalla versione precedente, che contengono soltanto quello.

  ```json
  {
    "Webcam": {
      "Nome": "USB2.0 HD UVC WebCam",
      "Percorso": "\\\\?\\usb#vid_...",
      "Indice": 0
    }
  }
  ```

  Si può modificare a mano: basta il solo `Nome` (con più webcam dello stesso nome vale la prima, salvo che sia indicato anche il `Percorso`).
- Se la webcam indicata nel file non è presente (scollegata) l'app parte in modalità simulazione con un avviso e **non** apre un'altra webcam al suo posto: si sceglie dalla lista. Se il file è assente si usa la prima webcam.

## 5. Calibrazione (4 click)

1. Premere **Calibra**.
2. Nell'anteprima della webcam cliccare, in quest'ordine, i quattro angoli dell'area bianca proiettata:
   1. alto-sinistra
   2. alto-destra
   3. basso-destra
   4. basso-sinistra

   Il testo nell'area avvisi in basso nella finestra di controllo indica l'angolo atteso ("clicca l'angolo 2/4 ..."). La riga di stato in alto mostra invece BPM, modo e stato di esecuzione.
3. Dopo il quarto click la calibrazione viene calcolata e salvata in `calibrazione.txt`, accanto all'exe. Il messaggio "Calibrazione mancante" scompare.
   Da questo momento l'anteprima non mostra più l'immagine grezza della webcam ma **l'area del tavolo raddrizzata**: è "stirata" secondo i quattro punti cliccati, in modo che il rettangolo calibrato riempia tutta l'anteprima (il rapporto è quello dello schermo del proiettore, o 16:9). Un testo sotto l'anteprima lo ricorda. **Se vedi parti che non sono il tavolo** (bordi, oggetti, zone fuori dall'area proiettata), la calibrazione non è precisa: premi di nuovo **Calibra** (durante i quattro click l'anteprima torna all'immagine originale) e rifai i click più vicino agli angoli.
4. Se i click sono sbagliati, premere di nuovo **Calibra** e ripetere. La webcam viene aperta a 1280x720 (se il driver non la concede si usa la risoluzione reale) e `calibrazione.txt` memorizza la risoluzione usata: se all'avvio la camera lavora a una risoluzione diversa compare "Calibrazione per una risoluzione diversa: rifai la calibrazione" e il rilevatore resta spento; un vecchio file senza risoluzione viene accettato con l'avviso "Calibrazione senza risoluzione: ripetila per sicurezza".
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

- La griglia è di **6 righe x 16 colonne**. La riga è determinata dal centro verticale del cartoncino; il colore determina lo strumento (canale MIDI) e non è più legato alla riga. Nella stessa riga si possono mescolare colori diversi (sezione 12).
- Lo step iniziale è determinato dal bordo sinistro del cartoncino. La durata è la larghezza in passi (30 mm per passo nei cartoncini stampati: la scala con la nuova griglia è in sezione 12).
- La nota viene riconosciuta (verde sulla guida) dopo circa 3 frame consecutivi e rimossa dopo circa 5 frame di assenza.

Procedura:

1. Posare un cartoncino rosso `nota-rosso-0.png` nel primo step della riga 1. La guida lo mostra verde.
2. Premere **Avvia** (modo Master). Ad ogni passaggio del playhead la nota viene inviata: in Ableton il clip deve partire e la nota MIDI 36 canale 1 deve apparire nel monitor MIDI.
3. Rimuovere il cartoncino: la nota si spegne (note-off) alla fine della sua durata.
4. Ripetere la verifica per ciascuno dei sei colori (un canale per colore). Con il default le sei righe suonano in sequenza: per sentire più righe insieme posare il cartoncino «1 riga» (sezione 12).
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
| Sovrapposto | Due note dello stesso colore sulla stessa riga si sovrappongono nei passi, oppure due cartoncini speciali nella stessa zona. | Spostare o accorciare una delle due note; lasciare un solo cartoncino per zona. |
| Fuori griglia | Cartoncino fuori dalla griglia (ad esempio oltre la riga 6 o prima del primo step), oppure una nota posata nella colonna sinistra. | Riposizionare il cartoncino. |
| Impostazione non valida | Cartoncino speciale con un valore non ammesso (ad esempio `MISURA 5`), o A-B non compatibile con il collegamento (ad esempio B oltre le righe collegate). Il parametro torna al default. | Usare un cartoncino della tabella di sezione 12. |
| Fuori misura | Nota che inizia in una colonna oscurata (in 3/4 dalla colonna 13, in 2/4 dalla colonna 9). | Spostare la nota nelle colonne attive, o usare 4/4. |

Colori di riferimento (corpo, RGB): Rosso 220/40/40, Arancione 255/140/0, Giallo 250/210/0, Verde 40/160/70, Blu 30/90/200, Viola 130/50/170. La classificazione è per tonalità HSV (intervalli in `ClassificatoreColore`): prima della prova completa, verificare il riconoscimento con un cartoncino per colore, sotto la luce reale del proiettore.

## 10. Latenza

Latenza attesa, dalla struttura del codice (da verificare sul banco):

- Ciclo di cattura: circa 66 ms per fotogramma (circa 15 fps).
- Stabilizzazione: una nota diventa stabile dopo 3 frame consecutivi (circa 200 ms), e resta attiva finché non è assente per 5 frame. La struttura (misura, collegamento, A-B) cambia solo dopo 3 frame consecutivi con lo stesso valore: un cartoncino speciale coperto per un frame o due non fa tornare al default.
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

## 12. Modello a righe e cartoncini speciali

Il tavolo non ha più «sei tracce con un colore fisso per riga». Il modello è questo:

- **Riga = misura.** Ogni riga contiene una misura: 4/4 (16 sedicesimi, default), 3/4 (12) o 2/4 (8). Con 3/4 e 2/4 le colonne oltre la misura sono oscurate e le note che vi iniziano sono scartate (`FuoriMisura`).
- **Colore = strumento.** Il colore del cartoncino sceglie il canale MIDI (mappatura di sezione 6). Nella stessa riga si possono mescolare colori diversi. Due cartoncini dello stesso colore non possono sovrapporsi nei passi; colori diversi sì.
- **Gruppi.** Il collegamento divide le 6 righe in gruppi consecutivi. Dentro un gruppo le righe suonano in sequenza, misura dopo misura. Gruppi diversi suonano in contemporanea, sempre sincronizzati.
- **A-B.** Il giro ripete solo le righe da A a B, numerate dentro il gruppo a partire da 1. Si usa con collegamento 2, 3 o 6.

### Cartoncini speciali e dove posarli

Sono 22 cartoncini (`assets/cartoncini/speciale-*.png`). Si posano nella colonna a sinistra della griglia, con il centro del cartoncino nella zona giusta:

| Zona | Cartoncini | Valori |
|---|---|---|
| Misura (in alto) | `speciale-misura-2`, `-3`, `-4` | 2/4, 3/4, 4/4 |
| Collegamento (al centro) | `speciale-link-1`, `-2`, `-3`, `-6` | 1, 2, 3 o 6 righe per gruppo |
| A-B (in basso) | `speciale-ab-1-2` ... `speciale-ab-5-6` (15 file) | coppie A-B con 1 ≤ A < B ≤ 6 |

- Per ogni zona vale un solo cartoncino. Se ne sono posati più d'uno vale quello più in alto (poi quello più a sinistra); gli altri sono segnalati come sovrapposti e ignorati.
- Un A-B con B oltre le righe collegate (per esempio `AB 2-5` con collegamento 3), o con collegamento a 1 riga, è scartato come `ImpostazioneNonValida` e si usa il default dell'A-B.
- Un cartoncino di nota posato nelle zone speciali è segnalato come `FuoriGriglia`; un cartoncino speciale nella griglia o nella zona comandi è segnalato come `PayloadNonValido`.

### Default

Senza cartoncini speciali (o con cartoncini non validi) vale il **default**: **4/4**, **6 righe collegate in sequenza**, **A-B 1-6**. Le sei righe suonano una dopo l'altra, misura per misura, e il giro dura sei misure.

> **Attenzione: il default è cambiato.** Il comportamento precedente suonava le sei righe in parallelo, ognuna in loop su una sola misura. Per ottenerlo ora posare il cartoncino `speciale-link-1.png` nella zona Collegamento.

### Esempi da provare

| Cartoncini posati | Cosa deve accadere (numeri di riga come nella guida, da 1) |
|---|---|
| Nessuno | 4/4, sei righe in sequenza: 1, poi 2, ..., poi 6, poi di nuovo 1. |
| `speciale-link-1` | 4/4, sei righe in parallelo, ciascuna una misura in loop (il comportamento precedente). |
| `speciale-link-2` | Tre coppie. Misura 1: righe 1, 3, 5 insieme. Misura 2: righe 2, 4, 6 insieme. Poi di nuovo. |
| `speciale-link-3` + `speciale-ab-2-3` | Due terne. Il giro dura due misure: misura 1 con le righe 2 e 5, misura 2 con le righe 3 e 6. Le righe 1 e 4 non suonano mai. |
| `speciale-misura-3` | Misure da 3/4: le colonne 13-16 sono oscurate, le note che vi iniziano sono scartate. |

### Scala fisica dei cartoncini

La griglia occupa il 67% della larghezza del tavolo, non più l'80%: lo step proiettato è circa il 16% più stretto. I PNG committati restano a **30 mm per passo**. Per allineare la scala fisica ricalcolare `VMBL_PASSO_MM` (circa 30 x 0,84, quindi **circa 25 mm** invece di 30 mm) e rigenerare il materiale, come indicato in `assets/cartoncini/LEGGIMI.md`. Il valore da usare va verificato sul tavolo, con un righello e con la proiezione reale: **da verificare sul tavolo**.

### Cosa mostra la proiezione

- **Zone a sinistra della griglia**, con etichetta e valore attivo: «MISURA» con 4/4, 3/4 o 2/4; «COLLEGA» con il numero di righe; «A-B» con l'intervallo. La zona «COMANDI» è indicata come riferimento.
- **Barre di gruppo** sul lato sinistro della griglia: le righe dello stesso gruppo sono unite da una barra.
- **Numeri di riga** da 1 a 6, a sinistra della griglia, per leggere A-B.
- **Colonne oltre la misura** in grigio scuro (note non utilizzabili); **righe fuori da A-B** in grigio chiaro (non suonano).
- **Playhead** (colonna arancione) solo sulle righe attive: una per gruppo. Da fermo è un contorno.
- **Note riconosciute** in verde; **cartoncini scartati** con contorno rosso (anche `ImpostazioneNonValida` e `FuoriMisura`).

## 13. Problemi noti

- **Comando ONESHOT con più QR visibili.** Il cartoncino `comando-oneshot-36.png` ha un payload più lungo (`VMBL1;C;ONESHOT;36`) che richiede il QR di versione 2 con correzione M. Quando più QR sono visibili insieme, la lettura multipla di OpenCV può scartarlo. Al banco: verificare il ONESHOT con gli altri cartoncini nel campo, e se serve posarlo da solo.
- **Cartoncini speciali: livello di correzione L.** I cartoncini speciali usano la correzione d'errore L, che li mantiene nella versione QR 1 come gli altri cartoncini e facilita la lettura quando più QR sono visibili. Il livello L corregge meno errori del M: la robustezza di lettura dipende quindi molto da stampa nitida e messa a fuoco.
- **Colori e leggibilità sotto la luce del proiettore.** Il corpo dei cartoncini speciali è viola-grigio neutro (sotto la soglia di saturazione: non è un colore di traccia). I test automatici verificano i PNG, non la lettura reale. Da verificare al banco: la lettura del QR e la distinzione tra le tre zone, con la luce reale del proiettore e della stanza.

## Esito della prova

> **Sezione per l'operatore.** Da compilare durante la prova sul banco reale. Non compilare in anticipo: lasciare vuoti i campi fino all'esecuzione.

| Prova | Esito (OK / KO / parziale) | Note | Data |
|---|---|---|---|
| Porte `VMBL` aperte, nessun avviso MIDI | | | |
| Webcam aperta (nessun avviso di simulazione) | | | |
| Calibrazione con 4 click, file `calibrazione.txt` creato | | | |
| Nota on/off per ciascun colore (rosso, arancione, giallo, verde, blu, viola: un canale per colore) | | | |
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
| Misura 3/4 e 4/4 (colonne oltre la misura oscurate, nota nelle colonne attive) | | | |
| Collegamento 2 righe (misura 1: righe 1, 3, 5; misura 2: righe 2, 4, 6) | | | |
| A-B 2-3 con collegamento 3 (righe 1 e 4 mai attive) | | | |
| Cartoncino mancante (zona vuota) → default 4/4, 6 righe in sequenza | | | |

## Correzioni emerse

> **Sezione per l'operatore.** Annotare qui le correzioni a codice, configurazione, materiale o procedura emerse durante la prova, con riferimento alla prova che le ha generate.

| # | Prova di riferimento | Problema osservato | Correzione proposta | Stato |
|---|---|---|---|---|
| 1 | | | | |
