namespace Core.Models;

public class AnalysisResult
{
    // File Information
    public string? OriginalFilePath { get; set; }
    public string? ConvertedFilePath { get; set; }

    // Basic Elements
    public List<ControlInfo> Controls { get; set; } = new List<ControlInfo>();
    public List<string> Panels { get; set; } = new List<string>();
    public List<GridInfo> Grids { get; set; } = new List<GridInfo>();
    public List<RadioInfo> Radios { get; set; } = new List<RadioInfo>();
    public List<string> HiddenFields { get; set; } = new List<string>();

    // Extended UI Elements
    public List<ButtonInfo> Buttons { get; set; } = new List<ButtonInfo>();
    public List<NavigationInfo> NavigationElements { get; set; } = new List<NavigationInfo>();

    // Layout Information
    public LayoutInfo Layout { get; set; } = new LayoutInfo();
    public List<ContentAreaInfo> ContentAreas { get; set; } = new List<ContentAreaInfo>();

    // Scripting and Functionality
    public List<string> Scripts { get; set; } = new List<string>();
    public List<FunctionInfo> ScriptAnalysis { get; set; } = new List<FunctionInfo>();
    public List<EventHandlerInfo> EventHandlers { get; set; } = new List<EventHandlerInfo>();

    // Legacy/Compatibility Properties
    public List<CustomControl> CustomControls { get; set; } = new List<CustomControl>();
    public CodeBehindAnalysis? CodeBehindAnalysis { get; set; }
    public List<FormSection>? Sections { get; set; }
}

public class GridItemInfo
{
    public string Id { get; set; }
    public string Area { get; set; }
    public string Column { get; set; }
    public string Row { get; set; }
    public string ColumnSpan { get; set; }
    public string RowSpan { get; set; }
}

public class FunctionInfo
{
    public string Name { get; set; }
    public string Definition { get; set; }
    public string Url { get; set; }
    public bool IsPopup { get; set; }
    public List<PopEventInfo> Events { get; set; } = new List<PopEventInfo>();
}

public class PopEventInfo
{
    public string ComponentId { get; set; }
    public string EventName { get; set; }
    public string EventValue { get; set; }
}