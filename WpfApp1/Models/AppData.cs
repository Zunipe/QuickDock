namespace WpfApp1.Models;

public class AppData
{
    public List<Category> Categories { get; set; } = [];
    public List<LaunchItem> Items { get; set; } = [];
    public AppSettings Settings { get; set; } = new();
}
