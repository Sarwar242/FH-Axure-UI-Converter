namespace Core.Models;

public class LayoutStructure
{
    public HeaderConfig Header { get; set; } = new HeaderConfig();
    public SidebarConfig Sidebar { get; set; } = new SidebarConfig();
    public MainContentConfig MainContent { get; set; } = new MainContentConfig();
    public FooterConfig Footer { get; set; } = new FooterConfig();
}
