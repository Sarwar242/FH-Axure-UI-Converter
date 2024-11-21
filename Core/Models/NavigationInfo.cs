namespace Core.Models;

public class NavigationInfo
{
    public string ID { get; set; }
    public string Text { get; set; }
    public string Type { get; set; }
    public string Url { get; set; }
    public string Icon { get; set; }
    public string ParentID { get; set; }
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; } = true;
    public int Order { get; set; }
    public List<NavigationInfo> Children { get; set; } = new List<NavigationInfo>();
    public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
    public string Permission { get; set; }
    public string Target { get; set; }
    public string OnClick { get; set; }
}
