using System.Windows;
using System.Windows.Controls;
using WpfApp1.Models;
using WpfApp1.Services;

namespace WpfApp1.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Action<AppSettings> _onSave;
    private bool _originalStartup;

    public SettingsWindow(AppSettings settings, Action<AppSettings> onSave)
    {
        InitializeComponent();
        _settings = CloneSettings(settings);
        _onSave = onSave;
        _originalStartup = _settings.StartWithWindows;
        LoadSettings();
    }

    private void LoadSettings()
    {
        StartupCheckBox.IsChecked = StartupHelper.IsStartupEnabled();
        EdgeSnapCheckBox.IsChecked = _settings.EnableEdgeSnap;
        ProximitySlider.Value = _settings.ProximityThreshold;

        SelectComboItem(SnapEdgeCombo, _settings.SnapEdge);
        SelectComboItem(SummonModeCombo, _settings.SummonMode);
    }

    private static void SelectComboItem(System.Windows.Controls.ComboBox combo, object value)
    {
        foreach (System.Windows.Controls.ComboBoxItem item in combo.Items)
        {
            if (item.Tag?.Equals(value) == true)
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    private static T GetComboTag<T>(System.Windows.Controls.ComboBox combo) where T : struct, Enum
    {
        if (combo.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is T tag)
        {
            return tag;
        }

        return default;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var wantStartup = StartupCheckBox.IsChecked == true;

        if (wantStartup != _originalStartup)
        {
            if (!StartupHelper.SetStartup(wantStartup))
            {
                DialogHelper.ShowWarning(
                    "设置开机自启动失败。请确认已授予管理员权限。");
                StartupCheckBox.IsChecked = StartupHelper.IsStartupEnabled();
                return;
            }
        }

        _settings.StartWithWindows = StartupHelper.IsStartupEnabled();
        _settings.EnableEdgeSnap = EdgeSnapCheckBox.IsChecked == true;
        _settings.SnapEdge = GetComboTag<SnapEdge>(SnapEdgeCombo);
        _settings.SummonMode = GetComboTag<SummonMode>(SummonModeCombo);
        _settings.ProximityThreshold = (int)ProximitySlider.Value;

        _onSave(_settings);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static AppSettings CloneSettings(AppSettings source) => new()
    {
        StartWithWindows = source.StartWithWindows,
        EnableEdgeSnap = source.EnableEdgeSnap,
        SnapEdge = source.SnapEdge,
        SummonMode = source.SummonMode,
        ProximityThreshold = source.ProximityThreshold,
        WindowLeft = source.WindowLeft,
        WindowTop = source.WindowTop,
        ExpandedWidth = source.ExpandedWidth,
        ExpandedHeight = source.ExpandedHeight,
        IsExpanded = source.IsExpanded
    };
}
