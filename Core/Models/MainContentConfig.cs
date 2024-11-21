namespace Core.Models;

public class MainContentConfig
{
    public string Padding { get; set; }
    public string MaxWidth { get; set; }
    public bool HasScrollbar { get; set; }
    public string BackgroundClass { get; set; }
    public List<string> ContentSections { get; set; } = new List<string>();
    public Dictionary<string, string> GridLayout { get; set; } = new Dictionary<string, string>();
}
