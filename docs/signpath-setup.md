# SignPath — preparazione verificabile

## Prima / dopo

Prima: repository privato e firma non configurata; Smart App Control blocca il nuovo eseguibile non firmato. Dopo: politica di firma, privacy, metadati prodotto Eugenio, elenco dei nove binari, configurazione XML, workflow manuale e verificatore locale pronti per revisione. **Non equivale ad ammissione o firma attiva.**

Repository da rendere pubblico solo dopo approvazione esplicita: https://github.com/loopora08-oss/eugenio-voice-desktop. GitHub ha confermato che è ancora privato; la nuova evoluzione locale non è ancora pubblicata. Occorre prima esaminare anche la storia che diventerebbe pubblica. Il codice MIT non rende automaticamente pubblico il repository.

## Percorso proposto

1. Revisione sorgenti/storia, licenze, dati esclusi e credenziali; approvazione della pubblicazione concreta.
2. Pubblicazione sorgente e prima prerelease chiaramente non firmata, con limiti noti. Build su GitHub-hosted runner con test passati. Non dichiarare il collaudo locale che Windows ha impedito.
3. Invio della candidatura ufficiale con nome, contatto autorizzato, URL pubblici, descrizione e reputazione reale. Il progetto è agli inizi: non inventare adozione o approvazioni.
4. L'utente deve accettare le condizioni e il trattamento dei dati del manutentore. MFA, ruoli e accesso della GitHub App vanno configurati consapevolmente. Nessuna chiave deve essere incollata in chat.
5. Dopo ammissione: impostare i riferimenti SignPath e il segreto nell'ambiente `signpath-release`; scegliere una policy con approvazione umana. Attivare `SIGNPATH_ENABLED=true` soltanto allora.
6. Avviare il workflow manuale da `main`, verificare il risultato e solo dopo predisporre una release firmata. La firma dei nostri binari non risolve automaticamente eventuali blocchi di librerie upstream non firmate.

## Configurazione operativa richiesta dopo l'ammissione

Segreto: `SIGNPATH_API_TOKEN`, con permessi di sola richiesta firma sul progetto/policy necessari. Variabili: `SIGNPATH_ORGANIZATION_ID`, `SIGNPATH_PROJECT_SLUG`, `SIGNPATH_SIGNING_POLICY_SLUG`, `SIGNPATH_ARTIFACT_CONFIGURATION_SLUG`, `SIGNPATH_CERTIFICATE_SUBJECT`, `SIGNPATH_ENABLED`. Valori ancora assenti: non vengono inventati.

Importare `signing/artifact-configuration.xml` nel progetto SignPath; revisionare i vincoli dei metadati e l'elenco dei file. Il subject del certificato va copiato dal certificato effettivamente approvato. La verifica locale può leggere i metadati senza eseguire l'app: `./scripts/Test-SigningPackage.ps1 -Path <pacchetto>`. Dopo firma aggiungere `-RequireSignature -UnsignedPath <originale> -ExpectedSignerSubject <subject-esatto>`.

## Costi, prove e limiti

Prove locali del 27 ottobre: publish di app e due worker riuscito; i nove binari riportano `ProductName=Eugenio`, stessa ProductVersion e stessa FileVersion. Sei controlli del verificatore passati: originale coerente; rifiuto del non firmato presentato come firmato; editore mancante; DLL mancante; file estraneo modificato; file aggiunto. Nessuna firma valida reale disponibile per il caso positivo Authenticode. Scansione Gitleaks di 29 commit senza segreti rilevati; non è una garanzia su tutti i dati personali o sui diritti dei contenuti. Parser PowerShell e controlli repository passati. Il controllo YAML con PyYAML non è stato eseguito perché il modulo non è installato: validazione GitHub e workflow reale restano da effettuare. Nessuna esecuzione locale dell'app bloccata riproposta.

SignPath Foundation dichiara gratuità per OSS ammessi. La richiesta richiede release esistente, manutenzione e reputazione verificabile; può essere rifiutata e non c'è un tempo garantito. Il modulo richiede esplicitamente dati di reputazione: Eugenio al momento non ha adozione pubblica verificata. GitHub Actions può avere costi secondo visibilità e piano: nessuna esecuzione remota avviata in questa preparazione.

Fonti ufficiali consultate il 27 ottobre 2026: [programma](https://signpath.org/), [condizioni](https://signpath.org/terms.html), [modulo](https://signpath.org/apply), [integrazione GitHub](https://docs.signpath.io/trusted-build-systems/github), [configurazione artefatti](https://docs.signpath.io/artifact-configuration/), [esempio ufficiale GitHub](https://github.com/SignPath/demo-github-actions). L'azione ufficiale è fissata al commit `f6d04783b4569d051e0c80105fe66e82819d0092` del tag v3 verificato. XML e workflow richiedono ancora convalida del servizio e un'esecuzione reale autorizzata.
