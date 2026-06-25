using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfApp1.Models;
using WpfApp1.Services;
using WpfApp1.ViewModels;

namespace WpfApp1;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly EdgeSnapManager _snapManager = new();
    private readonly EdgeSummonService _summonService = new();
    private bool _isDragging;
    private System.Windows.Point _dragStart;
    private LaunchItemViewModel? _pendingItemDrag;
    private System.Windows.Point _itemDragStart;
    private CategoryViewModel? _rightClickCategory;

    private const double ExpandedMinWidth = 310;
    private const double ExpandedMinHeight = 360;

    public MainWindow()
    {
        InitializeComponent();
        var windowIcon = TrayIconService.GetWindowIconSource();
        Icon = windowIcon;
        TitleBarIcon.Source = windowIcon;
        _viewModel = (MainViewModel)DataContext;
        _viewModel.ExpansionChanged += OnExpansionChanged;
        _viewModel.SettingsChanged += OnSettingsChanged;
        _viewModel.HideToTrayRequested += HideToTray;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        LocationChanged += (_, _) => SaveWindowPosition();
        SizeChanged += (_, _) =>
        {
            if (_viewModel.IsExpanded)
            {
                _viewModel.Settings.ExpandedWidth = Width;
                _viewModel.Settings.ExpandedHeight = Height;
            }
        };
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplySettings();

        if (_viewModel.Settings.EnableEdgeSnap && _viewModel.Settings.SnapEdge != SnapEdge.None)
        {
            _viewModel.SetExpanded(false);
            ApplyCollapsedState();
        }
        else
        {
            _viewModel.SetExpanded(_viewModel.Settings.IsExpanded);
            MinWidth = ExpandedMinWidth;
            MinHeight = ExpandedMinHeight;
        }

        StartSummonService();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveWindowPosition();
        _viewModel.Save();
        _summonService.Dispose();
    }

    public void HideToTray()
    {
        SaveWindowPosition();
        _viewModel.Save();
        Hide();
        ShowInTaskbar = false;
    }

    public void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;

        if (_viewModel.IsExpanded)
        {
            ShowInTaskbar = true;
        }
        else if (_viewModel.Settings.EnableEdgeSnap && _viewModel.Settings.SnapEdge != SnapEdge.None)
        {
            ShowInTaskbar = false;
        }
        else
        {
            ShowInTaskbar = true;
        }

        Activate();
        Focus();
        Topmost = true;
    }

    private void OnExpansionChanged(bool expanded)
    {
        if (expanded)
        {
            ApplyExpandedState();
        }
        else
        {
            ApplyCollapsedState();
        }
    }

    private void OnSettingsChanged()
    {
        _summonService.UpdateSettings(_viewModel.Settings);
        _viewModel.NotifySnapStateChanged();

        if (_viewModel.Settings.EnableEdgeSnap && _viewModel.Settings.SnapEdge != SnapEdge.None)
        {
            if (_viewModel.IsExpanded)
            {
                ApplyExpandedState();
            }
            else
            {
                ApplyCollapsedState();
            }
        }
        else
        {
            ShowInTaskbar = true;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            MinWidth = ExpandedMinWidth;
            MinHeight = ExpandedMinHeight;
            ApplySettings();
        }
    }

    private void ApplySettings()
    {
        var settings = _viewModel.Settings;

        if (settings.EnableEdgeSnap && settings.SnapEdge != SnapEdge.None)
        {
            _snapManager.ApplySnap(this, settings, collapse: !_viewModel.IsExpanded);
        }
        else if (settings.WindowLeft is double left && settings.WindowTop is double top)
        {
            Left = left;
            Top = top;
            Width = settings.ExpandedWidth;
            Height = settings.ExpandedHeight;
        }
    }

    private void ApplyExpandedState()
    {
        ShowInTaskbar = true;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        MinWidth = ExpandedMinWidth;
        MinHeight = ExpandedMinHeight;
        _snapManager.ApplySnap(this, _viewModel.Settings, collapse: false);
        _summonService.IsExpanded = true;
        Activate();
    }

    private void ApplyCollapsedState()
    {
        if (_viewModel.IsExpanded)
        {
            _viewModel.Settings.ExpandedWidth = Math.Max(Width, ExpandedMinWidth);
            _viewModel.Settings.ExpandedHeight = Math.Max(Height, ExpandedMinHeight);
        }

        if (_viewModel.Settings.EnableEdgeSnap && _viewModel.Settings.SnapEdge != SnapEdge.None)
        {
            _viewModel.Settings.WindowTop = Top;

            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            MinWidth = 0;
            MinHeight = 0;
            _snapManager.ApplySnap(this, _viewModel.Settings, collapse: true);
        }

        _summonService.IsExpanded = false;
    }

    private void StartSummonService()
    {
        _summonService.IsExpanded = _viewModel.IsExpanded;
        _summonService.Start(
            this,
            _viewModel.Settings,
            GetPhysicalBounds,
            ExpandFromEdge,
            CollapseToEdge);
    }

    private NativeRect GetPhysicalBounds()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        return new NativeRect
        {
            Left = (int)(Left * dpi.DpiScaleX),
            Top = (int)(Top * dpi.DpiScaleY),
            Right = (int)((Left + ActualWidth) * dpi.DpiScaleX),
            Bottom = (int)((Top + ActualHeight) * dpi.DpiScaleY)
        };
    }

    private void ExpandFromEdge()
    {
        if (!_viewModel.IsExpanded)
        {
            _viewModel.SetExpanded(true);
        }
    }

    private void CollapsedStrip_DragEnter(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
            ExpandFromEdge();
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void CollapsedStrip_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
            if (!_viewModel.IsExpanded)
            {
                ExpandFromEdge();
            }
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void CollapsedStrip_Drop(object sender, System.Windows.DragEventArgs e)
    {
        ExpandFromEdge();
        HandleItemsDrop(e, _viewModel.FilteredItems.Count);
    }

    private void CollapseToEdge()
    {
        if (_viewModel.IsExpanded
            && _viewModel.Settings.EnableEdgeSnap
            && _viewModel.Settings.SnapEdge != SnapEdge.None)
        {
            _viewModel.SetExpanded(false);
        }
    }

    private void SaveWindowPosition()
    {
        if (_viewModel.Settings.SnapEdge == SnapEdge.None)
        {
            _viewModel.Settings.WindowLeft = Left;
            _viewModel.Settings.WindowTop = Top;
        }
        else if (_viewModel.Settings.EnableEdgeSnap)
        {
            _viewModel.Settings.WindowTop = Top;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            return;
        }

        if (!_viewModel.IsExpanded)
        {
            e.Handled = true;
            return;
        }

        _isDragging = true;
        _dragStart = e.GetPosition(this);
        CaptureMouse();
    }

    private void RootBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.IsExpanded)
        {
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_isDragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPos = PointToScreen(e.GetPosition(this));
        Left = currentPos.X - _dragStart.X;
        Top = currentPos.Y - _dragStart.Y;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        ReleaseMouseCapture();
        HandleDragEnd();
    }

    private void HandleDragEnd()
    {
        var settings = _viewModel.Settings;

        if (!settings.EnableEdgeSnap)
        {
            SaveWindowPosition();
            return;
        }

        var detected = _snapManager.DetectSnapEdge(this);
        settings.SnapEdge = detected;

        if (detected == SnapEdge.None)
        {
            EnterFreeMode();
        }
        else
        {
            EnterSnapMode(detected);
        }

        _summonService.UpdateSettings(settings);
        _viewModel.NotifySnapStateChanged();
        _viewModel.Save();
    }

    private void EnterFreeMode()
    {
        var settings = _viewModel.Settings;

        ShowInTaskbar = true;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        MinWidth = ExpandedMinWidth;
        MinHeight = ExpandedMinHeight;

        if (!_viewModel.IsExpanded)
        {
            _viewModel.SetExpanded(true);
        }

        settings.ExpandedWidth = Math.Max(Width, ExpandedMinWidth);
        settings.ExpandedHeight = Math.Max(Height, ExpandedMinHeight);
        settings.WindowLeft = Left;
        settings.WindowTop = Top;
        _summonService.IsExpanded = true;
    }

    private void EnterSnapMode(SnapEdge edge)
    {
        var settings = _viewModel.Settings;
        settings.SnapEdge = edge;
        settings.WindowTop = Top;
        settings.ExpandedWidth = Math.Max(Width, ExpandedMinWidth);
        settings.ExpandedHeight = Math.Max(Height, ExpandedMinHeight);

        if (_viewModel.IsExpanded)
        {
            ApplyExpandedState();
        }
        else
        {
            ApplyCollapsedState();
        }
    }

    private void RootBorder_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (IsInternalItemDrag(e) || e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = IsInternalItemDrag(e)
                ? System.Windows.DragDropEffects.Move
                : System.Windows.DragDropEffects.Copy;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void RootBorder_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] files)
        {
            return;
        }

        var insertIndex = _viewModel.FilteredItems.Count;
        foreach (var path in files)
        {
            if (File.Exists(path) || Directory.Exists(path))
            {
                _viewModel.AddItemFromPath(path, insertIndex);
                insertIndex++;
            }
        }

        e.Handled = true;
    }

    private void LaunchItemsArea_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        UpdateItemsDragEffects(e);
        e.Handled = true;
    }

    private void LaunchItemsArea_Drop(object sender, System.Windows.DragEventArgs e)
    {
        var insertIndex = ItemDragDropHelper.GetInsertIndexFromItemsControl(
            LaunchItemsControl,
            e.GetPosition(LaunchItemsControl));
        HandleItemsDrop(e, insertIndex);
    }

    private void ItemCard_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        UpdateItemsDragEffects(e);
        e.Handled = true;
    }

    private void ItemCard_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: LaunchItemViewModel targetItem } targetElement)
        {
            return;
        }

        var itemIndex = _viewModel.FilteredItems.IndexOf(targetItem);
        if (itemIndex < 0)
        {
            return;
        }

        var insertIndex = ItemDragDropHelper.GetInsertIndexRelativeToItem(
            itemIndex,
            e.GetPosition(targetElement),
            targetElement.ActualWidth);
        HandleItemsDrop(e, insertIndex);
    }

    private void HandleItemsDrop(System.Windows.DragEventArgs e, int insertIndex)
    {
        if (IsInternalItemDrag(e)
            && e.Data.GetData(ItemDragDropFormats.LaunchItemId) is string itemId
            && !string.IsNullOrEmpty(itemId))
        {
            _viewModel.MoveItem(itemId, insertIndex);
            e.Handled = true;
            return;
        }

        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] files)
        {
            return;
        }

        var nextIndex = insertIndex;
        foreach (var path in files)
        {
            if (File.Exists(path) || Directory.Exists(path))
            {
                _viewModel.AddItemFromPath(path, nextIndex);
                nextIndex++;
            }
        }

        e.Handled = true;
    }

    private static void UpdateItemsDragEffects(System.Windows.DragEventArgs e)
    {
        if (IsInternalItemDrag(e))
        {
            e.Effects = System.Windows.DragDropEffects.Move;
        }
        else if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
    }

    private static bool IsInternalItemDrag(System.Windows.DragEventArgs e)
    {
        return e.Data.GetDataPresent(ItemDragDropFormats.LaunchItemId);
    }

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

    private void ItemCard_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_pendingItemDrag == null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(null);
        if (!HasExceededDragThreshold(_itemDragStart, position))
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

        if (string.IsNullOrWhiteSpace(item.Path)
            || (!File.Exists(item.Path) && !Directory.Exists(item.Path)))
        {
            DragDrop.DoDragDrop(source, data, DragDropEffects.Move);
            return;
        }

        data.SetData(DataFormats.FileDrop, new[] { item.Path });
        DragDrop.DoDragDrop(
            source,
            data,
            DragDropEffects.Move | DragDropEffects.Copy | DragDropEffects.Link);
    }

    private void ItemCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _pendingItemDrag = null;

        if (sender is UIElement element && element.IsMouseCaptured)
        {
            element.ReleaseMouseCapture();
        }
    }

    private static bool HasExceededDragThreshold(System.Windows.Point start, System.Windows.Point current)
    {
        return Math.Abs(current.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(current.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance;
    }

    private void ContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.ContextMenu menu)
        {
            return;
        }

        if (menu.PlacementTarget is FrameworkElement target)
        {
            menu.Tag = target switch
            {
                ListBoxItem listBoxItem => listBoxItem.DataContext,
                ListBox when _rightClickCategory != null => _rightClickCategory,
                _ => target.DataContext ?? target.Tag
            };
        }
    }

    private void CategoryListBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _rightClickCategory = null;

        if (FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject) is ListBoxItem item)
        {
            item.IsSelected = true;
            item.Focus();
            _rightClickCategory = item.DataContext as CategoryViewModel;
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        return ItemDragDropHelper.FindVisualParent<T>(child);
    }

    private void EditItemMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextMenuTarget(sender) is LaunchItemViewModel item)
        {
            _viewModel.EditLaunchItem(item);
        }
    }

    private void DeleteItemMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextMenuTarget(sender) is LaunchItemViewModel item)
        {
            _viewModel.DeleteLaunchItem(item);
        }
    }

    private void RenameCategoryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextMenuTarget(sender) is CategoryViewModel category)
        {
            _viewModel.RenameCategoryItem(category);
        }
    }

    private void DeleteCategoryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextMenuTarget(sender) is CategoryViewModel category)
        {
            _viewModel.DeleteCategoryItem(category);
        }
    }

    private static object? GetContextMenuTarget(object sender)
    {
        if (sender is System.Windows.Controls.MenuItem { Parent: System.Windows.Controls.ContextMenu menu })
        {
            return menu.Tag;
        }

        return null;
    }
}
