# Cartoncini stampabili

Questa cartella contiene i 75 PNG da stampare e ritagliare per il tavolo:

- `nota-<colore>-<idClip>.png`: 6 colori (rosso, arancione, giallo, verde, blu, viola) x 8 clip (0-7) = 48 cartoncini di nota.
- `comando-play.png`, `comando-stop.png`, `comando-tap.png`, `comando-modo.png`, `comando-oneshot-36.png`: 5 cartoncini comando.
- `speciale-*.png`: 22 cartoncini speciali (misura, collegamento, A-B), vedi sotto.

## Scala fisica

- 1 passo di griglia = **30 mm** di larghezza.
- Altezza del cartoncino = **40 mm**.
- Il cartoncino standard è largo **2 passi = 60 mm x 40 mm**.
- Risoluzione dei PNG: **300 dpi** (metadati inclusi).

## Come stampare

1. Stampare su carta opaca, in formato A4 o A3, con il colore a pieno campo.
2. Nella finestra di stampa impostare la scala al **100%** (disattivare "Adatta alla pagina" / "Fit to page" e qualsiasi riduzione).
3. Verificare con un righello: il cartoncino di 2 passi deve misurare 60 x 40 mm.
4. Tagliare lungo il contorno nero sottile. Il QR (quadrato bianco a sinistra) deve restare intero e a filo del taglio o con poco margine colorato intorno.

## Colori e payload

| Colore | Traccia (indice) | Corpo (RGB) |
|---|---|---|
| Rosso | 0 | 220, 40, 40 |
| Arancione | 1 | 255, 140, 0 |
| Giallo | 2 | 250, 210, 0 |
| Verde | 3 | 40, 160, 70 |
| Blu | 4 | 30, 90, 200 |
| Viola | 5 | 130, 50, 170 |

Nei cartoncini di nota l'etichetta mostra il colore, l'idClip e la nota MIDI (es. "Rosso 3 · nota 39" = idClip 3, nota 36 + 3). I cartoncini comando hanno corpo grigio-azzurro (110, 120, 140) e non hanno colore di traccia.

Payload dei comandi: `VMBL1;C;PLAY`, `VMBL1;C;STOP`, `VMBL1;C;TAP`, `VMBL1;C;MODO`, `VMBL1;C;ONESHOT;36`.

## Durata: cartoncini più lunghi

La durata di una nota è la larghezza del corpo colorato, in passi (arrotondata, minimo 1). Per una nota di 3 passi il cartoncino deve essere largo 90 mm; per 4 passi 120 mm.

- Modo consigliato: impostare `VMBL_PASSI` (vedi sotto) e rigenerare: tutti i 75 file vengono riscritti con quel numero di passi; si stampano solo quelli necessari.
- Modo manuale: incollare sul lato destro una striscia dello **stesso colore** e della stessa altezza, lunga un multiplo di 30 mm. Il bordo di giunzione deve essere allineato al passo; eventuali strisce di colore diverso o bordi neri interrompono la misura della durata.

## Cartoncini speciali

Oltre a note e comandi, il tavolo legge 22 cartoncini speciali che impostano la struttura del giro. Si posano nella colonna a sinistra della griglia, in tre zone:

- **Misura** (in alto): battiti per misura, 2/4, 3/4 o 4/4.
- **Collegamento** (al centro): righe per gruppo, 1, 2, 3 o 6 righe.
- **A-B** (in basso): l'intervallo di righe che il giro percorre, da A a B.

Il corpo è viola-grigio (120, 100, 150), diverso da quello dei comandi, e non ha colore di traccia. Ogni zona accetta un solo cartoncino: se ne sono posati più di uno vale quello più in alto (poi quello più a sinistra), gli altri sono segnalati come sovrapposti e ignorati.

