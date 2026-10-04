# WissensChat

Avalonia 12 Desktop-App (Windows): PDF/DOCX importieren, semantisch durchsuchen, per RAG mit den Dokumenten chatten.
Alles lokal über Ollama, Daten in %LocalAppData%\WissensChat\wissen.db

## Voraussetzungen
1. .NET 10 SDK (kommt mit Visual Studio 2026)
2. Ollama für Windows, danach:
   ollama pull llama3.1:8b
   ollama pull bge-m3
3. Empfohlen: Umgebungsvariable OLLAMA_CONTEXT_LENGTH=8192 setzen und Ollama neu starten
   (sonst wird der Prompt mit den Quellen ggf. abgeschnitten).

## Start
WissensChat.slnx in Visual Studio öffnen, WissensChat.Desktop als Startprojekt, F5.
Modelle/URL: Core/Models.cs -> AppSettings. Bei Wechsel des Embedding-Modells Dokumente neu importieren.
