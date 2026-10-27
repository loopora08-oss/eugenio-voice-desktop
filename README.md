# Eugenio — dettatura e Notetaker

**Tema pixel in revisione, preview.9:** lampada d'oro e scie blu, navigazione condivisa e layout riallineato al concept approvato. I sorgenti compilano; Windows ha bloccato la nuova DLL per mancanza di firma attendibile. La resa nell'app non è ancora verificata. [Prima/dopo, prove e sblocco necessario](docs/pixel-prima-dopo.md).

**Nuova interfaccia del 27 ottobre:** tema antracite, barra laterale con Dettatura/Dizionario/Stile/Impostazioni e Notetaker con tre schede a tutta larghezza: Appunti, Trascrizione, Riepilogo IA. Le sorgenti audio sono in un pannello espandibile. [Confronto prima/dopo e verifiche native](docs/ui-direction.md). `VoceLocale.exe --preview-design --meeting` apre l'anteprima isolata con microfono e scorciatoie disattivati, senza salvare preferenze personali. Il ridisegno non modifica i risultati dei modelli.

**Evoluzione open source in corso.** Codice del progetto sotto [licenza MIT](LICENSE), implementazione indipendente da Wispr Flow. La [roadmap verificabile](docs/roadmap-open-source.md) distingue requisiti pronti, mancanti e non provati. Repository pubblico e nuova release non sono ancora dichiarati disponibili. Windows x64; nessuna parità di accuratezza/velocità con Wispr certificata.

**Prima modifica:** Equilibrata e Velocità riconoscono una volta ciascun segmento e inviano il testo completo alla correzione finale; Precisione conserva il riconoscimento dell'audio concatenato. Il conteggio riconoscimenti/segmenti e il tempo del blocco sono visibili dopo l'inserimento, senza registrare contenuti. Il tempo del blocco non include tutta l'acquisizione e non equivale alla latenza fine voce → testo.

**Preview 1.5.0-preview.3:** Notetaker con microfono e audio PC opzionale, sorgenti separate e conferma del consenso prima di acquisire; non identifica i partecipanti. Nessuna sostituzione automatica del modello Ollama configurato. Controlli più conservativi sulle omissioni e avviso quando la riscrittura conserva l'originale. Compilazione locale e 171 controlli superati; dispositivi reali, confronto Wispr e pubblicazione rimangono da verificare. Dettagli e limiti: [rapporto prima/dopo](docs/avanzamento-1.5-preview.3.md).

**Preview 1.5.0-preview.4:** ricerca locale nella trascrizione della riunione aperta, anche riaperta da file. Cerca nell'originale e nell'inglese senza distinzione di maiuscole o accenti; mantiene i blocchi completi. `Ctrl+F` porta al campo, `Invio`/`Maiusc+Invio` passano tra i blocchi trovati, `Esc` cancella la ricerca. La query non viene salvata né inviata a modelli o servizi. Non cerca in altre riunioni o negli appunti manuali. [Prove e limiti](docs/ricerca-1.5-preview.4.md).

**Preview 1.5.0-preview.5:** export JSON/Markdown con marcatura del flusso assistito dall'IA e indicazione di revisione umana. I nuovi blocchi ASR del Notetaker registrano nome/hash del modello, versione Whisper.net, modalità decoder, lingua richiesta e timestamp UTC. I vecchi blocchi restano apribili senza inventare metadati. La provenienza di note e traduzioni non è ancora registrata; i metadati sono dichiarazioni dell'app, non firme. [Prove e limiti](docs/provenienza-1.5-preview.5.md).

**Preview 1.5.0-preview.6:** schede separate per appunti personali e riepilogo IA. Genera riepilogo usa trascrizione e appunti, anche senza audio; gli estratti non vengono più presentati come un riepilogo riuscito. Errori e annullamento conservano il riepilogo precedente. [Verifica reale del Notetaker e limiti dei modelli](docs/verifica-notetaker-2026-10-27.md).

Per compilare e contribuire: [CONTRIBUTING.md](CONTRIBUTING.md). La licenza del codice non modifica le licenze dei pesi: TranslateGemma ha termini propri; il vecchio tag MiniCPM personalizzato è un requisito locale da rendere portabile. Non sono richiesti servizi a pagamento. Hardware, energia e manutenzione restano costi.

