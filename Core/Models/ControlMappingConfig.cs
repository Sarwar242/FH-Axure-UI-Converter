namespace Core.Models;

public class ComponentMappingConfig
{
    public List<ComponentMapping> Components { get; set; } = new List<ComponentMapping>();
}

public class ComponentMapping
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string Description { get; set; }
    public bool HasLabelText { get; set; }
    public bool HasInnerText { get; set; }
    public bool NeedVisibilityBlock { get; set; }
    public bool IsContainer { get; set; }
    public List<string> HtmlClasses { get; set; } = new List<string>();
    public List<string> HtmlAttributes { get; set; } = new List<string>();
    public Dictionary<string, string> DefaultAttributes { get; set; } = new Dictionary<string, string>();
    public List<ComponentEvent> Events { get; set; } = new List<ComponentEvent>();
    public Dictionary<string, string> Styles { get; set; } = new Dictionary<string, string>();
    public List<string> RequiredVariables { get; set; } = new List<string>();
}

public class ComponentEvent
{
    public string EventName { get; set; }
    public string SourceAttribute { get; set; }
    public string TargetEventName { get; set; }
    public bool RequiresStateHasChanged { get; set; }
    public string DefaultHandler { get; set; }
    public string? Body { get; set; }
    public List<string> RequiredParameters { get; set; } = new List<string>();
}