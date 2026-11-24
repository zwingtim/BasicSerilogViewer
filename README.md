# Serilog Viewer

A small WPF app (.NET 10) for quickly viewing Serilog log files in the format

```
2026-11-19 02:01:54.077 +01:00 [Information] [My.Source.Context] Message
```

## Usage

| Action | How |
|---|---|
| Load a log | Drag file(s) into the window, press **Ctrl+O**, or copy text and press **Ctrl+V** |
| Filter by caller | Click in the list on the left, **Ctrl+Click** to select multiple |
| Show/hide levels | Checkboxes at the top (e.g. turn off Debug) |
| Search | **Ctrl+F** searches message, caller and stack trace; **Esc** clears |
| Details / stack trace | Click a row → shown below |
| Only this caller | Right-click a row |

Multiple files loaded at once are merged in chronological order.
A file can be read while the application is still writing to it.

## Structure

- `SerilogParser.cs` – parses the text format; lines without a timestamp (stack traces) belong to the previous entry.
  Adjust the regex here if the `outputTemplate` changes.
- `LogEntry.cs` – a single log entry
- `Models.cs` – caller list, level filter, colors
- `MainWindow.xaml(.cs)` – user interface
