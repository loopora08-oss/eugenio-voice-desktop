# Comprensione e velocità — Eugenio 1.4.1

## Comportamento

- Il meeting cerca una pausa naturale dopo sei secondi e taglia forzatamente a dieci; mantiene il secondo di sovrapposizione quando serve. Una pausa di 450 ms conferma i blocchi sufficientemente lunghi.
- Equilibrata usa base fino a sei secondi e small per blocchi italiani più lunghi. Dopo un risultato inglese il blocco successivo può usare base, continuando a riconoscere automaticamente la lingua. Sono disponibili velocità e precisione, senza traduzione automatica.
- Il worker conserva al massimo due modelli verificati e non li ricarica continuamente. Il dizionario viene fornito anche alle frasi lunghe.
- ASR e finestra di ripensamento di 1,5 s si sovrappongono. Più segmenti consecutivi sono riuniti prima dell'inserimento; il risultato preliminare non viene inserito se arriva una correzione.
- Sostituzioni esplicite di date, quantità e destinatari con la stessa preposizione sono applicate letteralmente. Ambiguità e testo tra virgolette restano al modello. MiniCPM riceve il blocco completo in una chiamata, con limiti di output effettivi e quattro thread, e viene riscaldato con una frase sintetica pubblica. La modalità codice conserva la revisione del modello.
- I controlli impediscono cambiamenti evidenti di nomi, numeri, giorni e domande in frasi senza ripensamenti. Sono conservativi e non provano l'equivalenza semantica universale.

## Confronto acustico locale

Undici registrazioni **sintetiche**, italiano e inglese, sono state provate intere e attraverso il segmentatore: 22 elaborazioni, 508 parole di riferimento complessive. La distanza ignora maiuscole e punteggiatura e normalizza alcune quantità. Non è un corpus di voce umana né una misura generale della comprensione.

| Configurazione | Errori di parola | WER normalizzato | Tempo ASR aggregato |
|---|---:|---:|---:|
| 1.4.0, base e tagli fissi | 33/508 | 6,50% | 26,58 s |
| 1.4.1, profilo adattivo e pause | 13/508 | 2,56% | 59,30 s |

L'errore diminuisce del 60,6% su questi campioni. Small rallenta la sola ASR dei blocchi lunghi: Equilibrata privilegia la comprensione; Massima velocità mantiene base. La velocità della dettatura migliora soprattutto sovrapponendo attesa e ASR ed evitando chiamate AI superflue; non viene dichiarata un'accelerazione universale del riconoscimento.

Le cinque frasi critiche mantengono date, destinatari, negazioni e quantità nel profilo adattivo. Restano errori su esitazioni e alcune parole italiane. Small Q5 è stato provato ma non adottato: non ha mostrato un vantaggio sufficiente di velocità.

## Verifiche

La suite eseguibile locale supera 123 controlli logici: preparazione anticipata, inserimento singolo, tre correzioni successive, cancellazione e azzeramento dei buffer. Con Ollama reale: 133 controlli superati. Cinque sostituzioni esplicite di data/numero, anche con una negazione nella seconda frase, richiedono meno di un millisecondo dopo ASR, rispetto a circa 0,5–4,1 s nel confronto precedente del rewriter. Avvio a freddo e correzioni ambigue possono ancora richiedere vari secondi.

I tre eseguibili 1.4.1 compilano. Il DLL del nuovo harness di benchmark completo in `Tests/bin/package-checks` è bloccato localmente dal criterio Windows Code Integrity (0x800711C7, eventi 3033/3077). Nessun criterio di sicurezza è modificato e nessuna eccezione è aggiunta: quel benchmark locale non è dichiarato riuscito. La suite eseguibile contiene lo stesso codice funzionale e i nuovi controlli della coda; l'harness aggiunge una misura end-to-end ancora da eseguire.

Anche `engines/WhisperWorker/WhisperWorker.exe` del pacchetto finale è rifiutato da Windows (errore 4551). I confronti acustici usano una build di sviluppo del nuovo codice del riconoscitore; non dimostrano che il pacchetto finale possa essere avviato su questo PC. La precedente 1.4.0 resta installata e aperta. Nessun certificato di firma del codice è stato trovato nello store personale. Prima dell'aggiornamento operativo serve una firma attendibile o una distribuzione autorizzata dal gestore del criterio. Il codice e gli hash del pacchetto sono forniti per questa verifica.

Le prove da 61 minuti della 1.4.0 restano separate: 732 blocchi, nessuna perdita, p95 13,37 s con traduzione disattivata. Non sono una prova da un'ora della nuova selezione adattiva. I contenuti di prova sono inventati; audio personale e trascrizioni reali non sono esportati nei report.
