using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SerilogViewer;

/// <summary>
/// Liest Text im Format des Serilog-File-Sinks, z. B.
/// <c>2026-11-19 02:01:54.077 +01:00 [Information] [My.Source.Context] Nachricht</c>.
/// Zeilen ohne Zeitstempel werden dem vorherigen Eintrag als Details angehängt.
/// Der [SourceContext]-Teil ist optional, Level dürfen lang (Information) oder kurz (INF) sein.
/// </summary>
public static partial class SerilogParser
{
    [GeneratedRegex(
        @"^(?<ts>\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(?:[.,]\d{1,7})?(?: ?(?:[+-]\d{2}:?\d{2}|Z))?) \[(?<lvl>[A-Za-z]+)\] (?:\[(?<src>[^\]]*)\](?: |$))?(?<msg>.*)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex HeaderRegex();

    private static readonly string[] TimestampFormats =
    {
        "yyyy-MM-dd HH:mm:ss.fff zzz",
        "yyyy-MM-dd HH:mm:ss zzz",
        "yyyy-MM-dd HH:mm:ss.fff",
        "yyyy-MM-dd HH:mm:ss",
    };

    public static List<LogEntry> Parse(TextReader reader, string file)
    {
        var result = new List<LogEntry>();
        Pending? current = null;
        var lineNumber = 0;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            var match = HeaderRegex().Match(line);

            if (match.Success)
            {
                if (current is not null) result.Add(current.Build());
                current = new Pending(match, file, lineNumber);
            }
            else if (current is not null)
            {
                current.AppendDetail(line);
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                // Text vor dem ersten erkannten Eintrag – nicht wegwerfen, sondern als "unbekannt" anzeigen.
                result.Add(new LogEntry(null, "", LogLevel.Unknown, "?", "", line, null, file, lineNumber));
            }
        }

        if (current is not null) result.Add(current.Build());
        return result;
    }

    private static LogLevel ParseLevel(string text) => text.ToUpperInvariant() switch
    {
        "VERBOSE" or "VRB" or "TRACE" or "TRC" => LogLevel.Verbose,
        "DEBUG" or "DBG" => LogLevel.Debug,
        "INFORMATION" or "INF" or "INFO" => LogLevel.Information,
        "WARNING" or "WRN" or "WARN" => LogLevel.Warning,
        "ERROR" or "ERR" => LogLevel.Error,
        "FATAL" or "FTL" or "CRITICAL" or "CRT" => LogLevel.Fatal,
        _ => LogLevel.Unknown
    };

    private static DateTimeOffset? ParseTimestamp(string text)
    {
        var normalized = text.Replace(',', '.').Replace('T', ' ');
        if (DateTimeOffset.TryParseExact(normalized, TimestampFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out var exact))
            return exact;

        if (DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var loose))
            return loose;

        return null;
    }

    private sealed class Pending(Match match, string file, int lineNumber)
    {
        private readonly string _timestampText = match.Groups["ts"].Value;
        private readonly string _rawLevel = match.Groups["lvl"].Value;
        private readonly string _source = match.Groups["src"].Success ? match.Groups["src"].Value : "";
        private readonly string _message = match.Groups["msg"].Value;
        private StringBuilder? _details;

        public void AppendDetail(string line) => (_details ??= new StringBuilder()).AppendLine(line);

        public LogEntry Build()
        {
            var details = _details?.ToString().TrimEnd();
            if (string.IsNullOrEmpty(details)) details = null;

            return new LogEntry(
                ParseTimestamp(_timestampText),
                _timestampText,
                ParseLevel(_rawLevel),
                _rawLevel,
                _source,
                _message,
                details,
                file,
                lineNumber);
        }
    }
}
