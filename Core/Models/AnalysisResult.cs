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
    public List<FormElementInfo> FormElements { get; set; } = new List<FormElementInfo>();
    public List<NavigationInfo> NavigationElements { get; set; } = new List<NavigationInfo>();

    // Layout Information
    public LayoutInfo Layout { get; set; } = new LayoutInfo();
    public List<ContentAreaInfo> ContentAreas { get; set; } = new List<ContentAreaInfo>();

    // Scripting and Functionality
    public List<string> Scripts { get; set; } = new List<string>();
    public List<FunctionInfo> ScriptAnalysis { get; set; } = new List<FunctionInfo>();
    public List<EventHandlerInfo> EventHandlers { get; set; } = new List<EventHandlerInfo>();

    // Legacy/Compatibility Properties
    public List<string> PageDirectives { get; set; } = new List<string>();
    public List<CustomControl> CustomControls { get; set; } = new List<CustomControl>();
    public List<string> ContentPlaceholders { get; set; } = new List<string>();
    public CodeBehindAnalysis? CodeBehindAnalysis { get; set; }

    // Additional Analysis Properties
    public Dictionary<string, List<ValidationRule>> ValidationRules { get; set; } = new Dictionary<string, List<ValidationRule>>();
    public Dictionary<string, List<string>> Dependencies { get; set; } = new Dictionary<string, List<string>>();
    public List<PopupInfo> Popups { get; set; } = new List<PopupInfo>();
    public List<GridLayoutInfo> GridLayouts { get; set; } = new List<GridLayoutInfo>();
    public Dictionary<string, string> PageMetadata { get; set; } = new Dictionary<string, string>();
    public UIStateInfo UIState { get; set; } = new UIStateInfo();
}

public class ValidationRule
{
    public string ControlId { get; set; }
    public string RuleType { get; set; }
    public string ErrorMessage { get; set; }
    public string Condition { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
}

public class PopupInfo
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string TriggerControlId { get; set; }
    public bool IsModal { get; set; }
    public List<string> ContentControls { get; set; } = new List<string>();
    public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
    public List<ButtonInfo> Actions { get; set; } = new List<ButtonInfo>();
}

public class GridLayoutInfo
{
    public string Id { get; set; }
    public string ContainerId { get; set; }
    public List<string> Areas { get; set; } = new List<string>();
    public Dictionary<string, string> ColumnDefinitions { get; set; } = new Dictionary<string, string>();
    public Dictionary<string, string> RowDefinitions { get; set; } = new Dictionary<string, string>();
    public List<GridItemInfo> Items { get; set; } = new List<GridItemInfo>();
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

public class UIStateInfo
{
    public Dictionary<string, bool> Visibility { get; set; } = new Dictionary<string, bool>();
    public Dictionary<string, bool> EnabledState { get; set; } = new Dictionary<string, bool>();
    public Dictionary<string, string> DefaultValues { get; set; } = new Dictionary<string, string>();
    public List<DependencyRule> DependencyRules { get; set; } = new List<DependencyRule>();
    public Dictionary<string, List<string>> ValidationGroups { get; set; } = new Dictionary<string, List<string>>();
}

public class DependencyRule
{
    public string SourceControlId { get; set; }
    public string TargetControlId { get; set; }
    public string DependencyType { get; set; }
    public string Condition { get; set; }
    public string Action { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
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