using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using WpfApp1.Services;

namespace WpfApp1.Views;

public partial class AddItemsDialog : Window
{
    private readonly ObservableCollection<string> _paths = [];

    public IReadOnlyList<string> SelectedPaths => _paths.ToList();

    public AddItemsDialog()
    {
        InitializeComponent();
        PathsList.ItemsSource = _paths;
    }

    private void PickFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择文件或应用程序",
            Filter = "所有支持的文件|*.exe;*.lnk;*.bat;*.cmd;*.msi;*.*|所有文件|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            AddPaths(dialog.FileNames);
        }
    }

    private void PickFolders_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择文件夹",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            AddPaths(dialog.FolderNames);
        }
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var normalized = Path.GetFullPath(path);
            if (!_paths.Any(p => string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                _paths.Add(normalized);
            }
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (_paths.Count == 0)
        {
            DialogHelper.ShowInfo("请至少选择一个文件或文件夹。");
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
