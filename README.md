# VoceLocale

App personale Windows x64 per dettare in italiano e inglese, correggere il parlato e inserire il risultato nel campo attivo. Whisper trascrive localmente; la correzione usa il MiniCPM installato in Ollama attraverso `127.0.0.1`. Audio e testo non vengono inviati a servizi cloud. Nessun account o Python richiesto.

**Stato della consegna:** badge mobile con posizione persistente, mascotte pixel art animata, wake word «Eugenio», inserimento dei blocchi dopo le pause e stili/dizionario locali. La versione 1.3.9 è stata compilata e ha superato i test logici e MiniCPM su Windows; prove audio sintetiche italiane e inglesi verificano Whisper → MiniCPM, correzioni di date e negazioni; il microfono Intel del PC è stato aperto e misurato nella sessione Windows normale. Whisper e MiniCPM caricano, e l’inserimento Unicode è stato provato in una finestra di test. La prova finale con la voce dell’utente e negli editor delle chat resta necessaria. I modelli si scaricano o si importano dal pannello. Nessuna protezione di Windows è stata disattivata.

## Segnali visivi

Il badge rettangolare è trascinabile. Verde con alone pulsante significa microfono attivo e in attesa della parola di attivazione; le barrette turchesi restano ferme a riposo e si muovono quando il rilevatore sente la voce. «Ti sento» conferma il rilevamento, non il riconoscimento della parola Eugenio. Rosso con onde animate indica la dettatura in corso; ambra indica trascrizione o correzione; un errore resta visibile sul badge anche se l’ascolto riparte. Grigio indica che il microfono non è in ascolto o i motori vocali non sono pronti. Per dettare serve il modello Whisper small locale; per riscrivere serve Ollama avviato con il MiniCPM già installato.

## Mascotte Eugenio

La sprite sheet `App/assets/EugenioMascot.png` contiene quattro pose originali pixel art: riposo nella lampada, risveglio, ascolto e ascolto alternativo. Nel badge WPF il genio resta nella lampada durante lo standby; quando inizia una registrazione emerge, poi alterna le pose mentre la sessione è attiva. Un lieve movimento verticale continua anche in standby e in stato di errore. La mascotte non usa rete e non legge il contenuto delle finestre.

## Distribuzione aziendale (Windows/Intune)

Le impostazioni dell’utente sono conservate in `%LOCALAPPDATA%\Eugenio`; l’app installata in `Program Files` non richiede permessi di scrittura. IT può predisporre una copia condivisa e sola lettura del solo modello Whisper in `%ProgramData%\Eugenio\models`. La riscrittura usa un’installazione aziendale di Ollama e un tag MiniCPM locale predisposto da IT. Disinstallare l’app non cancella modelli né impostazioni.

Per creare il contenuto Win32 Intune, prima pubblica l’app self-contained (dalla cartella sorgente):

```powershell
./build.ps1 -Destination C:/build/Eugenio/payload
./deploy/intune/package.ps1 -PublishedDirectory C:/build/Eugenio/payload -OutputDirectory C:/build/Eugenio
```

Per includere modelli verificati nella distribuzione, aggiungi `-ModelsDirectory C:/models`; lo script di installazione controlla SHA-256 e dimensioni prima di copiarli in ProgramData. Poi avvolgi la cartella `Eugenio-Intune` con `IntuneWinAppUtil.exe` (contenuto: `Eugenio-Intune`, setup: `install.ps1`; ad esempio `IntuneWinAppUtil.exe -c C:/build/Eugenio/Eugenio-Intune -s install.ps1 -o C:/build/Eugenio/wrapped`). In Intune usa il comando `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install.ps1`, installazione **System**, architettura x64. Per rilevamento usa `detect.ps1`; per rimozione `uninstall.ps1`. Il codice prodotto non è firmato: per la distribuzione generale aziendale l’organizzazione deve firmare i binari con il proprio certificato attendibile o approvare la policy Windows prevista. L’app non configura l’avvio automatico.

Questa è una base tecnica per il deployment gestito, non una dichiarazione di certificazione, conformità GDPR o supporto universale agli editor. I criteri centrali per imporre impostazioni, aggiornamenti automatici firmati, firma del codice, SBOM formale e una matrice di compatibilità aziendale restano requisiti per una release commerciale successiva.

## Avvio e uso

