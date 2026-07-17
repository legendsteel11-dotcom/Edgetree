using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SidebarExplorer.App.Models;
using SidebarExplorer.App.Services;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace SidebarExplorer.App;

public partial class ColorSettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Action _onChanged;

    // Set around the native ColorDialog's own ShowDialog (see PickColor) -
    // that dialog takes activation away from this window too, which would
    // otherwise trigger the same "clicked outside the app" flash below for a
    // completely ordinary in-app interaction (picking a color).
    private bool _isPickingColor;

    public ColorSettingsWindow(AppSettings settings, Action onChanged)
    {
        InitializeComponent();
        _settings = settings;
        _onChanged = onChanged;
        RefreshSwatches();
        Deactivated += Window_Deactivated;
    }

    // Nudges attention back to this dialog if the user clicks outside the
    // whole app while it's open (ShowDialog only blocks its owner, not other
    // applications, so that's still possible) - see _isPickingColor above for
    // the one in-app interaction this deliberately ignores.
    private void Window_Deactivated(object? sender, EventArgs e)
    {
        if (_isPickingColor)
        {
            return;
        }

        var flashBrush = new SolidColorBrush(((SolidColorBrush)RootBorder.BorderBrush).Color);
        RootBorder.BorderBrush = flashBrush;
        flashBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation
        {
            To = Color.FromRgb(0x4F, 0xA8, 0xFF),
            Duration = TimeSpan.FromMilliseconds(150),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(2)
        });
    }

    private void RefreshSwatches()
    {
        BackgroundSwatch.Background = ParseBrush(CurrentBackgroundColorHex);
        FolderNameFontSwatch.Background = ParseBrush(CurrentFolderNameColorHex);
        FolderNameHighlightFontSwatch.Background = ParseBrush(CurrentFolderNameHighlightColorHex);
        FileNameFontSwatch.Background = ParseBrush(CurrentFileNameColorHex);
        FileNameHighlightFontSwatch.Background = ParseBrush(CurrentFileNameHighlightColorHex);
        SelectionSwatch.Background = ParseBrush(CurrentSelectionColorHex);
        HistorySwatch.Background = ParseBrush(CurrentHistoryBackgroundColorHex);
        HoverBackgroundSwatch.Background = ParseBrush(CurrentHoverBackgroundColorHex);
        FolderNameHoverFontSwatch.Background = ParseBrush(CurrentFolderNameHoverColorHex);
        FileNameHoverFontSwatch.Background = ParseBrush(CurrentFileNameHoverColorHex);
        ShowMoreFontSwatch.Background = ParseBrush(CurrentShowMoreColorHex);
        GuideLineSwatch.Background = ParseBrush(CurrentGuideLineColorHex);
        GuideLineActiveSwatch.Background = ParseBrush(CurrentGuideLineActiveColorHex);
        HeaderSwatch.Background = ParseBrush(CurrentHeaderBackgroundColorHex);
        PanelDividerSwatch.Background = ParseBrush(CurrentPanelDividerColorHex);

        ThemeToggleButton.Content = _settings.IsLightMode ? Strings.ColorThemeLightMode : Strings.ColorThemeDarkMode;
        UpdateResetButtonEnabled();
    }

    private static SolidColorBrush ParseBrush(string hex)
        => new((Color)ColorConverter.ConvertFromString(hex));

    // One pair of get/set properties per color row, each reading/writing
    // whichever of the dark/light fields IsLightMode currently points at -
    // every other place in this file (RefreshSwatches, the 15 PickColor
    // wirings below, ResetDefaults_Click) goes through these instead of the
    // raw AppSettings fields directly, so there's exactly one place that
    // knows the dark/light field-name mapping for each row.
    private string CurrentBackgroundColorHex
    {
        get => _settings.IsLightMode ? _settings.LightBackgroundColorHex : _settings.BackgroundColorHex;
        set { if (_settings.IsLightMode) _settings.LightBackgroundColorHex = value; else _settings.BackgroundColorHex = value; }
    }
    private string CurrentFolderNameColorHex
    {
        get => _settings.IsLightMode ? _settings.LightFolderNameColorHex : _settings.FolderNameColorHex;
        set { if (_settings.IsLightMode) _settings.LightFolderNameColorHex = value; else _settings.FolderNameColorHex = value; }
    }
    private string CurrentFolderNameHighlightColorHex
    {
        get => _settings.IsLightMode ? _settings.LightFolderNameHighlightColorHex : _settings.FolderNameHighlightColorHex;
        set { if (_settings.IsLightMode) _settings.LightFolderNameHighlightColorHex = value; else _settings.FolderNameHighlightColorHex = value; }
    }
    private string CurrentFileNameColorHex
    {
        get => _settings.IsLightMode ? _settings.LightFileNameColorHex : _settings.FileNameColorHex;
        set { if (_settings.IsLightMode) _settings.LightFileNameColorHex = value; else _settings.FileNameColorHex = value; }
    }
    private string CurrentFileNameHighlightColorHex
    {
        get => _settings.IsLightMode ? _settings.LightFileNameHighlightColorHex : _settings.FileNameHighlightColorHex;
        set { if (_settings.IsLightMode) _settings.LightFileNameHighlightColorHex = value; else _settings.FileNameHighlightColorHex = value; }
    }
    private string CurrentSelectionColorHex
    {
        get => _settings.IsLightMode ? _settings.LightSelectionColorHex : _settings.SelectionColorHex;
        set { if (_settings.IsLightMode) _settings.LightSelectionColorHex = value; else _settings.SelectionColorHex = value; }
    }
    private string CurrentHistoryBackgroundColorHex
    {
        get => _settings.IsLightMode ? _settings.LightHistoryBackgroundColorHex : _settings.HistoryBackgroundColorHex;
        set { if (_settings.IsLightMode) _settings.LightHistoryBackgroundColorHex = value; else _settings.HistoryBackgroundColorHex = value; }
    }
    private string CurrentHoverBackgroundColorHex
    {
        get => _settings.IsLightMode ? _settings.LightHoverBackgroundColorHex : _settings.HoverBackgroundColorHex;
        set { if (_settings.IsLightMode) _settings.LightHoverBackgroundColorHex = value; else _settings.HoverBackgroundColorHex = value; }
    }
    private string CurrentFolderNameHoverColorHex
    {
        get => _settings.IsLightMode ? _settings.LightFolderNameHoverColorHex : _settings.FolderNameHoverColorHex;
        set { if (_settings.IsLightMode) _settings.LightFolderNameHoverColorHex = value; else _settings.FolderNameHoverColorHex = value; }
    }
    private string CurrentFileNameHoverColorHex
    {
        get => _settings.IsLightMode ? _settings.LightFileNameHoverColorHex : _settings.FileNameHoverColorHex;
        set { if (_settings.IsLightMode) _settings.LightFileNameHoverColorHex = value; else _settings.FileNameHoverColorHex = value; }
    }
    private string CurrentShowMoreColorHex
    {
        get => _settings.IsLightMode ? _settings.LightShowMoreColorHex : _settings.ShowMoreColorHex;
        set { if (_settings.IsLightMode) _settings.LightShowMoreColorHex = value; else _settings.ShowMoreColorHex = value; }
    }
    private string CurrentGuideLineColorHex
    {
        get => _settings.IsLightMode ? _settings.LightGuideLineColorHex : _settings.GuideLineColorHex;
        set { if (_settings.IsLightMode) _settings.LightGuideLineColorHex = value; else _settings.GuideLineColorHex = value; }
    }
    private string CurrentGuideLineActiveColorHex
    {
        get => _settings.IsLightMode ? _settings.LightGuideLineActiveColorHex : _settings.GuideLineActiveColorHex;
        set { if (_settings.IsLightMode) _settings.LightGuideLineActiveColorHex = value; else _settings.GuideLineActiveColorHex = value; }
    }
    private string CurrentPanelDividerColorHex
    {
        get => _settings.IsLightMode ? _settings.LightPanelDividerColorHex : _settings.PanelDividerColorHex;
        set { if (_settings.IsLightMode) _settings.LightPanelDividerColorHex = value; else _settings.PanelDividerColorHex = value; }
    }
    private string CurrentHeaderBackgroundColorHex
    {
        get => _settings.IsLightMode ? _settings.LightHeaderBackgroundColorHex : _settings.HeaderBackgroundColorHex;
        set { if (_settings.IsLightMode) _settings.LightHeaderBackgroundColorHex = value; else _settings.HeaderBackgroundColorHex = value; }
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        _settings.IsLightMode = !_settings.IsLightMode;
        RefreshSwatches();
        _onChanged();
    }

    // True only if every one of the current theme's 15 colors already
    // matches that theme's own default (a fresh AppSettings() instance) -
    // ResetDefaultsButton is disabled in that case, since there'd be nothing
    // to actually reset.
    private bool IsCurrentThemeAtDefaults()
    {
        var defaults = new AppSettings { IsLightMode = _settings.IsLightMode };
        return CurrentBackgroundColorHex == GetDefault(defaults, s => s.BackgroundColorHex, s => s.LightBackgroundColorHex)
            && CurrentFolderNameColorHex == GetDefault(defaults, s => s.FolderNameColorHex, s => s.LightFolderNameColorHex)
            && CurrentFolderNameHighlightColorHex == GetDefault(defaults, s => s.FolderNameHighlightColorHex, s => s.LightFolderNameHighlightColorHex)
            && CurrentFileNameColorHex == GetDefault(defaults, s => s.FileNameColorHex, s => s.LightFileNameColorHex)
            && CurrentFileNameHighlightColorHex == GetDefault(defaults, s => s.FileNameHighlightColorHex, s => s.LightFileNameHighlightColorHex)
            && CurrentSelectionColorHex == GetDefault(defaults, s => s.SelectionColorHex, s => s.LightSelectionColorHex)
            && CurrentHistoryBackgroundColorHex == GetDefault(defaults, s => s.HistoryBackgroundColorHex, s => s.LightHistoryBackgroundColorHex)
            && CurrentHoverBackgroundColorHex == GetDefault(defaults, s => s.HoverBackgroundColorHex, s => s.LightHoverBackgroundColorHex)
            && CurrentFolderNameHoverColorHex == GetDefault(defaults, s => s.FolderNameHoverColorHex, s => s.LightFolderNameHoverColorHex)
            && CurrentFileNameHoverColorHex == GetDefault(defaults, s => s.FileNameHoverColorHex, s => s.LightFileNameHoverColorHex)
            && CurrentShowMoreColorHex == GetDefault(defaults, s => s.ShowMoreColorHex, s => s.LightShowMoreColorHex)
            && CurrentGuideLineColorHex == GetDefault(defaults, s => s.GuideLineColorHex, s => s.LightGuideLineColorHex)
            && CurrentGuideLineActiveColorHex == GetDefault(defaults, s => s.GuideLineActiveColorHex, s => s.LightGuideLineActiveColorHex)
            && CurrentPanelDividerColorHex == GetDefault(defaults, s => s.PanelDividerColorHex, s => s.LightPanelDividerColorHex)
            && CurrentHeaderBackgroundColorHex == GetDefault(defaults, s => s.HeaderBackgroundColorHex, s => s.LightHeaderBackgroundColorHex);

        string GetDefault(AppSettings d, Func<AppSettings, string> dark, Func<AppSettings, string> light)
            => _settings.IsLightMode ? light(d) : dark(d);
    }

    private void UpdateResetButtonEnabled()
    {
        ResetDefaultsButton.IsEnabled = !IsCurrentThemeAtDefaults();
    }

    private void HeaderGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // Only the currently active theme's 15 colors - the other theme's
    // customizations are left completely untouched (per explicit request).
    // ResetDefaultsButton is disabled whenever there'd be nothing to reset
    // (see UpdateResetButtonEnabled), so reaching this handler at all means a
    // real change is about to happen - hence the confirmation.
    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        string modeLabel = _settings.IsLightMode ? Strings.ColorThemeLightLabel : Strings.ColorThemeDarkLabel;
        var result = System.Windows.MessageBox.Show(this, string.Format(Strings.ColorResetConfirmBody, modeLabel),
            Strings.ColorResetConfirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        var defaults = new AppSettings { IsLightMode = _settings.IsLightMode };
        CurrentBackgroundColorHex = defaults.IsLightMode ? defaults.LightBackgroundColorHex : defaults.BackgroundColorHex;
        CurrentFolderNameColorHex = defaults.IsLightMode ? defaults.LightFolderNameColorHex : defaults.FolderNameColorHex;
        CurrentFolderNameHighlightColorHex = defaults.IsLightMode ? defaults.LightFolderNameHighlightColorHex : defaults.FolderNameHighlightColorHex;
        CurrentFileNameColorHex = defaults.IsLightMode ? defaults.LightFileNameColorHex : defaults.FileNameColorHex;
        CurrentFileNameHighlightColorHex = defaults.IsLightMode ? defaults.LightFileNameHighlightColorHex : defaults.FileNameHighlightColorHex;
        CurrentSelectionColorHex = defaults.IsLightMode ? defaults.LightSelectionColorHex : defaults.SelectionColorHex;
        CurrentHistoryBackgroundColorHex = defaults.IsLightMode ? defaults.LightHistoryBackgroundColorHex : defaults.HistoryBackgroundColorHex;
        CurrentHoverBackgroundColorHex = defaults.IsLightMode ? defaults.LightHoverBackgroundColorHex : defaults.HoverBackgroundColorHex;
        CurrentFolderNameHoverColorHex = defaults.IsLightMode ? defaults.LightFolderNameHoverColorHex : defaults.FolderNameHoverColorHex;
        CurrentFileNameHoverColorHex = defaults.IsLightMode ? defaults.LightFileNameHoverColorHex : defaults.FileNameHoverColorHex;
        CurrentShowMoreColorHex = defaults.IsLightMode ? defaults.LightShowMoreColorHex : defaults.ShowMoreColorHex;
        CurrentGuideLineColorHex = defaults.IsLightMode ? defaults.LightGuideLineColorHex : defaults.GuideLineColorHex;
        CurrentGuideLineActiveColorHex = defaults.IsLightMode ? defaults.LightGuideLineActiveColorHex : defaults.GuideLineActiveColorHex;
        CurrentHeaderBackgroundColorHex = defaults.IsLightMode ? defaults.LightHeaderBackgroundColorHex : defaults.HeaderBackgroundColorHex;
        CurrentPanelDividerColorHex = defaults.IsLightMode ? defaults.LightPanelDividerColorHex : defaults.PanelDividerColorHex;

        RefreshSwatches();
        _onChanged();
    }

    private void BackgroundSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(BackgroundSwatch, () => CurrentBackgroundColorHex, hex => CurrentBackgroundColorHex = hex);

    private void FolderNameFontSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(FolderNameFontSwatch, () => CurrentFolderNameColorHex, hex => CurrentFolderNameColorHex = hex);

    private void FolderNameHighlightFontSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(FolderNameHighlightFontSwatch, () => CurrentFolderNameHighlightColorHex, hex => CurrentFolderNameHighlightColorHex = hex);

    private void FileNameFontSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(FileNameFontSwatch, () => CurrentFileNameColorHex, hex => CurrentFileNameColorHex = hex);

    private void FileNameHighlightFontSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(FileNameHighlightFontSwatch, () => CurrentFileNameHighlightColorHex, hex => CurrentFileNameHighlightColorHex = hex);

    private void SelectionSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(SelectionSwatch, () => CurrentSelectionColorHex, hex => CurrentSelectionColorHex = hex);

    private void HistorySwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(HistorySwatch, () => CurrentHistoryBackgroundColorHex, hex => CurrentHistoryBackgroundColorHex = hex);

    private void HoverBackgroundSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(HoverBackgroundSwatch, () => CurrentHoverBackgroundColorHex, hex => CurrentHoverBackgroundColorHex = hex);

    private void FolderNameHoverFontSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(FolderNameHoverFontSwatch, () => CurrentFolderNameHoverColorHex, hex => CurrentFolderNameHoverColorHex = hex);

    private void FileNameHoverFontSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(FileNameHoverFontSwatch, () => CurrentFileNameHoverColorHex, hex => CurrentFileNameHoverColorHex = hex);

    private void ShowMoreFontSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(ShowMoreFontSwatch, () => CurrentShowMoreColorHex, hex => CurrentShowMoreColorHex = hex);

    private void GuideLineSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(GuideLineSwatch, () => CurrentGuideLineColorHex, hex => CurrentGuideLineColorHex = hex);

    private void GuideLineActiveSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(GuideLineActiveSwatch, () => CurrentGuideLineActiveColorHex, hex => CurrentGuideLineActiveColorHex = hex);

    private void HeaderSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(HeaderSwatch, () => CurrentHeaderBackgroundColorHex, hex => CurrentHeaderBackgroundColorHex = hex);

    private void PanelDividerSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => PickColor(PanelDividerSwatch, () => CurrentPanelDividerColorHex, hex => CurrentPanelDividerColorHex = hex);

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
        _isPickingColor = true;
        bool accepted = dialog.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK;
        _isPickingColor = false;

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
        UpdateResetButtonEnabled();
        _onChanged();
    }

    private sealed class Win32Window(IntPtr handle) : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }
}
