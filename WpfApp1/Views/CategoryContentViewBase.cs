using System.Windows.Controls;
using WpfApp1.ViewModels;

namespace WpfApp1.Views;

public class CategoryContentViewBase : UserControl
{
    protected MainViewModel? ViewModel => DataContext as MainViewModel;
}
