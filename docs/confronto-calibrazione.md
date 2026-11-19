# Confronto dei modelli sui dodici riferimenti esistenti

Il comando seguente legge un file CAL3 e scrive un rapporto senza modificare i riferimenti o l'app in esecuzione:

```powershell
python tools/confronta_calibrazione.py percorso/calibrazione.txt --output TestResults/calibrazione/confronto-modelli.json
```

Dipendenza: NumPy. Il file deve contenere i dodici riferimenti perimetrali nell'ordine dello schema dell'app.

## Modelli

- Omografia: riferimento senza correzione curva.
- TPS: riproduzione matematica della spline attuale, con interpolazione esatta dei dodici riferimenti.
- Coons: interpolazione cubica dei quattro bordi e fusione transfinita, sottraendo il contributo bilineare dei vertici. Anche questo modello passa per i dodici riferimenti.
- Polinomio cubico: dieci termini per coordinata, stimati ai minimi quadrati sul residuo dell'omografia; non forza l'interpolazione di eventuale rumore dei click.

Per tutti i modelli la mappa diretta e tavolo -> camera. Le distanze sui riferimenti sono espresse nei pixel della camera. La geometria del residuo viene campionata su 41x41 punti per controllare pieghe e anisotropia; questo controllo non dimostra che il tavolo reale sia raddrizzato.

## Risultati preliminari, 17 novembre 2026

Sul CAL3 esistente (SHA-256 `3aa587411f1e8ec76cecf06f029568a19ebffb3144d1c90763302a8817a830fa`), RMS sui dodici riferimenti:

| Modello | RMS camera | Massimo camera |
| --- | ---: | ---: |
| Omografia | 29,37 px | 47,51 px |
| TPS | circa 0 | circa 0 |
| Coons | circa 0 | circa 0 |
| Polinomio cubico | 1,81 px | 2,68 px |

TPS e Coons hanno errore nullo per costruzione: non si puo scegliere fra loro usando solo questo numero. Nessuno dei modelli ha pieghe nella griglia campionata, ma Coons e il cubico hanno variazioni di scala piu forti della TPS; vanno controllati sulle righe interne reali.

Il programma esegue anche una prova sintetica indipendente, con dodici punti di fit e 961 punti interni non usati nel fit. Il generatore applica una deformazione radiale cubica e poi una prospettiva, come il caso di prova gia presente in `CalibrazioneCurvaTests`. Con coefficiente radiale -0,30:

| Modello | RMS interno sintetico |
| --- | ---: |
| Omografia | 14,08 px |
| TPS | 4,81 px |
| Coons | 0,044 px |
| Polinomio cubico | 0,047 px |

Questo risultato indica candidati promettenti per quel tipo di distorsione, non una misura della webcam reale. Un solo perimetro non identifica univocamente la deformazione interna: piu mappe diverse possono rispettare gli stessi dodici click.

## Esito misurato e modello adottato

La prova sul frame reale ha mostrato che le linee orizzontali interne sono gia dritte nella camera, mentre il bordo proiettato e curvo. Il TPS che passa per i dodici click incurva quindi le linee interne per raggiungere il bordo: e l'effetto bolla osservato. L'omografia conserva le rette ma lascia il bordo curvo. La soluzione adottata e un **blend**:

- al centro si usa l'omografia;
- nella fascia di bordo (0,04–0,22 dalla cornice) si usa il TPS, con transizione smoothstep;
- i dodici riferimenti restano esatti perché si trovano a 0,03 dal bordo, dentro la fascia piena.

