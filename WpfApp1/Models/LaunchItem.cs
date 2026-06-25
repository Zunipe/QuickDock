namespace WpfApp1.Models;

public class LaunchItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public LaunchItemType ItemType { get; set; }
    public string? CustomIconPath { get; set; }
    public string CategoryId { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
