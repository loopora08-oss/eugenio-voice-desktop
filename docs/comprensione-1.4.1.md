# Comprensione e velocità — Eugenio 1.4.1

## Comportamento

- Il meeting cerca una pausa naturale dopo sei secondi e taglia forzatamente a dieci; mantiene il secondo di sovrapposizione quando serve. Una pausa di 450 ms conferma i blocchi sufficientemente lunghi.
- Equilibrata usa base fino a sei secondi e small per blocchi italiani più lunghi. Dopo un risultato inglese il blocco successivo può usare base, continuando a riconoscere automaticamente la lingua. Sono disponibili velocità e precisione, senza traduzione automatica.
- Il nuovo worker nel sorgente conserva al massimo due modelli verificati e fornisce il dizionario anche alle frasi lunghe. Queste due ottimizzazioni richiedono il worker 1.4.1: il pacchetto compatibile installato su questo PC conserva il worker 1.4.0, senza modificarlo.
- ASR e finestra di ripensamento di 1,5 s si sovrappongono. Più segmenti consecutivi sono riuniti prima dell'inserimento; il risultato preliminare non viene inserito se arriva una correzione.
- Se riprendi a parlare prima della conferma, l'inserimento attende che il nuovo segmento sia completato. Quantità negative o espresse con meno/minus restano al modello: il percorso immediato non sostituisce soltanto le cifre lasciando un segno errato.
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

La suite eseguibile locale supera 123 controlli logici: preparazione anticipata, inserimento singolo, tre correzioni successive, cancellazione e azzeramento dei buffer. Con Ollama reale: 133 controlli superati. Tre ulteriori controlli su ripresa del parlato e quantità negative portano la suite logica a 126, superati dalla CI Windows. Cinque sostituzioni esplicite di data/numero, anche con una negazione nella seconda frase, richiedono meno di un millisecondo dopo ASR, rispetto a circa 0,5–4,1 s nel confronto precedente del rewriter. Avvio a freddo e correzioni ambigue possono ancora richiedere vari secondi.

I tre eseguibili 1.4.1 compilano. Il DLL del nuovo harness di benchmark completo in `Tests/bin/package-checks` è bloccato localmente dal criterio Windows Code Integrity (0x800711C7, eventi 3033/3077). Nessun criterio di sicurezza è modificato e nessuna eccezione è aggiunta: quel benchmark locale non è dichiarato riuscito. La suite eseguibile contiene lo stesso codice funzionale e i nuovi controlli della coda; l'harness aggiunge una misura end-to-end ancora da eseguire.

Il nuovo `engines/WhisperWorker/WhisperWorker.exe` 1.4.1 è rifiutato da Windows (errore 4551). Nessun certificato di firma del codice è stato trovato nello store personale. Per quel componente serve una firma attendibile o una distribuzione autorizzata dal gestore del criterio. Il pacchetto compatibile usa invece l'interfaccia e la gestione dei blocchi 1.4.1 insieme al worker 1.4.0 già funzionante, verificato identico byte per byte. Nessun criterio o controllo di sicurezza è modificato. L'app compatibile è installata e avviata sul desktop; le note esistenti sono state ripristinate in memoria.

La combinazione compatibile supera dieci prove Whisper locali (cinque frasi critiche sintetiche, intere e a blocchi): zero errori su 108 parole di riferimento, tempo ASR aggregato 12,937 s. Report in `verifiche-1.4.1/compatibile-critici.json`. Questo verifica l'inferenza effettiva con il motore installato; non misura la latenza del microfono né la precisione su voce umana.

La verifica dall'interfaccia aggiornata apre il microfono, produce otto blocchi in 38 secondi e completa l'arresto con microfono spento. Una frase sintetica riprodotta dagli altoparlanti non compare nei risultati: la prova conferma il funzionamento della cattura e della coda, ma non l'accuratezza del percorso altoparlanti/microfono o della voce dell'utente. Il contenuto della prova microfono resta esclusivamente in memoria, senza esportazione.

La build Windows della revisione `c3ee88b` è riuscita su GitHub: [run 36324739249](https://github.com/loopora08-oss/eugenio-voice-desktop/actions/runs/36324739249). Compila i tre eseguibili e il nuovo harness, ed esegue i 126 controlli logici. Non usa il microfono né i modelli installati sul PC e non sostituisce il requisito di firma locale.

Le prove da 61 minuti della 1.4.0 restano separate: 732 blocchi, nessuna perdita, p95 13,37 s con traduzione disattivata. Non sono una prova da un'ora della nuova selezione adattiva. I contenuti di prova sono inventati; audio personale e trascrizioni reali non sono esportati nei report.
