namespace Core.Models;

public class MethodInfo
{
    public string Name { get; set; }
    public List<ParameterInfo> Parameters { get; set; }
    public string Body { get; set; }
    public string ReturnType { get; set; }
    public string Modifiers { get; set; }
    //public string? Namespace { get; set; }
    //public string? ClassName { get; set; }
}