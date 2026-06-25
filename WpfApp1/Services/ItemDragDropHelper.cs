using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfApp1.Services;

public static class ItemDragDropFormats
{
    public const string LaunchItemId = "QuickDock.LaunchItemId";
}

public static class ItemDragDropHelper
{
    public static int GetInsertIndexRelativeToItem(int itemIndex, System.Windows.Point position, double width)
    {
        return position.X > width / 2 ? itemIndex + 1 : itemIndex;
    }

    public static int GetInsertIndexFromItemsControl(ItemsControl itemsControl, System.Windows.Point positionInItemsControl)
    {
        var wrapPanel = FindVisualChild<WrapPanel>(itemsControl);
        if (wrapPanel == null || wrapPanel.Children.Count == 0)
        {
            return itemsControl.Items.Count;
        }

        var pointInPanel = itemsControl.TranslatePoint(positionInItemsControl, wrapPanel);

        for (var i = 0; i < wrapPanel.Children.Count; i++)
        {
            if (wrapPanel.Children[i] is not FrameworkElement child)
            {
                continue;
            }

            var origin = child.TranslatePoint(new System.Windows.Point(0, 0), wrapPanel);
            var rect = new Rect(origin, new System.Windows.Size(child.ActualWidth, child.ActualHeight));
            var midX = rect.X + rect.Width / 2;
            var midY = rect.Y + rect.Height / 2;

            if (pointInPanel.Y < midY || (pointInPanel.Y < rect.Bottom && pointInPanel.X < midX))
            {
                return i;
            }
        }

        return wrapPanel.Children.Count;
    }

    public static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            var nested = FindVisualChild<T>(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    public static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T match)
            {
                return match;
            }

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }
}
