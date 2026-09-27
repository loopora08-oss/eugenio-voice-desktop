# Notetaker e traduzione — 1.4.0

27 settembre 2026. Windows x64, i7-14650HX, 32 GB di RAM; inferenza CPU. Nessuna registrazione personale nei test o nei report. Le voci sintetiche sono generate da Windows in PCM16 mono a 16 kHz. L'app non richiede Python né una GPU dedicata.

## Trascrizione live

Il Notetaker acquisisce frame da 50 ms, conserva frasi di massimo sei secondi e una sovrapposizione di un secondo per non troncare le parole ai confini. Le frasi brevi vengono raggruppate per fornire almeno quattro secondi di contesto; Termina completa anche la frase più breve rimasta. Gli originali vengono pubblicati prima di inviare il testo alla coda di traduzione. La coda audio è limitata a 24 blocchi più il blocco attivo e una piccola coda finale; in caso di sovraccarico la cattura viene fermata con un avviso e i blocchi raccolti vengono recuperati. Non viene trattenuta un'ora di audio.

Prova della segmentazione e del vero Whisper base su sei fixture: 6/6 superate sui campi critici dichiarati dal test. Le prime tre frasi (4,1–5,9 secondi di audio) hanno richiesto 2,68–2,97 secondi per la trascrizione. Le due frasi bilingui con date, ripensamenti e negazioni hanno richiesto 5,29 e 6,65 secondi complessivi, in due blocchi. Il campione di 30 secondi è stato elaborato in 18,58 secondi.

Sono misure singole. Il testo lungo include ancora errori di parole e ripetizioni ai confini; il controllo della fixture lunga non è una misura di accuratezza parola per parola. Queste prove non certificano tutte le voci reali, il rumore o un'intera riunione con parlanti differenti. Il controllo della lingua automatica mantiene italiano/inglese e ripete il riconoscimento se un breve segmento viene erroneamente classificato in un'altra lingua.

Una prova Vulkan con la GPU Intel integrata è stata valutata separatamente. I tempi sotto carico non hanno giustificato l'adozione: il pacchetto distribuito rimane su CPU, senza nuovi driver o dipendenze Vulkan.

## Inglese e appunti