1. Mantieni insieme **tutta la cartella VoceLocale**, incluse `engines` e `data/models`. Non spostare soltanto l’eseguibile. Usa una cartella locale scrivibile.
2. Avvia Ollama e verifica che `minicpm-assistente-it:latest` sia nell’elenco dei modelli. Apri `VoceLocale.exe`. Se Whisper manca, premi **Scarica Whisper small** oppure **Importa Whisper da disco** (487 MB). MiniCPM non si scarica nell’app.
3. Nelle impostazioni scegli microfono, lingua, stile, modello Ollama e parole del dizionario (una per riga), poi premi **Salva impostazioni**. Il badge resta nella posizione scelta anche al riavvio. Per rilevare «Eugenio», l’app controlla brevi segmenti vocali con Whisper multilingue, interamente sul PC.
4. Seleziona una casella di testo e dì **Eugenio**. Il badge cambia stato e la dettatura comincia; di’ il testo dopo la parola di attivazione.
5. Clicca il badge oppure premi la scorciatoia configurata per fermare; **Esc** annulla. Dopo due minuti la registrazione termina automaticamente.
6. Dopo una pausa, Whisper trascrive la frase e MiniCPM la ripulisce prima dell’inserimento. Il primo blocco parte dopo una finestra di correzione di 1,5 secondi, anche se non arriva una seconda frase; due frasi vicine vengono elaborate insieme per risolvere meglio ripensamenti come «martedì… no, giovedì». La voce pronunciata mentre Whisper riconosce «Eugenio» viene conservata nel buffer della stessa registrazione. Non viene premuto Invio. Se cambia la destinazione, il blocco viene trattenuto per il recupero esplicito.

Il badge resta sempre visibile sopra le altre finestre; trascinalo per riposizionarlo. Il pulsante con l’ingranaggio apre le impostazioni. Se Eugenio è già aperto, fare di nuovo doppio clic sull’icona del Desktop riapre le impostazioni; quando sono aperte compaiono anche nella barra delle applicazioni. Usa **Esci** nell’area di notifica per arrestare l’ascolto e scaricare i modelli dalla memoria. Non viene configurato alcun avvio automatico con Windows.

Se al primo avvio Ctrl+Alt+Spazio è già occupata, l’app prova **Ctrl+Alt+F9** e mostra il cambiamento. Fa sempre fede la scorciatoia visualizzata nella finestra. Le impostazioni già salvate non vengono sostituite automaticamente.

### Esempi

- «Ci vediamo martedì, no, giovedì alle tre.» → «Ci vediamo giovedì alle tre.»
- «Non voglio annullare la prenotazione.» deve mantenere la negazione.
- «Scrivi una mail a Luca.» resta una frase dettata: l’app non scrive autonomamente la mail e non invia messaggi.

Ogni blocco è inserito come singolo paragrafo: a capo e tabulazioni vengono sostituiti con spazi, per non inviare accidentalmente messaggi nelle chat. Puoi scegliere gli stili generale, messaggio, email o codice. Il dizionario suggerisce grafie di nomi e termini senza sovrascrivere parole chiaramente diverse. I modelli possono sbagliare parole, nomi e intenzioni: controlla il risultato prima di inviarlo.

## Privacy

- Audio PCM, trascrizione e risultato vengono gestiti in memoria; l’app non crea registrazioni, cronologia o log del contenuto.
- Si salvano soltanto impostazioni e modelli in `%LOCALAPPDATA%\Eugenio`; i modelli predisposti da IT possono risiedere in `%ProgramData%\Eugenio\models`.
- I due motori comunicano con l’interfaccia tramite pipe anonime del sistema, non tramite server o porte di rete.
- Il download esplicito dei modelli è l’unica funzione di rete implementata. L’inferenza non effettua richieste HTTP.
- Per riconoscere il campo vengono letti solo identità, focus e proprietà di accessibilità; non vengono letti testo della pagina, conversazioni o contenuti circostanti.
- Le osservazioni di tastiera e mouse servono soltanto per scorciatoie, annullamento e rilevamento di attività. Non viene raccolta una sequenza di tasti.
- Gli appunti non sono usati nell’inserimento automatico. **Copia esplicitamente** è un’azione volontaria; l’app richiede a Windows di escluderla da cronologia e sincronizzazione, ma altri gestori di appunti possono comunque osservarla.
- La memoria è rilasciata a fine operazione e i buffer audio vengono azzerati. Non è una garanzia di cancellazione forense: file di paging, ibernazione o dump del sistema operativo sono esterni al controllo dell’app.
- Dopo l’inserimento, il testo è gestito dall’applicazione di destinazione e dalle sue impostazioni di privacy.

## Compatibilità e limiti

Windows x64, processore moderno e circa 8 GB di memoria libera consigliati. La macchina di riferimento ha Intel i7-14650HX e circa 32 GB di RAM. Nessuna GPU dedicata necessaria.

Campi password riconosciuti, campi in sola lettura e processi amministrativi vengono rifiutati. Alcuni editor personalizzati non espongono un campo accessibile compatibile: in quel caso il testo va recuperato manualmente. Il riconoscimento non garantisce compatibilità con ogni editor web o app. La selezione presente nel campo può essere sostituita dal normale comportamento di digitazione.

Le correzioni riguardano solo la registrazione corrente. Non sono inclusi comandi di modifica di testi precedenti, traduzione, formattazione ricca, sincronizzazione, account o aggiornamenti automatici.

**Distribuzione non firmata:** durante lo sviluppo alcune esecuzioni sono state bloccate dalla policy Windows `VerifiedAndReputableDesktop` (`0x800711C7`, evento 3077). L’ultima esecuzione locale autorizzata dei motori della build finale è riuscita; nessuna policy è stata modificata. Questo non garantisce l’autorizzazione su altri PC o per future ricompilazioni. Se Windows blocca una build, occorre una firma attendibile o il percorso di approvazione dell’amministratore. Non è stato trovato un certificato di firma del codice valido con chiave privata nello store personale dell’utente. Consultare `VERIFICA.md` per le prove eseguite.

