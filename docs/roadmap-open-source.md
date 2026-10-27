# Eugenio open source — specifica e registro di avanzamento

Obiettivo richiesto: rendere Eugenio un'alternativa open source indipendente a Wispr Flow, con dettatura e Notetaker realmente utilizzabili. Questo documento non riduce l'obiettivo alle modifiche già pronte. L'obiettivo resta aperto finché tutti i requisiti e i gate sono provati.

Piattaforma esistente: Windows x64. macOS/Linux/mobile richiedono porting e matrice separata, non vengono dichiarati supportati dal progetto WPF. Nessun componente proprietario Wispr riutilizzato. Scelta di base: locale, senza abbonamento/API e senza registrazione audio persistente. Risorse hardware, energia e manutenzione non sono gratuite.

## Criteri e stato prima delle modifiche

| ID | Requisito dell'alternativa | Stato iniziale / prova necessaria |
|---|---|---|
| OSS-1 | Licenza esplicita, diritti dei componenti, sorgenti e istruzioni riproducibili | Repo privato, nessuna licenza di progetto; pubblicazione solo dopo revisione di contenuti/storia |
| OSS-2 | CI su ogni modifica, build da checkout pulito e dipendenze bloccate | Solo workflow manuale; baseline locale 126 test, 0 warning |
| D-1 | Hotkey e attivazione opzionale, testo nel campo corretto, nessun invio | Esistenti; regressioni UI reali e focus/password/cambio campo richieste |
| D-2 | Italiano/inglese, nomi/dizionario, correzioni, negazioni/cifre fedeli | Esistenti parzialmente; benchmark stessa voce contro Wispr e casi negativi necessari |
| D-3 | Elaborazione durante il parlato, risultato rapido dopo stop | ASR anticipato ma ritrascrizione concatenata; misura fine voce-coda-ASR-riscrittura-inserimento richiesta |
| D-4 | Parziali revisionabili e finale senza duplicati | Mancante come esperienza completa; parziale mai digitato prematuramente nel campo esterno |
| D-5 | Profili e modello identificabile/riproducibile, installazione semplice | Tag Ollama personalizzato e worker compatibile divergente; onboarding e distribuzione da completare |
| N-1 | Meeting microfono + audio PC, sorgenti dichiarate e tempi coerenti | Solo microfono; loopback, eco, cambio dispositivo, perdita sorgente da implementare/provare |
| N-2 | Trascrizione live, pausa/ripresa/stop, backlog limitato e recupero | Esistenti su singola sorgente, test integrazione richiesti |
| N-3 | Parlanti con provenienza/confidenza, sintesi/decisioni/azioni fedeli | Nessuna identità speaker nel modello attuale; non attribuire nomi inventati |
| N-4 | Ricerca locale, riapertura/export, appunti e conversazioni lunghe | Salvataggio/riapertura esistenti; ricerca e persistenza controllata da completare |
| Q-1 | UI accessibile, stati chiari, disclosure IA, nessun testo privato nei log | QA desktop e revisione Taste con adattamento WPF necessarie |
| Q-2 | Errori reali, annullamento/doppie richieste, memoria/coda/risorse limitate | Test per ogni nuova funzione con almeno due errori |
| Q-3 | Provenienza finale e versioni puntuali, revisione licenze e dati | Da completare; nessuna conformità dichiarata dal solo triage |
| R-1 | Pacchetto nuovo realmente eseguibile e aggiornamento reversibile | Firma/policy Windows e differenza worker 1.4.0/1.4.1 da risolvere senza aggirare controlli |
| R-2 | Prova voce reale e confronto qualità/velocità Wispr | Non eseguiti; niente percentuali promesse |

## Architettura e decisioni

Avanzamento locale al 27 ottobre 2026: dettatura incrementale, sorgenti audio separate, ricerca nel documento corrente e provenienza ASR degli export implementate. Build completa preview.5: 200 controlli passati. Le prove UI della ricerca usano dati sintetici; la matrice sopra conserva lo stato iniziale e richiede ancora i gate sul percorso reale. Rapporto ricerca: `docs/ricerca-1.5-preview.4.md`; provenienza e limiti: `docs/provenienza-1.5-preview.5.md`. D-4, onboarding portabile D-5, identità speaker N-3, traccia completa Q-3, CI remota e confronto reale R-2 restano aperti.

Si evolve il repository esistente .NET/WPF/NAudio/Whisper.net: sostituire subito l'intero stack non risolve le attese dimostrate e rende più difficile separare regressioni e migliorie. Il nucleo audio/trascrizione deve rimanere separato dall'inserimento desktop e dalle note. Una sola fonte di verità per il documento riunione, code con budget, risultati correlati alla sessione e pulizia dei buffer anche dopo errore/annullamento.

Prima fetta: riconoscere ogni segmento una sola volta nel profilo rapido/adattivo, mantenendo la correzione congiunta sul testo e l'alternativa di riconoscimento audio completo nel profilo Precisione; misurare il percorso senza contenuti. Non abbassare tempi di correzione alla cieca.

Componenti candidati consultati: whisper.cpp (MIT, runtime già affine), Silero VAD (MIT), faster-whisper (MIT, runtime aggiuntivo), Handy (MIT per codice, marchio separato). Licenze/versioni da fissare prima di importare; nessuna copia repository upstream nel prodotto. Costo canoni/API aggiuntivi del percorso locale: €0; sviluppo, energia, firma e manutenzione da preventivare. Wispr può restare un riferimento manuale: Free con limiti, Pro $15/utente/mese o $12 con annuale, prezzi verificati il 26 ottobre 2026, nessun piano attivato.

## Fonti e verifica normativa

Report REA separato dal progetto, conservato nel fascicolo locale del 26 ottobre 2026 e non incluso nel pacchetto pubblico. Metodi e pattern, mai sorgenti proprietari nel repo. Date ricontrollate il 26 ottobre su EUR-Lex consolidato 27 luglio 2026 e linee guida Commissione 20 luglio: Art.50 dal 2 agosto 2026; transizione Art.50(2) per sistemi precedenti fino al 2 dicembre. Le date principali delle GUIDE risultano coerenti; ciò non certifica applicabilità o adempimento del prodotto.

## Gate finale

Tutti i requisiti sopra con evidenze sul percorso reale; review HTTP/API loopback, autorizzazione locale, indici/transazioni quando applicabili, cache, code, idempotenza, rate limit, concorrenza e scalabilità. Test errore e recupero, suite/CI/build, UI e tastiera, dati/modelli/licenze, pacchetto e rollback. Pubblicazione pubblica e release solo su contenuto concreto revisionato. Stato globale: in costruzione, non equivalente a Wispr, non DONE livello 3.
