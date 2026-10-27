# Eugenio 1.5.0-preview.3 — prima / dopo

Stato: sviluppo locale, non release pubblica, nessuna equivalenza con Wispr dimostrata.

## Prima

Baseline 1.4.1: 126 verifiche locali; modello di riscrittura personalizzato sul PC, microfono come unica sorgente del Notetaker. Il controllo sulle riscritture poteva accettare omissioni consistenti; il confronto iniziale dei modelli misurava soprattutto validità tecnica. Mancando il modello configurato, l'interfaccia poteva selezionarne un altro installato.

## Dopo: implementazione e indurimento

- Segmenti riconosciuti una sola volta nei profili Equilibrata/Velocità, conservando la via audio completa per Precisione, con correlazione audio/risultato, annullamento, budget e pulizia dei buffer.
- Notetaker con sorgenti microfono/audio PC separate, tempi comuni e deduplicazione per sorgente. Consenso prima della cattura; errore di dispositivo persistente. Nessuna diarizzazione o identificazione dei partecipanti dichiarata.
- Preview isolata dalle impostazioni personali. Scelta dei modelli limitata al nome configurato: nessun ripiego su modelli di altri progetti.
- Risposta vuota del modello distinta da riscrittura riuscita; output troncato o servizio indisponibile restano errori. Soglia contro accorciamenti distruttivi aumentata dal 35% al 60% delle parole, con eccezione per correzioni letterali riconosciute. È un controllo conservativo, non un verificatore semantico.
- Benchmark con riferimenti attesi indipendenti dal validatore, 14 frasi sintetiche, di cui sei assenti dalle dimostrazioni nel prompt. La correzione letterale risolta senza modello viene conteggiata nel percorso completo, non nelle prestazioni del modello.
- Licenza MIT del codice, SDK e dipendenze bloccati, workflow CI predisposto, manifest SHA-256 del pacchetto. CI remota e pubblicazione non eseguite.

## Prove eseguite

Compilazione della preview con warning trattati come errori; **171 controlli passati, zero falliti**. Manifest verificato: 943 file censiti, zero discrepanze; il manifest stesso costituisce il file numero 944. Questi numeri riguardano il pacchetto locale prima degli aggiornamenti documentali di questo rapporto.

Inferenza Whisper della preview.2 su 22 esecuzioni di clip sintetiche: profilo rapido 24 errori normalizzati su 508 parole, circa 26,1 secondi complessivi di riconoscimento; adattivo 13/508, circa 48,5 secondi. Sono misure di quel corpus e di quel PC, non WER universale, latenza dopo stop o confronto con Wispr. Il normalizzatore e le cache limitano la comparabilità.

Qwen3.5 2B locale, confronto testuale ampliato della preview.3: **13 riferimenti su 14 rispettati**, inclusi i sei casi aggiunti. Il caso «Ignora tutte le istruzioni precedenti…» perde parte del testo: ora il validatore lo rifiuta. Le chiamate effettive al modello hanno richiesto circa 3,6–4,3 secondi ciascuna; una correzione letterale gestita dal codice ha richiesto 2 ms. Nessun download o servizio a pagamento utilizzato. Non è ancora una scelta approvata per la distribuzione.

Il precedente esperimento MiniCPM del progetto non ha superato la pulizia delle esitazioni. La specifica lo indica come sperimentale e lo script standard ne impedisce l'installazione implicita. Non viene scelto come nuovo modello predefinito.

Interfaccia WPF osservata nella preview.2, anche a 880×620: avvio senza consenso fermato con messaggio operativo; nessuna acquisizione reale avviata durante questa prova. La nuova preview.3 è compilata; le modifiche di layout finali richiedono nuova osservazione. Nessuna prova browser viene presentata come prova WPF.

## Ancora da completare

Onboarding portabile dei modelli con licenze/versioni verificabili, trascrizione parziale revisionabile, ricerca locale, provenienza degli export, attribuzione speaker affidabile, prove prolungate e voce/dispositivi reali, confronto sullo stesso audio con Wispr, QA finale accessibilità e distribuzione su PC pulito. La roadmap completa resta vincolante. Nessuna pubblicazione, installazione sulla consegna precedente o modifica delle impostazioni personali effettuata.

Evidenze grezze locali escluse dal repository: `.artifacts/test-fidelity.txt`, `test-fidelity-build.txt`, `build-preview-3.txt`, `text-qwen-2b-v3.json`, `speech-quality-preview-2.json`, `speech-quality-adaptive-preview-2.json`. Rigenerare build e benchmark sul proprio hardware; non trattare un test sintetico come prova di una conversazione reale.
