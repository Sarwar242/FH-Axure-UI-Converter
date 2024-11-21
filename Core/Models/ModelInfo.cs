
using Core.Models.Enums;

namespace Core.Models;

public class ModelInfo
{
    public string Id { get; set; }
    public string? Name { get; set; }
    public ClassTypes? ClassType { get; set; }
    public string? AccessModifier { get; set; }
    public List<PropertyInfo> Properties { get; set; }
}
