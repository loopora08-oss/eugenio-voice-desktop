# Misure della versione 1.3.11

27 settembre 2026, Windows x64, Intel Core i7-14650HX, inferenza CPU tramite Whisper.net 1.9.1. Audio esclusivamente sintetico; nessuna registrazione personale nei dati di prova o nei log. Le prove sono state eseguite in sequenza, con l'app chiusa per evitare un'altra inferenza del comando vocale.

| Audio | Whisper small standard | Whisper small veloce |
| --- | ---: | ---: |
| Italiano: correzione martedì/giovedì e negazione | 13,693 s | 9,601 s |
| Inglese: correzione Tuesday/Thursday e negazione | 13,838 s | 8,884 s |

Riduzione osservata del tempo di trascrizione: circa 30–36%. Sono misure singole, non una garanzia di latenza quotidiana. Non comprendono la pausa di rilevamento, la finestra di correzione, MiniCPM o l'inserimento. MiniCPM impiega altri secondi e il suo tempo varia con lo stato di Ollama. La modalità Standard mantiene il contesto completo come alternativa in caso di regressioni.

Il contesto veloce è proporzionato alla durata audio, con un margine di un secondo, minimo 256 frame e massimo 1500. La riduzione del contesto è una funzione sperimentale di Whisper.net: queste due prove non dimostrano pari accuratezza su tutte le voci, lingue e durate. Whisper base usato per dettare ha sbagliato la data italiana di prova; per questo small resta il modello di dettatura quando disponibile.

## Comando vocale

Whisper base, lingua italiana, prompt «Eugenio.», temperatura fissa e contesto minimo 512 frame. I tre positivi sintetici («Eugenio», «Eugenio, scrivi una frase di prova», «Ehi Eugenio») e i tre negativi (frase ordinaria e due menzioni di «genio») passano. Tempo del motore: primo caso 1,117 s, successivi 0,549–0,625 s. Questi tempi escludono la cattura: una pausa di 400 ms chiude il segmento, oppure una finestra viene controllata dopo due secondi di voce continua.

Prima del suggerimento, base ha trascritto il nome come «Il genio», «E u genio», «E io, genio» nei tre casi sintetici. Non sono accettati alias come «il genio» o «è un genio». È accettato il saluto breve «E Eugenio» prodotto per «Ehi Eugenio», oltre a ehi/hey/eh.

La cattura continua durante il controllo. Le finestre di due secondi hanno 500 ms di sovrapposizione. La coda conserva al massimo quattro finestre in attesa; se l'inferenza non tiene il passo, viene eliminata la più vecchia per evitare un'attivazione molto tardiva. Un buffer circolare di massimo due minuti permette di trasferire soltanto l'audio successivo alla finestra riconosciuta, senza duplicarlo. Tutti questi buffer vengono azzerati all'arresto.

Questi sei esempi non certificano il requisito di 95% su 100 pronunce né i falsi risvegli su otto ore. Non sono ancora una misura della voce reale dell'utente. L'avvio manuale dal pulsante microfono o dalla scorciatoia resta disponibile.
