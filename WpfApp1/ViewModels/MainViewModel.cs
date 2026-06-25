using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfApp1.Models;
using WpfApp1.Services;
using WpfApp1.Views;

namespace WpfApp1.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DataStore _dataStore = new();
    private AppData _data;

    public MainViewModel()
    {
        _data = _dataStore.Load();
        LoadCategories();
        LoadItems();
        SyncStartupState();
    }

    public AppSettings Settings => _data.Settings;

    [ObservableProperty]
    private ObservableCollection<CategoryViewModel> _categories = [];

    [ObservableProperty]
    private CategoryViewModel? _selectedCategory;

    [ObservableProperty]
    private ObservableCollection<LaunchItemViewModel> _filteredItems = [];

    [ObservableProperty]
    private LaunchItemViewModel? _selectedItem;

    [ObservableProperty]
    private bool _isExpanded = true;

    public bool ShowEdgeCollapsedIndicator =>
        !IsExpanded && Settings.EnableEdgeSnap && Settings.SnapEdge != SnapEdge.None;

    public System.Windows.HorizontalAlignment CollapsedStripAlignment =>
        Settings.SnapEdge == SnapEdge.Left
            ? System.Windows.HorizontalAlignment.Left
            : System.Windows.HorizontalAlignment.Right;

    partial void OnSelectedCategoryChanged(CategoryViewModel? value)
    {
        RefreshFilteredItems();
    }

    partial void OnIsExpandedChanged(bool value)
    {
        Settings.IsExpanded = value;
        Save();
        OnPropertyChanged(nameof(ShowEdgeCollapsedIndicator));
        ExpansionChanged?.Invoke(value);
    }

    public void NotifySnapStateChanged()
    {
        OnPropertyChanged(nameof(ShowEdgeCollapsedIndicator));
        OnPropertyChanged(nameof(CollapsedStripAlignment));
    }

    public event Action<bool>? ExpansionChanged;

    private void LoadCategories()
    {
        Categories = new ObservableCollection<CategoryViewModel>(
            _data.Categories.OrderBy(c => c.SortOrder).Select(c => new CategoryViewModel(c)));

        SelectedCategory = Categories.FirstOrDefault();
    }

    private void LoadItems()
    {
        RefreshFilteredItems();
    }

    private void RefreshFilteredItems()
    {
        if (SelectedCategory == null)
        {
            FilteredItems = [];
            return;
        }

        FilteredItems = new ObservableCollection<LaunchItemViewModel>(
            _data.Items
                .Where(i => i.CategoryId == SelectedCategory.Id)
                .OrderBy(i => i.SortOrder)
                .Select(i => new LaunchItemViewModel(i)));
    }

    [RelayCommand]
    private void OpenItem(LaunchItemViewModel? item)
    {
        if (item == null)
        {
            return;
        }

        LaunchService.Open(item.Model);
    }

    [RelayCommand]
    private void AddItems()
    {
        var dialog = new AddItemsDialog();
        if (DialogHelper.ShowDialog(dialog) == true)
        {
            foreach (var path in dialog.SelectedPaths)
            {
                AddItemFromPath(path);
            }
        }
    }

    public void EditLaunchItem(LaunchItemViewModel item) => EditItem(item);

    public void DeleteLaunchItem(LaunchItemViewModel item) => DeleteItem(item);

    public void RenameCategoryItem(CategoryViewModel category) => RenameCategory(category);

    public void DeleteCategoryItem(CategoryViewModel category) => DeleteCategory(category);

    public void AddItemFromPath(string path, int insertIndex = -1)
    {
        if (SelectedCategory == null)
        {
            return;
        }

        if (_data.Items.Any(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var item = LaunchService.CreateFromPath(path, SelectedCategory.Id);
        _data.Items.Add(item);

        var list = FilteredItems.ToList();
        var vm = new LaunchItemViewModel(item);

        if (insertIndex < 0 || insertIndex > list.Count)
        {
            insertIndex = list.Count;
        }

        list.Insert(insertIndex, vm);
        ApplySortOrders(list);
        FilteredItems = new ObservableCollection<LaunchItemViewModel>(list);
        Save();
    }

    public void MoveItem(string itemId, int insertIndex)
    {
        if (SelectedCategory == null)
        {
            return;
        }

        var list = FilteredItems.ToList();
        var oldIndex = list.FindIndex(i => i.Id == itemId);
        if (oldIndex < 0)
        {
            return;
        }

        if (insertIndex < 0)
        {
            insertIndex = 0;
        }

        if (insertIndex > list.Count)
        {
            insertIndex = list.Count;
        }

        if (oldIndex < insertIndex)
        {
            insertIndex--;
        }

        if (oldIndex == insertIndex)
        {
            return;
        }

        var item = list[oldIndex];
        list.RemoveAt(oldIndex);
        list.Insert(insertIndex, item);
        ApplySortOrders(list);
        FilteredItems = new ObservableCollection<LaunchItemViewModel>(list);
        Save();
    }

    private void ApplySortOrders(IList<LaunchItemViewModel> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            items[i].Model.SortOrder = i;
        }
    }

    [RelayCommand]
    private void DeleteItem(LaunchItemViewModel? item)
    {
        if (item == null)
        {
            return;
        }

        var result = DialogHelper.ShowQuestion($"确定删除 \"{item.Name}\" 吗？");

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _data.Items.RemoveAll(i => i.Id == item.Id);
        RefreshFilteredItems();
        Save();
    }

    [RelayCommand]
    private void EditItem(LaunchItemViewModel? item)
    {
        if (item == null)
        {
            return;
        }

        var dialog = new ItemEditDialog(item);
        if (DialogHelper.ShowDialog(dialog) == true)
        {
            item.RefreshIcon();
            OnPropertyChanged(nameof(FilteredItems));
            Save();
        }
    }

    [RelayCommand]
    private void AddCategory()
    {
        var dialog = new InputDialog("新建分类", "分类名称:", "新分类");
        if (DialogHelper.ShowDialog(dialog) == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            var category = new Category
            {
                Name = dialog.InputText.Trim(),
                SortOrder = _data.Categories.Count
            };
            _data.Categories.Add(category);
            Categories.Add(new CategoryViewModel(category));
            SelectedCategory = Categories.Last();
            Save();
        }
    }

    [RelayCommand]
    private void RenameCategory(CategoryViewModel? category)
    {
        if (category == null)
        {
            return;
        }

        var dialog = new InputDialog("重命名分类", "分类名称:", category.Name);
        if (DialogHelper.ShowDialog(dialog) == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            category.Name = dialog.InputText.Trim();
            Save();
        }
    }

    [RelayCommand]
    private void DeleteCategory(CategoryViewModel? category)
    {
        if (category == null || Categories.Count <= 1)
        {
            DialogHelper.ShowInfo("至少保留一个分类。");
            return;
        }

        var result = DialogHelper.ShowQuestion(
            $"确定删除分类 \"{category.Name}\" 及其所有项目吗？");

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _data.Items.RemoveAll(i => i.CategoryId == category.Id);
        _data.Categories.RemoveAll(c => c.Id == category.Id);
        Categories.Remove(category);
        SelectedCategory = Categories.FirstOrDefault();
        RefreshFilteredItems();
        Save();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var dialog = new SettingsWindow(_data.Settings, SaveSettings);
        DialogHelper.ShowDialog(dialog);
    }

    [RelayCommand]
    private void HideToTray()
    {
        Save();
        HideToTrayRequested?.Invoke();
    }

    public event Action? HideToTrayRequested;

    public void Save()
    {
        _dataStore.Save(_data);
    }

    private void SaveSettings(AppSettings settings)
    {
        _data.Settings = settings;
        Save();
        SettingsChanged?.Invoke();
    }

    public event Action? SettingsChanged;

    private void SyncStartupState()
    {
        Settings.StartWithWindows = StartupHelper.IsStartupEnabled();
    }

    public void SetExpanded(bool expanded)
    {
        if (IsExpanded != expanded)
        {
            IsExpanded = expanded;
        }
    }
}
