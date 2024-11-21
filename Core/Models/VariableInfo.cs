namespace Core.Models;

public class VariableInfo
{
    public string Name { get; set; }
    public string DataType { get; set; }
    public string InitialValue { get; set; }
    public string? AccessModifier { get; set; }
    public bool IsNullable { get; set; }
    public bool IsTracked { get; set; }
    public string ControlType { get; set; }
    public bool IsStatic { get; set; }
    public VariableInfo()
    {
        
    }
    public VariableInfo(string name, string dataType, string initialValue = "\"\"", string modifier= "private", bool isNullable=true, bool isStatic = false, bool isTracked = false, string controlType = "")
    {
        Name = name;
        DataType = dataType;
        InitialValue = FormatInitialValue(initialValue);
        AccessModifier = modifier;
        IsTracked = isTracked;
        IsNullable = isNullable;
        IsStatic = isStatic;
        ControlType = controlType;
    }

    public static string FormatInitialValue(string initialValue)
    {
        if (string.IsNullOrEmpty(initialValue))
            return "\"\"";

        // Remove System namespace references
        initialValue = initialValue.Replace("System.", "");

        // Handle special cases
        if (initialValue.Contains("String.Empty"))
            return "\"\"";

        if (initialValue.Contains("DateTime.Now"))
            return "DateTime.Now";

        if (initialValue.Contains("Guid.Empty"))
            return "Guid.Empty";

        // Handle numeric types
        if (decimal.TryParse(initialValue, out _))
            return initialValue;

        if (int.TryParse(initialValue, out _))
            return initialValue;

        // Default case - return as string
        return initialValue;
    }
}
