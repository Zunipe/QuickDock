using System.IO;
using System.Windows;
using System.Windows.Input;
using WpfApp1.Services;
using WpfApp1.ViewModels;

namespace WpfApp1.Views;

public partial class ShortcutCategoryView : CategoryContentViewBase
{
    private LaunchItemViewModel? _pendingItemDrag;
    private System.Windows.Point _itemDragStart;

    public ShortcutCategoryView()
    {
        InitializeComponent();
    }

    private void ItemsArea_DragOver(object sender, DragEventArgs e)
    {
        UpdateDragEffects(e);
        e.Handled = true;
    }

    private void ItemsArea_Drop(object sender, DragEventArgs e)
    {
        var insertIndex = ItemDragDropHelper.GetInsertIndexFromItemsControl(
            LaunchItemsControl,
            e.GetPosition(LaunchItemsControl));
        HandleDrop(e, insertIndex);
    }

    private void ItemCard_DragOver(object sender, DragEventArgs e)
    {
        UpdateDragEffects(e);
        e.Handled = true;
    }

    private void ItemCard_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel == null
            || sender is not FrameworkElement { Tag: LaunchItemViewModel targetItem } targetElement)
        {
            return;
        }

        var itemIndex = ViewModel.FilteredItems.IndexOf(targetItem);
        if (itemIndex < 0)
        {
            return;
        }

        var insertIndex = ItemDragDropHelper.GetInsertIndexRelativeToItem(
            itemIndex,
            e.GetPosition(targetElement),
            targetElement.ActualWidth);
        HandleDrop(e, insertIndex);
    }

    private void HandleDrop(DragEventArgs e, int insertIndex)
    {
        if (ViewModel == null)
        {
            return;
        }

        if (IsInternalItemDrag(e)
            && e.Data.GetData(ItemDragDropFormats.LaunchItemId) is string itemId
            && !string.IsNullOrEmpty(itemId))
        {
            ViewModel.MoveItem(itemId, insertIndex);
            e.Handled = true;
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return;
        }

        var nextIndex = insertIndex;
        foreach (var path in files)
        {
            if (File.Exists(path) || Directory.Exists(path))
            {
                ViewModel.AddItemFromPath(path, nextIndex);
                nextIndex++;
            }
        }

        e.Handled = true;
    }

    private static void UpdateDragEffects(DragEventArgs e)
    {
        if (IsInternalItemDrag(e))
        {
            e.Effects = DragDropEffects.Move;
        }
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private static bool IsInternalItemDrag(DragEventArgs e) =>
        e.Data.GetDataPresent(ItemDragDropFormats.LaunchItemId);

    private void ItemCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: LaunchItemViewModel item })
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            _pendingItemDrag = null;
            e.Handled = true;
            LaunchService.Open(item.Model);
            return;
        }

        _pendingItemDrag = item;
        _itemDragStart = e.GetPosition(null);

        if (sender is UIElement element)
        {
            element.CaptureMouse();
        }
    }

    private void ItemCard_MouseMove(object sender, MouseEventArgs e)
    {
        if (_pendingItemDrag == null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _itemDragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _itemDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var item = _pendingItemDrag;
        _pendingItemDrag = null;

        if (sender is UIElement element && element.IsMouseCaptured)
        {
            element.ReleaseMouseCapture();
        }

        if (sender is not DependencyObject source)
        {
            return;
        }

        var data = new DataObject();
        data.SetData(ItemDragDropFormats.LaunchItemId, item.Id);

        if (!string.IsNullOrWhiteSpace(item.Path)
            && (File.Exists(item.Path) || Directory.Exists(item.Path)))
        {
            data.SetData(DataFormats.FileDrop, new[] { item.Path });
            DragDrop.DoDragDrop(
                source,
                data,
                DragDropEffects.Move | DragDropEffects.Copy | DragDropEffects.Link);
            return;
        }

        DragDrop.DoDragDrop(source, data, DragDropEffects.Move);
    }

    private void ItemCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _pendingItemDrag = null;

        if (sender is UIElement element && element.IsMouseCaptured)
        {
            element.ReleaseMouseCapture();
        }
    }

    private void ContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.ContextMenu { PlacementTarget: FrameworkElement target } menu)
        {
            menu.Tag = target.DataContext ?? target.Tag;
        }
    }

    private void EditItemMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null && GetContextMenuTarget(sender) is LaunchItemViewModel item)
        {
            ViewModel.EditLaunchItem(item);
        }
    }

    private void DeleteItemMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null && GetContextMenuTarget(sender) is LaunchItemViewModel item)
        {
            ViewModel.DeleteLaunchItem(item);
        }
    }

    private static object? GetContextMenuTarget(object sender)
    {
        return sender is System.Windows.Controls.MenuItem
        {
            Parent: System.Windows.Controls.ContextMenu menu
        }
            ? menu.Tag
            : null;
    }
}
