namespace Core.Models;

public class GridInfo
{
    public string Id { get; set; }
    public string? DataLabel { get; set; }
    public List<string> ColumnsDataLbls { get; set; }
    public List<string> Columns { get; set; }
}
