using Core.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;

namespace Core.Generators;

public class CustomComponentGenerator
{
    private readonly ComponentMappingConfig _customControlMappings;
    private readonly HashSet<VariableInfo> _variables = new();
    private readonly Dictionary<string, string> _gridColumnFields = new(); // Maps grid columns to form fields
    private readonly StringBuilder _stateVariables = new();
    private bool _openPanel = false;
    private Dictionary<string, Dictionary<string, string>> _ddVendor = new();

    public CustomComponentGenerator(string mappingFilePath)
    {
        var json = File.ReadAllText(mappingFilePath);
        _customControlMappings = JsonConvert.DeserializeObject<ComponentMappingConfig>(json);
    }
    public string GenerateComponent(AnalysisResult analysis, string pageName)
    {
        var builder = new StringBuilder();
        _variables.Clear();
        _ddVendor.Clear();
        _gridColumnFields.Clear();
        _openPanel = false;

        // Map form fields to grid columns
        MapFieldsToGridColumns(analysis);

        // Generate component structure
        GenerateComponentHeader(builder, analysis.OriginalFilePath??pageName);
        GenerateComponentBody(builder, analysis, pageName);
        GenerateCodeBlock(builder, analysis, pageName);

        return builder.ToString();
    }

    private void MapFieldsToGridColumns(AnalysisResult analysis)
    {
        foreach (var control in analysis.CustomControls.Where(c => c.Type != null && !c.Type.Equals("label", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var grid in analysis.Grids)
            {
                if (grid.ColumnsDataLbls.Contains(control.DataLabel, StringComparer.OrdinalIgnoreCase))
                {
                    var columnName = grid.Columns[grid.ColumnsDataLbls.FindIndex(x =>
                                                string.Equals(x, control.DataLabel, StringComparison.OrdinalIgnoreCase))];
                    if (!_gridColumnFields.ContainsKey(columnName)){
                        _gridColumnFields[columnName] = control.Id;
                    }
                }
            }
        }
    }

    private void GenerateComponentHeader(StringBuilder builder, string filePath)
    {
        builder.AppendLine($"@page \"/{GenHelper.GetPageName(filePath,false)}\"");
        builder.AppendLine();
        builder.AppendLine("@using System.Text.Json");
        builder.AppendLine("@using Microsoft.AspNetCore.Components");
        builder.AppendLine("@rendermode InteractiveServer");
        builder.AppendLine();
    }

    private void GenerateComponentBody(StringBuilder builder, AnalysisResult analysis, string pageName)
    {
        var pagePathName = GenHelper.GetPageName(analysis.OriginalFilePath, false);
        var formId = pagePathName.Replace("UI", "Form");
        // Add NavMenu
        builder.AppendLine($@"<NavMenu ShowButtonDelete=""false"" 
                      ShowButtonView=""false"" 
                      PageName=""{pageName}"" 
                      ShowButtonRefresh=""true"" 
                      OnFHBtnRefreshClick=""Refresh"" 
                      OnFHBtnAddClick=""AddBtnClick"" 
                      OnFHBtnExitClick=""Exit"">
            </NavMenu>");

        // Start form
        builder.AppendLine($@"<form id=""{formId}"" onsubmit=""return false;"" class=""needs-validation"" novalidate>");
        builder.AppendLine("<div class=\"box box-primary\">");
        builder.AppendLine("    <div class=\"box-body\">");

        // Generate sections
        foreach (var section in analysis.Sections)
        {
            GenerateSection(builder, section);
            if (section.AssociatedGrid != null)
            {
                GenerateGrid(builder, section.AssociatedGrid);
            }
        }

        // Close form
        builder.AppendLine("    </div>");
        builder.AppendLine("</div>");
        builder.AppendLine($"<button type=\"submit\" style=\"visibility:hidden;\" id=\"{formId}Submit\" @onclick=\"SaveData\"></button>");
        builder.AppendLine("</form>");
        builder.AppendLine($@"
    <script>   
    function triggerHiddenButtonClick() {{       
        document.getElementById(""{formId}Submit"").click();
    }}
    </script>");
    }

    private void GenerateSection(StringBuilder builder, FormSection section)
    {
        // Panel header
        builder.AppendLine($@"<div id=""{section.PanelId}"" class=""row border my-2"">");
        builder.AppendLine($@"    <div class=""box-title"">");
        builder.AppendLine($@"        <div>");
        builder.AppendLine($@"            <h6 class=""GridTitlebar"">{section.PanelLabel}</h6>");
        builder.AppendLine($@"        </div>");
        builder.AppendLine($@"    </div>");

        // Form fields
        foreach (var field in section.Fields)
        {
            GenerateField(builder, field);
        }

        // Add/Update buttons if grid exists
        if (section.AssociatedGrid != null)
        {
            builder.AppendLine(@"    <div class=""form-group my-2"">");
            GenerateGridButtons(builder, section);
            builder.AppendLine(@"    </div>");
        }

        builder.AppendLine("</div>");
    }

    private void GenerateField(StringBuilder builder, CustomControl field)
    {
        var componentType = MapToBlazorComponent(field.Type);
        _variables.Add(new VariableInfo(field.Id, GetVariableType(field.Type), GetDefaultValue(field.Type)));

        builder.AppendLine($@"    <div class=""form-group col-3 my-2"">");
        if(field.Type == "File")
        {
            builder.AppendLine($@"        <{componentType}");
            builder.AppendLine($@"                     Visible=""{GenHelper.LowerValue(Convert.ToString(!field.IsDisabled))}"">");
            builder.AppendLine($@"        </{componentType}>");
        }
        else { 
            builder.AppendLine($@"        <{componentType} Id=""{field.Id}""");
            builder.AppendLine($@"                     Label_Text=""{field.LabelText}""");
            builder.AppendLine($@"                     @bind-Value=""{field.Id}""");

            if (field.Type == "DropDown" && field.Options != null)
            {
                builder.AppendLine($@"                     DataSource=""{field.Id}List""");
                _variables.Add(new VariableInfo($"{field.Id}List ", "List<Dropdown>", "new()", "private", false));

                Dictionary<string, string> dict = new();
                foreach (var option in field.Options)
                {
                    if (option.ToLower().Contains("-select"))
                    {
                        dict.Add(option, "");
                    }
                    else
                    {
                        dict.Add(option, $"{option.ToLower()}");
                    }
                }
                _ddVendor.Add($"{field.Id}List", dict);
            }

            if(field.Type == "TextArea")
            {
                builder.AppendLine($@"                     Disable=""{GenHelper.LowerValue(Convert.ToString(field.IsDisabled))}"">");
            }
            else
            {
                builder.AppendLine($@"                     Enable=""{GenHelper.LowerValue(Convert.ToString(!field.IsDisabled))}"">");
            }
            builder.AppendLine($@"        </{componentType}>");
        }
        builder.AppendLine($@"    </div>");
    }

    private void GenerateGrid(StringBuilder builder, GridInfo grid)
    {
        builder.AppendLine($@"<div class=""my-2 overflow-auto"" style=""max-width: 100%;"">");
        builder.AppendLine($@"    <UXC_DataGrid Id=""{grid.Id}""");
        builder.AppendLine($@"        SelectedColumns=""{grid.Id}SelectedColumns""");
        builder.AppendLine($@"        CustomColumnNames=""{grid.Id}CustomColumnNames""");
        builder.AppendLine($@"        @key=""@({grid.Id}key)""");
        builder.AppendLine($@"        DataSource=""{grid.Id}DataList""");
        builder.AppendLine($@"        ShowEditButton=""true""");
        builder.AppendLine($@"        ShowDeleteButton=""true""");
        builder.AppendLine($@"        BtnColValue=""Id""");
        builder.AppendLine($@"        OnFHEditClick=""OnGrid{grid.Id}Edit""");
        builder.AppendLine($@"        OnFHDeleteClick=""OnGrid{grid.Id}Delete"" />");
        builder.AppendLine($@"</div>");

        _variables.Add(new VariableInfo($"{grid.Id}DataList", $"List<{GenHelper.CapitalizeFirstLetter(grid.Id)}Model>", $"new List<{GenHelper.CapitalizeFirstLetter(grid.Id)}Model>()", "private", false));
        _variables.Add(new VariableInfo($"{grid.Id}Model", $"{GenHelper.CapitalizeFirstLetter(grid.Id)}Model", $"new {GenHelper.CapitalizeFirstLetter(grid.Id)}Model()", "private", false));
        _variables.Add(new VariableInfo($"{grid.Id}key", "Guid", "new Guid()", "private", false));
        _variables.Add(new VariableInfo($"grid{grid.Id}ModelId", "string", "\"1\"", "private", false));
        _variables.Add(new VariableInfo($"is{grid.Id}Update", "bool", "false", "private", false));
    }

    private void GenerateGridButtons(StringBuilder builder, FormSection section)
    {
        var gridId = section.AssociatedGrid.Id;

        if (section.UpdateButtonId != null)
        {
            builder.AppendLine($@"@if(is{gridId}Update){{");
            builder.AppendLine($@"    <button class=""btn btn-sm btn-info"" @onclick=""UpdateGrid{gridId}Data"">Update</button>");
            builder.AppendLine($@"}} else {{");
            builder.AppendLine($@"    <button class=""btn btn-sm btn-primary"" @onclick=""AddDataToGrid{gridId}"">Add</button>");
            builder.AppendLine($@"}}");
        }
        else
        {
            builder.AppendLine($@"<button class=""btn btn-primary"" @onclick=""AddDataToGrid{gridId}"">Add</button>");
        }
    }

    private void GenerateGridModels(StringBuilder builder, AnalysisResult analysis)
    {
        // Before generating code block, create model classes
        foreach (var grid in analysis.Grids)
        {
            builder.AppendLine($@"    public class {GenHelper.CapitalizeFirstLetter(grid.Id)}Model
    {{
        public string Id {{ get; set; }}");

            // Add properties for each column in the grid
            foreach (var column in grid.Columns)
            {
                var propertyName = GenHelper.GetColumnPropName(column);
                builder.AppendLine($"        public string? {propertyName} {{ get; set; }}");
            }

            builder.AppendLine("    }");
            builder.AppendLine();
        }
    }

    private void GenerateCodeBlock(StringBuilder builder, AnalysisResult analysis, string pageName)
    {
        builder.AppendLine("@code {");

        // Variables
        foreach (var variable in _variables)
        {
            builder.AppendLine($"    private {variable.DataType} {variable.Name} = {variable.InitialValue};");
        }
        builder.AppendLine();

        // Grid variables and methods for each grid
        foreach (var grid in analysis.Grids)
        {
            // Grid state variables
            builder.AppendLine($"    private Dictionary<string, string> {grid.Id}CustomColumnNames = new Dictionary<string, string> {{ {string.Join(", ", grid.Columns.Select(c => $"{{\"{GenHelper.GetColumnPropName(c)}\", \"{c}\"}}"))} }};");
            builder.AppendLine($"    private List<string> {grid.Id}SelectedColumns = new List<string> {{ {string.Join(", ", grid.Columns.Select(c => $"\"{GenHelper.GetColumnPropName(c)}\""))} }};");
            builder.AppendLine();

            // Generate CRUD methods for grid
            GenerateGridMethods(builder, grid);
            builder.AppendLine();
        }

        // Standard methods
        GenerateStandardMethods(builder, pageName);
        // Generate model classes
        GenerateGridModels(builder, analysis);
        builder.AppendLine("}");
    }


