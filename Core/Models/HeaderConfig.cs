namespace Core.Models;

public class HeaderConfig
{
    public bool IsFixed { get; set; }
    public string Height { get; set; }
    public bool ShowLogo { get; set; }
    public bool ShowNavigation { get; set; }
    public bool ShowUserMenu { get; set; }
    public List<string> ActionButtons { get; set; } = new List<string>();
    public string BackgroundClass { get; set; }
}
