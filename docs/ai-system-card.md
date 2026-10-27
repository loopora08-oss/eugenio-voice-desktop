# Eugenio — scheda del sistema IA

Prodotto: Eugenio, alternativa indipendente open source per dettatura e appunti locali.
Versione di partenza: 1.4.1. Evoluzione in corso: 1.5.0-dev.
Aggiornamento: 26 ottobre 2026. Ruolo previsto: fornitore del software; modelli di terzi separati.

## 1. Triage

| Domanda | Risposta e motivazione |
|---|---|
| Interazione con persone | Sì: dettatura, trascrizione e riepiloghi tramite IA, da dichiarare nell'interfaccia |
| Generazione/manipolazione sintetica | Sì: riconoscimento e riscrittura testo; provenienza negli export e metadati, limiti dell'inserimento desktop da documentare |
| Emozioni o categorizzazione biometrica | No: nessun riconoscimento emozionale o identificazione biometrica prevista |
| Deepfake/pubblicazione interesse pubblico | No: non è una funzione del prodotto; l'utente controlla il successivo uso del testo |
| Allegato III | No per l'uso previsto: strumento di produttività, non selezione/valutazione persone o decisioni su servizi essenziali |
| Pratiche vietate | No: nessuna generazione di immagini, sorveglianza o manipolazione di persone |

Esito: go; regole R1/R2/R3/R4/R7/R8/R9/R10/R11/R12. Autonomia minima: catena fissa scelta dal codice; il modello produce testo e non decide strumenti, destinatari o invii. Digitazione solo nel campo scelto dall'utente con verifiche, annullamento e recupero. Mai premere Invio automaticamente.

## 2. Uso previsto

Windows x64, utente locale. Dettatura italiano/inglese, punteggiatura e correzioni fedeli; note di riunioni con consenso dei partecipanti. Elaborazione sul PC dopo l'acquisizione iniziale dei modelli. Nessun account cloud o invio a provider remoto previsto. Audio in memoria e azzeramento best effort; nessuna persistenza audio implicita.

## 3. Modelli, provenienza e limiti

Whisper multilingual base/small tramite Whisper.net 1.9.1, hash nei sorgenti. Riscrittura/appunti/traduzione tramite Ollama esclusivamente loopback. I vecchi tag personalizzati non rendono il prodotto riproducibile: identificazione puntuale del modello e onboarding portabile sono requisiti aperti. Licenze di codice e pesi sono distinte e vanno verificate per ciascun modello distribuito. Il modello non deve essere dichiarato open source soltanto perche eseguibile in locale.

Errori possibili: parole inventate, numeri e negazioni errati, nomi propri, accenti, rumore, cambi lingua, attribuzioni speaker e sintesi non fedeli. Controlli deterministici riducono il rischio ma non provano equivalenza semantica. Output non validato conserva l'originale per revisione esplicita; indisponibilità del motore non viene mascherata con una risposta simulata.

## 4. Privacy e tracciabilità

Niente testo/audio personale nei log diagnostici. Le misure di performance devono usare ID casuali, tempi monotoni, durate e contatori, senza hash di trascrizioni facilmente riconoscibili. Nessun apprendimento automatico o lettura del contesto di altre finestre senza scelta esplicita. Export a richiesta, mai audio; provenienza generazione da conservare nell'artefatto finale. Per la digitazione in applicazioni terze l'app non controlla metadati e salvataggio del destinatario: il limite deve restare visibile, senza introdurre caratteri invisibili che danneggiano il testo.

## 5. Stato verifiche

Baseline del 26 ottobre: compilazione Core/Tests .NET 10.0.401, 126 verifiche di logica passate. Non certifica accuratezza su voce reale, prestazioni, runtime installato o prontezza commerciale. Confronto Wispr: percorsi client statici, backend sconosciuto; nessun codice proprietario copiato in Eugenio. Evidenze della nuova implementazione e incognite nel registro docs/roadmap-open-source.md e nei rapporti di consegna per versione.

## 6. Revisione umana e assistenza

La persona può annullare, rivedere e recuperare esplicitamente il testo. Il programma non invia messaggi e non prenota. Contributi e problemi riproducibili tramite il repository del progetto, senza allegare audio/conversazioni private. Firma Windows, test dispositivo reale, audit dipendenze/licenze e pubblicazione revisionata restano gate di rilascio.
