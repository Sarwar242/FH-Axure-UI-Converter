namespace Core.Models;

public class SidebarConfig
{
    public bool IsCollapsible { get; set; }
    public bool IsFixed { get; set; }
    public string Width { get; set; }
    public string Position { get; set; }
    public bool ShowUserProfile { get; set; }
    public bool ShowSearch { get; set; }
    public List<string> QuickActions { get; set; } = new List<string>();
    public string BackgroundClass { get; set; }
}
