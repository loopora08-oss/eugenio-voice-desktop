# Eugenio pixel — correzione del concept

Stato: sorgenti corretti e compilazione/publish dell'app riusciti. **Avvio della preview.9 bloccato da Windows; risultato visivo finale non verificato nell'app.** Non è una consegna commerciale approvata.

## Prima

La preview.8 sostituiva le scie del concept con linee sottili e una lampada disegnata con pochi segmenti. Nel Notetaker c'era una colonna quasi vuota anziché la navigazione completa. Ricerca sempre visibile e struttura del documento non corrispondevano alla proposta approvata. L'utente ha rifiutato questo risultato; non viene presentato come versione conclusa.

## Dopo nei sorgenti

- Asset originale della lampada ricavato con ImageGen dal concept approvato, con trasparenza verificata, contorno e ombreggiature pixel più dettagliati. Riutilizzato da pagine, Notetaker, badge e avviso di stato.
- Scie blu in più fasce e quattro toni, con percorsi ondulati, pixel e particelle statici. Margini riservati, decorazioni senza focus o intercettazione dei clic. Nessun timer per animare la cornice.
- Componente laterale condiviso con Dettatura, Notetaker, Dizionario, Stile e Impostazioni; icone a pixel, selezione blu, lampada di 196×124 e firma locale. Le voci del Notetaker richiamano le pagine reali della finestra principale senza chiudere il documento.
- Titolo del documento senza cornice, data del documento, schede sottolineate in blu, pulsante principale blu, trascrizione dentro un pannello con barra degli strumenti. Ricerca richiamabile con pulsante o Ctrl+F. Nessun contenuto fittizio inserito nella sessione reale.
- Dimensioni iniziali adattate all'area di lavoro disponibile; rimane da verificare visivamente anche la finestra minima. Niente nuove librerie, API o servizi a pagamento. La richiesta estetica dell'utente prevale sui divieti decorativi upstream di Taste; la verifica resta nativa WPF.
- La lampada del badge è condivisa con il resto dell'app; la preferenza Windows di riduzione delle animazioni viene rispettata dal movimento del badge. Questo ramo non è ancora collaudato in esecuzione.

## Verifiche e blocco riproducibile

Publish self-contained dell'app preview.9 riuscito con `RestoreLockedMode=true` e avvisi trattati come errori. Controllo della struttura del repository e `git diff --check` riusciti. PNG RGBA 1585×992 con alpha tra 0 e 255. Queste prove non attestano resa, navigazione o accessibilità della nuova UI.

Il tentativo di avviare `.artifacts/Eugenio-pixel-preview.9/VoceLocale.exe` termina con `FileLoadException`, codice `0x800711C7`, sul caricamento di `VoceLocale.dll`. Gli eventi Code Integrity 3077 richiedono livello di firma Enterprise per il criterio `{0283ac0f-fff1-49ae-ada1-8a933130cad6}`. Nessun certificato di firma codice valido con chiave privata disponibile negli archivi personali consultati. Evidenza e hash esatto nella cartella `.artifacts/pixel-approval`.

Non sono state cambiate protezioni o criteri Windows. Non si è tentato di eseguire la DLL bloccata altrove, con un altro runtime o dopo una ricompilazione per eludere il controllo. Serve firma attendibile o autorizzazione dell'amministratore responsabile del criterio sul pacchetto concreto. Il sorgente può essere revisionato prima dell'approvazione.

### Diagnosi aggiornata dopo conferma del PC personale

La lettura del registro riporta `VerifiedAndReputablePolicyState=1`. Il GUID del blocco coincide con il criterio di applicazione Smart App Control documentato nell'analisi del BSI. È quindi Smart App Control attivo sul PC personale; la dicitura Enterprise nell'evento non dimostra una gestione aziendale. L'inventario dettagliato tramite CiTool ha restituito accesso negato e non è stato ripetuto con privilegi elevati.

Correzione alla generica indicazione precedente di approvazione IT: Microsoft dichiara che Smart App Control non offre un'eccezione per la singola app. Non basta autorizzare manualmente l'hash o creare un certificato autofirmato. Il percorso compatibile con la protezione mantenuta attiva è una firma da un emittente attendibile, verificata poi sul pacchetto concreto. Nessuna impostazione Windows modificata.

Fonti consultate il 27 ottobre 2026: [Microsoft FAQ](https://support.microsoft.com/en-us/windows/security/threat-malware-protection/smart-app-control-frequently-asked-questions), [firma per Smart App Control](https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/code-signing-for-smart-app-control), [analisi BSI, identificativi dei criteri](https://www.bsi.bund.de/SharedDocs/Downloads/EN/BSI/Publications/Studies/Smart_App_Control/Studie_Smart_App_Control.pdf?__blob=publicationFile&v=2).

Opzioni di firma studiate, senza iscrizioni, invii o acquisti: SignPath Foundation offre firma gratuita per progetti open source ammessi, con requisiti di licenza, release e revisione; l'idoneità di Eugenio non è stata approvata. Azure Artifact Signing Basic costa 9,99 USD/mese per 5000 firme, con eventuali imposte e cambio; Public Trust accetta organizzazioni UE idonee, ma la convalida del singolo sviluppatore è attualmente limitata a USA e Canada. PC personale non equivale automaticamente a identità organizzativa idonea. Fonti: [SignPath e condizioni](https://signpath.org/terms.html), [prezzi Azure](https://learn.microsoft.com/en-us/azure/artifact-signing/how-to-change-sku), [requisiti geografici](https://learn.microsoft.com/it-it/azure/artifact-signing/quickstart). Nessuna attivazione di fatturazione, pubblicazione del repository o trasmissione di codice a questi servizi autorizzata o effettuata.

## Cosa manca prima di dichiarare conclusa l'applicazione

Avvio autorizzato; confronto di screenshot reali con il concept; verifica della lampada e delle scie a finestra normale e minima; navigazione di tutte le pagine; Ctrl+F dal riepilogo; salvataggio/riapertura; errori senza consenso e senza input; controlli del badge e dell'opzione movimento ridotto; nuova regressione dei flussi audio. La versione .8 ancora aperta non è la nuova versione.

Review tecnica: nessuna modifica a HTTP/API pubbliche, autenticazione, database, indici, transazioni, cache, code esterne o contratti del riconoscimento. Il cambiamento riguarda rendering, navigazione, visibilità della ricerca e dimensionamento. Annullamento e limiti dei servizi preesistenti restano nel codice. Nessuna nuova persistenza di audio o contenuti. La compilazione non sostituisce la verifica delle interazioni o della qualità dei modelli.
