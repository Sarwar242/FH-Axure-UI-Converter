namespace Core.Models;

public class FooterConfig
{
    public bool IsFixed { get; set; }
    public string Height { get; set; }
    public bool ShowCopyright { get; set; }
    public bool ShowLinks { get; set; }
    public string BackgroundClass { get; set; }
    public List<string> FooterSections { get; set; } = new List<string>();
}