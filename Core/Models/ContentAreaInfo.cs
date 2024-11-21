namespace Core.Models;

public class ContentAreaInfo
{
    public string ID { get; set; }
    public string Title { get; set; }
    public string Type { get; set; }
    public string Layout { get; set; }
    public List<string> ChildControls { get; set; } = new List<string>();
    public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    public bool IsVisible { get; set; } = true;
    public bool IsCollapsible { get; set; }
    public string ParentID { get; set; }
}