App personale Windows x64 per dettare in italiano e inglese, correggere il parlato e inserire il risultato nel campo attivo. Whisper trascrive localmente; la correzione usa il MiniCPM installato in Ollama attraverso `127.0.0.1`. Audio e testo non vengono inviati a servizi cloud. Nessun account o Python richiesto.

**Versione 1.4.1:** badge mobile con mascotte animata, dettatura a blocchi, finestra dedicata **Eugenio Notetaker** e traduzione letterale in inglese. **Equilibrata** usa base per frasi brevi e inglese già rilevato, small per frasi italiane più lunghe; **Massima velocità** usa base e **Precisione** small con contesto completo. Nel meeting scegli profilo e lingua prima di avviare. L'audio cerca pause naturali e viene diviso entro dieci secondi, senza il limite di due minuti della dettatura. ASR e finestra di ripensamento si sovrappongono; sostituzioni esplicite semplici sono immediate, quelle ambigue restano affidate a MiniCPM. Prove e limiti in [docs/comprensione-1.4.1.md](docs/comprensione-1.4.1.md) e [prove precedenti](docs/meeting-1.4.0.md). I modelli possono commettere errori.

## Meeting e traduzione

La precedente consegna compatibile combinava app 1.4.1 e WhisperWorker 1.4.0. La preview 1.5 compila e include il nuovo worker: l'inferenza è stata eseguita su questo PC con clip sintetiche. Questa prova non assicura l'esecuzione su altri PC né sostituisce la verifica della firma e delle policy aziendali.

Apri **Eugenio Notetaker** dal pulsante nelle impostazioni o dal menu dell'area di notifica. Scegli il microfono, abilita eventualmente **Audio PC**, informa i partecipanti e conferma il consenso prima di premere **Avvia meeting**. L'audio PC usa l'uscita predefinita: con gli altoparlanti il microfono può ricatturarlo; sono consigliate cuffie. Le etichette indicano la sorgente, senza attribuire identità ai parlanti. **Pausa** sospende la cattura, **Riprendi** la riattiva e **Termina** completa i blocchi in coda. Il cronometro include le pause. Un solo meeting o dettato usa il microfono alla volta. Il Notetaker conserva il testo nella propria finestra e non scrive in altre applicazioni.

Scrivi nella scheda **Personali**, poi usa **Genera riepilogo**: l'IA legge appunti e trascrizione senza modificare gli appunti personali. **Annulla riepilogo** ferma la richiesta conservando la versione precedente. **Riepilogo automatico** genera durante il meeting e alla fine; il modello si sceglie nelle impostazioni, separatamente dalla dettatura. Il vecchio modello personalizzato non è una scelta di qualità già approvata. Un output non validato lascia intatta la versione precedente e mostra un errore operativo; la trascrizione rimane disponibile. I controlli non provano equivalenza semantica universale. Nei sorgenti è anche prevista la sospensione automatica quando si modifica manualmente il riepilogo; questa ulteriore modifica richiede verifica runtime dopo il blocco Windows descritto nel rapporto.

**Traduci letteralmente in inglese** usa il modello locale `translategemma:4b`. Va installato una volta con `ollama pull translategemma:4b` (circa 3,3 GB), poi funziona senza Internet. La traduzione conserva anche i ripensamenti: «cinquanta, scusa, quindici» mantiene entrambi i valori; non applica la riscrittura della dettatura. Il testo inglese già riconosciuto viene mantenuto. Un risultato non validato lascia l'originale e un avviso. Sulla CPU l'inglese può arrivare in ritardo; il contatore mostra la sua coda separata. **Termina** libera il microfono mentre le traduzioni residue continuano.

**Salva** crea un file `.eugenio.json` riapribile o un'esportazione Markdown, contenenti appunti, originale, inglese già disponibile e timestamp; non contengono audio. Se salvi prima che l'inglese sia completato, salva di nuovo per includerlo. La riapertura ripristina anche il punto raggiunto nella generazione degli appunti. **Nuovo** e l'apertura di altri appunti mantengono il documento precedente in un'altra finestra. Chiudere la finestra la nasconde: puoi riaprirla dal menu; un meeting attivo continua e il badge resta rosso. **Esci** termina l'app: salva prima gli appunti che vuoi conservare. Non esistono cronologia o salvataggi automatici dei contenuti. Se scegli una cartella sincronizzata, la sincronizzazione del file è gestita dal relativo programma, per esempio OneDrive.