Misura sul frame reale, linee centrali (curvatura residua in unita normalizzate del tavolo, equivalente in pixel sull'uscita 655x368):

| Modello | RMS sui 12 punti | Curvatura linee centrali |
| --- | ---: | ---: |
| Omografia | 19,6 px | circa 0,0005 (0,2 px) |
| TPS | 0 px | circa 0,0092 (3,4 px) |
| Radiale k1+k2 | 5,7 px | circa 0,0095 (3,5 px) |
| Blend 0,04–0,22 | 0 px | circa 0,0016 (0,6 px) |

Il blend e quindi il modello usato da `CalibrazioneCurva`/`DeformazioneCalibrazione`. La mappa diretta e usata dal remap; l'inversa si ottiene con Newton e, in ripiego, con un punto fisso. Il formato `VMBL-CAL3` resta invariato: i file esistenti applicano la nuova correzione senza rifare i dodici click. Il remap usa la cache gia presente e costruisce la mappa solo nella fascia di bordo; l'inversa aggiunge pochi passi di Newton per i singoli centri dei cartoncini, non per tutti i pixel.

La fascia è configurabile in `configurazione.json`:

```json
"CorrezioneBordo": {
  "Modello": "Bordo",
  "Pieno": 0.04,
  "Esterno": 0.22,
  "FattoreX": 1.0,
  "FattoreY": 1.0,
  "Smussamento": 0.0
}
```

`Pieno` deve restare almeno 0,03 per rispettare i dodici click. Valori utili di `Esterno`: 0,18–0,26. Con 0,12 i cartoncini ai bordi si deformano ancora molto; con 0,22–0,24 diventano quasi rettangolari e le linee centrali restano entro circa 0,6 px. Oltre 0,26 la correzione si distribuisce troppo e le linee centrali iniziano a incurvarsi.

### Modello «BordoSmussato»

Il secondo modello sostituisce il minimo netto con una **softmin** delle quattro distanze dal bordo. I parametri `FattoreX` e `FattoreY` allargano o restringono la fascia in orizzontale e in verticale; `Smussamento` controlla la morbidezza dell'angolo. Un punto di partenza è:

```json
"CorrezioneBordo": {
  "Modello": "BordoSmussato",
  "Pieno": 0.04,
  "Esterno": 0.24,
  "FattoreX": 1.0,
  "FattoreY": 0.8,
  "Smussamento": 20
}
```

Un fattore **minore di 1** allarga la fascia in quella direzione: `FattoreY = 0,8` corregge di più sopra e sotto, `FattoreX = 0,9` di più a sinistra e a destra. Con `Smussamento = 0` il modello usa ancora il minimo netto, ma con i fattori di anisotropia. Il calcolo resta O(1) per pixel; la mappa continua a essere costruita una volta e memorizzata nella cache. Il modello e i parametri si possono cambiare dal vivo dal pulsante **Correzione bordo...** della finestra di controllo; **Salva** li scrive in `configurazione.json` per il prossimo avvio.

Il test sintetico `CalibrazioneCurvaTests.BordoCurvo_LineeInterneDritteE12PuntiRispettati` riproduce il caso reale: bordo curvo, centro omografico, linee orizzontali e verticali interne rette. Il vecchio caso sintetico con distorsione radiale resta utile per studiare una lente reale, ma non rappresenta il tavolo attuale.

### Modello «Sferico»

Il terzo modello assimila la lente a una sfera: un raggio che entra con angolo `θ` rispetto all'asse ottico viene proiettato sul sensore a distanza radiale `r_d`. Le proiezioni classiche sono:

| Proiezione | Legge | Inversa |
| --- | --- | --- |
| Equisolido | `r_d = 2f·sin(θ/2)` | `θ = 2·asin(r_d/(2f))` |
| Equidistante | `r_d = f·θ` | `θ = r_d/f` |
| Stereografico | `r_d = 2f·tan(θ/2)` | `θ = 2·atan(r_d/(2f))` |

La mappa completa è: i dodici punti del tavolo passano per un'omografia `H` verso il piano ideale; il punto ideale `q` ha raggio `r = |q - centro|` e angolo `θ = atan(r/f)`; la sfera produce `r_d`; il pixel camera è `centro + (q-centro)·r_d/r`. L'inversa usa la tabella e poi `r_u = f·tan(θ)`, quindi `H⁻¹`.

I dodici click stimano `H`, `f` e il centro ottico. Non serve un nuovo formato: si parte dalle stesse dodici coppie di `VMBL-CAL3`. Sul frame reale 1284x966 la stima ha dato `f ≈ 740 px`, centro `≈ (693, 483)` e RMS sui dodici punti `≈ 5,3 px`; visivamente i cartoncini ai bordi risultano più rettangolari dei due modelli di bordo.

```json
"CorrezioneBordo": {
  "Modello": "Sferico",
  "TipoSferico": "Equisolido",
  "Focale": 740,
  "CentroX": 693,
  "CentroY": 483
}
```

Con `Focale = 0`, `CentroX = 0` o `CentroY = 0` il programma stima il valore dai dodici punti. Il pulsante **Correzione bordo...** permette di scegliere la proiezione e regolare focale e centro dal vivo.

Riferimenti consultati: [ortorettifica fisheye equisolida](https://www.mdpi.com/2072-4292/14/17/4175), [rassegna dei modelli fisheye](https://arxiv.org/html/2401.00442v1), [tabella delle proiezioni](https://commonlands.com/blogs/technical/wide-angle-lenses-and-fisheye-camera-lens-distortion), [equazioni di proiezione fisheye ISPRS](https://www.isprs.org/proceedings/xxxvi/5-W8/Paper/PanoWS_Berlin2005_Schwalbe.pdf).

## Limiti

I dodici punti descrivono solo il perimetro. Il modello sferico aggiunge un vincolo fisico globale e ha dato il risultato migliore sul frame reale, ma non è una garanzia per ogni lente: se la forma non è assimilabile a una sfera, restano i modelli di bordo o una calibrazione guidata dalle linee proiettate. In tutti i casi la verifica va fatta su un frame grezzo e sulle linee interne, non solo sul rettangolo esterno. I dati CAL3 originali restano invariati; la cattura serve a scegliere e verificare il modello, non a sostituire i dodici riferimenti.