| File | Payload | Etichetta | Significato |
|---|---|---|---|
| `speciale-misura-2.png` | `VMBL1;S;MISURA;2` | 2/4 | misura di 2 battiti |
| `speciale-misura-3.png` | `VMBL1;S;MISURA;3` | 3/4 | misura di 3 battiti |
| `speciale-misura-4.png` | `VMBL1;S;MISURA;4` | 4/4 | misura di 4 battiti (default) |
| `speciale-link-1.png` | `VMBL1;S;LINK;1` | 1 riga | ogni riga è un gruppo a sé |
| `speciale-link-2.png` | `VMBL1;S;LINK;2` | 2 righe | righe collegate a 2 a 2 |
| `speciale-link-3.png` | `VMBL1;S;LINK;3` | 3 righe | righe collegate a 3 a 3 |
| `speciale-link-6.png` | `VMBL1;S;LINK;6` | 6 righe | tutte le righe collegate (default) |
| `speciale-ab-1-2.png` ... `speciale-ab-5-6.png` (15 file) | `VMBL1;S;AB;<a>;<b>` | A-B 1-2 ... A-B 5-6 | il giro percorre le righe da A a B |

Ogni cartoncino A-B è una sola coppia: le 15 coppie con 1 <= a < b <= 6 sono `speciale-ab-1-2.png`, `speciale-ab-1-3.png`, ..., `speciale-ab-5-6.png`.

### Dove posarli

- Misura, Collegamento e A-B vanno nella colonna a sinistra, con il centro del cartoncino nella zona corrispondente: Misura in alto, Collegamento al centro, A-B in basso.
- Per ogni zona basta un cartoncino. Un cartoncino A-B con B oltre le righe collegate, o con il collegamento a 1 riga, viene scartato come non valido e si usa il default di quel parametro.

### Default

Se una zona è vuota vale il default: **4/4**, **6 righe collegate**, **A-B 1-6**. Con 1 riga per gruppo le righe sono sei gruppi indipendenti, come nel comportamento precedente.

### Scala

La griglia è ora più stretta (67% della larghezza invece di 80%), perché la colonna a sinistra ospita le tre zone. Lo step proiettato risulta circa il 16% più stretto: per allineare la scala fisica dei cartoncini ricalcolare `VMBL_PASSO_MM` (circa 30 x 0.84, quindi circa 25 mm) e verificare con la proiezione reale. I PNG committati restano a 30 mm, come gli altri cartoncini.

I QR speciali usano la correzione d'errore L: il payload di misura passa così alla versione QR 1, come gli altri cartoncini, e il rilevatore lo legge anche quando più QR sono visibili insieme. La griglia QR resta di 21 moduli, quindi il layout non cambia.

## Rigenerare il materiale

Il generatore è il test esplicito `GeneraCartoncini` in `src/Tests/CartonciniStampabiliTests.cs`, con la logica in `src/Tests/GeneratoreCartoncini.cs`. Dalla radice del repository:

```
dotnet test src/Tests --filter "Name=GeneraCartoncini"
```

Il test legge due variabili d'ambiente opzionali (senza variabili valgono 30 mm e 2 passi):

- `VMBL_PASSO_MM`: larghezza di un passo in mm, tra 15 e 60. Esempio: `set VMBL_PASSO_MM=25` (PowerShell: `$env:VMBL_PASSO_MM = "25"`), poi lo stesso comando.
- `VMBL_PASSI`: passi per cartoncino, intero tra 2 e 8. Esempio: `set VMBL_PASSI=3`.

Valori fuori intervallo fanno fallire il test con un messaggio che indica la variabile sbagliata.

Attenzione:

- **Ogni esecuzione riscrive tutti i 75 PNG**, anche con i valori di default. Con i valori di default i file risultano invariati byte per byte; con altri valori cambiano tutti. Per tornare alla versione committata usare `git checkout -- assets/cartoncini`.
- La scala fisica dei PNG è nei metadati di risoluzione (300 dpi) e nelle dimensioni in pixel: cambiare `VMBL_PASSO_MM` cambia la larghezza in mm di tutti i cartoncini. Il materiale committato usa 30 mm.
- Il generatore usa il font **Arial** per le etichette. Su macchine con font diversi i byte dei PNG e la posizione del testo possono differire leggermente; il contenuto dei QR è identico.

Il test non esplicito `Rileva_CartoncinoGenerato_PayloadEColoreCorretti` verifica che i cartoncini generati siano letti dal rilevatore reale con il payload e il colore attesi. Il test `Rileva_CartoncinoSpeciale_PayloadCorretto` fa lo stesso per `speciale-misura-3.png`, `speciale-link-2.png` e `speciale-ab-2-5.png`, e `Speciali_PayloadAccettatiERisoltiSenzaErrori` controlla che tutti i 22 payload speciali siano validi nella loro zona.
