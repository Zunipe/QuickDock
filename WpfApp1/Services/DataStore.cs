using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WpfApp1.Models;

namespace WpfApp1.Services;

public class DataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _dataPath;

    public DataStore()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "QuickDock");
        Directory.CreateDirectory(folder);
        _dataPath = Path.Combine(folder, "data.json");
    }

    public AppData Load()
    {
        if (!File.Exists(_dataPath))
        {
            return CreateDefault();
        }

        try
        {
            var json = File.ReadAllText(_dataPath);
            json = json
                .Replace("\"snapEdge\": \"top\"", "\"snapEdge\": \"right\"", StringComparison.OrdinalIgnoreCase)
                .Replace("\"snapEdge\": \"bottom\"", "\"snapEdge\": \"right\"", StringComparison.OrdinalIgnoreCase);
            var data = JsonSerializer.Deserialize<AppData>(json, JsonOptions);
            if (data != null)
            {
                SanitizeSettings(data.Settings);
                return data;
            }
            return CreateDefault();
        }
        catch
        {
            return CreateDefault();
        }
    }

    public void Save(AppData data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(_dataPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存配置失败: {ex.Message}");
        }
    }

    private static void SanitizeSettings(AppSettings settings)
    {
        settings.ExpandedWidth = Math.Clamp(settings.ExpandedWidth, 310, 800);
        if (settings.ExpandedWidth <= 320)
        {
            settings.ExpandedWidth = 360;
        }
        settings.ExpandedHeight = Math.Clamp(settings.ExpandedHeight, 360, 900);
    }

    private static AppData CreateDefault()
    {
        var defaultCategory = new Category { Name = "默认", SortOrder = 0 };
        return new AppData
        {
            Categories = [defaultCategory],
            Items = [],
            Settings = new AppSettings()
        };
    }
}
