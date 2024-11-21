namespace Core.Models;

public class LayoutInfo
{
    public bool HasHeader { get; set; }
    public bool HasFooter { get; set; }
    public bool HasSidebar { get; set; }
    public List<ContentAreaInfo> MainContentAreas { get; set; } = new List<ContentAreaInfo>();
    public Dictionary<string, string> PageMetadata { get; set; } = new Dictionary<string, string>();
    public List<string> StyleClasses { get; set; } = new List<string>();
    public List<NavigationInfo> Navigation { get; set; } = new List<NavigationInfo>();
    public List<string> Scripts { get; set; } = new List<string>();
    public List<string> Styles { get; set; } = new List<string>();
    public string MainContentId { get; set; }
    public string HeaderId { get; set; }
    public string FooterId { get; set; }
    public string SidebarId { get; set; }
    public Dictionary<string, List<string>> Regions { get; set; } = new Dictionary<string, List<string>>();

    public LayoutStructure Structure { get; set; } = new LayoutStructure();
}
