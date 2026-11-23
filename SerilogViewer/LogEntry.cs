using System.Globalization;
using System.Text;

namespace SerilogViewer;

public enum LogLevel
{
    Verbose,
    Debug,
    Information,
    Warning,
    Error,
    Fatal,
    Unknown
}

/// <summary>Ein Serilog-Eintrag inkl. aller Folgezeilen (z. B. Stacktrace).</summary>
public sealed class LogEntry
{
    private const int MaxGridTextLength = 2000;

    public LogEntry(
        DateTimeOffset? timestamp,
        string timestampText,
        LogLevel level,
        string rawLevel,
        string sourceContext,
        string message,
        string? details,
        string file,
        int lineNumber)
    {
        Timestamp = timestamp;
        TimestampText = timestampText;
        Level = level;
        RawLevel = rawLevel;
        SourceContext = sourceContext;
        Message = message;
        Details = details;
        File = file;
        LineNumber = lineNumber;

        DisplayTime = timestamp?.ToString("dd.MM. HH:mm:ss.fff", CultureInfo.InvariantCulture) ?? timestampText;
        ShortSource = sourceContext.Length == 0 ? "" : SourceNames.Short(sourceContext);
        FirstLine = message.Length > MaxGridTextLength ? message[..MaxGridTextLength] + " …" : message;
        IsException = details is not null &&
                      (details.Contains("Exception", StringComparison.Ordinal) ||
                       details.Contains("\n   at ", StringComparison.Ordinal) ||
                       details.StartsWith("   at ", StringComparison.Ordinal));
    }

    public DateTimeOffset? Timestamp { get; }
    public string TimestampText { get; }
    public LogLevel Level { get; }
    public string RawLevel { get; }
    public string SourceContext { get; }
    public string Message { get; }

    /// <summary>Folgezeilen ohne eigenen Zeitstempel (Exception, Stacktrace, mehrzeilige Nachricht).</summary>
    public string? Details { get; }

    public string File { get; }
    public int LineNumber { get; }

    // --- Anzeige ---
    public string DisplayTime { get; }
    public string ShortSource { get; }
    public string FirstLine { get; }
    public bool IsException { get; }

    /// <summary>Name, auf den die Farb-Trigger im XAML reagieren.</summary>
    public string LevelText => Level == LogLevel.Unknown ? RawLevel : Level.ToString();

    public string DetailsMarker => Details is null ? "" : IsException ? "⚠" : "¶";

    public string FullText
    {
        get
        {
            var sb = new StringBuilder();
            sb.Append(TimestampText).Append("   [").Append(LevelText).Append("]   ").AppendLine(SourceContext);
            sb.Append(File).Append(", Zeile ").Append(LineNumber).AppendLine();
            sb.AppendLine();
            sb.AppendLine(Message);
            if (Details is not null)
            {
                sb.AppendLine();
                sb.Append(Details);
            }
            return sb.ToString();
        }
    }

    public bool Contains(string text) =>
        Message.Contains(text, StringComparison.OrdinalIgnoreCase) ||
        SourceContext.Contains(text, StringComparison.OrdinalIgnoreCase) ||
        (Details?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false);
}
