# Componenti di terze parti

VoceLocale è un’app personale indipendente; non è affiliata a Wispr Flow.

| Componente | Versione/modello | Licenza | Fonte |
|---|---|---|---|
| .NET e WPF | 10, self-contained Windows x64 | MIT e avvisi del runtime | https://github.com/dotnet/runtime |
| NAudio | 2.2.1 | MIT | https://github.com/naudio/NAudio |
| Whisper.net / Runtime | 1.9.1 | MIT | https://github.com/sandrohanea/whisper.net |
| whisper.cpp | runtime incluso in Whisper.net | MIT | https://github.com/ggml-org/whisper.cpp |
| Whisper small | multilingual | MIT | https://github.com/openai/whisper |
| LLamaSharp / Backend.Cpu | 0.27.0 | MIT | https://github.com/SciSharp/LLamaSharp |
| llama.cpp | runtime incluso in LLamaSharp | MIT | https://github.com/ggml-org/llama.cpp |
| MiniCPM5-2B-GGUF | Q4_K_M | Apache-2.0 | https://huggingface.co/openbmb/MiniCPM5-2B-GGUF |
| CommunityToolkit.HighPerformance | 8.4.2 | MIT | https://github.com/CommunityToolkit/dotnet |
| System.Linq.Async / System.Interactive.Async | 7.0.0 | MIT | https://github.com/dotnet/reactive |

Le dipendenze transitive e gli hash dei pacchetti sono elencati nei `packages.lock.json`. I testi di licenza e gli avvisi sono nella cartella `licenses`. I modelli sono distribuiti senza modifiche; nessun servizio cloud dei fornitori viene usato per elaborare dettature.
