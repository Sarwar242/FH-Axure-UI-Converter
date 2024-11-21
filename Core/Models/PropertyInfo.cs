namespace Core.Models;

public class PropertyInfo
{
    public string Name { get; set; }
    public string DataType { get; set; }
    public string AccessModifier { get; set; }
    public bool HasGetter { get; set; }
    public bool HasSetter { get; set; }
    public string DefaultValue { get; set; }
}
