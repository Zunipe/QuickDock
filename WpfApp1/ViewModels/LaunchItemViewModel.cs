using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using WpfApp1.Models;
using WpfApp1.Services;

namespace WpfApp1.ViewModels;

public partial class LaunchItemViewModel : ObservableObject
{
    private readonly LaunchItem _item;

    public LaunchItemViewModel(LaunchItem item)
    {
        _item = item;
        RefreshIcon();
    }

    public LaunchItem Model => _item;

    public string Id => _item.Id;

    public string Name
    {
        get => _item.Name;
        set
        {
            if (_item.Name != value)
            {
                _item.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public string Path => _item.Path;

    public LaunchItemType ItemType => _item.ItemType;

    [ObservableProperty]
    private ImageSource? _icon;

    public void RefreshIcon()
    {
        Icon = IconHelper.GetIcon(_item);
    }

    public void UpdateCustomIcon(string? iconPath)
    {
        _item.CustomIconPath = iconPath;
        RefreshIcon();
    }
}