## Modelli e integrità

| Modello | File | Dimensione | SHA-256 |
|---|---|---:|---|
| Whisper multilingual small | `ggml-small.bin` | 487601967 byte | `1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b` |
| MiniCPM personalizzato (Ollama) | `minicpm-assistente-it:latest` | gestito da Ollama | il digest Ollama si controlla in `ollama list` |

Fonte Whisper: [repository whisper.cpp](https://huggingface.co/ggerganov/whisper.cpp). Eugenio non scarica né accede ai file interni dei modelli Ollama: richiede il nome del tag via API locale e invia soltanto la trascrizione corrente all’endpoint loopback. L’installazione di Ollama deve essere avviata localmente.

## Sviluppo e test

Richiede .NET SDK 10 su Windows. Pacchetti bloccati dai file `packages.lock.json`: NAudio 2.2.1, Whisper.net/Runtime 1.9.1, LLamaSharp/Backend.Cpu 0.27.0.

```powershell
./build.ps1
dotnet run --project Tests/Tests.csproj -c Release
```

`build.ps1 -Dotnet C:/percorso/dotnet.exe -Destination C:/percorso/VoceLocale` produce i tre eseguibili autonomi e lancia i test senza modelli. Il pacchetto risultante non richiede .NET installato. I modelli non vengono scaricati dalla build.

Per i test reali dei modelli e i benchmark, usando solo voci sintetiche Windows locali:

```powershell
./Tests/make-synthetic-audio.ps1 -OutputDirectory C:/percorso/work/audio-tests
dotnet run --project Tests/Tests.csproj -c Release -- C:/percorso/VoceLocale C:/percorso/work/audio-tests
```

Con Ollama avviato e il modello MiniCPM installato, `dotnet run --project Tests/Tests.csproj -c Release -- --ollama` prova l’API locale, correzioni italiane e inglesi, negazioni indipendenti e istruzioni dettate come testo. La 1.3.6 corregge ciascuna frase nel suo contesto e sceglie esempi nella lingua del dettato; un risultato anomalo viene trattenuto per il recupero manuale.

Il test dei modelli stampa esclusivamente i campioni sintetici che genera o riceve: non usarlo con registrazioni personali se non vuoi salvarne l’output nella console. Non usa il microfono.

`DesktopTests` verifica il codice di inserimento in una propria finestra sintetica separata. Richiede un desktop sbloccato e non va eseguito mentre stai digitando altrove. Non viene incluso nell’app distribuita.

Architettura: `App` gestisce interfaccia, registrazione, segmentazione VAD, inserimento e richieste Ollama locali; `Core` definisce protocollo e regole comuni; `WhisperWorker` isola il motore di trascrizione. Le richieste di correzione possono includere stile e suggerimenti di dizionario; audio e dettati restano temporanei in memoria. Il dettato viene passato all’API Ollama su loopback, non a un server remoto; gli output incompleti o insicuri vengono rifiutati. I file personali non entrano nei messaggi di errore.

Nei testi con più frasi, le correzioni esplicite vengono elaborate prima nel proprio contesto di frase; una correzione a inizio frase viene associata alla precedente. Il testo completo viene poi ripulito. Questo aumenta i tempi sui testi lunghi. Uscite incomplete o eccessivamente abbreviate vengono rifiutate e l’app offre la trascrizione originale per il recupero manuale. Questa protezione riduce gli errori evidenti, ma non dimostra automaticamente l’equivalenza semantica di ogni riscrittura.

Per ripetere soltanto la correzione su trascrizioni sintetiche già raccolte dal test, senza avviare il motore vocale: `dotnet run --project Tests/Tests.csproj -c Release -- C:/percorso/VoceLocale --transcripts C:/percorso/risultati-sintetici.txt`.

La 1.3.7 evita che eventi di focus duplicati o di applicazioni in background blocchino il campo selezionato. Controlla anche l’identità del campo durante la sessione, perché alcuni editor non notificano tutti i cambiamenti di focus. Conserva lo spazio tra blocchi consecutivi e distingue correzione fallita, destinazione cambiata e inserimento incompleto. Il recupero dopo un errore richiede un’azione esplicita. I 13 controlli desktop comprendono questi casi, password, campi di sola lettura e assenza di Invio.

La 1.3.8 misura il livello audio reale nel badge e nel pannello microfono. Il rilevamento usa l’energia del suono per frame, anche a volume basso, invece della percentuale di campioni oltre una soglia elevata. Whisper amplifica soltanto i blocchi che superano il filtro di attività, con guadagno limitato. Silenzio, rumore basso e impulsi brevi restano esclusi nei test sintetici; l’attivazione reale con la voce dell’utente va verificata sul microfono scelto.

La 1.3.9 avvia il microfono dopo la conclusione della preparazione dei modelli: nelle versioni precedenti l’avvio dell’ascolto poteva essere saltato al primo lancio, pur mostrando uno stato pronto. ‘In ascolto’ viene ora mostrato soltanto dopo l’apertura del microfono.
