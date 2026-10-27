# Verifica Notetaker — 27 ottobre 2026

**Esito: acquisizione e riepilogo degli appunti funzionano nei percorsi provati; qualità equivalente a Wispr e affidabilità su riunioni lunghe non dimostrate. La prova prolungata non è stata superata ed è stata alterata dallo standby del PC.**

## Prima

La generazione considerava soltanto la trascrizione, aggiungeva risultati agli appunti manuali e ripiegava spesso su estratti. Nel primo confronto sul modello pubblico Qwen3.5 4B tutti i quattro casi sintetici sono ricaduti su estratti, non su un riepilogo verificato. Alcuni scarti dipendevano da falsi positivi del validatore; altri da problemi reali del modello.

## Dopo: cosa cambia

- Schede Personali e Riepilogo IA separate, incluse nel salvataggio e nel Markdown. Anche gli appunti senza registrazione possono essere riassunti.
- Input composto da trascrizione e appunti, suddiviso in brani di massimo 6000 caratteri senza troncamento della coda.
- Risultato pubblicato solo se tutti i brani superano i controlli. Nessun estratto spacciato per riepilogo, nessun rimpiazzo parziale in caso di errore. Annullamento disponibile, tempo massimo complessivo di dieci minuti.
- Prompt con distinzione di riepilogo, decisioni, attività e questioni aperte; controlli corretti per negazioni, incertezze e attività future. Aggiunti controlli contro l'inversione di chi deve porre/ricevere una domanda e contro il dichiararla già posta.
- Ulteriore salvaguardia nei sorgenti: aggiornamento automatico sospeso dopo una modifica manuale al riepilogo, stato conservato nel documento. L'app finale compila senza avvisi o errori; questa salvaguardia non è ancora verificata in esecuzione, come indicato sotto.

## Prove eseguite

| Prova | Evidenza | Limite |
|---|---|---|
| Dispositivi audio reali | Circa 5 secondi: 80 callback del microfono, due blocchi microfono e uno audio PC; pausa/ripresa e doppio stop, nessun errore; callback fermi dopo stop | Prova di attività dei dispositivi, non di comprensione del parlato; nessun audio o testo acquisito salvato |
| Pipeline ASR | Sei clip sintetiche con worker reale, zero fallimenti nei controlli mirati su parole/cifre/date/negazioni | Non equivale a zero errori di trascrizione o a una conversazione reale |
| Riepilogo nella UI WPF | Appunti sintetici digitati, generazione col modello configurato, riepilogo apparso nella scheda separata | Un singolo caso; non stima l'accuratezza generale |
| Salvataggio dalla UI | File `.artifacts/native-notetaker-summary.eugenio.json`: appunti conservati, riepilogo separato di 278 caratteri, marcatura IA, nessun audio | Documento sintetico locale; non un test di sincronizzazione o recupero da crash |
| Logica e errori | 221 controlli passati nella build della preview.6, inclusi errori di servizio, risposta vuota, input fuori budget, annullamento e fallimento di un brano intermedio | Non copre automaticamente le ulteriori modifiche successive a quella build |
| Durata | Test previsto di 61 minuti terminato con `success=false`: 1254,05 secondi di audio sintetico, 209 blocchi/209 risultati, un errore, picco coda 26 e latenza p95 23,19 secondi; durata reale 1644,16 secondi | Standby Windows durante la prova: non dimostra né affidabilità per un'ora né un difetto del solo motore; non acquisisce dispositivi reali |

### Causa osservata nell'interruzione della prova prolungata

Fino al minuto 18: 179 blocchi e 179 risultati, coda vuota. Gli eventi di sistema Windows attestano ingresso in Modern Standby alle 13:00:44 e uscita alle 13:09:12 (eventi 506 e 507). Nello stesso intervallo il consumo CPU del worker quasi si ferma e la coda sale. Il simulatore usa scadenze assolute: dopo una sospensione tenta di recuperare i frame arretrati senza attesa, producendo un carico diverso da un ingresso audio in tempo reale. È quindi plausibile che questo abbia contribuito alla saturazione; non è una prova isolata della causa unica. Il canale conserva e completa i 209 blocchi emessi, ma il criterio di durata e latenza fallisce.

