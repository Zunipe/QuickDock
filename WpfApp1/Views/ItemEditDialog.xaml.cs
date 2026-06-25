using System.Windows;
using Microsoft.Win32;
using WpfApp1.Services;
using WpfApp1.ViewModels;

namespace WpfApp1.Views;

public partial class ItemEditDialog : Window
{
    private readonly LaunchItemViewModel _item;
    private string? _customIconPath;

    public ItemEditDialog(LaunchItemViewModel item)
    {
        InitializeComponent();
        _item = item;
        NameBox.Text = item.Name;
        PathBox.Text = item.Path;
        IconPreview.Source = item.Icon;
        _customIconPath = item.Model.CustomIconPath;
    }

    private void PickIcon_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择图标",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.ico;*.bmp|所有文件|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            _customIconPath = dialog.FileName;
            _item.UpdateCustomIcon(_customIconPath);
            IconPreview.Source = _item.Icon;
        }
    }

    private void ResetIcon_Click(object sender, RoutedEventArgs e)
    {
        _customIconPath = null;
        _item.UpdateCustomIcon(null);
        IconPreview.Source = _item.Icon;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _item.Name = NameBox.Text.Trim();
        _item.UpdateCustomIcon(_customIconPath);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
