using CommunityToolkit.Mvvm.ComponentModel;
using WpfApp1.Models;

namespace WpfApp1.ViewModels;

public partial class CategoryViewModel : ObservableObject
{
    private readonly Category _category;

    public CategoryViewModel(Category category)
    {
        _category = category;
    }

    public Category Model => _category;

    public string Id => _category.Id;

    public string Name
    {
        get => _category.Name;
        set
        {
            if (_category.Name != value)
            {
                _category.Name = value;
                OnPropertyChanged();
            }
        }
    }
}
