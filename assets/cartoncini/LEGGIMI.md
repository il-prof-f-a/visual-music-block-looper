# Cartoncini stampabili

Questa cartella contiene i 53 PNG da stampare e ritagliare per il tavolo:

- `nota-<colore>-<idClip>.png`: 6 colori (rosso, arancione, giallo, verde, blu, viola) x 8 clip (0-7) = 48 cartoncini di nota.
- `comando-play.png`, `comando-stop.png`, `comando-tap.png`, `comando-modo.png`, `comando-oneshot-36.png`: 5 cartoncini comando.

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

- Modo consigliato: impostare `VMBL_PASSI` (vedi sotto) e rigenerare: tutti i 53 file vengono riscritti con quel numero di passi; si stampano solo quelli necessari.
- Modo manuale: incollare sul lato destro una striscia dello **stesso colore** e della stessa altezza, lunga un multiplo di 30 mm. Il bordo di giunzione deve essere allineato al passo; eventuali strisce di colore diverso o bordi neri interrompono la misura della durata.

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

- **Ogni esecuzione riscrive tutti i 53 PNG**, anche con i valori di default. Con i valori di default i file risultano invariati byte per byte; con altri valori cambiano tutti. Per tornare alla versione committata usare `git checkout -- assets/cartoncini`.
- La scala fisica dei PNG è nei metadati di risoluzione (300 dpi) e nelle dimensioni in pixel: cambiare `VMBL_PASSO_MM` cambia la larghezza in mm di tutti i cartoncini. Il materiale committato usa 30 mm.
- Il generatore usa il font **Arial** per le etichette. Su macchine con font diversi i byte dei PNG e la posizione del testo possono differire leggermente; il contenuto dei QR è identico.

Il test non esplicito `Rileva_CartoncinoGenerato_PayloadEColoreCorretti` verifica che i cartoncini generati siano letti dal rilevatore reale con il payload e il colore attesi.
