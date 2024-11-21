using Core.Models;

namespace Core.Models;

public class ControlInfo
{
    public string Type { get; set; }
    public string ID { get; set; }
    public string ControlType { get; set; }
    public string InnerText { get; set; }
    public List<ControlInfo> Children { get; set; } = new List<ControlInfo>();
    public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();

    // New properties for enhanced control info
    public string LabelText { get; set; }                   // The text to display (from label or inner content)
    public string ConnectedFieldId { get; set; }            // For labels, the ID of connected field
    public string ConnectedLabelId { get; set; }            // For fields, the ID of connected label
    public bool IsDisabled { get; set; }
    public List<DropDownOption> Options { get; set; }       // For dropdowns
    public string ButtonText { get; set; }
    public ControlInfo Parent { get; set; } // Add this line to define the Parent property
}

public class DropDownOption
{
    public string Text { get; set; }
    public string Value { get; set; }
}
