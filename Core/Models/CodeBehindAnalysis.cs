namespace Core.Models;

public class CodeBehindAnalysis
{
    public string ClassName { get; set; }
    public string Namespace { get; set; }
    public List<string> BaseClasses { get; set; } = new List<string>();
    public List<MethodInfo> Methods { get; set; } = new List<MethodInfo>();
    public List<PropertyInfo> Properties { get; set; } = new List<PropertyInfo>();
    public List<VariableInfo> Fields { get; set; } = new List<VariableInfo>();
    public List<EventHandlerInfo> EventHandlers { get; set; } = new List<EventHandlerInfo>();
    public MethodInfo PageLoadMethod { get; set; }
    public List<string> UsingStatements { get; set; } = new List<string>();
}