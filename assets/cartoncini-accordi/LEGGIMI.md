# Cartoncini accordi e canali

Materiale stampabile generato da `GeneratoreCartoncini.GeneraAccordiECanali` (test esplicito
`GeneraCartonciniAccordi`). Ogni PNG è 60 × 40 mm a 300 dpi, con QR leggibile dal rilevatore.

## Accordi (96 cartoncini)

Dodici fondamentali × otto qualità:

| Simbolo | Qualità | Intervalli dal fondamentale |
|---|---|---|
| `+` | maggiore | 0, 4, 7 |
| `-` | minore | 0, 3, 7 |
| `7` | settima (dominante) | 0, 4, 7, 10 |
| `-7` | minore settima | 0, 3, 7, 10 |
| `+7` | maggiore settima | 0, 4, 7, 11 |
| `dim` | diminuito | 0, 3, 6 |
| `dim7` | diminuito settima | 0, 3, 6, 9 |
| `aug` | aumentato | 0, 4, 8 |

Il protocollo accetta anche `sus2`, `sus4`, `6` e `-6`, ma questi cartoncini non sono ancora
stampati.

Codifica QR compatta:

```text
VMBL1;A;<fondamentale 0..11>;<tipo>
```

Esempi:

| Cartoncino | Payload |
|---|---|
| Do+ | `VMBL1;A;0;+` |
| Sol7 | `VMBL1;A;7;7` |
| Do-7 | `VMBL1;A;0;m7` |
| Dodim7 | `VMBL1;A;1;dim7` |

La voce parte sempre da **Do3 (MIDI 48)**: Do+ suona 48-52-55, Sol7 suona 55-59-62-65.
Il corpo del cartoncino usa il colore spettrale del fondamentale **nel terzo registro**:
Do+ è colorato come Do3, Sol7 come Sol3, ecc. Il colore non viene letto dalla telecamera:
la guida lo ricalcola dal QR.

Nel rettangolo bianco a destra tutte le note dell'accordo sono disegnate insieme sul pentagramma,
con chiave di basso e teste di nota vuote come nei cartoncini cromatici. Diesis e bemolli sono
raggruppati all'inizio del rigo, in corrispondenza delle note alterate, anziché accanto alle teste.
La grafia segue gli intervalli dell'accordo e può usare anche doppi diesis o doppi bemolli.
Gli accordi di settima mostrano tutte e quattro le note.

Sulla griglia il cartoncino si usa come una nota: la colonna dà l'attacco, la larghezza dà la
durata. Tutte le note dell'accordo partono e si spengono insieme.

## Canali (16 cartoncini)

Codifica:

```text
VMBL1;S;CH;<canale 1..16>
```

Nel riquadro bianco i canali 1–15 mostrano un'illustrazione vettoriale dello strumento associato:

| Canale MIDI | Strumento illustrato |
|---:|---|
| 1 | Pianoforte |
| 2 | Chitarra acustica |
| 3 | Basso elettrico |
| 4 | Archi |
| 5 | Tromba |
| 6 | Sax |
| 7 | Organo |
| 8 | Sintetizzatore |
| 9 | Percussioni aggiuntive |
| 10 | Batteria e percussioni |
| 11 | Chitarra elettrica |
| 12 | Pad sintetico |
| 13 | Violino |
| 14 | Flauto |
| 15 | Effetti sonori |

Il canale 16 resta senza illustrazione perché non è presente nell'associazione richiesta.

Il cartoncino si posa nella **fascia destra**, nella sezione orizzontale della riga da configurare
(sezione 1 = riga 1, in alto). Vale solo a **sistema fermo**: durante Play la lettura non cambia
nulla. Il canale è 1-based sul cartoncino e viene inviato come canale MIDI 0-based.

La guida divide la fascia destra in sei sezioni numerate. I comandi PLAY/STOP/TAP/MODO/ONESHOT
restano validi in tutte le sezioni; un cartoncino CANALE o STRUMENTO vale per la sezione in cui
è posato.

Le assegnazioni sono salvate in `configurazione.json` (`CanaliRiga`) e restano attive al riavvio.
Le righe senza cartoncino continuano a usare `mappatura-default.json` (`CanalePerTraccia`).

## Strumenti (seconda fase)

Il protocollo è già riconosciuto:

```text
VMBL1;S;ST;<nome breve>
```

Il nome viene salvato in `configurazione.json` (`StrumentiRiga`), ma **non viene ancora inviato ad
Ableton**. Le strade possibili sono:

1. dispositivo Max for Live che ascolta un canale di controllo e carica un dispositivo da browser;
2. AbletonOSC / Live Object Model via OSC;
3. Program Change MIDI verso un rack che seleziona catene di strumenti.

La scelta dipende da come vuoi organizzare il set: caricare un dispositivo vero e proprio richiede
la API del browser di Live, non basta il MIDI.
