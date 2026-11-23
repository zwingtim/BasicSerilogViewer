# Serilog Viewer

Kleine WPF-App (.NET 8) zum schnellen Ansehen von Serilog-Logdateien im Format

```
2026-11-19 02:01:54.077 +01:00 [Information] [My.Source.Context] Nachricht
```

## Bedienung

| Aktion | Wie |
|---|---|
| Log laden | Datei(en) ins Fenster ziehen, **Strg+O**, oder Text kopieren und **Strg+V** |
| Nach Caller filtern | Links in der Liste klicken, **Strg+Klick** für mehrere |
| Level ein-/ausblenden | Checkboxen oben (z. B. Debug aus) |
| Suchen | **Strg+F**, sucht in Nachricht, Caller und Stacktrace; **Esc** leert |
| Details / Stacktrace | Zeile anklicken → unten |
| Nur diesen Caller | Rechtsklick auf eine Zeile |

Mehrere Dateien auf einmal werden chronologisch zusammengeführt.
Die Datei kann gelesen werden, während die Anwendung noch hineinschreibt.

## Aufbau

- `SerilogParser.cs` – liest das Textformat; Zeilen ohne Zeitstempel (Stacktraces) gehören zum vorherigen Eintrag.
  Den Regex hier anpassen, falls sich das `outputTemplate` ändert.
- `LogEntry.cs` – ein Eintrag
- `Models.cs` – Caller-Liste, Level-Filter, Farben
- `MainWindow.xaml(.cs)` – Oberfläche
