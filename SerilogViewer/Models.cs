using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace SerilogViewer;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Checkbox oben in der Toolbar: ein Level ein-/ausblenden.</summary>
public sealed class LevelFilter(LogLevel level, int count) : Observable
{
    private bool _isEnabled = true;

    public LogLevel Level { get; } = level;
    public int Count { get; } = count;
    public Brush Brush { get; } = LevelColors.BrushFor(level);
    public string Label => $"{LevelColors.DisplayName(Level)} ({Count:N0})";

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            OnPropertyChanged();
        }
    }
}

/// <summary>Ein Eintrag in der Caller-Liste links (= Serilog SourceContext).</summary>
public sealed class CallerItem : Observable
{
    private int _pendingCount, _pendingErrors, _pendingWarnings;

    private CallerItem(string name, bool isAll)
    {
        Name = name;
        IsAll = isAll;
        if (isAll)
        {
            ShortName = "Alle Caller";
            Namespace = "";
            ToolTipText = "Alle Einträge anzeigen";
        }
        else
        {
            ShortName = SourceNames.Short(name);
            Namespace = SourceNames.Namespace(name);
            ToolTipText = name.Length == 0 ? "Einträge ohne SourceContext" : name;
        }
    }

    public static CallerItem All() => new("", isAll: true);
    public static CallerItem For(string sourceContext) => new(sourceContext, isAll: false);

    public string Name { get; }
    public bool IsAll { get; }
    public string ShortName { get; }
    public string Namespace { get; }
    public bool HasNamespace => Namespace.Length > 0;
    public string ToolTipText { get; }

    public int Count { get; private set; }
    public int ErrorCount { get; private set; }
    public int WarningCount { get; private set; }
    public bool HasErrors => ErrorCount > 0;
    public bool HasWarnings => WarningCount > 0;
    public bool IsEmpty => !IsAll && Count == 0;

    internal void ResetCounts() => _pendingCount = _pendingErrors = _pendingWarnings = 0;

    internal void Add(LogLevel level)
    {
        _pendingCount++;
        if (level is LogLevel.Error or LogLevel.Fatal) 
            _pendingErrors++;
        else if (level == LogLevel.Warning) 
            _pendingWarnings++;
    }

    /// <summary>Übernimmt die gesammelten Zahlen und benachrichtigt die UI einmalig.</summary>
    internal void Publish()
    {
        if (Count == _pendingCount && ErrorCount == _pendingErrors && WarningCount == _pendingWarnings) return;
        Count = _pendingCount;
        ErrorCount = _pendingErrors;
        WarningCount = _pendingWarnings;
        OnPropertyChanged(string.Empty); // alle Properties
    }
}

public static class SourceNames
{
    private const string HttpClientPrefix = "System.Net.Http.HttpClient.";

    /// <summary>Kurzer Anzeigename, z. B. "D365ServiceOrderSyncService".</summary>
    public static string Short(string sourceContext)
    {
        if (sourceContext.Length == 0) return "(ohne Caller)";

        // Bei HttpClient-Logs ist der Client-Name interessant, nicht "LogicalHandler".
        if (sourceContext.StartsWith(HttpClientPrefix, StringComparison.Ordinal) &&
            sourceContext.Length > HttpClientPrefix.Length)
            return sourceContext[HttpClientPrefix.Length..];

        var lastDot = sourceContext.LastIndexOf('.');
        return lastDot < 0 || lastDot == sourceContext.Length - 1 ? sourceContext : sourceContext[(lastDot + 1)..];
    }

    public static string Namespace(string sourceContext)
    {
        if (sourceContext.Length == 0) return "";
        var shortName = Short(sourceContext);
        return shortName.Length >= sourceContext.Length
            ? ""
            : sourceContext[..(sourceContext.Length - shortName.Length)].TrimEnd('.');
    }
}

public static class LevelColors
{
    private static readonly Dictionary<LogLevel, Brush> LevelBrushes = new()
    {
        [LogLevel.Verbose] = Make("#BDBDBD"),
        [LogLevel.Debug] = Make("#9E9E9E"),
        [LogLevel.Information] = Make("#1E88E5"),
        [LogLevel.Warning] = Make("#F9A825"),
        [LogLevel.Error] = Make("#E53935"),
        [LogLevel.Fatal] = Make("#8E0000"),
        [LogLevel.Unknown] = Make("#CFCFCF"),
    };

    public static Brush BrushFor(LogLevel level) => LevelBrushes[level];

    public static string DisplayName(LogLevel level) => level == LogLevel.Unknown ? "Sonstige" : level.ToString();

    private static Brush Make(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
