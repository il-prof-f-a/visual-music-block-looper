# Visual Music Block Looper

[Read in English](README.md)

Visual Music Block Looper trasforma cartoncini musicali stampati e disposti su un tavolo in
un'esecuzione MIDI dal vivo. Una webcam legge i cartoncini e i loro corpi colorati, il programma
proietta una guida sul tavolo e invia note MIDI e comandi di trasporto a una DAW.

## Perché usarlo?

Il progetto rende visibile e fisico il tempo musicale. Invece di inserire ogni nota in un piano
roll, i partecipanti dispongono i cartoncini su una griglia condivisa e ascoltano subito il
risultato. È pensato per comporre con le mani, lavorare in gruppo, insegnare musica e sperimentare
con installazioni interattive. Dopo installazione e calibrazione si usa senza programmare; per
compilarlo dal sorgente serve un ambiente di sviluppo Windows.

## A chi è rivolto?

- Insegnanti e studenti che esplorano ritmo, altezza, durata e arrangiamento.
- Musicisti e conduttori di laboratori che cercano un sequencer tangibile e collaborativo.
- Maker e sviluppatori interessati a visione artificiale, proiezione e MIDI.

È uno strumento sperimentale e didattico, non sostituisce una DAW.

## Come funziona

La superficie di esecuzione è una griglia di **6 righe × 16 celle**. Il QR identifica una nota,
un accordo, un'impostazione o un comando. Il corpo colorato del cartoncino identifica la traccia e,
per le note, ne determina la durata. La webcam legge il tavolo, il programma prepara una partitura
MIDI e un worker dedicato invia gli eventi alla DAW. Il proiettore mostra la griglia e le note
rilevate, così chi suona può controllare ciò che il programma ha letto.

All'avvio il programma è in modalità **Slave** e segue clock e trasporto MIDI della DAW. Il
pulsante **Master / Slave** permette di far generare il clock al programma.

## Requisiti

- Windows 10 o 11 a 64 bit.
- .NET Framework 4.7.2 o un runtime .NET Framework successivo compatibile.
- Per compilare il sorgente: .NET SDK con supporto ai progetti Windows Forms per .NET Framework
  4.7.2 e relativo targeting pack.
- Per l'uso fisico: webcam, proiettore collegato come secondo schermo, cartoncini stampati e un
  tavolo o una superficie di proiezione adatta.
- Per il MIDI: [loopMIDI](https://www.tobias-erichsen.de/software/loopmidi.html) e una DAW. La
  procedura documentata usa Ableton Live; altre DAW possono funzionare se supportano note MIDI e
  routing del clock.

Questa repository non include un installer pronto. Per ora il programma si compila dal sorgente.

## Installazione e compilazione

Apri PowerShell nella cartella della repository:

```powershell
dotnet restore src/VisualMusicBlockLooper.sln
dotnet build src/App/App.csproj -c Release
```

Avvia il programma generato:

```powershell
& .\src\App\bin\Release\net472\VisualMusicBlockLooper.App.exe
```

La compilazione copia `mappatura-default.json` accanto all'eseguibile. Lascialo in quella
cartella: contiene le porte MIDI e la mappatura predefinite. Il programma salva le impostazioni
della webcam e del tavolo in `configurazione.json` e la calibrazione in `calibrazione.txt`, sempre
accanto all'eseguibile. Conserva questi file quando sposti un'installazione esistente.

## Collegamento MIDI con Ableton Live

Il file `mappatura-default.json` fornito usa due porte loopMIDI:

| Porta | Direzione | Uso |
|---|---|---|
| `VMBL` | programma → Ableton | Note e, in modalità Master, clock e trasporto. |
| `VMBL-DAW` | Ableton → programma | Clock e trasporto quando il programma è in Slave. |

1. Avvia loopMIDI e crea le porte chiamate esattamente `VMBL` e `VMBL-DAW`.
2. In Ableton vai in **Preferences → Link, Tempo & MIDI** e abilita **Track** sull'ingresso
   `VMBL`.
3. Per la modalità Slave predefinita, abilita **Sync** sull'uscita `VMBL-DAW` e avvia la
   riproduzione in Ableton. Il programma seguirà clock e trasporto di Live.
4. Per la modalità Master, abilita **Sync** sull'ingresso `VMBL` di Ableton e avvia la
   riproduzione dal programma.

Se rinomini una porta, aggiorna `mappatura-default.json` accanto all'eseguibile e riavvia il
programma. La procedura hardware completa è in [docs/prova-hardware.md](docs/prova-hardware.md).

## Prima sessione al tavolo

1. Collega il proiettore come secondo schermo ed estendi il desktop. Proietta a schermo intero una
   superficie bianca uniforme.
2. Inquadra con la webcam l'area proiettata e i cartoncini; controlla fuoco e illuminazione.
3. Avvia il programma, scegli webcam e schermo del proiettore, poi premi **Calibra**. Clicca in
   ordine numerico i centri dei 12 bersagli proiettati. La calibrazione è legata alla webcam e alla
   sua risoluzione; un salvataggio CAL3 può essere riutilizzato con la stessa configurazione.
4. Stampa i cartoncini in scala **100%**. Disponili sulla griglia: le colonne rappresentano le
   suddivisioni temporali, le righe le linee musicali. Il corpo colorato di una nota ne imposta la
   durata.
5. In Slave avvia Ableton; in alternativa passa a Master e avvia il programma. Usa la guida
   proiettata per controllare le celle riconosciute dalla webcam.

La [guida hardware](docs/prova-hardware.md) descrive per intero webcam, calibrazione e Ableton.

## Cartoncini disponibili

| Cartoncini | Contenuto |
|---|---|
| [Note cromatiche](assets/cartoncini-note/LEGGIMI.md) | Cartoncini colorati per le sei tracce. |
| [Accordi e canali](assets/cartoncini-accordi/LEGGIMI.md) | 96 accordi, inclusa una serie in chiave di violino un'ottava sopra; i canali MIDI sono in `assets/cartoncini-canale/`. |
| [Batteria](assets/cartoncini-batteria/LEGGIMI.md) | Cartoncini General MIDI in due stili e cartoncini per le sezioni Jamstix. |
| [Impostazioni tavolo](assets/cartoncini-setting/LEGGIMI.md) | Tempi, righe collegate e loop A–B. |

Stampa i cartoncini alla dimensione reale, senza adattarli alla pagina. QR e colori del corpo sono
entrambi parte del formato letto dal programma.

## Configurazione e limiti

- `assets/config/mappatura-default.json` viene copiato accanto all'eseguibile e definisce porte
  MIDI, velocity, canali e note predefinite.
- `configurazione.json` contiene webcam, analisi e assegnazioni per riga; `calibrazione.txt`
  contiene la calibrazione tra camera e tavolo.
- I cartoncini canale si applicano a riproduzione ferma. I cartoncini strumento (`ST`) vengono
  riconosciuti e salvati, ma non caricano ancora strumenti in Ableton.
- Fuoco, luce, geometria della proiezione e calibrazione influenzano il riconoscimento. Prima di
  suonare controlla l'allineamento delle linee della griglia proiettata.

## Sviluppo

La soluzione contiene cinque progetti: `Core`, `Vision`, `Midi`, `App` e `Tests`. Per compilare
l'intera soluzione:

```powershell
dotnet build src/VisualMusicBlockLooper.sln -c Release
dotnet test src/Tests/Tests.csproj -c Release
```

Vedi [docs/architettura.md](docs/architettura.md) per i componenti e
[docs/esecuzione-midi.md](docs/esecuzione-midi.md) per il modello di esecuzione MIDI.
