# Provenienza degli export — prima / dopo

## Prima

JSON e Markdown contenevano testo e tempi senza una marcatura strutturata dell'assistenza IA o l'identità del modello ASR per blocco.

## Dopo

Gli export includono metadati `AiMetadata`, indicazione del flusso assistito dall'IA, revisione umana necessaria e stato della provenienza ASR. Nei nuovi blocchi del Notetaker sono registrati file/hash SHA-256 del modello verificato prima della sessione, versione Whisper.net 1.9.1, modalità decoder, lingua richiesta e timestamp UTC del risultato. Nessun percorso personale, audio o testo aggiuntivo nei metadati.

Il JSON conserva questi dati alla riapertura; il Markdown li espone in commenti strutturati `eugenio-ai-metadata` ed `eugenio-asr-provenance`, insieme a un avviso visibile. La traduzione conserva la provenienza del blocco originale: non viene presentata come provenienza del modello di traduzione. File precedenti validi restano apribili; l'assenza di metadati viene dichiarata, mai ricostruita da supposizioni. I metadati riepilogativi sono ricalcolati e non accettano una dichiarazione in ingresso di completezza.

## Indurimento e test

Validazione di hash, nome file senza percorso, versione non vuota, modalità, lingua e timestamp UTC. Provenienza malformata impedisce apertura/esportazione senza sostituire il documento corrente. Stesso serializzatore usato da Salva e test. Verifiche sulla marcatura effettivamente serializzata in JSON e Markdown, riapertura, legacy, documenti misti e conservazione attraverso la coda di trascrizione/traduzione. Fixture sintetiche: non attestano un modello reale.

Nessun nuovo endpoint, database, indice, cache persistente o servizio. Protocollo worker esteso con un campo opzionale: i risultati precedenti rimangono leggibili. Il percorso di salvataggio mantiene la scrittura temporanea e sostituzione finale già esistenti; nessun autosalvataggio. Concorrenza gestita dai percorsi dispatcher/coda precedenti; non viene acquisito nuovo audio per verificare i metadati.

## Limiti aperti

La traccia riguarda l'ASR del Notetaker, non l'intera catena. Identità immutabile dei modelli Ollama, hash/versione dei prompt, tutti i parametri effettivi, provenienza delle note/traduzioni e modifiche manuali sono ancora da completare. Non è una firma, certificazione normativa, prova di accuratezza o attestazione contro manomissioni. Il campo `Trust` dichiara `self-reported-not-signed`; la provenienza degli altri contenuti è `not-recorded`. App precedenti o editor terzi possono eliminare i metadati se risalvano/esportano il documento.

La roadmap Q-3 resta aperta. Nessuna parità Wispr o prontezza commerciale dichiarata.
