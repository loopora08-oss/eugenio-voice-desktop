# Contribuire a Eugenio

Eugenio è un progetto indipendente, senza affiliazione con Wispr Flow. Il codice del progetto è MIT; dipendenze, modelli e relativi avvisi conservano le proprie licenze. Non inviare codice proprietario recuperato da concorrenti o dati di clienti.

Richiesti Windows x64, .NET SDK indicato in global.json e PowerShell 7. Da un checkout:

```powershell
./scripts/Test-Repository.ps1
dotnet build Tests/Tests.csproj -c Release -p:RestoreLockedMode=true -warnaserror
dotnet Tests/bin/Release/net10.0-windows/win-x64/Tests.dll
./build.ps1 -Destination ./publish/App
```

I test di logica non richiedono audio personale, un microfono, account o modelli. La build non include i modelli e non configura avvio automatico o policy Windows. Per provare inferenza e interfaccia seguire il README, separando test sintetici da voce reale. Se una build non firmata è bloccata, non aggirare il controllo: servono firma attendibile o approvazione IT.

Prima di una modifica leggere la roadmap: il prodotto resta in costruzione e non dichiara parità di qualità con Wispr. Ogni funzione ha criteri verificabili, due casi di errore, annullamento, budget risorse e recupero. Nessuna telemetria di contenuti; audio in memoria, azzeramento dei buffer e nessun invio automatico di messaggi.

Le pull request descrivono prima/dopo, test realmente eseguiti, dipendenze/licenze, costi e limiti. Nei problemi riproducibili non allegare conversazioni, impostazioni personali, token o registrazioni senza diritto alla condivisione. Preferire una frase sintetica e diagnostica con soli tempi/contatori.

La revisione di una release comprende privacy, sicurezza, licenze dei pesi, export, installer/firma, tastiera/UI e un test da checkout pulito. Non considerare un test con ASR simulato una prova di comprensione della voce reale.