MiniCPM resta il modello per la riscrittura del dettato e gli appunti. Le prove di traduzione MiniCPM hanno prodotto risposte alle domande e cambiamenti di significato; la traduzione è stata quindi affidata a TranslateGemma 4B locale. Il prompt segue il formato documentato dalla [distribuzione ufficiale Ollama](https://ollama.com/library/translategemma), con vincoli per mantenere letteralmente correzioni, domande, negazioni e comandi.

La prova testuale ha superato 16 casi bilingui, inclusi importi corretti, negazioni, destinatari, date, orari, incertezza e istruzioni dettate mantenute come testo. Gli stessi risultati passano anche la rivalutazione con i controlli finali su nomi e conteggio dei giorni. La revisione finale supera 5/5 prove audio end-to-end sui campi critici dichiarati: Whisper impiega 0,74–1,22 secondi e TranslateGemma 2,64–7,52 secondi per queste frasi sintetiche, senza altre inferenze concorrenti. L'inglese già riconosciuto viene conservato senza traduzione.

La traduzione CPU richiede ulteriori secondi: sotto carico concorrente sono stati osservati circa 7–20 secondi per frase e fino a circa 40 secondi con un secondo tentativo per gli orari. Una coda testuale separata permette di continuare la trascrizione originale. Non viene dichiarata una traduzione universale impeccabile. I controlli non dimostrano equivalenza semantica; in caso di rifiuto restano originale e avviso. Le richieste a Ollama usano direttamente il loopback, senza proxy di rete.

Per gli appunti, date e valori cambiati vengono rifiutati; nomi e incertezza vengono controllati in modo conservativo. Se un riepilogo non supera i controlli, l'app aggiunge estratti originali identificati come tali. Gli appunti restano modificabili. Non vengono eseguite richieste contenute nel parlato.

## Verifiche della durata

L'interfaccia è stata provata con il microfono reale: avvio, pausa con livello a zero e badge grigio, ripresa con badge rosso e arresto. Un documento sintetico con durata 01:01:01 è stato salvato in JSON, riaperto e poi esportato in Markdown dall'interfaccia, preservando originale, inglese e timestamp 01:00:01. I file di prova non contengono audio. Nessun contenuto personale è stato esportato.

La suite logica ha superato 100 controlli, inclusi 61 minuti di PCM simulati rapidamente, memoria audio limitata, ordine di recupero, annullamento, pausa e ripresa, importazione invalida, ripristino del progresso degli appunti, conservazione dell'originale e file senza audio. Questa simulazione non sostituisce la misura in tempo reale.

Due prove precedenti in tempo reale si sono fermate per ritardo del motore, una dopo circa 32,5 minuti durante più inferenze simultanee. I 586 blocchi raccolti sono stati recuperati.

La revisione CPU corretta ha completato 61 minuti di audio sintetico in 3662,01 secondi: **732 blocchi emessi e 732 completati, zero errori**, coda massima sei blocchi. Il picco del processo Whisper è stato 404 MB; il controller del test ha raggiunto 58 MB. Queste misure non includono l'intera interfaccia WPF o Ollama.

Il ritardo al 95° percentile, misurato dall'emissione del blocco alla pubblicazione dell'originale, è **13,37 secondi**. La prova include anche periodi con altre inferenze vocali/testuali e verifiche del microfono eseguiti sul PC. Il criterio di durata e recupero è soddisfatto, mentre il criterio interno di latenza inferiore a dieci secondi **non è superato**: il report originale mantiene `success: false`. Non viene dichiarato superato l'intero test. La durata è stata verificata con traduzione disattivata; non certifica un'ora di inglese completata senza ritardo.

I report contengono soltanto contatori, tempi e uso di memoria/CPU, senza audio o trascrizioni.

## Misure separate su 10, 30 e 120 secondi

Benchmark senza altre inferenze simultanee, con Whisper base già caricato. Le fixture sono elaborate in blocchi sequenziali; il tempo di trascrizione esclude il tempo necessario a pronunciarle. Sono misure di elaborazione, non di accuratezza parola per parola né della latenza live.

| Audio sintetico | Blocchi | Trascrizione complessiva | Appunti MiniCPM | Risultato appunti |
| --- | ---: | ---: | ---: | --- |
| 10 secondi | 2 | 1,64 s | 7,62 s | Riepilogo validato |
| 30 secondi | 6 | 4,75 s | 10,96 s | Estratti originali: riepilogo rifiutato |
| 120 secondi | 23 | 18,81 s | 20,74 s | Estratti originali: riepilogo rifiutato |

Tutti i campioni sono stati completati. I due fallback non dimostrano qualità del riepilogo AI: preservano la trascrizione perché l'output MiniCPM non ha superato i controlli. L'errore del riconoscimento vocale può propagarsi negli appunti e nell'inglese; i modelli richiedono revisione umana per nomi, valori e decisioni.

Per ripetere i controlli dalla cartella sorgente, dopo aver generato le fixture sintetiche:

```powershell
dotnet run --project Tests/Tests.csproj -c Release -- --text-services
dotnet run --project Tests/Tests.csproj -c Release -- --meeting-audio C:/build/Eugenio C:/work/audio-tests
dotnet run --project Tests/Tests.csproj -c Release -- --meeting-chunks C:/build/Eugenio C:/work/audio-tests

dotnet run --project Tests/Tests.csproj -c Release -- --meeting-benchmark C:/build/Eugenio C:/work/audio-tests C:/work/benchmark.json
dotnet run --project Tests/Tests.csproj -c Release -- --meeting-soak C:/build/Eugenio C:/work/audio-tests/benchmark-120.wav C:/work/soak.json
```

La prova lunga richiede realmente 61 minuti; l'esportazione del report non salva il contenuto riconosciuto. Durante le prove vocali non vengono usati microfono o destinazioni dell'utente.