    private string MapToBlazorComponent(string type) => type switch
    {
        "TextBox" => "UXC_TextBox",
        "TextArea" => "UXC_TxtArea",
        "Date" => "UXC_Date",
        "Number" => "UXC_Number",
        "DropDown" => "UXC_Dynamic_Dropdown",
        "Switch" => "UXC_Switch",
        "File" => "UXC_File",
        _ => "UXC_TextBox"
    };

    private string GetVariableType(string controlType) => controlType switch
    {
        "Amount" => "decimal?",
        "AmountToWord" => "decimal?",
        "Switch" => "bool",
        _ => "string"
    };

    private string GetDefaultValue(string controlType) => controlType switch
    {

        "Date" => "\"dd/mm/yyyy\"",
        "Amount" => "0m",
        "AmountToWord" => "0m",
        "Switch" => "false",
        _ => "\"\""
    };

    // Grid variables and methods generation
    private void GenerateGridVariables(StringBuilder builder, GridInfo grid)
    {
        // Column mappings and selected columns
        builder.AppendLine($"    private Dictionary<string, string> {grid.Id}CustomColumnNames = new Dictionary<string, string> {{ {string.Join(", ", grid.Columns.Select(c => $"\"{GenHelper.GetColumnPropName(c)}\", \"{c}\""))} }};");
        builder.AppendLine($"    private List<string> {grid.Id}SelectedColumns = new List<string> {{ {string.Join(", ", grid.Columns.Select(c => $"\"{GenHelper.GetColumnPropName(c)}\""))} }};");

        // Grid state variables
        builder.AppendLine($"    private Guid {grid.Id}key = Guid.NewGuid();");
        builder.AppendLine($"    private List<{GenHelper.CapitalizeFirstLetter(grid.Id)}Model> {grid.Id}DataList = new();");
        builder.AppendLine($"    private {GenHelper.CapitalizeFirstLetter(grid.Id)}Model {grid.Id}Model = new();");
        builder.AppendLine($"    private bool is{grid.Id}Update = false;");
        builder.AppendLine($"    private string grid{grid.Id}ModelId = \"1\";");
        builder.AppendLine();

        // Generate grid model class
        GenerateGridModelClass(builder, grid);
    }

    private void GenerateGridModelClass(StringBuilder builder, GridInfo grid)
    {
        builder.AppendLine($@"    public class {GenHelper.CapitalizeFirstLetter(grid.Id)}Model
    {{
        public string Id {{ get; set; }} = string.Empty;");

        foreach (var column in grid.Columns)
        {
            builder.AppendLine($"        public string {GenHelper.GetColumnPropName(column)} {{ get; set; }} = string.Empty;");
        }

        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private void GenerateGridMethods(StringBuilder builder, GridInfo grid)
    {
        // Edit handler
        builder.AppendLine($@"    private async Task OnGrid{grid.Id}Edit(string value)
    {{
        try 
        {{
            is{grid.Id}Update = true;
            {grid.Id}Model = {grid.Id}DataList.FirstOrDefault(_ => _.Id.Equals(value)) ?? new();
            if ({grid.Id}Model != null)
            {{");

        // Map grid values back to form fields
        foreach (var column in grid.Columns)
        {
            if (_gridColumnFields.TryGetValue(column, out var fieldId))
            {
                builder.AppendLine($"                {fieldId} = {grid.Id}Model.{GenHelper.GetColumnPropName(column)};");
            }
        }

        builder.AppendLine($@"            }}
            StateHasChanged();
        }}
        catch (Exception ex)
        {{
            await _jsruntime.InvokeVoidAsync(""globalFunctions.fireToastEvent"", ""bg-warning"", ""Warning"", ex.Message);
        }}
    }}");

        // Delete handler
        builder.AppendLine($@"
    private async Task OnGrid{grid.Id}Delete(string value)
    {{
        try 
        {{
            var confirmed = await _jsruntime.InvokeAsync<bool>(""confirm"", ""Are you sure you want to delete this item?"");
            if (confirmed)
            {{
                {grid.Id}Model = {grid.Id}DataList.FirstOrDefault(_ => _.Id.Equals(value)) ?? new();
                if ({grid.Id}Model != null)
                {{
                    {grid.Id}DataList.Remove({grid.Id}Model);
                    {grid.Id}key = Guid.NewGuid();
                }}");

        // Clear form fields
        foreach (var column in grid.Columns)
        {
            if (_gridColumnFields.TryGetValue(column, out var fieldId))
            {
                builder.AppendLine($"                {fieldId} = string.Empty;");
            }
        }

        builder.AppendLine($@"                is{grid.Id}Update = false;
                StateHasChanged();
            }}
        }}
        catch (Exception ex)
        {{
            await _jsruntime.InvokeVoidAsync(""globalFunctions.fireToastEvent"", ""bg-warning"", ""Warning"", ex.Message);
        }}
    }}");

        // Add handler
        builder.AppendLine($@"
    private void AddDataToGrid{grid.Id}()
    {{
        try
        {{
            var newId = Convert.ToInt32(grid{grid.Id}ModelId);
            newId++;
            grid{grid.Id}ModelId = newId.ToString();

            var newItem = new {GenHelper.CapitalizeFirstLetter(grid.Id)}Model
            {{
                Id = newId.ToString(),");

        foreach (var column in grid.Columns)
        {
            if (_gridColumnFields.TryGetValue(column, out var fieldId))
            {
                builder.AppendLine($"                {GenHelper.GetColumnPropName(column)} = {fieldId},");
            }
        }

        builder.AppendLine($@"            }};

            {grid.Id}DataList.Add(newItem);
            {grid.Id}key = Guid.NewGuid();");

        // Clear form fields
        foreach (var column in grid.Columns)
        {
            if (_gridColumnFields.TryGetValue(column, out var fieldId))
            {
                builder.AppendLine($"            {fieldId} = string.Empty;");
            }
        }

        builder.AppendLine($@"            StateHasChanged();
        }}
        catch (Exception ex)
        {{
            _jsruntime.InvokeVoidAsync(""globalFunctions.fireToastEvent"", ""bg-warning"", ""Warning"", ex.Message);
        }}
    }}");

        // Update handler
        builder.AppendLine($@"
    private void UpdateGrid{grid.Id}Data()
    {{
        try
        {{
            if ({grid.Id}Model != null)
            {{");

        foreach (var column in grid.Columns)
        {
            if (_gridColumnFields.TryGetValue(column, out var fieldId))
            {
                builder.AppendLine($"                {grid.Id}Model.{GenHelper.GetColumnPropName(column)} = {fieldId};");
            }
        }

        builder.AppendLine($@"                is{grid.Id}Update = false;
                {grid.Id}key = Guid.NewGuid();");

        // Clear form fields
        foreach (var column in grid.Columns)
        {
            if (_gridColumnFields.TryGetValue(column, out var fieldId))
            {
                builder.AppendLine($"                {fieldId} = string.Empty;");
            }
        }

        builder.AppendLine($@"                StateHasChanged();
            }}
        }}
        catch (Exception ex)
        {{
            _jsruntime.InvokeVoidAsync(""globalFunctions.fireToastEvent"", ""bg-warning"", ""Warning"", ex.Message);
        }}
    }}");
    }

    private void GenerateStandardMethods(StringBuilder builder, string pageName)
    {
        // Add button click handler
        builder.AppendLine(@"    private async Task AddBtnClick()
    {
        await _jsruntime.InvokeVoidAsync(""triggerHiddenButtonClick"");
    }");

        // Save handler
        builder.AppendLine($@"    
    private async Task SaveData()
    {{
        try
        {{
            bool isValid = await _jsruntime.InvokeAsync<bool>(""globalFunctions.validateFormById"", ""{pageName}Form"");
            if (isValid)
            {{
                await _jsruntime.InvokeVoidAsync(""globalFunctions.fireToastEvent"", ""bg-success"", ""Success"", ""Saved Successfully"");
                await ClearFields();
                await LoadInitialData();
            }}
            else
            {{
                await _jsruntime.InvokeVoidAsync(""globalFunctions.fireToastEvent"", ""bg-danger"", ""Warning"", ""Invalid Data!"");
            }}
        }}
        catch (Exception ex)
        {{
            await _jsruntime.InvokeVoidAsync(""globalFunctions.fireToastEvent"", ""bg-warning"", ""Warning"", ex.Message);
        }}
    }}");

        // Navigation methods
        builder.AppendLine(@"    
    private void Exit()
    {
        nav.NavigateTo(""/Home"");
    }

    private async Task Refresh()
    {
        try
        {
            await ClearFields();
            await LoadInitialData();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            await _jsruntime.InvokeVoidAsync(""globalFunctions.fireToastEvent"", ""bg-warning"", ""Warning"", ex.Message);
        }
    }");

        // Clear fields
        builder.AppendLine(@"    
    private async Task ClearFields()
    {
        try
        {");

        foreach (var variable in _variables)
        {
            builder.AppendLine($"            {variable.Name} = {variable.InitialValue};");
        }

        builder.AppendLine(@"            StateHasChanged();
        }
        catch (Exception ex)
        {
            await _jsruntime.InvokeVoidAsync(""globalFunctions.fireToastEvent"", ""bg-warning"", ""Warning"", ex.Message);
        }
    }");

        // Load initial data
        builder.AppendLine(@$"    
    private async Task LoadInitialData()
    {{
        try
        {{
            {ddInitalization()}
            StateHasChanged();
        }}
        catch (Exception ex)
        {{
            await _jsruntime.InvokeVoidAsync(""globalFunctions.fireToastEvent"", ""bg-warning"", ""Warning"", ex.Message);
        }}
    }}");

        builder.AppendLine(@"    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                await ClearFields();
                await LoadInitialData();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                await _jsruntime.InvokeVoidAsync(""globalFunctions.fireToastEvent"", ""bg-warning"", ""Warning"", ex.Message);
            }
        }
    }");
    }

    private string ddInitalization()
    {
        StringBuilder ddBuilder = new();
        foreach (var dropDownEl in _ddVendor)
        {
            var ddKeyValues = dropDownEl.Value;
            if (ddKeyValues != null)
            {
                foreach (var ddKeyValue in ddKeyValues)
                {
                    ddBuilder.AppendLine($@"{dropDownEl.Key}.Add(new Dropdown{{Value = ""{ddKeyValue.Value}"",Text = ""{ddKeyValue.Key}""}});");
                }
            }
        }

        return ddBuilder.ToString();
    }
}