## Segnali visivi

Il badge rettangolare è trascinabile anche durante la registrazione; un clic senza trascinare ferma la sessione. Verde con alone pulsante significa microfono attivo e in attesa della parola di attivazione; le barrette turchesi restano ferme a riposo e si muovono quando il rilevatore sente la voce. «Ti sento» conferma il rilevamento, non il riconoscimento della parola Eugenio. Rosso con onde animate indica dettatura o meeting in corso; ambra indica elaborazione; un errore resta visibile sul badge anche se l’ascolto riparte. In pausa meeting il badge diventa grigio, le barrette si fermano e il genio torna nella lampada: il microfono è spento. Dopo Termina può riprendere l'ascolto della parola Eugenio, segnalato in verde. Per dettare serve il modello Whisper small locale; per riscrivere serve Ollama avviato con il MiniCPM già installato.

## Mascotte Eugenio

La sprite sheet `App/assets/EugenioMascot.png` contiene quattro pose originali pixel art: riposo nella lampada, risveglio, ascolto e ascolto alternativo. Nel badge WPF il genio resta nella lampada durante lo standby; quando inizia una registrazione emerge, poi alterna le pose mentre la sessione è attiva. Un lieve movimento verticale continua anche in standby e in stato di errore. La mascotte non usa rete e non legge il contenuto delle finestre.

## Distribuzione aziendale (Windows/Intune)

Le impostazioni dell’utente sono conservate in `%LOCALAPPDATA%\Eugenio`; l’app installata in `Program Files` non richiede permessi di scrittura. IT può predisporre copie verificate e in sola lettura dei modelli Whisper in `%ProgramData%\Eugenio\models`. Riscrittura, appunti e traduzione usano Ollama locale e i modelli predisposti da IT. Disinstallare l’app non cancella modelli né impostazioni.

Per creare il contenuto Win32 Intune, prima pubblica l’app self-contained (dalla cartella sorgente):

```powershell
./build.ps1 -Destination C:/build/Eugenio/payload
./deploy/intune/package.ps1 -PublishedDirectory C:/build/Eugenio/payload -OutputDirectory C:/build/Eugenio
```

Per includere modelli verificati nella distribuzione, aggiungi `-ModelsDirectory C:/models`; lo script di installazione controlla SHA-256 e dimensioni prima di copiarli in ProgramData. Poi avvolgi la cartella `Eugenio-Intune` con `IntuneWinAppUtil.exe` (contenuto: `Eugenio-Intune`, setup: `install.ps1`; ad esempio `IntuneWinAppUtil.exe -c C:/build/Eugenio/Eugenio-Intune -s install.ps1 -o C:/build/Eugenio/wrapped`). In Intune usa il comando `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install.ps1`, installazione **System**, architettura x64. Per rilevamento usa `detect.ps1`; per rimozione `uninstall.ps1`. Il codice prodotto non è firmato: per la distribuzione generale aziendale l’organizzazione deve firmare i binari con il proprio certificato attendibile o approvare la policy Windows prevista. L’app non configura l’avvio automatico.

Questa è una base tecnica per il deployment gestito, non una dichiarazione di certificazione, conformità GDPR o supporto universale agli editor. I criteri centrali per imporre impostazioni, aggiornamenti automatici firmati, firma del codice, SBOM formale e una matrice di compatibilità aziendale restano requisiti per una release commerciale successiva.

## Avvio e uso

