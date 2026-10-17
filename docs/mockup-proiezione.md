# Mockup della proiezione

Questo documento mostra come appare il tavolo proiettato dalla guida (`FormProiettore` + `RenderGuida`). Il mockup è in `docs/mockup-proiezione.svg`.

![Mockup della guida proiettata](mockup-proiezione.svg)

## Elementi disegnati dalla guida

La guida è disegnata da `RenderGuida.Disegna` sull'intero schermo del proiettore, in quest'ordine:

| Elemento | Quando | Colore (RGB) | Dettaglio |
|---|---|---|---|
| Fondo | sempre | bianco (255, 255, 255) | `Color.White`: il tavolo è la zona bianca proiettata. |
| Griglia | sempre | grigio chiaro (211, 211, 211) | 6 tracce x 16 step, linee di 1 px. Area normalizzata del tavolo: x 0,02, y 0,04, larghezza 0,80, altezza 0,92. |
| Playhead | sempre | arancio (255, 140, 0) con opacità circa 35% quando in esecuzione | Colonna dello step corrente. Fermo: solo contorno grigio (128, 128, 128). |
| Nota riconosciuta | per ogni nota stabile | riempimento verde (60, 179, 75), bordo verde scuro (30, 110, 45) spessore 2 | Riempie la cella: da step iniziale per la durata in step (troncata a fine griglia), tutta l'altezza della traccia. |
| Errore | per ogni cartoncino scartato | riempimento rosso chiaro (255, 120, 120) con opacità circa 24%, bordo rosso (220, 30, 30) spessore 3 | Disegnato sull'area reale del cartoncino, non sulla cella. |

## Elementi fisici (non disegnati dalla guida)

- **Zona comandi**: fascia destra del tavolo, x da 0,84 a 0,98, y da 0,04 a 0,96 (`Compositore.ZonaComandiDefault`). Un cartoncino è un comando se il suo centro cade in questa zona. La guida **non la disegna**: va segnata sul tavolo (ad esempio con nastro) per aiutare l'operatore.
- **Cartoncini**: sono sul tavolo fisico, non sul proiettore. Il mockup mostra un cartoncino comando di esempio nella zona comandi solo come riferimento.
- **Etichette di testo** (nomi dei colori, "clip 3 · nota 39", "errore", "PLAY"): nel mockup sono annotazioni del documento. `RenderGuida` non disegna testo.

## Lettura del mockup

- **Playhead** sulla colonna 6: lo step corrente (indice 5) mentre la sequenza è in esecuzione.
- **Nota verde**: il cartoncino rosso (traccia 0, prima riga) è riconosciuto a step 3-5 (durata 3 step). Anche le altre note verdi sono nella riga del proprio colore: giallo (traccia 2), verde (traccia 3). In tutti i casi il colore del cartoncino deve coincidere con la riga.
- **Contorno rosso** nella riga blu: un cartoncino scartato, ad esempio per colore incoerente o sovrapposizione. Il motivo dello scarto è nella tabella della prova hardware (`docs/prova-hardware.md`, sezione 9).
- **Zona comandi** a destra: i cartoncini comando, come PLAY, STOP, TAP, MODO e ONESHOT, sono letti dalla zona e non diventano note.

## Coerenza con il codice

- Colori e dimensioni: `src/App/RenderGuida.cs`.
- Area della griglia e zona comandi: `src/App/Program.cs` (`ConfigGriglia.Area`) e `src/App/Compositore.cs` (`ZonaComandiDefault`).
- Tracce, step e colori dei cartoncini: `src/Core/ColoreTraccia.cs`, `src/Core/ConfigGriglia.cs`, `assets/cartoncini/LEGGIMI.md`.
