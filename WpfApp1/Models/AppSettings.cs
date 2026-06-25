namespace WpfApp1.Models;

public class AppSettings
{
    public bool StartWithWindows { get; set; }
    public bool EnableEdgeSnap { get; set; } = true;
    public SnapEdge SnapEdge { get; set; } = SnapEdge.Right;
    public SummonMode SummonMode { get; set; } = SummonMode.Proximity;
    public int ProximityThreshold { get; set; } = 20;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double ExpandedWidth { get; set; } = 360;
    public double ExpandedHeight { get; set; } = 480;
    public bool IsExpanded { get; set; }
}