1. Mantieni insieme **tutta la cartella VoceLocale**, incluse `engines` e `data/models`. Non spostare soltanto l’eseguibile. Usa una cartella locale scrivibile.
2. Avvia Ollama e verifica che `minicpm-assistente-it:latest` sia nell’elenco dei modelli. Apri `VoceLocale.exe`. Se Whisper manca, premi **Scarica modelli Whisper** oppure **Importa Whisper da disco**: small per dettare (487 MB) e base per il comando Eugenio (148 MB), verificati tramite SHA-256. MiniCPM non si scarica nell’app.
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
- Si salvano impostazioni e modelli in `%LOCALAPPDATA%\Eugenio`; i modelli predisposti da IT possono risiedere in `%ProgramData%\Eugenio\models`. Appunti e trascrizione del Notetaker vengono salvati soltanto con **Salva**, nella destinazione scelta.
- I due motori comunicano con l’interfaccia tramite pipe anonime del sistema, non tramite server o porte di rete.
- Whisper usa pipe locali; MiniCPM e TranslateGemma ricevono il testo corrente via HTTP esclusivamente su `127.0.0.1:11434`, il servizio Ollama sullo stesso PC. Dopo l'installazione dei modelli non è necessario Internet. L'app non apre nuovi server né invia contenuti a endpoint remoti.
- Per riconoscere il campo vengono letti solo identità, focus e proprietà di accessibilità; non vengono letti testo della pagina, conversazioni o contenuti circostanti.
- Le osservazioni di tastiera e mouse servono soltanto per scorciatoie, annullamento e rilevamento di attività. Non viene raccolta una sequenza di tasti.
- Gli appunti non sono usati nell’inserimento automatico. **Copia esplicitamente** è un’azione volontaria; l’app richiede a Windows di escluderla da cronologia e sincronizzazione, ma altri gestori di appunti possono comunque osservarla.
- La memoria è rilasciata a fine operazione e i buffer audio vengono azzerati. Non è una garanzia di cancellazione forense: file di paging, ibernazione o dump del sistema operativo sono esterni al controllo dell’app.
- Dopo l’inserimento, il testo è gestito dall’applicazione di destinazione e dalle sue impostazioni di privacy.

## Compatibilità e limiti

Windows x64, processore moderno e circa 8 GB di memoria libera consigliati. La macchina di riferimento ha Intel i7-14650HX e circa 32 GB di RAM. Nessuna GPU dedicata necessaria.

Campi password riconosciuti, campi in sola lettura e processi amministrativi vengono rifiutati. Alcuni editor personalizzati non espongono un campo accessibile compatibile: in quel caso il testo va recuperato manualmente. Il riconoscimento non garantisce compatibilità con ogni editor web o app. La selezione presente nel campo può essere sostituita dal normale comportamento di digitazione.

Le correzioni riguardano solo la registrazione corrente. Non sono inclusi comandi di modifica di testi precedenti, formattazione ricca, sincronizzazione, account o aggiornamenti automatici. La traduzione supportata è italiano → inglese; l'inglese resta invariato.

**Distribuzione non firmata:** durante lo sviluppo alcune esecuzioni sono state bloccate dalla policy Windows `VerifiedAndReputableDesktop` (`0x800711C7`, evento 3077). L’ultima esecuzione locale autorizzata dei motori della build finale è riuscita; nessuna policy è stata modificata. Questo non garantisce l’autorizzazione su altri PC o per future ricompilazioni. Se Windows blocca una build, occorre una firma attendibile o il percorso di approvazione dell’amministratore. Non è stato trovato un certificato di firma del codice valido con chiave privata nello store personale dell’utente. Consultare `VERIFICA.md` per le prove eseguite.

## Modelli e integrità

| Modello | File | Dimensione | SHA-256 |
|---|---|---:|---|
| Whisper multilingual small | `ggml-small.bin` | 487601967 byte | `1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b` |
| Whisper multilingual base | `ggml-base.bin` | 147951465 byte | `60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe` |
| MiniCPM personalizzato (Ollama) | `minicpm-assistente-it:latest` | gestito da Ollama | il digest Ollama si controlla in `ollama list` |
| TranslateGemma 4B (Ollama) | `translategemma:4b` | circa 3,3 GB | digest della versione provata: `c49d986b0764f5881c476eb21435bb62b7abc62347aab3d4a6071e811be510a1` |

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

La 1.3.10 conserva in memoria i 300 ms precedenti all’attività vocale per non tagliare l’inizio delle parole. I segmenti del risveglio sono più brevi e il passaggio all’avvio manuale annulla un riconoscimento in corso, evitando attese dietro ai controlli di risveglio. Il badge offre un pulsante microfono per avviare e fermare la dettatura nel campo selezionato; le impostazioni distinguono audio ricevuto da parola Eugenio riconosciuta. Il risveglio con Whisper resta da validare con la voce reale: il movimento delle barrette, da solo, non significa che sia iniziata la dettatura.
