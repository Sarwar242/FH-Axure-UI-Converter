using Core.Models;
using Newtonsoft.Json;
using System.Text;

namespace Core.Generators;

public class CustomComponentGenerator
{
    private readonly ComponentMappingConfig _customControlMappings;
    private readonly HashSet<VariableInfo> _variables = new();
    private readonly Dictionary<string, string> _gridColumnFields = new(); // Maps grid columns to form fields
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
                // Find the best match among grid column labels
                var bestMatchWithDataLabel = grid.ColumnsDataLbls
                    .Select(columnLabel => new
                    {
                        ColumnLabel = columnLabel,
                        MatchScore = GenHelper.CalculateStringSimilarity(control.DataLabel, columnLabel)
                    })
                    .OrderByDescending(x => x.MatchScore)
                    .FirstOrDefault();     
                
                var bestMatchWithInnerText = grid.Columns
                    .Select(columnLabel => new
                    {
                        ColumnLabel = grid.ColumnsDataLbls[grid.Columns.IndexOf(columnLabel)],
                        MatchScore = GenHelper.CalculateStringSimilarity(control.LabelText, columnLabel)
                    })
                    .OrderByDescending(x => x.MatchScore)
                    .FirstOrDefault();

                var bestMatch = bestMatchWithDataLabel?.MatchScore > bestMatchWithInnerText?.MatchScore ?
                                    bestMatchWithDataLabel : bestMatchWithInnerText;
                if (bestMatchWithDataLabel?.MatchScore > 0.8)
                {
                    bestMatch = bestMatchWithDataLabel;
                }
                // Set a threshold for matching (e.g., 0.8 or 80% similarity)
                if (bestMatch != null && bestMatch.MatchScore >= 0.8)
                {
                    var columnName = "";
                    if (grid.ColumnsDataLbls.Contains(bestMatch.ColumnLabel))
                        columnName = grid.Columns[grid.ColumnsDataLbls.IndexOf(bestMatch.ColumnLabel)];

                    if (!_gridColumnFields.ContainsKey(columnName))
                    {
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
        var pageTitle = GenHelper.GetPageTitle(analysis.OriginalFilePath ?? "", false);
        var pagePathName = GenHelper.GetPageName(analysis.OriginalFilePath??"", false);
        var formId = pagePathName.Replace("UI", "Form");
        // Add NavMenu
        builder.AppendLine($@"<NavMenu ShowButtonDelete=""false"" 
                      ShowButtonView=""false"" 
                      PageName=""{pageTitle}"" 
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
        var mappedComponent = GenHelper.FindComponentMapping(field, _customControlMappings);
        var mappedAttributes = GetMappedAtrributes(field, mappedComponent);
        GenerateMappedControlVariables(field, mappedComponent);

        builder.AppendLine($@"    <div class=""form-group col-3 my-2"">");
        builder.Append($@"         <{mappedComponent.Type ?? componentType} ");

        // Output attributes as key="value"
        foreach (var attribute in mappedAttributes)
        {
            builder.Append($"{attribute.Key}=\"{attribute.Value}\" ");
        }
        builder.AppendLine($@">");
        
        builder.AppendLine($@"        </{mappedComponent.Type ?? componentType}>");
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

    // Grid variables and methods generation

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

        var pagePathName = GenHelper.GetPageName(pageName ?? "", false);
        var formId = pagePathName.Replace("UI", "Form");
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
            bool isValid = await _jsruntime.InvokeAsync<bool>(""globalFunctions.validateFormById"", ""{formId}"");
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

    private Dictionary<string, string> GetMappedAtrributes(CustomControl field, ComponentMapping mapping)
    {
        var mappedAttributes = new Dictionary<string, string>();

        // Add default attributes from mapping
        foreach (var defaultAttr in mapping.DefaultAttributes)
        {
            mappedAttributes[defaultAttr.Key] = defaultAttr.Value
                .Replace("{id}", field.Id)
                .Replace("{lbl_Txt}", field.LabelText);
        }

        return mappedAttributes;
    }

    private void GenerateMappedControlVariables(CustomControl control, ComponentMapping mapping)
    {
        var variables = new StringBuilder();

        foreach (var varTemplate in mapping.RequiredVariables)
        {
            var varDef = varTemplate.Replace("{id}", control.Id);
            var parts = varDef.Split(':');

            if (control.IsDisabled)
            {
                if (parts[0].Contains("IsEnable", StringComparison.OrdinalIgnoreCase))
                    parts[2] = "false";                
                if (parts[0].Contains("IsDisable", StringComparison.OrdinalIgnoreCase))
                    parts[2] = "true";
            }
                        
            
            if (control.IsHidden)
            {
                if (parts[0].Contains("IsVisible", StringComparison.OrdinalIgnoreCase))
                    parts[2] = "false";                
            }

            if (parts.Length == 3 && !_variables.Any(_=>_.Name.Equals(parts[0],StringComparison.OrdinalIgnoreCase)))
            {
                var variableInfo = new VariableInfo(parts[0], parts[1], parts[2],"private", false);
                _variables.Add(variableInfo);
            }
        }


        if (control.Type == "DropDown" && control.Options != null)
        {
            Dictionary<string, string> dict = new();
            foreach (var option in control.Options)
            {
                if (option.ToLower().Contains("select"))
                {
                    dict.Add(option, "");
                }
                else
                {
                    dict.Add(option, $"{option.ToLower()}");
                }
            }
            _ddVendor.Add($"{control.Id}List", dict);
        }
    }
}