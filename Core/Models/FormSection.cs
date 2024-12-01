namespace Core.Models;

public class FormSection
{
    public string PanelId { get; set; }
    public string PanelLabel { get; set; }
    public List<CustomControl> Fields { get; set; } = new();
    public GridInfo AssociatedGrid { get; set; }
    public string AddButtonId { get; set; }
    public string UpdateButtonId { get; set; }
}
