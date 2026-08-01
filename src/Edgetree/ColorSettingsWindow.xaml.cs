using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SidebarExplorer.App.Models;
using SidebarExplorer.App.Services;
using SidebarExplorer.App.Behaviors;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;
using Key = System.Windows.Input.Key;
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

        // The codes beside them follow the same way - a theme flip, a reset to
        // defaults or a pick from the colour dialog all land here, so no caller
        // has to remember the boxes exist.
        foreach (var box in _hexBoxes)
        {
            RefreshHexBox(box);
        }

        // Labels the mode the button will switch TO (the action), not the one
        // currently active - "☀️ 라이트 모드" while in dark mode reads as "click
        // to go light", which is clearer than restating the current state.
        ThemeToggleButton.Content = _settings.IsLightMode ? Strings.ColorThemeDarkMode : Strings.ColorThemeLightMode;
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

    private void BackgroundSwatch_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => PickColor(BackgroundSwatch, () => CurrentBackgroundColorHex, hex => CurrentBackgroundColorHex = hex);

    private void FolderNameFontSwatch_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => PickColor(FolderNameFontSwatch, () => CurrentFolderNameColorHex, hex => CurrentFolderNameColorHex = hex);

    private void FolderNameHighlightFontSwatch_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => PickColor(FolderNameHighlightFontSwatch, () => CurrentFolderNameHighlightColorHex, hex => CurrentFolderNameHighlightColorHex = hex);

    private void FileNameFontSwatch_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => PickColor(FileNameFontSwatch, () => CurrentFileNameColorHex, hex => CurrentFileNameColorHex = hex);

    private void FileNameHighlightFontSwatch_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => PickColor(FileNameHighlightFontSwatch, () => CurrentFileNameHighlightColorHex, hex => CurrentFileNameHighlightColorHex = hex);

    private void SelectionSwatch_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => PickColor(SelectionSwatch, () => CurrentSelectionColorHex, hex => CurrentSelectionColorHex = hex);

    private void HistorySwatch_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => PickColor(HistorySwatch, () => CurrentHistoryBackgroundColorHex, hex => CurrentHistoryBackgroundColorHex = hex);

    private void HoverBackgroundSwatch_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => PickColor(HoverBackgroundSwatch, () => CurrentHoverBackgroundColorHex, hex => CurrentHoverBackgroundColorHex = hex);

    private void FolderNameHoverFontSwatch_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => PickColor(FolderNameHoverFontSwatch, () => CurrentFolderNameHoverColorHex, hex => CurrentFolderNameHoverColorHex = hex);

    private void FileNameHoverFontSwatch_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => PickColor(FileNameHoverFontSwatch, () => CurrentFileNameHoverColorHex, hex => CurrentFileNameHoverColorHex = hex);

    private void ShowMoreFontSwatch_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => PickColor(ShowMoreFontSwatch, () => CurrentShowMoreColorHex, hex => CurrentShowMoreColorHex = hex);

    private void GuideLineSwatch_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => PickColor(GuideLineSwatch, () => CurrentGuideLineColorHex, hex => CurrentGuideLineColorHex = hex);

    private void GuideLineActiveSwatch_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => PickColor(GuideLineActiveSwatch, () => CurrentGuideLineActiveColorHex, hex => CurrentGuideLineActiveColorHex = hex);

    private void HeaderSwatch_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => PickColor(HeaderSwatch, () => CurrentHeaderBackgroundColorHex, hex => CurrentHeaderBackgroundColorHex = hex);

    private void PanelDividerSwatch_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => PickColor(PanelDividerSwatch, () => CurrentPanelDividerColorHex, hex => CurrentPanelDividerColorHex = hex);

    // ----- 색상 코드 직접 입력 -----------------------------------------------
    //
    // Windows' own picker has three RGB number boxes and nowhere to paste
    // "#2E7D32" from a browser or a design tool, which is how colours actually
    // travel (user mail, 2026-07-31). So the swatch answers a right-click with
    // a box holding its current value.
    //
    // Right-click rather than a field on every row: seventeen rows with a text
    // box each would eat the width the Korean labels need, and multiply tab
    // order, validation and the Insert guard by seventeen - the opposite of the
    // direction this window has been going. A shared box at the bottom was the
    // other candidate and needs a "selected row" concept this window does not
    // have. Both were weighed and dropped with the user before any of this
    // existed; the tooltip on every swatch is what keeps the gesture findable.
    // One place that says which swatch edits which colour, so the left-click
    // picker and this cannot end up disagreeing - or a colour added later be
    // wired to one and not the other.
    private (Func<string> Get, Action<string> Set)? ColorBindingFor(Border swatch)
    {
        if (ReferenceEquals(swatch, BackgroundSwatch))
            return (() => CurrentBackgroundColorHex, hex => CurrentBackgroundColorHex = hex);
        if (ReferenceEquals(swatch, FolderNameFontSwatch))
            return (() => CurrentFolderNameColorHex, hex => CurrentFolderNameColorHex = hex);
        if (ReferenceEquals(swatch, FolderNameHighlightFontSwatch))
            return (() => CurrentFolderNameHighlightColorHex, hex => CurrentFolderNameHighlightColorHex = hex);
        if (ReferenceEquals(swatch, FolderNameHoverFontSwatch))
            return (() => CurrentFolderNameHoverColorHex, hex => CurrentFolderNameHoverColorHex = hex);
        if (ReferenceEquals(swatch, FileNameFontSwatch))
            return (() => CurrentFileNameColorHex, hex => CurrentFileNameColorHex = hex);
        if (ReferenceEquals(swatch, FileNameHighlightFontSwatch))
            return (() => CurrentFileNameHighlightColorHex, hex => CurrentFileNameHighlightColorHex = hex);
        if (ReferenceEquals(swatch, FileNameHoverFontSwatch))
            return (() => CurrentFileNameHoverColorHex, hex => CurrentFileNameHoverColorHex = hex);
        if (ReferenceEquals(swatch, ShowMoreFontSwatch))
            return (() => CurrentShowMoreColorHex, hex => CurrentShowMoreColorHex = hex);
        if (ReferenceEquals(swatch, SelectionSwatch))
            return (() => CurrentSelectionColorHex, hex => CurrentSelectionColorHex = hex);
        if (ReferenceEquals(swatch, HistorySwatch))
            return (() => CurrentHistoryBackgroundColorHex, hex => CurrentHistoryBackgroundColorHex = hex);
        if (ReferenceEquals(swatch, HoverBackgroundSwatch))
            return (() => CurrentHoverBackgroundColorHex, hex => CurrentHoverBackgroundColorHex = hex);
        if (ReferenceEquals(swatch, GuideLineSwatch))
            return (() => CurrentGuideLineColorHex, hex => CurrentGuideLineColorHex = hex);
        if (ReferenceEquals(swatch, GuideLineActiveSwatch))
            return (() => CurrentGuideLineActiveColorHex, hex => CurrentGuideLineActiveColorHex = hex);
        if (ReferenceEquals(swatch, HeaderSwatch))
            return (() => CurrentHeaderBackgroundColorHex, hex => CurrentHeaderBackgroundColorHex = hex);
        if (ReferenceEquals(swatch, PanelDividerSwatch))
            return (() => CurrentPanelDividerColorHex, hex => CurrentPanelDividerColorHex = hex);

        return null;
    }

    // Every box names its swatch in its Tag, so these handlers serve all
    // fifteen. The Tag holds the NAME and this looks it up, rather than holding
    // an {Binding ElementName=...} to the swatch itself: that binding is not
    // resolved yet when the box raises Loaded, so the first fill found no
    // swatch and every box came up EMPTY until it had been clicked into and
    // left again (2026-08-02). A name has no such moment.
    private Border? SwatchOf(object sender)
        => (sender as TextBox)?.Tag is string name ? FindName(name) as Border : null;

    // Collected as they load rather than named one by one, so RefreshSwatches
    // can put the current values back into all of them without a fifteen-line
    // list to keep in step.
    private readonly List<TextBox> _hexBoxes = new();

    private void HexBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box)
        {
            return;
        }

        // Every text box in this app disables overtype - a bare Insert
        // otherwise flips it on and breaks Korean composition, with no visible
        // sign it happened (see OvertypeGuard).
        OvertypeGuard.Disable(box);

        if (!_hexBoxes.Contains(box))
        {
            _hexBoxes.Add(box);
        }

        RefreshHexBox(box);
    }

    private void HexBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ApplyHexBox(sender);
            return;
        }

        // Puts back what the swatch actually holds. The box stays where it is -
        // there is nothing to close - so "cancel" can only mean "undo what I
        // typed".
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            RefreshHexBox(sender);
        }
    }

    // Committing on focus loss as well as on Enter: the box is a permanent part
    // of the row, so tabbing or clicking away from it is a perfectly ordinary
    // way to finish, and having to press Enter would silently drop the edit.
    private void HexBox_LostKeyboardFocus(object sender, RoutedEventArgs e) => ApplyHexBox(sender);

    private void ApplyHexBox(object sender)
    {
        if (sender is not TextBox box || SwatchOf(sender) is not { } swatch ||
            ColorBindingFor(swatch) is not { } binding)
        {
            return;
        }

        // Anything that doesn't parse is simply dropped and the box goes back to
        // the colour that is actually set - no error, no red outline, no dialog.
        // Showing the real value again says "that wasn't taken" more clearly
        // than a message would.
        if (ParseHex(box.Text, (Color)ColorConverter.ConvertFromString(binding.Get())) is not { } color)
        {
            RefreshHexBox(sender);
            return;
        }

        binding.Set($"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}");
        swatch.Background = new SolidColorBrush(color);
        RefreshHexBox(sender);
        UpdateResetButtonEnabled();
        _onChanged();
    }

    private void RefreshHexBox(object sender)
    {
        if (sender is TextBox box && SwatchOf(sender) is { } swatch &&
            ColorBindingFor(swatch) is { } binding)
        {
            box.Text = FormatHex(binding.Get());
        }
    }

    // Without the alpha byte. Every colour here is opaque, and an "FF" in front
    // of the part people actually paste is just something to delete - the
    // stored value keeps whatever alpha it had (see ParseHex).
    private static string FormatHex(string storedHex)
    {
        var color = (Color)ColorConverter.ConvertFromString(storedHex);
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    // Takes what a colour code looks like anywhere it might be copied from:
    // with or without the "#", three digits or six, either case, and the eight
    // -digit form this app stores. The alpha of the value being replaced is
    // kept unless the input states its own, so pasting "#2E7D32" over an opaque
    // colour cannot silently turn it transparent.
    private static Color? ParseHex(string? text, Color fallback)
    {
        string value = (text ?? string.Empty).Trim().TrimStart('#');
        if (!value.All(Uri.IsHexDigit))
        {
            return null;
        }

        // #abc is the CSS shorthand: each digit doubled.
        if (value.Length == 3)
        {
            value = string.Concat(value.Select(c => new string(c, 2)));
        }

        return value.Length switch
        {
            6 => Color.FromArgb(
                fallback.A,
                Convert.ToByte(value[..2], 16),
                Convert.ToByte(value.Substring(2, 2), 16),
                Convert.ToByte(value.Substring(4, 2), 16)),
            8 => Color.FromArgb(
                Convert.ToByte(value[..2], 16),
                Convert.ToByte(value.Substring(2, 2), 16),
                Convert.ToByte(value.Substring(4, 2), 16),
                Convert.ToByte(value.Substring(6, 2), 16)),
            _ => null,
        };
    }

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
    //
    // Every swatch calls this from MouseLeftButtonUP, never Down, and that
    // matters more than it looks. This opens a NATIVE modal dialog, which runs
    // its own message loop: opening it from the down event means the dialog
    // appears while the button is still physically held, and the release that
    // follows is consumed by that loop instead of reaching WPF. WPF is then
    // left believing the left button is still down, and every drag gesture in
    // the app begins with exactly one test - `e.LeftButton == Pressed`. With
    // that stuck true, merely moving the cursor over the tree afterwards starts
    // a drag nobody asked for, which takes the mouse capture and has no button
    // release coming to end it - the app keeps receiving all mouse input while
    // every other window stops responding. Reported after a long session in
    // which colors had just been changed. Waiting for the release keeps the
    // gesture WPF sees complete before the native loop takes over.
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

        // The code beside the swatch has to follow, or the two disagree the
        // moment a colour is picked with the dialog rather than typed - which
        // reads as the field being stale or broken (2026-08-02). All of them
        // rather than hunting for the one: fifteen string formats cost nothing
        // next to the dialog that just closed.
        foreach (var box in _hexBoxes)
        {
            RefreshHexBox(box);
        }

        UpdateResetButtonEnabled();
        _onChanged();
    }

    private sealed class Win32Window(IntPtr handle) : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }
}
