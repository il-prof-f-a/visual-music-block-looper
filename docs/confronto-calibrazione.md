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

## Validazione sul tavolo ancora necessaria

Prima di cambiare il modello usato dall'app occorre acquisire una vista corretta attuale e un frame grezzo della stessa scena. La misura utile e lo scostamento delle linee temporali interne rispetto alle rette dopo ciascun raddrizzamento, insieme a larghezza delle celle, coerenza delle inverse e assenza di pieghe. Il solo rettangolo esterno non e una verifica sufficiente.

I dati CAL3 originali restano invariati. Nessuna nuova raccolta di punti e necessaria per eseguire il confronto; la cattura serve a scegliere e verificare il modello, non a sostituire i dodici riferimenti.
