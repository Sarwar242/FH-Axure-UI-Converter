namespace Core.Models;

public class FormElementInfo
{
    public string ID { get; set; }
    public string Label { get; set; }
    public string Type { get; set; }
    public bool IsRequired { get; set; }
    public bool IsDisabled { get; set; }
    public string DefaultValue { get; set; }
    public string Placeholder { get; set; }
    public string ValidationMessage { get; set; }
    public Dictionary<string, string> ValidationRules { get; set; } = new Dictionary<string, string>();
    public List<DropdownOption> Options { get; set; } = new List<DropdownOption>();
    public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
    public string GroupName { get; set; }
    public string DataSource { get; set; }
    public string OnChangeHandler { get; set; }
}
