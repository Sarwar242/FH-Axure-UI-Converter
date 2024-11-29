using Core.Models;
using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace Core.Generators;

public class BlazorComponentGenerator
{
    #region Initializations
    private readonly ComponentMappingConfig _customControlMappings;
    private readonly HashSet<string> _generatedVariables = new HashSet<string>();
    private readonly List<string> _generatedMethods = new List<string>();
    private readonly StringBuilder _stateVariables = new StringBuilder();
    private readonly StringBuilder _stateMethods = new StringBuilder();
    private readonly List<string> _injectServices = new List<string>();
    private HashSet<VariableInfo> _variables = new();
    private List<CustomControl> _customControls = new List<CustomControl>();
    private Dictionary<string, Dictionary<string, string>> _ddVendor = new();
    private List<GridInfo> _grids = new List<GridInfo>();
    private List<ModelInfo> _gridModels = new List<ModelInfo>();
    private Dictionary<string, string> _gridColFields = new ();
    private bool _openPanel = false;

    public BlazorComponentGenerator(string mappingFilePath)
    {
        var json = File.ReadAllText(mappingFilePath);
        _customControlMappings = JsonConvert.DeserializeObject<ComponentMappingConfig>(json);
    }
    #endregion

    #region Template generations
    public string GenerateComponent(AnalysisResult analysis, string pageName, bool isPopup)
    {
        _variables.Clear();
        _customControls.Clear();
        _ddVendor.Clear();
        _grids.Clear();
        _gridModels.Clear();
        _grids.AddRange(analysis.Grids);
        _customControls.AddRange(analysis.CustomControls);
        _openPanel = false;
        var componentBuilder = new StringBuilder();
        pageName = GetPageName(analysis, isPopup);
        // Generate component structure
        #region analysis grid elements
        foreach (var grid in analysis.Grids)
        {
            var gridId = grid.Id;
            var gridModel = GenHelper.CreateGridModel(grid);
            if (gridModel != null)
            {
                _gridModels.Add(gridModel);
            }
        }
        foreach(var control in analysis.CustomControls.Where(_=> _.Type!=null && !_.Type.Equals("label", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var grid in _grids)
            {
                if (grid.ColumnsDataLbls.Where(_ => _.Equals(control.DataLabel, StringComparison.OrdinalIgnoreCase)).Count() > 0)
                {
                    _gridColFields.TryAdd(GenHelper.GetColumnPropName(control.LabelText), control.Id);
                }
            }
        }
        #endregion
        GenerateComponentHeader(componentBuilder, pageName, isPopup);
        GenerateComponentBody(componentBuilder, analysis, isPopup);
        GenerateCodeBlock(componentBuilder, analysis, pageName, isPopup);

        return componentBuilder.ToString();
    }

    private void GenerateComponentHeader(StringBuilder builder, string pageName, bool isPopup)
    { 
        builder.AppendLine($"@page \"/{pageName}\"");
        builder.AppendLine();

        // Add common imports
        builder.AppendLine("@using System.Text.Json");
        builder.AppendLine("@using Microsoft.AspNetCore.Components");
        builder.AppendLine("@rendermode InteractiveServer");
        builder.AppendLine();
    }

    private void GenerateComponentBody(StringBuilder builder, AnalysisResult analysis, bool isPopup)
    {
        var pageTitle = GetPageTitle(analysis, isPopup);
        var pageName = GetPageName(analysis, isPopup);

        // Add NavMenu for non-popup components
        if (!isPopup)
        {
            builder.AppendLine($@"<NavMenu ShowButtonDelete=""false"" 
                      ShowButtonView=""false"" 
                      PageName=""{pageTitle}"" 
                      ShowButtonRefresh=""true"" 
                      OnFHBtnRefreshClick=""Refresh"" 
                      OnFHBtnAddClick=""AddBtnClick"" 
                      OnFHBtnExitClick=""Exit"">
            </NavMenu>");
        }

        // Generate form structure
        var formId = pageName.Replace("UI","Form");
        builder.AppendLine($@"<form id=""{formId}"" onsubmit=""return false;"" class=""needs-validation"" novalidate>");
        builder.AppendLine("<div class=\"box box-primary\">");
        builder.AppendLine("    <div class=\"box-body\">");
        
        // Generate controls
        foreach (var control in analysis.Controls)
        {
            if(!control.Type.Equals("script", StringComparison.OrdinalIgnoreCase))
                GenerateControl(builder, control, 2);
        }
      
        if (_openPanel)
        {
            // Close container
            builder.AppendLine(@"        </div>");
            _openPanel = false;
        }
        

        builder.AppendLine("    </div>");
        builder.AppendLine("</div>");
        builder.AppendLine($"<button type=\"submit\" style=\"visibility:hidden;\" id=\"{formId}\" @onclick=\"SaveData\"></button>");
        builder.AppendLine("</form>");
        builder.AppendLine($@"<script>   
function triggerHiddenButtonClick() {{       
    document.getElementById(""{formId}"").click();
}}
</script>");
    }

    private void GenerateCodeBlock(StringBuilder builder, AnalysisResult analysis, string pageName, bool isPopup)
    {
        var pageForm = pageName.Replace("UI", "Form");

        builder.AppendLine("@code {");

        // 1. Inject services
        foreach(var injectStr in _injectServices)
        {
            builder.AppendLine($"{injectStr}");
        }
        builder.AppendLine();

        // 2. Parameters for popup components
        if (isPopup)
        {
            builder.AppendLine("    [Parameter] public EventCallback<string> GetSelectedData { get; set; }");
            builder.AppendLine("    [Parameter] public EventCallback<bool> IsVisibleChanged { get; set; }");
            builder.AppendLine();
        }

        // 3. Core variables
        builder.AppendLine("    private string ErrorMessage = string.Empty;");
        builder.AppendLine();
        // Add control variables
        foreach (var variable in _variables)
        {
            string nullableSign = variable.IsNullable ? "?" : "";
            builder.AppendLine($"    {variable.AccessModifier} {variable.DataType}{nullableSign} {variable.Name} = {variable.InitialValue};");
            // Add validation variables if needed
            //builder.AppendLine($"    private string IsHid{variable.Name} = string.Empty;");
            builder.AppendLine($"    private bool IsEnable{variable.Name} = true;");
        }
        builder.AppendLine();
        
        builder.AppendLine();

        // 5. Grid variables
        //if (analysis.Grids.Count() > 0)
        //{
        //    GenerateGridAddUpdateMethods(builder);
        //}
        foreach (var grid in analysis.Grids)
        {
            var gridId = grid.Id;
            builder.AppendLine($"    private Dictionary<string, string> {gridId}CustomColumnNames = new Dictionary<string, string> {{ {string.Join(", ", grid.Columns.Select(c => $"{{ \"{GenHelper.GetColumnPropName(c)}\", \"{c}\" }}"))} }};");
            builder.AppendLine($"    private List<string> {gridId}SelectedColumns = new List<string> {{ {string.Join(", ", grid.Columns.Select(c => $"\"{GenHelper.GetColumnPropName(c)}\""))} }};");
            builder.AppendLine($"    private Guid {gridId}key = Guid.NewGuid();");
            builder.AppendLine($"    private List<{GenHelper.CapitalizeFirstLetter(gridId)}Model> {gridId}DataList = new();");
            builder.AppendLine();

            GenerateGridMethods(builder, gridId);
            GenerateGridAddUpdateMethods(builder, gridId);
        }

        // 6. OnInitialized lifecycle method
        //{/*string.Join("\n                ", analysis.Grids.Select(g => $"await Load{g.ID}();"))*/true}
        builder.AppendLine($@"
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {{
        if (firstRender)
        {{
            try
            {{
                await ClearFields();
                await LoadInitialData();
                StateHasChanged();
            }}
            catch (Exception ex)
            {{
                await _jsruntime.InvokeVoidAsync(""globalFunctions.fireToastEvent"", ""bg-warning"", ""Warning"", ex.Message);
            }}
        }}
    }}");

        // 7. InitializeSession method
    //    builder.AppendLine(@"    private async Task InitializeSession()
    //{
    //    objTRSSession.BranchId = await _LocalSession.GetItem(""Home_Branch_Id"");
    //    objTRSSession.UserId = await _LocalSession.GetItem(""UserId"");
    //    objTRSSession.FunctionId = await _LocalSession.GetItem(""Function_Id"");
    //    objTRSSession.TransDate = await _LocalSession.GetItem(""Trans_Date"");
    //    objTRSSession.ServiceTypeId = await _LocalSession.GetItem(""Service_Type_Id"");
    //    objTRSSession.LocalCurrId = await _LocalSession.GetItem(""Local_Currency_Id"");
    //}");

        // 8. Common methods
        builder.AppendLine(@"    private async Task AddBtnClick()
        {
            await _jsruntime.InvokeVoidAsync(""triggerHiddenButtonClick"");
        }");

        builder.AppendLine($@"    private async Task SaveData()
    {{
        try
        {{
            bool isValid = await _jsruntime.InvokeAsync<bool>(""globalFunctions.validateFormById"", ""{pageForm}"");
            if (isValid)
            {{
                await _jsruntime.InvokeVoidAsync(""globalFunctions.fireToastEvent"", ""bg-success"", ""Success"", ""Saved SuccessFully"");
            }}
            else
            {{
                await _jsruntime.InvokeVoidAsync(""globalFunctions.fireToastEvent"", ""bg-danger"", ""Danger"", ""Invalid Data!"");
            }}

            await ClearFields();
            await LoadInitialData();
            StateHasChanged();
        }}
        catch (Exception ex)
        {{
            await _jsruntime.InvokeVoidAsync(""globalFunctions.fireToastEvent"", ""bg-warning"", ""Warning"", ex.Message);
        }}
    }}");

        // 9. Standard handlers
        if (!isPopup)
        {
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
        }
        builder.AppendLine($@"      
        private async Task LoadInitialData()
        {{
            try
            {{
                {ddInitalization()}
            }}
            catch (Exception ex)
            {{
                await _jsruntime.InvokeVoidAsync(""globalFunctions.fireToastEvent"", ""bg-warning"", ""Warning"", ex.Message);
            }}
        }}");
        GenerateEventHandlerMethods(builder); ;
        GenerateClearFieldsMethod(builder);

        foreach(var model in _gridModels)
        {
            GenerateModelClass(builder,model);
        }

        // 10. Close code block
        builder.AppendLine("}");
    }

    private void GenerateModelClass(StringBuilder builder, ModelInfo model)
    {
        builder.AppendLine($@"
    {model.AccessModifier} {model.ClassType.ToString().ToLower()} {model.Name}
    {{");
        foreach (var prop in model.Properties) {
            builder.AppendLine($@"      {prop.AccessModifier} {prop.DataType}  {prop.Name} {{ get; set; }}");
        }
        builder.AppendLine($@"
    }}");

    }
     
    private void GenerateGridAddUpdateMethods(StringBuilder builder, string gridId)
    {
        var grid = _grids.FirstOrDefault();
        //ModelInfo model = new();
        //if (grid != null && _gridModels.Count()>0){
        //    model = _gridModels.Where(_ => _.Id.Equals(grid.ID)).FirstOrDefault();
        //}
        var model = _gridModels.Where(_ => _.Id.Equals(gridId)).FirstOrDefault();
        if (grid != null && model!=null)
        {
            builder.AppendLine($@"
    
            private void AddDataToGrid{gridId}(){{
                var newId = Convert.ToInt32(grid{gridId}ModelId);
                newId++;
                grid{gridId}ModelId = newId.ToString();");

            builder.AppendLine($@"
    
            {model.Name} obj = new {model.Name}
            {{
                Id = newId.ToString(),");
                foreach (var prop in model.Properties.Where(_ => !_.Name.Equals("id", StringComparison.OrdinalIgnoreCase)))
                {
                    if (_gridColFields.TryGetValue(prop.Name, out var value))
                    {
                        builder.AppendLine($@"        {prop.Name} = {value},");
                    }
                
                }
                builder.AppendLine($@"
            }};
            {grid.Id}key = Guid.NewGuid();
            {grid.Id}DataList.Add(obj);");

        foreach (var prop in model.Properties.Where(_ => !_.Name.Equals("id", StringComparison.OrdinalIgnoreCase)))
        {
            if (_gridColFields.TryGetValue(prop.Name, out var value))
            {
                builder.AppendLine($@"    {value} = """";");
            }
        }
        builder.AppendLine($@"
        }}

        private void UpdateGrid{gridId}Data()
        {{");

        foreach (var prop in model.Properties.Where(_ => !_.Name.Equals("id", StringComparison.OrdinalIgnoreCase)))
        {
            if (_gridColFields.TryGetValue(prop.Name, out var value))
            {
                builder.AppendLine($@"        {grid.Id}Model.{prop.Name} = {value};");
            }
        }

        builder.AppendLine($@"
            is{gridId}Update = false;");

        foreach (var prop in model.Properties.Where(_ => !_.Name.Equals("id", StringComparison.OrdinalIgnoreCase)))
        {
            if (_gridColFields.TryGetValue(prop.Name, out var value))
            {
                builder.AppendLine($@"    {value} = """";");
            }
        }
            
        builder.AppendLine($@" 
        }}");
        }
    }

    private void GenerateGridMethods(StringBuilder builder, string gridId)
    {
        var model = _gridModels.Where(_ => _.Id.Equals(gridId)).FirstOrDefault();
        builder.AppendLine($@"
    private async Task OnGrid{gridId}Edit(string value)
    {{
        try 
        {{
            is{gridId}Update = true;
            {gridId}Model = {gridId}DataList.Where(_ => _.Id.Equals(value, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if ({gridId}Model != null)
            {{");
        if (_gridColFields.ContainsKey("Id")){
            foreach (var prop in model.Properties)
            {
                if (_gridColFields.TryGetValue(prop.Name, out var value))
                {
                    builder.AppendLine($@"    {value} = {gridId}Model.{prop.Name};");
                }
            }
        }
        else
        {
            foreach (var prop in model.Properties.Where(_ => !_.Name.Equals("id", StringComparison.OrdinalIgnoreCase)))
            {
                if (_gridColFields.TryGetValue(prop.Name, out var value))
                {
                    builder.AppendLine($@"    {value} = {gridId}Model.{prop.Name};");
                }
            }
        }

        builder.AppendLine($@"     
            }}
            StateHasChanged();
        }}
        catch (Exception ex)
        {{
            await _jsruntime.InvokeVoidAsync(""globalFunctions.fireToastEvent"", ""bg-warning"", ""Warning"", ex.Message);
        }}
    }}

    private async Task OnGrid{gridId}Delete(string value)
    {{
        try 
        {{
            var confirmed = await _jsruntime.InvokeAsync<bool>(""confirm"", ""Are you sure you want to delete this item?"");
            if (confirmed)
            {{
                {gridId}Model = {gridId}DataList.Where(_ => _.Id.Equals(value, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                {gridId}DataList.Remove({gridId}Model);
                {gridId}key = Guid.NewGuid();
            }}");
        if (_gridColFields.ContainsKey("Id"))
        {
            foreach (var prop in model.Properties)
            {
                if (_gridColFields.TryGetValue(prop.Name, out var value))
                {
                    builder.AppendLine($@"    {value} = """";");
                }
            }
        }
        else
        {
            foreach (var prop in model.Properties.Where(_ => !_.Name.Equals("id", StringComparison.OrdinalIgnoreCase)))
            {
                if (_gridColFields.TryGetValue(prop.Name, out var value))
                {
                    builder.AppendLine($@"    {value} = """";");
                }
            }
        }
        builder.AppendLine($@"     
            is{gridId}Update = false;
            StateHasChanged();
        }}
        catch (Exception ex)
        {{
            await _jsruntime.InvokeVoidAsync(""globalFunctions.fireToastEvent"", ""bg-warning"", ""Warning"", ex.Message);
        }}
    }}");
    }

    private void GenerateControl(StringBuilder builder, ControlInfo control, int indentLevel)
    {
        var controlId = control.Attributes.GetValueOrDefault("id", "");
        var indent = new string(' ', indentLevel * 4);
        var customControl = _customControls.FirstOrDefault(c =>
        (c.Id == controlId));

        if (IsGridControl(control))
        {
            GenerateGridControl(builder, control, indentLevel);
            return;
        }
        // Skip if this control has already been generated
        if (customControl?.IsGenerated == true)
            return;

        if (customControl!=null && !control.Type.Equals(customControl.Type, StringComparison.OrdinalIgnoreCase))
        {
            control.Type = customControl.Type;
        }

        var mapping = FindComponentMapping(control);
       
        if (mapping == null)
        {
            // Handle unknown control type
            GenerateDefaultControl(builder, control, new ComponentMapping(), indent);
            return;
        }

        // Generate control based on mapping
        var attributes = GenerateControlAttributes(control, mapping);
        var variables = GenerateControlVariables(control, mapping);

        if (!string.IsNullOrEmpty(variables))
        {
            _stateVariables.AppendLine(variables);
        }

        if (mapping.IsContainer)
        {
            GenerateContainerControl(builder, control, mapping, indent);
        }
        else
        {
            // Pass the customControl to use its properties
            GenerateSingleControl(builder, control, mapping, attributes, indent, customControl??new CustomControl());
        }

        // Mark as generated if we have a custom control
        if (customControl != null)
        {
            customControl.IsGenerated = true;
        }
    }

    private void GenerateGridControl(StringBuilder builder, ControlInfo control, int indentLevel)
    {
        var indent = new string(' ', indentLevel * 4);
        var controlId = control.Attributes.GetValueOrDefault("id", "");

        builder.AppendLine($"{indent}<div class=\"my-2 overflow-auto\" style=\"max-width: 100%;\">");
        builder.AppendLine($"{indent}    <UXC_DataGrid Id=\"{controlId}\"");
        builder.AppendLine($"{indent}        SelectedColumns=\"{controlId}SelectedColumns\"");
        builder.AppendLine($"{indent}        CustomColumnNames=\"{controlId}CustomColumnNames\"");
        builder.AppendLine($"{indent}        @key=\"@({controlId}key)\"");
        builder.AppendLine($"{indent}        DataSource=\"{controlId}DataList\"");
        builder.AppendLine($"{indent}        ShowEditButton=\"true\"");
        builder.AppendLine($"{indent}        ShowDeleteButton=\"true\"");
        builder.AppendLine($"{indent}        BtnColValue=\"Id\"");
        builder.AppendLine($"{indent}        OnFHEditClick=\"OnGrid{controlId}Edit\"");
        builder.AppendLine($"{indent}        OnFHDeleteClick=\"OnGrid{controlId}Delete\">");
        builder.AppendLine($"{indent}    </UXC_DataGrid>");
        builder.AppendLine($"{indent}</div>");

        _variables.Add(new VariableInfo(@$"{controlId}Model", @$"{GenHelper.CapitalizeFirstLetter(controlId)}Model", @$"new {GenHelper.CapitalizeFirstLetter(controlId)}Model()", "private", false));
    }

    private bool HasMeaningfulContent(ControlInfo control)
    {
        // Check if control has inner text
        if (!string.IsNullOrWhiteSpace(control.InnerText?.Trim()))
            return true;

        // Check if control has meaningful attributes
        if (control.Attributes != null && control.Attributes.Any(a =>
            !string.IsNullOrWhiteSpace(a.Value) &&
            a.Key != "class" &&
            a.Key != "style" &&
            !a.Key.StartsWith("data-")))
            return true;

        // Check if any child controls have content
        if (control.Children != null && control.Children.Any(child => HasMeaningfulContent(child)))
            return true;

        // Check for label text in custom control
        if (!string.IsNullOrWhiteSpace(control.LabelText))
            return true;

        return false;
    }

    private void GenerateSingleControl(StringBuilder builder, ControlInfo control, ComponentMapping mapping,
        string attributes, string indent, CustomControl customControl)
    {

        // Skip generation if control has no meaningful content or children
        if (!HasMeaningfulContent(control))
            return;

        var controlId = control.Attributes.GetValueOrDefault("id", "");
        var wrapperClass = GetWrapperClass(control, mapping);
        var hasWrapper = !string.IsNullOrEmpty(wrapperClass);
        var originalIndent = indent;

        // Open wrapper div if needed
        if (hasWrapper)
        {
            builder.AppendLine($"{indent}<div class=\"{wrapperClass}\">");
            indent += "    ";
        }

        var componentAttributes = new List<string> { attributes };

        // Add Label_Text attribute if we have a custom control with label text
        if (customControl != null && !string.IsNullOrEmpty(customControl.LabelText) && mapping.HasLabelText)
        {
            componentAttributes.Add($"Label_Text=\"{customControl.LabelText}\"");
        }

        var bindingAttr = GenerateBindingAttribute(control, mapping, customControl ?? new CustomControl());
        var events = GenerateEventAttributes(control, mapping);
        var styles = GenerateStyleAttributes(control, mapping);

        componentAttributes.AddRange(new[] { bindingAttr, events, styles });

        var content = "";
        // Add options for dropdowns if available from CustomControl
        if (customControl?.Type == "DropDown" && customControl.Options?.Any() == true)
        {
            _variables.Add(new VariableInfo(customControl.Id + "List", "List<Dropdown>", "new List<Dropdown>()"));
            componentAttributes.Add($"DataSource=\"{customControl.Id}List\"");

            Dictionary<string, string> dict = new();
            foreach (var option in customControl.Options)
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
            _ddVendor.Add($"{customControl.Id}List", dict);
        }
        else if (NeedsInnerContent(mapping.Type))
        {
            content = GenerateInnerContent(control, mapping);
        }
        var allAttributes = string.Join(" ", componentAttributes.Where(a => !string.IsNullOrEmpty(a)));

        try
        {
            // Handle special button cases
            if (mapping.Type.Equals("button", StringComparison.OrdinalIgnoreCase) &&
                (control.InnerText.Contains("Add", StringComparison.OrdinalIgnoreCase) || control.InnerText.Contains("Save", StringComparison.OrdinalIgnoreCase)) &&
                _grids.Count() > 0)
            {
                GenerateGridActionButton(builder, mapping, control.ID, allAttributes, content, indent);
            }
            //else if (mapping.Type.Equals("switch", StringComparison.OrdinalIgnoreCase)) { 

            //}

            else if (!ShouldSkipGeneration(mapping, control, customControl!))
            {
                // Generate the component with all attributes
                builder.AppendLine($"{indent}<{mapping.Type} {allAttributes}>");

                if (mapping.Type == "button" && !string.IsNullOrEmpty(content))
                {
                    builder.AppendLine(content);
                }

                builder.AppendLine($"{indent}</{mapping.Type}>");
            }
        }
        finally
        {
            // Always close wrapper div if it was opened
            if (hasWrapper)
            {
                builder.AppendLine($"{originalIndent}</div>");
            }
        }
    }

    private bool ShouldSkipGeneration(ComponentMapping mapping, ControlInfo control, CustomControl customControl)
    {
        // Skip primary buttons with specific text
        if (mapping.Type.Equals("button", StringComparison.OrdinalIgnoreCase) &&
            (control.InnerText.Contains("Ok", StringComparison.OrdinalIgnoreCase) ||
            control.InnerText.Contains("Refresh", StringComparison.OrdinalIgnoreCase) ||
            control.InnerText.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
            control.InnerText.Contains("Exit", StringComparison.OrdinalIgnoreCase)) && 
            (control.Attributes.GetValueOrDefault("class", "").Contains("primary_button", StringComparison.OrdinalIgnoreCase) || 
            control.Attributes.GetValueOrDefault("class", "").Contains("ax_default_hidden", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Skip text boxes without label text
        if (mapping.Type.Equals("UXC_TextBox", StringComparison.OrdinalIgnoreCase) &&
            (customControl == null || (customControl != null && string.IsNullOrEmpty(customControl.LabelText))))
        {
            return true;
        }

        return false;
    }

    private void GenerateGridActionButton(StringBuilder builder, ComponentMapping mapping, string controlId, string allAttributes, string content, string indent)
    {
        var buttonDataLabel = _customControls.FirstOrDefault(c => c.Id.Equals(controlId))?.DataLabel ?? "";
        var buttonPrefix = buttonDataLabel.Split('_').Last();

        var matchingGrid = _grids.FirstOrDefault(g => g.DataLabel?.Split('_').Contains(buttonPrefix) ?? false);
        if (matchingGrid == null) return;

        var updateButtonExists = _customControls.Any(c => {
            var isMatchingPrefix = c.DataLabel?.Split('_')?.LastOrDefault()?.Equals(buttonPrefix, StringComparison.OrdinalIgnoreCase) ?? false;
            var containsUpdate = c.DataLabel?.Contains("update", StringComparison.OrdinalIgnoreCase) ?? false;
            return isMatchingPrefix && containsUpdate;
        });

        if (updateButtonExists)
        {
            builder.AppendLine(@$"{indent}@if(is{matchingGrid.Id}Update){{");
            builder.AppendLine(@$"{indent}<{mapping.Type} {allAttributes} @onclick=""UpdateGrid{matchingGrid.Id}Data"">");
            builder.AppendLine(@$"{indent}    Update");
            builder.AppendLine($"{indent}</{mapping.Type}>");
            builder.AppendLine(@$"{indent}}}else{{");
            builder.AppendLine(@$"{indent}<{mapping.Type} {allAttributes} @onclick=""AddDataToGrid{matchingGrid.Id}"">");
            builder.AppendLine(@$"{indent}    {content}");
            builder.AppendLine($"{indent}</{mapping.Type}>");
            builder.AppendLine(@$"{indent}}}");
            _variables.Add(new VariableInfo($"is{matchingGrid.Id}Update", "bool", "false", "private", false));
        }
        else
        {
            builder.AppendLine(@$"{indent}<{mapping.Type} {allAttributes} @onclick=""AddDataToGrid{matchingGrid.Id}"">");
            builder.AppendLine(@$"{indent}    {content}");
            builder.AppendLine($"{indent}</{mapping.Type}>");
        }

        _variables.Add(new VariableInfo($"grid{matchingGrid.Id}ModelId", "string", @"""1""", "private", false));
    }
    private void GenerateContainerControl(StringBuilder builder, ControlInfo control, ComponentMapping mapping, string indent)
    {
        var controlId = control.Attributes.GetValueOrDefault("id", "");
        var headerText = control.Attributes.GetValueOrDefault("groupingtext", "") ?? control.LabelText;
        var visibility = GenerateVisibilityAttribute(control);
        var customComponent = _customControls.Where(_ => _.Id.Equals(control.ID, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

        if(customComponent!=null && customComponent.Type.Equals("Panel") && !String.IsNullOrEmpty(customComponent.LabelText))
        {
            if (_openPanel)
            {
                // Close container
                builder.AppendLine($"{indent}</div>");
                _openPanel = false;
            }
            // Start container
            builder.AppendLine($"{indent}<div id=\"{controlId}\" class=\"{mapping.Styles["default"]}\" {visibility}>");

            builder.AppendLine($"{indent}    <div class=\"box-title\">");
            builder.AppendLine($"{indent}        <div>");
            builder.AppendLine($"{indent}            <h6 class=\"GridTitlebar\">{customComponent.LabelText}</h6>");
            builder.AppendLine($"{indent}        </div>");
            builder.AppendLine($"{indent}    </div>");

            _openPanel = true;
        }
        else
        {
            // Start container
            builder.AppendLine($"{indent}<div id=\"{controlId}\" class=\"{mapping.Styles["default"]}\" {visibility}>");

            // Add header if needed
            if (!string.IsNullOrEmpty(headerText))
            {
                builder.AppendLine($"{indent}    <div class=\"box-title\">");
                builder.AppendLine($"{indent}        <div>");
                builder.AppendLine($"{indent}            <h6 class=\"GridTitlebar\">{headerText}</h6>");
                builder.AppendLine($"{indent}        </div>");
                builder.AppendLine($"{indent}    </div>");
            }

            // Process child controls
            foreach (var childControl in control.Children)
            {
                GenerateControl(builder, childControl, 4);
            }

            // Close container
            builder.AppendLine($"{indent}</div>");
        }
        
    }
    #endregion

    #region Helpers
    private bool IsGridControl(ControlInfo control)
    {
        var controlId = control.Attributes.GetValueOrDefault("id", "");
        var dataLabel = control.Attributes.GetValueOrDefault("data-label", "").ToLower();

        // Check if this is a grid container
        return dataLabel.Contains("grid") &&
               _grids.Where(_=>_.Id.Equals(controlId)).Count()>0;
    }


    private ComponentMapping FindComponentMapping(ControlInfo control)
    {
        var componentType  =  _customControlMappings.Components.FirstOrDefault(m =>
            control.Type.Contains(m.Name, StringComparison.OrdinalIgnoreCase));

        if (componentType == null) return null;
        return componentType;
    }

    private void GenerateEventHandlerMethods(StringBuilder builder)
    {
        if(_stateMethods!=null)
        {
            builder.AppendLine($"    {_stateMethods}");
        }
    }
    
    private void GenerateClearFieldsMethod(StringBuilder builder)
    {
        builder.AppendLine(@"    private async Task ClearFields()
    {");

        // Clear all tracked variables using their initial values
        foreach (var variable in _variables)
        {
            builder.AppendLine($"        {variable.Name} = {variable.InitialValue};");
        }

        builder.AppendLine(@"        StateHasChanged();
    }");
    }

    private string GenerateControlAttributes(ControlInfo control, ComponentMapping mapping)
    {
        var attributes = new List<string>();

        // Add default attributes
        foreach (var defaultAttr in mapping.DefaultAttributes)
        {
            attributes.Add($"{defaultAttr.Key}=\"{defaultAttr.Value}\"");
        }

        // Process mapped attributes
        foreach (var attrMapping in mapping.AttributeMappings)
        {
            if (control.Attributes.TryGetValue(attrMapping.Value.SourceAttribute.ToLower(), out var value))
            {
                if (attrMapping.Value.Transformer != null)
                {
                    value = TransformAttributeValue(value, attrMapping.Value.Transformer);
                }
                attributes.Add($"{attrMapping.Value.TargetAttribute}=\"{value}\"");
            }
            else if (attrMapping.Value.Required)
            {
                attributes.Add($"{attrMapping.Value.TargetAttribute}=\"{attrMapping.Value.DefaultValue}\"");
            }
        }

        return string.Join(" ", attributes);
    }

    private string TransformAttributeValue(string value, string transformer)
    {
        return transformer switch
        {
            "AppendCssClass" => $"form-control {value}",
            "ToLower" => value.ToLower(),
            "ToBoolean" => value.ToLower() == "true" ? "true" : "false",
            _ => value
        };
    }

    private string GetPageTitle(AnalysisResult analysis, bool isPopup)
    {
        // Extract page title from analysis or generate from path
        var fileName = Path.GetFileNameWithoutExtension(analysis.OriginalFilePath ?? "");
        var words = fileName.Split('_')
    .Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower())
    .ToList();
        var combined = string.Join(" ", words);

        return Regex.Replace(combined, "(?<=[a-z])(?=[A-Z])", " ");
    }

    private string GetPageName(AnalysisResult analysis, bool isPopup)
    {
        // Extract page title from analysis or generate from path
        var fileName = Path.GetFileNameWithoutExtension(analysis.OriginalFilePath ?? "");
        var words = fileName.Split('_')
    .Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower())
    .ToList();
        var combined = string.Join("", words);

        return combined+"UI";
    }

    private string GenerateControlVariables(ControlInfo control, ComponentMapping mapping)
    {
        var variables = new StringBuilder();

        foreach (var varTemplate in mapping.RequiredVariables)
        {
            var varDef = varTemplate.Replace("{id}", control.Attributes.GetValueOrDefault("id", ""));
            var parts = varDef.Split(':');

            if (parts.Length == 3 && !_generatedVariables.Contains(parts[0]))
            {
                variables.AppendLine($"    private {parts[1]} {parts[0]} = {parts[2]};");
                _generatedVariables.Add(parts[0]);
            }
        }

        return variables.ToString();
    }

    
    private string GetWrapperClass(ControlInfo control, ComponentMapping mapping)
    {
        if (mapping.Type == "UXC_DataGrid")
        {
            return "my-2 overflow-auto";
        }

        return mapping.Type == "button" ? "my-2" : "form-group col-3 my-2";
    }

    private string GenerateBindingAttribute(ControlInfo control, ComponentMapping mapping, CustomControl customControl)
    {
        var controlId = control.Attributes.GetValueOrDefault("id", "");
        if (string.IsNullOrEmpty(controlId)) return string.Empty;

        // Determine variable type and default value based on control type
        VariableInfo variableInfo = GetVariableInfoForControl(controlId, mapping, customControl);
        if (variableInfo != null)
        {
            _variables.Add(variableInfo);
        }

        // Special handling for different control types
        switch (mapping.Type)
        {
            case "UXC_Amount":
            case "UXC_AmountToWord":
            case "UXC_TextBox":
            case "UXC_TxtArea":
            case "UXC_Date":
            case "UXC_Number":
            case "UXC_Dynamic_Dropdown":
                return $"@bind-Value=\"{controlId}\"";
            case "UXC_Switch":
                return $"IsChecked=\"{controlId}\"";
            default:
                return string.Empty;
        }
    }

    private VariableInfo GetVariableInfoForControl(string controlId, ComponentMapping mapping, CustomControl customControl)
    {
        var (type, defaultValue, nullable) = mapping.Type switch
        {
            "UXC_Amount" => ("decimal", "0.00m", true),
            "UXC_AmountToWord" => ("decimal", "0.00m", true),
            "UXC_Date" => ("string", "\"dd/mm/yyyy\"", false),
            "UXC_Dynamic_Dropdown" => ("string", "\"\"", false),
            "UXC_Switch" => ("bool", @$"{customControl.IsSelected.ToString().ToLower()}", false),
            "UXC_TextBox" => ("string", "\"\"", false),
            "UXC_TxtArea" => ("string", "\"\"", false),
            "UXC_Number" => ("string", "\"\"", false),
            _ => (null, null, false)
        };

        if (type == null) return null;

        return new VariableInfo(controlId, type, defaultValue, "private", nullable, false, true);
    }

    private string GenerateEventAttributes(ControlInfo control, ComponentMapping mapping)
    {
        var events = new List<string>();

        foreach (var eventMapping in mapping.Events)
        {
            if (control.Attributes.TryGetValue(eventMapping.SourceAttribute, out var handler))
            {
                // Generate method if needed
                if (eventMapping.RequiresStateHasChanged)
                {
                    GenerateEventHandler(control, handler, eventMapping);
                }

                events.Add($"{eventMapping.TargetEventName}=\"On{GenHelper.CapitalizeFirstLetter(control.ID)}{handler}\"");
            }
            else if (control.Type == "Switch" && !string.IsNullOrEmpty(eventMapping.SourceAttribute))
            {
                // Generate method if needed
                if (eventMapping.RequiresStateHasChanged)
                {
                    GenerateEventHandler(control, eventMapping.SourceAttribute, eventMapping);
                }

                events.Add($"{eventMapping.TargetEventName}= \"On{GenHelper.CapitalizeFirstLetter(control.ID)}{eventMapping.SourceAttribute}\"");
            }
            else if (!string.IsNullOrEmpty(eventMapping.DefaultHandler))
            {
                events.Add($"{eventMapping.TargetEventName}=\"{eventMapping.DefaultHandler}\"");
            }
        }

        return string.Join(" ", events);
    }

    private void GenerateEventHandler(ControlInfo control, string handler, ComponentEvent eventMapping)
    {
        var methodName = handler.Replace("()", "").Trim();
        methodName = $"On" + GenHelper.CapitalizeFirstLetter(control.ID) + handler;
        var changedLine = eventMapping?.Body?.Replace("{id}", control.ID);
        if (!_generatedMethods.Contains(methodName))
        {
            var parameters = string.Join(", ", eventMapping.RequiredParameters);
            var methodBuilder = new StringBuilder();
            methodBuilder.AppendLine($@"    private async Task {methodName}({parameters})
    {{
        try
        {{
            {changedLine}

            // Add Additional handler logic here
            StateHasChanged();
        }}
        catch (Exception ex)
        {{
            await _jsruntime.InvokeVoidAsync(""globalFunctions.fireToastEvent"", ""bg-warning"", ""Warning"", ex.Message);
        }}
    }}");

            _generatedMethods.Add(methodName);
            _stateMethods.AppendLine(methodBuilder.ToString());
        }
    }

   
    private string GenerateVisibilityAttribute(ControlInfo control)
    {
        var controlId = control.Attributes.GetValueOrDefault("id", "");
        if (string.IsNullOrEmpty(controlId)) return string.Empty;
        if (controlId.Equals("baseId")) 
            return string.Empty;
        //_variables.Add(new VariableInfo($"IsHid{controlId}", "string", "", "private", false));
        //_generatedVariables.Add($"IsHid{controlId}");
        //return $"@IsHid{controlId}";
        return "";
    }

    private bool NeedsInnerContent(string controlType)
    {
        return controlType switch
        {
            "button" => true,
            "UXC_AmountToWord" => false,
            "UXC_DataGrid" => false,
            _ => false
        };
    }

    private string GenerateInnerContent(ControlInfo control, ComponentMapping mapping)
    {
        if (mapping.Type == "button")
        {
            return GenerateButtonContent(control);
        }

        return string.Empty;
    }

    private string GenerateButtonContent(ControlInfo control)
    {
        // Try to get button text from various sources
        var buttonText = control.InnerText?.Trim() ??
                        control.Attributes.GetValueOrDefault("text", "") ??
                        GenerateButtonText(control.Attributes.GetValueOrDefault("id", "appButton"));

        return buttonText;
    }

    private string GenerateButtonText(string buttonId)
    {
        return Regex.Replace(
            buttonId.Replace("btn", "")
                   .Replace("Button", ""),
            "([a-z])([A-Z])",
            "$1 $2"
        ).Trim();
    }

    private string GenerateStyleAttributes(ControlInfo control, ComponentMapping mapping)
    {
        var styles = new List<string>();

        if (mapping.Type == "UXC_DataGrid")
        {
            styles.Add("style=\"max-height: 100%;\"");
        }

        if (mapping.Styles.TryGetValue("default", out var defaultStyle))
        {
            styles.Add(defaultStyle);
        }

        return string.Join(" ", styles);
    }

    private void GenerateDefaultControl(StringBuilder builder, ControlInfo control, ComponentMapping mapping, string indent)
    {
        var controlType = control.Type.ToLowerInvariant();
        var controlId = control.Attributes.GetValueOrDefault("id", "");
        var cssClass = control.Attributes.GetValueOrDefault("class", "");
        //var visibility = !string.IsNullOrEmpty(controlId) && !controlId.Equals("baseId")? $"@IsHid{controlId}" : "";
        
        switch (controlType)
        {
            case var t when t.Contains("panel") || t == "div":
                GenerateDiv(builder, control, indent);
                break;

            case var t when t.Contains("img"):
                GenerateImage(builder, control, indent);
                break;

            //case var t when t.Contains("label"):
            //    builder.AppendLine(GenerateControlLabel(control,mapping, indent));
            //    break;

            case var t when t.Contains("table"):
                GenerateSimpleTable(builder, control, indent);
                break;

            default:
                //GenerateGenericElement(builder, control, indent);
                break;
        }
    }

    private void GenerateDiv(StringBuilder builder, ControlInfo control, string indent)
    {
        var controlId = control.Attributes.GetValueOrDefault("id", "");
        var groupingText = control.Attributes.GetValueOrDefault("class", "");
        var headerText = control.LabelText??"";
        var cssClass = GetDivClass(control);
        var visibility = "";

        builder.AppendLine($"{indent}<div id=\"{controlId}\" class=\"{cssClass}\" {visibility}>");

        if (groupingText.ToString().Contains("box",  StringComparison.OrdinalIgnoreCase)||groupingText.ToString().Contains("heading_",  StringComparison.OrdinalIgnoreCase)) 
        {
            builder.AppendLine($"{indent}    <div class=\"box-title\">");
            builder.AppendLine($"{indent}       <div>");
            builder.AppendLine($"{indent}           <h6 class=\"GridTitlebar\">{headerText}</h6>");
            builder.AppendLine($"{indent}       </div>");
            builder.AppendLine($"{indent}    </div>");
        }
        if (control.Type == "div" && control.Children.Count() > 0)
        {
            foreach (var child in control.Children)
            {
                GenerateControl(builder, child, 4);
            }
        }
       

        builder.AppendLine($"{indent}</div>");
    }
    
    private void GenerateSimpleTable(StringBuilder builder, ControlInfo control, string indent)
    {
        var controlId = control.Attributes.GetValueOrDefault("id", "");
        var cssClass = "table table-bordered";

        builder.AppendLine($"{indent}<div class=\"table-responsive\">");
        builder.AppendLine($"{indent}    <table id=\"{controlId}\" class=\"{cssClass}\">");

        foreach (var row in control.Children)
        {
            builder.AppendLine($"{indent}        <tr>");
            foreach (var cell in row.Children)
            {
                var cellContent = cell.InnerText ?? "";
                builder.AppendLine($"{indent}            <td>{cellContent}</td>");
            }
            builder.AppendLine($"{indent}        </tr>");
        }

        builder.AppendLine($"{indent}    </table>");
        builder.AppendLine($"{indent}</div>");
    }

    
    private string GetDivClass(ControlInfo control)
    {
        var classes = new List<string> { "row" };
        var customClass = control.Attributes.GetValueOrDefault("class", "");
        
        if (!string.IsNullOrEmpty(customClass))
        {
            classes.AddRange(customClass.Split(' '));
        }
        if (customClass.Contains("ax_default_hidden"))
        {
            classes.AddRange(new[] { "d-none" });
        }
        if (control.Type.Contains("panel", StringComparison.OrdinalIgnoreCase))
        {
            classes.AddRange(new[] { "border", "my-2" });
        }

        return string.Join(" ", classes.Distinct());
    }

    private string ddInitalization()
    {
        StringBuilder ddBuilder = new();
        foreach(var dropDownEl in _ddVendor)
        { 
            var ddKeyValues = dropDownEl.Value;
            if (ddKeyValues != null)
            {
                foreach(var ddKeyValue in ddKeyValues)
                {
                    ddBuilder.AppendLine($@"{dropDownEl.Key}.Add(new Dropdown{{Value = ""{ddKeyValue.Value}"",Text = ""{ddKeyValue.Key}""}});");
                }
            }
        }

        return ddBuilder.ToString();
    }

    private void GenerateImage(StringBuilder builder, ControlInfo control, string indent)
    {
        var controlId = control.Attributes.GetValueOrDefault("id", "");
        var altText = control.Attributes.GetValueOrDefault("alt", "");
        var imageStyle = "margin-bottom:10px; width: 20px; height: 20px; object-fit: contain;";

        // Add wrapper div for layout
        builder.AppendLine($"{indent}<div style=\"display: flex; flex-direction: row; gap: 30px;\">");
        builder.AppendLine($"{indent}    <div>");

        // Generate label if needed
        var labelText = controlId.Length >= 3 ? controlId.Substring(0, 3) : controlId;
        builder.AppendLine($"{indent}        <label for=\"{controlId}\" style=\"margin-right:5px;margin-bottom:10px;font-weight:bold; font-size:15pt\">{labelText}</label>");

        // Generate image
        builder.AppendLine($"{indent}        <img id=\"{controlId}\" src=\"@{controlId}Img\" alt=\"{altText}\" @key=\"{controlId}Key\"");
        builder.AppendLine($"{indent}             style=\"{imageStyle}\" />");

        builder.AppendLine($"{indent}    </div>");
        builder.AppendLine($"{indent}</div>");

        // Add state variable for image source
        if (!_generatedVariables.Contains($"{controlId}Img"))
        {
            _variables.Add(new VariableInfo($"{controlId}Key", "Guid", "Guid.NewGuid()", "private", false));
            _variables.Add(new VariableInfo($"{controlId}Img", "string", @"""/img/FintechHub.png""", "private", false));
            _generatedVariables.Add(controlId);
            _stateVariables.AppendLine($"    private string {controlId}Img = \"/img/FintechHub.png\";");
        }
    }
    #endregion
}