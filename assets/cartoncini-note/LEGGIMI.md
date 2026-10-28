# Cartoncini cromatici Do2-Si5

48 note, diesis inclusi, MIDI 36-83. Do4 e il Do centrale (MIDI 60).
Do2-Si3 usano la chiave di basso; Do4-Si5 la chiave di violino.
La testa e un rombo vuoto senza gambo: indica l'altezza, mentre la durata
rimane determinata dalla larghezza del cartoncino.

## Stampa

PDF: `../../output/pdf/cartoncini-note-do2-si5.pdf`, quattro pagine A4,
una per ottava. Stampare al 100%, senza adattamento alla pagina.
Ogni cartoncino misura 60 x 40 mm (due passi da 30 mm).
La linea di controllo su ogni pagina deve misurare 60 mm.
I 48 file `nota-*.png` sono a 300 dpi, con dimensioni arrotondate al pixel.
`anteprima.png` e le immagini `pagina-*.png` sono solo anteprime.

## Accordatura e colori

Temperamento equabile con La4 = 440 Hz:

    f(m) = 440 * 2^((m - 69) / 12)

Frequenza stampata arrotondata a due decimali. Il CSV conserva sei decimali.

La scala colore e ancorata agli estremi del pianoforte a 88 tasti:
La0 (MIDI 21, 27.5 Hz) -> rosso a 700 nm;
Do8 (MIDI 108, circa 4186.01 Hz) -> violetto a 400 nm.
La frequenza ottica cresce linearmente con il logaritmo della frequenza
musicale, cioe con il numero MIDI:

    t = (m - 21) / 87
    f_ottica = c / (700 nm) + t * (c / (400 nm) - c / (700 nm))
    lambda = c / f_ottica

La conversione della lunghezza d'onda in RGB e illustrativa, con intensita
ridotta per mantenere il corpo rilevabile. Non e una equivalenza fisica tra
suono e luce ne una riproduzione colorimetrica calibrata. Poiche l'intervallo
stampato copre solo una parte del pianoforte, i fondi vanno dall'arancione
al blu: rosso e violetto appartengono alle note estreme fuori da questo set.

## Compatibilita

QR: `VMBL1;N;<midi-36>`, quindi idClip 0-47. La mappatura attuale
`assets/config/mappatura-default.json` produce le note MIDI corrette senza
modifiche. Una mappatura personalizzata puo cambiare questa corrispondenza.
La scelta dello strumento rimane legata al gruppo di righe del tavolo.

Il test `CartonciniNoteCromaticheTests` verifica tutti i QR, la nota MIDI e
l'ingombro misurato dal rilevatore su immagini sintetiche ridotte a
360 x 240 pixel per cartoncino. La prova con stampa e webcam resta necessaria.
I vecchi cartoncini e i relativi generatori sono conservati.

## Rigenerazione

Eseguire dalla radice: `python tools/genera_note_cromatiche.py`.
Dipendenze: reportlab, pypdfium2, Pillow e i font Windows Segoe UI e
Segoe UI Symbol. Il generatore produce PDF, PNG, anteprime e indice CSV.
