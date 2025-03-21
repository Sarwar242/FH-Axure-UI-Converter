﻿namespace Core.Models;
public class CustomControl
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }  // TextBox, Button, DropDown, Label 
    public string LabelId { get; set; }  // Related label control's ID
    public string LabelText { get; set; } // Text to display as label
    public string DataLabel { get; set; }  // From data-label attribute
    public Dictionary<string, string>? Attributes { get; set; } = new Dictionary<string, string>();
    public List<string> Options { get; set; } = new();  // For dropdowns
    public bool IsGenerated { get; set; } = false;
    public bool IsDisabled { get; set; } = false;
    public bool IsHidden { get; set; } = false;
    public bool IsSelected { get; set; } = false;
}