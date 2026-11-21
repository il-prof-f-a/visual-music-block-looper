# Cartoncini batteria

47 cartoncini per drum kit General MIDI, in due stili grafici:

- `elettroniche/`: corpo scuro, hardware metallo, dettagli neon.
- `realistiche/`: pelli chiare, fusti in legno, piatti in bronzo.

Ogni cartoncino è 60 × 40 mm a 300 dpi, con il QR a sinistra e l'icona dello strumento a destra.
`indice.csv` elenca file, nota Ableton, MIDI, nome, payload e icona.

## QR delle note classiche

Il QR usa lo stesso protocollo delle note, con un quarto campo che rende esplicita la nota MIDI:

```text
VMBL1;N;<idClip>;<midi>
```

Esempi:

| Cartoncino | Payload | Nota Ableton | MIDI |
|---|---|---|---|
| Grancassa acustica | `VMBL1;N;0;35` | B0 | 35 |
| Grancassa / Kick | `VMBL1;N;0;36` | C1 | 36 |
| Rullante acustico | `VMBL1;N;2;38` | D1 | 38 |
| Charleston chiuso | `VMBL1;N;6;42` | F#1 | 42 |
| Crash 1 | `VMBL1;N;13;49` | C#2 | 49 |
| Ride | `VMBL1;N;15;51` | D#2 | 51 |
| Bongo alto | `VMBL1;N;24;60` | C3 | 60 |
| Triangolo aperto | `VMBL1;N;45;81` | A4 | 81 |

Il quarto campo serve perché la mappatura delle note classiche parte da MIDI 36 (C1): senza di esso
B0 (35) non sarebbe rappresentabile. Per tutte le altre voci il valore coincide con `36 + idClip`.

Il colore del corpo è un grigio-blu scuro comune a tutta la batteria: il canale non dipende dal
colore, ma dalla riga e dal cartoncino `CH`.

## Come usarli in Ableton

1. Creare una traccia con un **Drum Rack** e impostarne il canale di ingresso MIDI su **10**
   (nel file di configurazione dell'app è il canale 10, cioè `9` 0-based).
2. Fermare la riproduzione.
3. Posare `canale-10.png` nella sezione della riga che vuoi usare per la batteria.
4. Posare i cartoncini batteria su quella riga, nella colonna dell'attacco; la larghezza dà la durata.
5. Premere Play: ogni cartoncino invia la propria nota MIDI esplicita al canale 10.

Le note della batteria sono one-shot naturali nel Drum Rack: la durata del cartoncino decide solo
quando arriva il note-off, non la lunghezza del campione.

## Sequenze Jamstix 4 Free

I sei cartoncini arancioni nella cartella `jamstix-4-free/` inviano le note MIDI che Jamstix usa
per selezionare le proprie sezioni. Ogni riquadro bianco mostra l'icona del drum kit, la nota e
la funzione della sequenza. Il QR mantiene il protocollo delle note con MIDI esplicito:
`VMBL1;N;<midi-36>;<midi>`.

| Cartoncino | Nota Ableton | MIDI | Sequenza |
|---|---:|---:|---|
| `jamstix-intro.png` | C2 | 48 | Suona l'introduzione, poi passa alla strofa |
| `jamstix-strofa.png` | D2 | 50 | Groove base continuo |
| `jamstix-ritornello.png` | E2 | 52 | Groove più energico |
| `jamstix-fill.png` | F2 | 53 | Rullata e ritorno alla sezione |
| `jamstix-bridge.png` | G2 | 55 | Variazione ritmica |
| `jamstix-finale.png` | A2 | 57 | Esegue la chiusura e termina |

Il generatore è `GeneratoreSequenzeJamstix` in `src/Tests/GeneratoreSequenzeJamstix.cs`.

## Icone

Le immagini senza testo fornite nel kit iconografico sono in `assets/icone-batteria/<stile>/`:
20 strumenti base per stile (MIDI 35–59) e tutte le 22 percussioni aggiuntive (MIDI 60–81).
I cinque strumenti base assenti in ciascuna categoria restano completati dalle icone vettoriali
del generatore. `GeneraIcone` crea solo file mancanti e non sovrascrive le immagini fornite.

Per sostituire un'icona basta mettere un PNG con lo stesso nome nella cartella dello stile e
rigenerare i cartoncini:

```powershell
dotnet test src/Tests/Tests.csproj -c Release --filter Name=GeneraCartonciniBatteria
```

I nomi delle icone sono nello `slug` di `indice.csv` (per esempio `kick.png`, `snare.png`,
`crash-1.png`). Il dettaglio della nota stampato sui cartoncini segue la convenzione Ableton,
per esempio `B0 · MIDI 35`, `C1 · MIDI 36` e `C#1 · MIDI 37`.