La ripetizione deve avvenire con PC attivo e annotazione delle interruzioni del simulatore; non è stata cambiata alcuna impostazione energetica. Serve inoltre una prova separata del vero Notetaker durante sospensione/ripresa, verificando che segnali chiaramente eventuali intervalli non acquisiti. Non si deve mascherare il fallimento riducendo la durata richiesta o aumentando la coda.

## Qualità effettiva dei riepiloghi

Quattro scenari sintetici: decisioni/incarichi, appunti manuali, cambi di decisione e inglese con istruzioni citate. I controlli sono lessicali e conservativi; gli output sono stati letti anche qualitativamente. Non sono una percentuale di accuratezza statistica.

| Modello già presente sul PC | Casi che superano i controlli | Tempi osservati |
|---|---|---|
| `minicpm-assistente-it:latest`, effettivamente configurato per Eugenio | 1 su 4 | Circa 5–20 secondi |
| `qwen3.5:4b`, alternativa locale provata | 3 su 4 | Circa 18–29 secondi |

Il caso inglese di Qwen classifica erroneamente come non decisa una scelta concordata, pur ricordandola nel recap: il risultato viene rifiutato. Nei casi italiani restano ripetizioni tra sezioni; organizzare un testo breve non implica accorciarlo. Il modello configurato resta quindi un limite concreto, e Qwen 4B non viene dichiarato universalmente affidabile o equivalente a Wispr. I tag sono accompagnati dai digest esatti nei rapporti locali; non viene cambiata automaticamente la configurazione personale. Nessuna API a pagamento o nuovo download utilizzato. Hardware, energia e manutenzione restano costi.

Due ulteriori casi mai usati per correggere il prompt sono stati aggiunti al benchmark, ma **non eseguiti**: Windows Code Integrity ha bloccato `Tests.dll` nella cartella `notes-heldout-checks`, eventi 3077 e 3033, requisito di firma Enterprise. Compilazione riuscita non significa esecuzione riuscita. Nessun criterio di sicurezza modificato, nessuna esecuzione del file bloccato da un'altra cartella. Per riprendere quel controllo serve un'esecuzione autorizzata dal criterio Windows o un ambiente CI autorizzato.

## Confronto Wispr e gate aperti

La documentazione ufficiale Wispr descrive note personali separate dai riepiloghi, decisioni e prossime attività, mantenimento della versione precedente dopo errori e generazione dopo la registrazione. Queste sono capacità dichiarate dal produttore, non risultati di un confronto sullo stesso audio: [editor e note](https://docs.wisprflow.ai/articles/9406970664-Meeting-notes-and-the-editor-in-Notetaker), [Flow Summaries](https://docs.wisprflow.ai/articles/1422535682-flow-summaries-in-notetaker-beta).

Mancano ancora: confronto diretto sul medesimo audio, riunioni reali con voci sovrapposte/rumore, identificazione dei partecipanti, riepilogo globale coerente tra molti brani, recupero durabile e cronologia, valutazione su un corpus ampio, ripetizione valida del test prolungato, gestione verificata di standby/ripresa e sblocco legittimo delle verifiche Windows pendenti. La riapertura del nuovo documento salvato non è stata provata nella UI: il controllo JSON non la sostituisce. Il requisito di qualità paragonabile resta aperto.

## Review tecnica delle aree toccate

Ollama solo loopback senza proxy, nessun nuovo endpoint pubblico o chiave. Nessun database o indice aggiunto. Una generazione per finestra; risultati applicati alla fine, annullamento e limiti di tempo/dimensione. Appunti modificabili durante la richiesta, riepilogo segnalato da aggiornare quando gli input cambiano. Nessuna coda esterna o retry di scrittura; persiste la scrittura temporanea con sostituzione finale. Non è un audit completo o una certificazione commerciale.

Evidenze locali escluse dal repo: `notes-baseline-qwen4b.json`, `notes-final-qwen4b.json`, `notes-actual-minicpm.json`, `real-meeting-capture.json`, `notetaker-chunks-verified.txt`, `notes-safety-tests.txt`, `notetaker-soak-61min.json`, `native-notetaker-summary.eugenio.json` nella cartella `.artifacts`.
