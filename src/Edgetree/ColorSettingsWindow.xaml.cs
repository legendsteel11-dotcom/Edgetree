using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SidebarExplorer.App.Models;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace SidebarExplorer.App;

public partial class ColorSettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Action _onChanged;

    public ColorSettingsWindow(AppSettings settings, Action onChanged)
    {
        InitializeComponent();
        _settings = settings;
        _onChanged = onChanged;
        RefreshSwatches();
    }

    private void RefreshSwatches()
    {
        BackgroundSwatch.Background = ParseBrush(_settings.BackgroundColorHex);
        FolderNameFontSwatch.Background = ParseBrush(_settings.FolderNameColorHex);
        FolderNameHighlightFontSwatch.Background = ParseBrush(_settings.FolderNameHighlightColorHex);
        FileNameFontSwatch.Background = ParseBrush(_settings.FileNameColorHex);
        FileNameHighlightFontSwatch.Background = ParseBrush(_settings.FileNameHighlightColorHex);
        SelectionSwatch.Background = ParseBrush(_settings.SelectionColorHex);
        HistorySwatch.Background = ParseBrush(_settings.HistoryBackgroundColorHex);
        HoverBackgroundSwatch.Background = ParseBrush(_settings.HoverBackgroundColorHex);
        FolderNameHoverFontSwatch.Background = ParseBrush(_settings.FolderNameHoverColorHex);
        FileNameHoverFontSwatch.Background = ParseBrush(_settings.FileNameHoverColorHex);
        GuideLineSwatch.Background = ParseBrush(_settings.GuideLineColorHex);
        GuideLineActiveSwatch.Background = ParseBrush(_settings.GuideLineActiveColorHex);
        HeaderSwatch.Background = ParseBrush(_settings.HeaderBackgroundColorHex);
        PanelDividerSwatch.Background = ParseBrush(_settings.PanelDividerColorHex);
    }

    private static SolidColorBrush ParseBrush(string hex)
        => new((Color)ColorConverter.ConvertFromString(hex));

    private void HeaderGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        var defaults = new AppSettings();
        _settings.BackgroundColorHex = defaults.BackgroundColorHex;
        _settings.FolderNameColorHex = defaults.FolderNameColorHex;
        _settings.FolderNameHighlightColorHex = defaults.FolderNameHighlightColorHex;
        _settings.FileNameColorHex = defaults.FileNameColorHex;
        _settings.FileNameHighlightColorHex = defaults.FileNameHighlightColorHex;
        _settings.SelectionColorHex = defaults.SelectionColorHex;
        _settings.HistoryBackgroundColorHex = defaults.HistoryBackgroundColorHex;
        _settings.HoverBackgroundColorHex = defaults.HoverBackgroundColorHex;
        _settings.FolderNameHoverColorHex = defaults.FolderNameHoverColorHex;
        _settings.FileNameHoverColorHex = defaults.FileNameHoverColorHex;
        _settings.GuideLineColorHex = defaults.GuideLineColorHex;
        _settings.GuideLineActiveColorHex = defaults.GuideLineActiveColorHex;
        _settings.HeaderBackgroundColorHex = defaults.HeaderBackgroundColorHex;
        _settings.PanelDividerColorHex = defaults.PanelDividerColorHex;

        RefreshSwatches();
        _onChanged();
    }

    private void BackgroundSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(BackgroundSwatch, () => _settings.BackgroundColorHex, hex => _settings.BackgroundColorHex = hex);

    private void FolderNameFontSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(FolderNameFontSwatch, () => _settings.FolderNameColorHex, hex => _settings.FolderNameColorHex = hex);

    private void FolderNameHighlightFontSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(FolderNameHighlightFontSwatch, () => _settings.FolderNameHighlightColorHex, hex => _settings.FolderNameHighlightColorHex = hex);

    private void FileNameFontSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(FileNameFontSwatch, () => _settings.FileNameColorHex, hex => _settings.FileNameColorHex = hex);

    private void FileNameHighlightFontSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(FileNameHighlightFontSwatch, () => _settings.FileNameHighlightColorHex, hex => _settings.FileNameHighlightColorHex = hex);

    private void SelectionSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(SelectionSwatch, () => _settings.SelectionColorHex, hex => _settings.SelectionColorHex = hex);

    private void HistorySwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(HistorySwatch, () => _settings.HistoryBackgroundColorHex, hex => _settings.HistoryBackgroundColorHex = hex);

    private void HoverBackgroundSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(HoverBackgroundSwatch, () => _settings.HoverBackgroundColorHex, hex => _settings.HoverBackgroundColorHex = hex);

    private void FolderNameHoverFontSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(FolderNameHoverFontSwatch, () => _settings.FolderNameHoverColorHex, hex => _settings.FolderNameHoverColorHex = hex);

    private void FileNameHoverFontSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(FileNameHoverFontSwatch, () => _settings.FileNameHoverColorHex, hex => _settings.FileNameHoverColorHex = hex);

    private void GuideLineSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(GuideLineSwatch, () => _settings.GuideLineColorHex, hex => _settings.GuideLineColorHex = hex);

    private void GuideLineActiveSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(GuideLineActiveSwatch, () => _settings.GuideLineActiveColorHex, hex => _settings.GuideLineActiveColorHex = hex);

    private void HeaderSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(HeaderSwatch, () => _settings.HeaderBackgroundColorHex, hex => _settings.HeaderBackgroundColorHex = hex);

    private void PanelDividerSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(PanelDividerSwatch, () => _settings.PanelDividerColorHex, hex => _settings.PanelDividerColorHex = hex);

    // Shared across every PickColor call (and, being static, across every time
    // this window is reopened within the same app run) - a fresh
    // System.Windows.Forms.ColorDialog only remembers custom palette colors
    // for as long as that ONE dialog instance is open, so a color added while
    // picking one row's color used to vanish the moment the dialog closed
    // and a different row's picker opened a brand-new instance.
    private static int[]? _customColors;

    // Windows' own color picker (System.Windows.Forms.ColorDialog, already
    // available - the project already references WinForms elsewhere for the
    // tray icon and Recycle Bin support) rather than building a custom one.
    private void PickColor(Border swatch, Func<string> getHex, Action<string> setHex)
    {
        var current = (Color)ColorConverter.ConvertFromString(getHex());

        using var dialog = new System.Windows.Forms.ColorDialog
        {
            Color = System.Drawing.Color.FromArgb(current.A, current.R, current.G, current.B),
            FullOpen = true,
            CustomColors = _customColors ?? Array.Empty<int>()
        };

        var owner = new Win32Window(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        bool accepted = dialog.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK;

        // Kept even on Cancel - a custom color added to the palette before
        // backing out of that particular pick should still be there next time.
        _customColors = dialog.CustomColors;

        if (!accepted)
        {
            return;
        }

        var picked = dialog.Color;
        string hex = $"#{picked.A:X2}{picked.R:X2}{picked.G:X2}{picked.B:X2}";
        setHex(hex);
        swatch.Background = new SolidColorBrush(Color.FromArgb(picked.A, picked.R, picked.G, picked.B));
        _onChanged();
    }

    private sealed class Win32Window(IntPtr handle) : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }
}
