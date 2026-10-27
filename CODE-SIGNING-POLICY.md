# Code signing policy

**Stato: preparazione della candidatura SignPath Foundation. Non ancora ammesso, nessuna firma di release emessa o verificata.** Non viene dichiarata una collaborazione già attiva.

Se ammesso, il programma proposto è: “Free code signing provided by SignPath.io, certificate by SignPath Foundation”. È gratuito per i progetti ammessi; non è un diritto automatico alla firma.

## Perimetro e responsabilità

Il codice proprio è MIT. La firma riguarda esclusivamente i nove percorsi di `signing/owned-binaries.json`, costruiti dai sorgenti Eugenio. Runtime e librerie di terzi sono distribuiti con le loro licenze e firme, senza rifirmarli come software proprio.

Il responsabile del repository è [loopora08-oss](https://github.com/loopora08-oss), che deve confermare i ruoli di autore, revisore e approvatore e l'attivazione MFA su GitHub e SignPath prima della richiesta operativa. Questi requisiti non risultano verificati. I contributi esterni richiedono revisione; ogni firma di release richiede approvazione umana. La firma non attesta accuratezza dei modelli o adeguatezza commerciale.

## Build e consegna

Il workflow manuale `sign-windows.yml` è limitato a `main` e rimane disattivato finché `SIGNPATH_ENABLED` non è impostata esplicitamente dopo l'ammissione. Usa runner GitHub, revisioni puntuali delle azioni, test prima della firma e un elenco chiuso dei file. L'ambiente `signpath-release` deve essere configurato con approvazione del responsabile. Non vengono firmati binari arbitrari caricati dal PC.

Dopo il ritorno dal servizio si verificano validità delle firme, editore atteso, metadati e identità dei file estranei alla firma. Il manifesto SHA-256 viene rigenerato soltanto dopo quei controlli. Nessuna pubblicazione automatica o aggiramento di Smart App Control è previsto.

Per dati e rete vedere [Privacy](PRIVACY.md). La richiesta di candidatura e l'account SignPath richiedono dati del manutentore separati dai dati dell'app. Requisiti e decisione di ammissione restano sotto il controllo di [SignPath Foundation](https://signpath.org/terms.html).
