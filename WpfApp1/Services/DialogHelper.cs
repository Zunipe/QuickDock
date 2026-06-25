using System.Windows;

namespace WpfApp1.Services;

public static class DialogHelper
{
    public static System.Windows.Window? OwnerWindow => System.Windows.Application.Current.MainWindow;

    public static bool? ShowDialog(System.Windows.Window dialog)
    {
        PrepareDialog(dialog);
        dialog.ShowActivated = true;

        bool? result = null;
        WithOwnerTopmostSuspended(() => result = dialog.ShowDialog());
        return result;
    }

    public static MessageBoxResult ShowQuestion(string message, string title = "QuickDock")
    {
        MessageBoxResult result = MessageBoxResult.No;
        WithOwnerTopmostSuspended(() =>
        {
            result = System.Windows.MessageBox.Show(
                OwnerWindow,
                message,
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
        });
        return result;
    }

    public static void ShowInfo(string message, string title = "QuickDock")
    {
        WithOwnerTopmostSuspended(() =>
        {
            System.Windows.MessageBox.Show(OwnerWindow, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    public static void ShowWarning(string message, string title = "QuickDock")
    {
        WithOwnerTopmostSuspended(() =>
        {
            System.Windows.MessageBox.Show(OwnerWindow, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        });
    }

    public static void PrepareDialog(System.Windows.Window dialog)
    {
        dialog.Owner = OwnerWindow;
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
    }

    private static void WithOwnerTopmostSuspended(Action action)
    {
        var owner = OwnerWindow;
        var wasTopmost = owner?.Topmost == true;

        if (wasTopmost && owner != null)
        {
            owner.Topmost = false;
        }

        try
        {
            action();
        }
        finally
        {
            if (wasTopmost && owner != null)
            {
                owner.Topmost = true;
                owner.Activate();
            }
        }
    }
}
