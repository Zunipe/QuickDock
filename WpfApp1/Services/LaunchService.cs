using System.Diagnostics;
using System.IO;
using WpfApp1.Models;

namespace WpfApp1.Services;

public static class LaunchService
{
    public static void Open(LaunchItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Path))
        {
            return;
        }

        try
        {
            if (item.ItemType == LaunchItemType.Folder)
            {
                OpenFolder(item.Path);
                return;
            }

            OpenFileOrApplication(item.Path);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                DialogHelper.OwnerWindow,
                $"无法打开: {item.Path}\n{ex.Message}",
                "QuickDock",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    private static void OpenFolder(string path)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{path}\"",
            UseShellExecute = true
        };
        Process.Start(psi);
    }

    private static void OpenFileOrApplication(string path)
    {
        var psi = new ProcessStartInfo
        {
            UseShellExecute = true
        };

        if (TryResolveShortcut(path, out var targetPath, out var arguments, out var shortcutWorkingDir))
        {
            psi.FileName = targetPath;
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                psi.Arguments = arguments;
            }

            psi.WorkingDirectory = GetWorkingDirectory(shortcutWorkingDir, targetPath);
        }
        else
        {
            psi.FileName = path;
            psi.WorkingDirectory = GetWorkingDirectory(null, path);
        }

        Process.Start(psi);
    }

    private static string? GetWorkingDirectory(string? preferredDirectory, string targetPath)
    {
        if (!string.IsNullOrWhiteSpace(preferredDirectory) && Directory.Exists(preferredDirectory))
        {
            return preferredDirectory;
        }

        return Path.GetDirectoryName(targetPath);
    }

    private static bool TryResolveShortcut(
        string path,
        out string targetPath,
        out string? arguments,
        out string? workingDirectory)
    {
        targetPath = string.Empty;
        arguments = null;
        workingDirectory = null;

        if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                return false;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(path);

            targetPath = shortcut.TargetPath;
            arguments = shortcut.Arguments;
            workingDirectory = shortcut.WorkingDirectory;

            return !string.IsNullOrWhiteSpace(targetPath);
        }
        catch
        {
            return false;
        }
    }

    public static LaunchItem CreateFromPath(string path, string categoryId)
    {
        var itemType = IconHelper.DetectType(path);
        return new LaunchItem
        {
            Name = IconHelper.GetDisplayName(path),
            Path = Path.GetFullPath(path),
            ItemType = itemType,
            CategoryId = categoryId
        };
    }
}
