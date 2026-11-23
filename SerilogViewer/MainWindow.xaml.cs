using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace SerilogViewer;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<CallerItem> _callers = new();
    private readonly ObservableCollection<LevelFilter> _levels = new();
    private readonly Dictionary<string, CallerItem> _callerByName = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _searchTimer;

    private List<LogEntry> _all = new();
    private ListCollectionView? _view;
    private HashSet<LogLevel> _enabledLevels = new();
    private HashSet<string>? _selectedCallers; // null = alle
    private string _search = "";
    private bool _suppressRefresh;

    public MainWindow()
    {
        InitializeComponent();

        CallerList.ItemsSource = _callers;
        LevelList.ItemsSource = _levels;

        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            _search = SearchBox.Text.Trim();
            RefreshView(recountCallers: true);
        };

        // Dateien per Kommandozeile / "Öffnen mit" / Drag auf die .exe
        Loaded += async (_, _) =>
        {
            var files = Environment.GetCommandLineArgs().Skip(1).Where(File.Exists).ToArray();
            if (files.Length > 0) await LoadFilesAsync(files);
        };
    }

    // ------------------------------------------------------------------ Laden

    private async Task LoadFilesAsync(string[] paths)
    {
        if (paths.Length == 0) return;
        try
        {
            StatusText.Text = "Lade …";
            var entries = await Task.Run(() =>
            {
                var list = new List<LogEntry>();
                foreach (var path in paths)
                {
                    // FileShare.ReadWrite: funktioniert auch, während die Anwendung noch in die Datei schreibt.
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    list.AddRange(SerilogParser.Parse(reader, Path.GetFileName(path)));
                }
                return list;
            });
            SetEntries(entries, string.Join(", ", paths.Select(p => Path.GetFileName(p))));
        }
        catch (Exception ex)
        {
            StatusText.Text = "Fehler beim Laden";
            MessageBox.Show(this, ex.Message, "Datei konnte nicht geladen werden", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadText(string text, string sourceName)
    {
        using var reader = new StringReader(text);
        SetEntries(SerilogParser.Parse(reader, sourceName), sourceName);
    }

    private void SetEntries(List<LogEntry> entries, string sourceName)
    {
        _suppressRefresh = true;

        // OrderBy ist stabil → mehrere Dateien werden chronologisch zusammengeführt.
        _all = entries.OrderBy(e => e.Timestamp ?? DateTimeOffset.MinValue).ToList();

        // Level-Checkboxen
        foreach (var level in _levels) level.PropertyChanged -= Level_PropertyChanged;
        _levels.Clear();
        foreach (var group in _all.GroupBy(e => e.Level).OrderBy(g => g.Key))
        {
            var filter = new LevelFilter(group.Key, group.Count());
            filter.PropertyChanged += Level_PropertyChanged;
            _levels.Add(filter);
        }
        _enabledLevels = _levels.Select(l => l.Level).ToHashSet();

        // Caller-Liste
        _callers.Clear();
        _callerByName.Clear();
        _callers.Add(CallerItem.All());
        var names = _all.Select(e => e.SourceContext)
                        .Distinct()
                        .OrderBy(n => n.Length == 0)                       // "(ohne Caller)" ans Ende
                        .ThenBy(n => n, StringComparer.OrdinalIgnoreCase); // nach Namespace gruppiert
        foreach (var name in names)
        {
            var item = CallerItem.For(name);
            _callerByName[name] = item;
            _callers.Add(item);
        }

        _selectedCallers = null;
        _search = SearchBox.Text.Trim();
        _view = new ListCollectionView(_all) { Filter = FilterEntry };
        EntryGrid.ItemsSource = _view;
        CallerList.SelectedIndex = 0;

        EmptyHint.Visibility = _all.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Title = $"Serilog Viewer – {sourceName}";

        _suppressRefresh = false;
        RefreshView(recountCallers: true);
    }

    // ------------------------------------------------------------------ Filtern

    private bool FilterEntry(object item)
    {
        var entry = (LogEntry)item;
        return PassesLevelAndSearch(entry) &&
               (_selectedCallers is null || _selectedCallers.Contains(entry.SourceContext));
    }

    private bool PassesLevelAndSearch(LogEntry entry) =>
        _enabledLevels.Contains(entry.Level) &&
        (_search.Length == 0 || entry.Contains(_search));

    private void RefreshView(bool recountCallers)
    {
        if (_suppressRefresh || _view is null) return;

        _enabledLevels = _levels.Where(l => l.IsEnabled).Select(l => l.Level).ToHashSet();
        _view.Refresh();

        if (recountCallers) RecountCallers();

        StatusText.Text = $"{_view.Count:N0} von {_all.Count:N0} Einträgen";

        if (EntryGrid.SelectedItem is not null)
            EntryGrid.ScrollIntoView(EntryGrid.SelectedItem);
    }

    /// <summary>Zahlen in der Caller-Liste passend zu Level-Filter und Suche aktualisieren.</summary>
    private void RecountCallers()
    {
        foreach (var caller in _callers) caller.ResetCounts();

        var all = _callers[0];
        foreach (var entry in _all)
        {
            if (!PassesLevelAndSearch(entry)) continue;
            all.Add(entry.Level);
            _callerByName[entry.SourceContext].Add(entry.Level);
        }

        foreach (var caller in _callers) caller.Publish();
    }

    private void Level_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LevelFilter.IsEnabled))
            RefreshView(recountCallers: true);
    }

    private void CallerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = CallerList.SelectedItems.Cast<CallerItem>().ToList();
        _selectedCallers = selected.Count == 0 || selected.Any(c => c.IsAll)
            ? null
            : selected.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        RefreshView(recountCallers: false);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void ShowOnlyThisCaller_Click(object sender, RoutedEventArgs e)
    {
        if (EntryGrid.SelectedItem is not LogEntry entry ||
            !_callerByName.TryGetValue(entry.SourceContext, out var caller))
            return;

        CallerList.SelectedItems.Clear();
        CallerList.SelectedItem = caller;
        CallerList.ScrollIntoView(caller);
    }

    private void ShowAllCallers_Click(object sender, RoutedEventArgs e) => CallerList.SelectedIndex = 0;

    // ------------------------------------------------------------------ Öffnen / Einfügen / Drag & Drop

    private async void Open_Click(object sender, RoutedEventArgs e) => await OpenFileAsync();

    private async void Paste_Click(object sender, RoutedEventArgs e) => await PasteAsync();

    private async Task OpenFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Serilog-Logdatei öffnen",
            Filter = "Logdateien (*.txt;*.log)|*.txt;*.log|Alle Dateien (*.*)|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) == true)
            await LoadFilesAsync(dialog.FileNames);
    }

    private async Task PasteAsync()
    {
        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList().Cast<string>().Where(File.Exists).ToArray();
                if (files.Length > 0)
                {
                    await LoadFilesAsync(files);
                    return;
                }
            }

            if (Clipboard.ContainsText())
                LoadText(Clipboard.GetText(), "Zwischenablage");
            else
                StatusText.Text = "Die Zwischenablage enthält keinen Text.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Zwischenablage konnte nicht gelesen werden: " + ex.Message;
        }
    }

    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (IsWithin(e.OriginalSource as DependencyObject, SearchBox)) return;

        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.UnicodeText)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_PreviewDrop(object sender, DragEventArgs e)
    {
        if (IsWithin(e.OriginalSource as DependencyObject, SearchBox)) return;

        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            e.Handled = true;
            await LoadFilesAsync(files.Where(File.Exists).ToArray());
        }
        else if (e.Data.GetData(DataFormats.UnicodeText) is string { Length: > 0 } text)
        {
            e.Handled = true;
            LoadText(text, "Drag & Drop");
        }
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && SearchBox.IsKeyboardFocusWithin)
        {
            SearchBox.Clear();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        switch (e.Key)
        {
            // Strg+V in der Suchbox soll normal einfügen
            case Key.V when Keyboard.FocusedElement is not TextBox { IsReadOnly: false }:
                e.Handled = true;
                await PasteAsync();
                break;
            case Key.O:
                e.Handled = true;
                await OpenFileAsync();
                break;
            case Key.F:
                e.Handled = true;
                SearchBox.Focus();
                SearchBox.SelectAll();
                break;
        }
    }

    private static bool IsWithin(DependencyObject? element, DependencyObject ancestor)
    {
        while (element is not null)
        {
            if (ReferenceEquals(element, ancestor)) return true;
            element = element is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        }
        return false;
    }
}
