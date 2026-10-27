# Ricerca nel Notetaker — prima / dopo

## Prima

Per ritrovare un passaggio era necessario scorrere la trascrizione. Nuovi blocchi portavano la vista in fondo. Nessuna ricerca nel documento riaperto.

## Dopo

Campo di ricerca nel documento aperto, originale e inglese, confronto senza distinzione di maiuscole o accenti. Risultati contati per blocco, con testo completo e tempi conservati. Navigazione circolare, Ctrl+F, Invio/Maiusc+Invio, Esc e pulsante Cancella. Nessuna selezione residua quando non ci sono risultati. I nuovi blocchi e le traduzioni ricalcolano i risultati senza filtrare o modificare il documento.

## Indurimento

Massimo 200 caratteri, confronto letterale senza regex, nessuna query nei log/salvataggi/modelli. Debounce di 180 ms, snapshot delle entry sul dispatcher, ricerca su worker annullabile e numero di generazione contro risultati obsoleti. Annullamento alla chiusura; messaggio operativo in caso di errore, documento conservato. Nessun database, HTTP, indice persistente, cache, coda esterna o nuovo endpoint: autenticazione, transazioni e rate limit di rete non sono coinvolti. Le richieste locali obsolete vengono annullate; non viene creata una cronologia.

## Verifiche

Suite locale ampliata a **185 controlli passati, zero falliti**: 14 nuovi casi su accenti/maiuscole, traduzione, negazioni, vuoto/nessun risultato, query letterali e invisibili, navigazione, riapertura JSON, arrivo di nuovi blocchi, aggiornamento della traduzione, lunghezza, annullamento e conservazione del documento. Compilazione App/Tests con warning trattati come errori.

UI WPF eseguita in preview con documento sintetico di tre blocchi: riapertura tramite Apri appunti, ricerca `CAFFE` trova due blocchi contenenti «caffè», Invio seleziona il secondo e lo porta in vista, senza togliere gli altri dal documento. Stato vuoto osservato a 880×620. Il runtime di sviluppo deve essere disponibile per l'eseguibile framework-dependent; il pacchetto self-contained viene costruito separatamente. Nessun download di runtime o modello eseguito per la prova.

La ricerca riguarda solo la trascrizione corrente; ricerca negli appunti e tra file diversi non sono implementate. Inserimento live e aggiornamento delle traduzioni coperti dai test sul motore, non da una registrazione reale. Accessibilità con lettore schermo e stress su 100.000 blocchi richiedono ancora prove dedicate. L'obiettivo completo della roadmap resta aperto; questa funzione non dimostra parità con Wispr.
