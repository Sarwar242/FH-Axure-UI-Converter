using System.Text.RegularExpressions;
using Core.Models;
using HtmlAgilityPack;

namespace Core.Parsers;

public class AspxParser
{
    private readonly string _mappingFilePath;
    private List<string> dataLabelsToSkip = new List<string>
    {
        "master_background","header_nav","logo","static_nav","user_info","setting","logout", "content_pannel",
        "notification","Menu","search","nav","fav","home","square", "content_panel"
    };
    public AspxParser(string mappingFilePath)
    {
        _mappingFilePath = mappingFilePath;
    }
    
    public async Task<AnalysisResult> ParseAspx(string filePath)
    {
        var doc = new HtmlDocument { OptionOutputOriginalCase = true };
        doc.Load(filePath);
        var contentRoot = FindContentRoot(doc.DocumentNode);
        var analysisResult = new AnalysisResult
        {
            OriginalFilePath = filePath,
            Controls = AnalyzeControls(contentRoot),
            CustomControls = AnalyzeCustomControls(contentRoot),
            Panels = AnalyzePanels(contentRoot),
            Grids = AnalyzeGrids(contentRoot),
            Radios = AnalyzeRadioButtons(doc),
            HiddenFields = AnalyzeHiddenFields(doc),
            Buttons = AnalyzeButtons(doc),
            //FormElements = AnalyzeFormElements(doc),
            NavigationElements = AnalyzeNavigationElements(doc),
            ScriptAnalysis = AnalyzeScriptContents(doc),
            Layout = AnalyzeLayout(doc)
        };

        return analysisResult;
    }

    private List<ControlInfo> AnalyzeControls(HtmlNode doc)
    {
        return AnalyzeControlsRecursive(doc);
    }

    private List<ControlInfo> AnalyzeControlsRecursive(HtmlNode node, ControlInfo parent = null)
    {
        var controls = new List<ControlInfo>();

        foreach (var childNode in node.ChildNodes)
        {
            if (childNode.NodeType == HtmlNodeType.Element)
            {
                if (childNode.GetAttributeValue("id", "") != "baseId" &&
                !HasChildWithClass(childNode, "panel_state_content") &&
                childNode.GetAttributeValue("class", "").Contains("panel_state_content", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                //if (childNode.GetAttributeValue("id", "") != "baseId" &&
                //childNode.GetAttributeValue("class", "").Contains("ax_default", StringComparison.OrdinalIgnoreCase) &&
                //(HasChildWithDataLabel(childNode, "logout")|| HasChildWithDataLabel(childNode, "mainmenu")))
                //{
                //    continue;
                //}         
                if (ShouldSkipByDataLabel(childNode))
                {
                    continue;
                }
                
                if (IsEffectivelyEmpty(childNode)) 
                    continue;

                // Extract control info based on HTML classes and data attributes
                var control = new ControlInfo
                {
                    Type = DetermineControlType(childNode),
                    ID = GetControlId(childNode),
                    InnerText = childNode.InnerText?.Trim(),
                    ControlType = MapHtmlToControlType(childNode),
                    Parent = parent,
                    Attributes = ExtractAttributes(childNode),
                    LabelText = childNode.SelectSingleNode(".//div[@class='text ']//p//span")?.InnerText.Trim() ??
                                      childNode.SelectSingleNode(".//div[@class='text ']//p")?.InnerText.Trim() ??
                                      childNode.SelectSingleNode(".//div[@class='text ']")?.InnerText.Trim()

                };

                control.Children = AnalyzeControlsRecursive(childNode, control);
                controls.Add(control);
            }
        }

        return controls;
    }
    #region Helper Checker
    private bool ShouldSkipByDataLabel(HtmlNode node)
    {
        // Get the data-label attribute of the current node
        var currentDataLabel = node.GetAttributeValue("data-label", string.Empty);

        // Check if the current node's data-label is in the skip list
        if (!string.IsNullOrEmpty(currentDataLabel) &&
            dataLabelsToSkip.Contains(currentDataLabel, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private bool HasChildWithDataLabel(HtmlNode node, string dataLabel)
    {
        return node.SelectNodes($".//*[@data-label='{dataLabel}']")?.Any() ?? false;
    }    

    private bool HasChildWithClass(HtmlNode node, string className)
    {
        return node.SelectNodes($".//*[@class and contains(@class, '{className}')]")?.Any() ?? false;
    }
    private bool IsEffectivelyEmpty(HtmlNode node)
    {
        // If node has no children at all
        if (!node.HasChildNodes)
            return true;

        // Check each child node
        foreach (var child in node.ChildNodes)
        {
            // If it's an input element, div is not empty
            if (child.Name == "input")
                return false;// If it's an input element, div is not empty

            if (child.Name == "textarea")
                return false;

            // If it's a div with "text" class, check for span content
            if (child.Name == "div" && child.GetAttributeValue("class", "").Contains("text"))
            {
                var spanText = child.SelectNodes(".//span")?.Any(s => !string.IsNullOrWhiteSpace(s.InnerText));
                if (spanText == true)
                    return false;
                continue;
            }

            // Skip div with "_div" suffix in id
            if (child.Name == "div" && child.Id?.EndsWith("_div") == true)
                continue;

            // If it's a paragraph, check for span content
            if (child.Name == "p")
            {
                var spanText = child.SelectNodes(".//span")?.Any(s => !string.IsNullOrWhiteSpace(s.InnerText));
                if (spanText == true)
                    return false;
                continue;
            }

            // If child has any meaningful content
            if (!string.IsNullOrWhiteSpace(child.InnerText))
                return false;
        }

        return true;
    }
    #endregion

    #region SpecAnalyzer
    private string DetermineControlType(HtmlNode node)
    {
        // Check class attributes for control type indicators
        var classes = node.GetAttributeValue("class", "").Split(' ');
        var type = node.GetAttributeValue("type", "").ToLower();
        var dataLabel = node.GetAttributeValue("data-label", "").ToLower();
        if (type.Contains("date")) return "Date";

        if (dataLabel.Contains("pnl_", StringComparison.OrdinalIgnoreCase)) return "Panel";

        if (classes.Any(c => c.Contains("ax_default")))
        {
            if (classes.Contains("text_field")) return "TextBox";
            if (classes.Contains("droplist")) return "DropDown";
            if (classes.Contains("button") || classes.Contains("primary_button")) return "Button";
            if (classes.Contains("label")) return "Label";
            if (classes.Contains("box_") || classes.Contains("heading_")) return "Panel";
            if (classes.Contains("icon")) return "Image";
        }

        return node.Name;
    }

    private string GetControlId(HtmlNode node)
    {
        // Try getting ID from data-label first, then fallback to id attribute
        var id = node.GetAttributeValue("id", null);
        if (!string.IsNullOrEmpty(id) && id.Equals("base", StringComparison.OrdinalIgnoreCase)) 
            return "baseId";
        //var dataLabel = node.GetAttributeValue("data-label", null);
        //if (!string.IsNullOrEmpty(dataLabel)) return dataLabel;
        return node.GetAttributeValue("id", null);
    }

    private Dictionary<string, string> ExtractAttributes(HtmlNode node)
    {
        var attributes = new Dictionary<string, string>();

        foreach (var attribute in node.Attributes)
        {
            if(attribute.Name.Equals("id", StringComparison.OrdinalIgnoreCase)&& attribute.Value.Equals("base", StringComparison.OrdinalIgnoreCase)) 
            attributes[attribute.Name] = "baseId";
            else
            attributes[attribute.Name] = attribute.Value;
        }

        // Extract additional metadata from classes
        var classes = node.GetAttributeValue("class", "").Split(' ');
        if (classes.Contains("disabled"))
        {
            attributes["Enabled"] = "false";
        }

        return attributes;
    }

    private string MapHtmlToControlType(HtmlNode node)
    {
        var classes = node.GetAttributeValue("class", "").ToLower().Split(' ');
        var dataLabel = node.GetAttributeValue("data-label", "").ToLower();
        var type = node.GetAttributeValue("type", "").ToLower();

        // Check for grid patterns
        if (IsGridControl(node, classes, dataLabel))
        {
            return "Grid";
        }
        if (type.Contains("date")) return "Date";
        if (type.Contains("number")) return "Number";
        if (type.Contains("file")) return "File";
        if (classes.Contains("text_field")) return "TextBox";
        if (classes.Contains("droplist")) return "DropDown";
        if (classes.Contains("button") || classes.Contains("primary_button")) return "Button";
        if (classes.Contains("label")) return "Label";
        if (classes.Contains("box_") || 
            classes.Contains("box_2") || 
            classes.Contains("box_3") ||
            dataLabel.Contains("pnl_", StringComparison.OrdinalIgnoreCase)) 
                return "Panel";

        if (classes.Contains("icon")) return "Icon";
        if (classes.Contains("image")) return "Image";

        return "Other";
    }

    private bool IsGridControl(HtmlNode node, string[] classes, string dataLabel)
    {
        // Check if data-label contains 'grid'
        bool hasGridLabel = dataLabel.Contains("grid", StringComparison.OrdinalIgnoreCase);

        // Check for repeater template
        bool hasRepeaterTemplate = node.SelectNodes(".//script[@type='axure-repeater-template']") != null;

        // Return true only if both conditions are met
        return hasGridLabel && hasRepeaterTemplate;
    }

    private List<string> AnalyzePanels(HtmlNode doc)
    {
        return doc.SelectNodes("//*[@class and contains(@class, 'box_1') or contains(@class, 'box_2') or contains(@class, 'box_3') or contains(@data-label, 'pnl_') or contains(@class, 'heading_')]")?
            .Select(n => n.GetAttributeValue("id", null))
            .Where(id => id != null)
            .ToList() ?? new List<string>();
    }

    private List<GridInfo> AnalyzeGrids(HtmlNode doc)
    {
        var grids = new List<GridInfo>();

        // Find all panels with data-label="Show_grid"
        var gridPanels = doc.SelectNodes("//div[contains(@data-label, 'grid')]");
        if (gridPanels == null) return grids;

        foreach (var gridPanel in gridPanels)
        {
            // Look for template sections within the grid panel
            var templates = gridPanel.SelectNodes(".//script[@type='axure-repeater-template']");
            var headerCells = gridPanel.SelectNodes(".//div[contains(@class, 'ax_default box_')]")
                ?.Where(node => node.SelectNodes(".//div[contains(@class, 'text')]")?.Any() ?? false)
                ?.Select(node => node.SelectSingleNode(".//div[contains(@class, 'text')]").InnerText?.Trim())
                ?.Where(text => !string.IsNullOrEmpty(text))
                ?.ToList() ?? new List<string>();

            if (templates != null || headerCells.Any())
            {
                var grid = new GridInfo
                {
                    ID = gridPanel.GetAttributeValue("id", null),
                    Columns = new List<string>()
                };

                // Add header columns
                foreach (var headerText in headerCells)
                {
                    if (!grid.Columns.Contains(headerText))
                    {
                        grid.Columns.Add(headerText);
                    }
                }

                // Process template columns if they exist
                if (templates != null)
                {
                    foreach (var template in templates)
                    {
                        var templateCells = template.SelectNodes(".//div[contains(@data-label, 'rp_')]");
                        if (templateCells != null)
                        {
                            foreach (var cell in templateCells)
                            {
                                var textNode = cell.SelectSingleNode(".//div[contains(@class, 'text')]");
                                if (textNode != null)
                                {
                                    var columnText = textNode.InnerText?.Trim();
                                    if (!string.IsNullOrEmpty(columnText) && !grid.Columns.Contains(columnText))
                                    {
                                        grid.Columns.Add(columnText);
                                    }
                                }
                            }
                        }
                    }
                }

                // Remove any special system columns like "Edit" or "Remove"
                grid.Columns.RemoveAll(col =>
                    col.Equals("Edit", StringComparison.OrdinalIgnoreCase) ||
                    col.Equals("Remove", StringComparison.OrdinalIgnoreCase));

                if (grid.Columns.Any())
                {
                    grids.Add(grid);
                }
            }
        }

        return grids;
    }

    private List<RadioInfo> AnalyzeRadioButtons(HtmlDocument doc)
    {
        var radioButtons = new List<RadioInfo>();
        var radioNodes = doc.DocumentNode.SelectNodes("//*[contains(@class, 'radio')]");

        if (radioNodes != null)
        {
            foreach (var radioNode in radioNodes)
            {
                var radio = new RadioInfo
                {
                    ID = radioNode.GetAttributeValue("id", null),
                    GroupName = radioNode.GetAttributeValue("data-group", null) // Use data-group attribute for grouping
                };
                radioButtons.Add(radio);
            }
        }

        return radioButtons;
    }

    private List<string> AnalyzeHiddenFields(HtmlDocument doc)
    {
        return doc.DocumentNode.SelectNodes("//*[@style and contains(@style, 'display:none')]")?
            .Select(n => n.GetAttributeValue("id", null))
            .Where(id => id != null)
            .ToList() ?? new List<string>();
    }

    private List<ButtonInfo> AnalyzeButtons(HtmlDocument doc)
    {
        var buttons = new List<ButtonInfo>();
        var buttonNodes = doc.DocumentNode.SelectNodes("//*[contains(@class, 'button') or contains(@class, 'primary_button')]");

        if (buttonNodes != null)
        {
            foreach (var buttonNode in buttonNodes)
            {
                var button = new ButtonInfo
                {
                    ID = buttonNode.GetAttributeValue("id", null),
                    Text = buttonNode.InnerText?.Trim(),
                    Type = buttonNode.GetAttributeValue("data-label", ""),
                    OnClick = buttonNode.GetAttributeValue("onclick", null)
                };
                buttons.Add(button);
            }
        }

        return buttons;
    }

    private List<NavigationInfo> AnalyzeNavigationElements(HtmlDocument doc)
    {
        var navElements = new List<NavigationInfo>();
        var elements = doc.DocumentNode.SelectNodes("//*[contains(@class, 'nav') or contains(@data-label, 'menu')]");

        if (elements != null)
        {
            foreach (var element in elements)
            {
                var navInfo = new NavigationInfo
                {
                    ID = element.GetAttributeValue("id", null),
                    Text = element.InnerText?.Trim(),
                    Type = element.GetAttributeValue("data-label", ""),
                    ParentID = element.ParentNode?.GetAttributeValue("id", null)
                };
                navElements.Add(navInfo);
            }
        }

        return navElements;
    }

    private List<FunctionInfo> AnalyzeScriptContents(HtmlDocument doc)
    {
        var functionInfos = new Dictionary<string, FunctionInfo>();
        var scriptNodes = doc.DocumentNode.SelectNodes("//script");

        if (scriptNodes != null)
        {
            foreach (var scriptNode in scriptNodes)
            {
                AnalyzeFunctions(scriptNode.InnerText, functionInfos);
            }
        }

        // Analyze click handlers and other events
        AnalyzeEventHandlers(doc, functionInfos);

        return functionInfos.Values.ToList();
    }

    private void AnalyzeFunctions(string scriptContent, Dictionary<string, FunctionInfo> functionInfos)
    {
        var functionRegex = new Regex(@"function\s+(\w+)\s*\([^)]*\)\s*\{([^}]+)\}");
        foreach (Match match in functionRegex.Matches(scriptContent))
        {
            var name = match.Groups[1].Value;
            var body = match.Groups[2].Value;

            var functionInfo = new FunctionInfo
            {
                Name = name,
                Definition = body,
                IsPopup = DetermineIfPopup(body)
            };

            if (!functionInfos.ContainsKey(name))
            {
                functionInfos[name] = functionInfo;
            }
        }
    }

    private bool DetermineIfPopup(string functionBody)
    {
        return functionBody.Contains("modal") ||
               functionBody.Contains("popup") ||
               functionBody.Contains("dialog") ||
               functionBody.Contains("show(") ||
               functionBody.Contains("display");
    }

    private void AnalyzeEventHandlers(HtmlDocument doc, Dictionary<string, FunctionInfo> functionInfos)
    {
        var elements = doc.DocumentNode.SelectNodes("//*[@onclick or @data-toggle]");
        if (elements != null)
        {
            foreach (var element in elements)
            {
                var onclick = element.GetAttributeValue("onclick", "");
                if (!string.IsNullOrEmpty(onclick))
                {
                    foreach (var functionInfo in functionInfos.Values)
                    {
                        if (onclick.Contains(functionInfo.Name))
                        {
                            functionInfo.Events.Add(new PopEventInfo
                            {
                                ComponentId = element.GetAttributeValue("id", "Unknown"),
                                EventName = "onclick",
                                EventValue = onclick
                            });
                        }
                    }
                }

                // Check for modal/popup triggers
                var dataToggle = element.GetAttributeValue("data-toggle", "");
                if (dataToggle == "modal")
                {
                    var targetId = element.GetAttributeValue("data-target", "");
                    if (!string.IsNullOrEmpty(targetId))
                    {
                        foreach (var functionInfo in functionInfos.Values.Where(f => f.IsPopup))
                        {
                            functionInfo.Events.Add(new PopEventInfo
                            {
                                ComponentId = element.GetAttributeValue("id", "Unknown"),
                                EventName = "data-toggle",
                                EventValue = targetId
                            });
                        }
                    }
                }
            }
        }
    }

    private LayoutInfo AnalyzeLayout(HtmlDocument doc)
    {
        return new LayoutInfo
        {
            HasHeader = doc.DocumentNode.SelectNodes("//*[contains(@class, 'header')]") != null,
            HasFooter = doc.DocumentNode.SelectNodes("//*[contains(@class, 'footer')]") != null,
            HasSidebar = doc.DocumentNode.SelectNodes("//*[contains(@class, 'static_nav')]") != null,
            MainContentAreas = AnalyzeMainContentAreas(doc)
        };
    }

    private List<ContentAreaInfo> AnalyzeMainContentAreas(HtmlDocument doc)
    {
        var contentAreas = new List<ContentAreaInfo>();
        var areas = doc.DocumentNode.SelectNodes("//*[contains(@class, 'box_1') or contains(@class, 'box_2') or contains(@class, 'box_3')]");

        if (areas != null)
        {
            foreach (var area in areas)
            {
                contentAreas.Add(new ContentAreaInfo
                {
                    ID = area.GetAttributeValue("id", null),
                    Title = area.SelectSingleNode(".//div[contains(@class, 'text')]")?.InnerText?.Trim(),
                    Type = DetermineContentAreaType(area)
                });
            }
        }

        return contentAreas;
    }

    private string DetermineContentAreaType(HtmlNode area)
    {
        var dataLabel = area.GetAttributeValue("data-label", "").ToLower();
        if (dataLabel.Contains("grid")) return "Grid";
        if (dataLabel.Contains("form")) return "Form";
        if (dataLabel.Contains("info")) return "Info";
        if (dataLabel.Contains("details")) return "Details";
        return "Other";
    }

    private HtmlNode FindContentRoot(HtmlNode rootNode)
    {
        // First try to find div with id containing "base"
        var mainPanel = rootNode.Descendants("div")
                               .FirstOrDefault(n => n.GetAttributeValue("id", "")
                                                   .Contains("base", StringComparison.OrdinalIgnoreCase));
        if (mainPanel != null)
        {
            return mainPanel;
        }

        // Fallback to body if no base div found
        return rootNode.Descendants("body").FirstOrDefault() ?? rootNode;
    }

    private List<CustomControl> AnalyzeCustomControls(HtmlNode contentRoot)
    {
        var controls = new List<CustomControl>();
        var nodes = contentRoot.SelectNodes("//*[@data-label]");

        if (nodes == null) return controls;

        // First pass - gather all controls
        foreach (var node in nodes)
        {
            var classes = node.GetAttributeValue("class", "").Split(' ');
            var dataLabel = node.GetAttributeValue("data-label", "");
            var isDisabled = classes.Contains("disabled");
            var inputNode = node.SelectSingleNode("./input");
            var textNode = node.SelectSingleNode("./textarea");
            var typeInput = inputNode?.GetAttributeValue("type", "") ?? node.GetAttributeValue("type", "");
            if (textNode != null)
            {
                typeInput = "textarea";
            }
  
            var controlType = DetermineCustomControlType(classes, typeInput, dataLabel);

            // Skip if this ID or LabelId is already processed
            if (controls.Any(c => c.Id == node.Id || c.LabelId == node.Id))
            {
                var ct = controls.FirstOrDefault(c => c.Id == node.Id || c.LabelId == node.Id);
                ct.Type = controlType;
                continue;
            }
                

            if (controlType != null)
            {
                var control = new CustomControl
                {
                    Id = node.Id,
                    Type = controlType,
                    DataLabel = dataLabel,
                    IsDisabled = isDisabled,
                    IsGenerated = controlType.Equals("Label", StringComparison.OrdinalIgnoreCase)
                };
                if (controlType.Equals("Panel"))
                {
                    control.LabelText = node.SelectSingleNode(".//div[@class='text ']//p//span")?.InnerText.Trim() ??
                                      node.SelectSingleNode(".//div[@class='text ']//p")?.InnerText.Trim() ??
                                      node.SelectSingleNode(".//div[@class='text ']")?.InnerText.Trim();
                }
                if (dataLabel.StartsWith("lbl_", StringComparison.OrdinalIgnoreCase))
                {
                    // This is a label
                    control.LabelText = node.SelectSingleNode(".//div[@class='text ']//p//span")?.InnerText.Trim() ??
                                      node.SelectSingleNode(".//div[@class='text ']//p")?.InnerText.Trim()??
                                      node.SelectSingleNode(".//div[@class='text ']")?.InnerText.Trim();

                    // Find matching field
                    var fieldDataLabel = dataLabel.Substring(4); // Remove "lbl_"
                    var fieldNode = nodes.FirstOrDefault(n =>
                        n.GetAttributeValue("data-label", "").Equals(fieldDataLabel, StringComparison.OrdinalIgnoreCase));

                    if (fieldNode != null)
                    {
                        var type = DetermineCustomControlType(fieldNode.GetAttributeValue("class", "").Split(' '), typeInput, dataLabel);
                        var fieldControl = new CustomControl
                        {
                            Id = fieldNode.Id,
                            Type = type,
                            DataLabel = fieldDataLabel,
                            LabelId = control.Id,
                            LabelText = control.LabelText,
                            IsDisabled = fieldNode.GetAttributeValue("class", "").Contains("disabled"),
                            IsGenerated = !string.IsNullOrEmpty(type) && type.Equals("Label", StringComparison.OrdinalIgnoreCase)
                        };

                        // Handle dropdown options
                        if (fieldControl.Type == "DropDown")
                        {
                            fieldControl.Options = fieldNode.SelectNodes(".//option")?
                                .Select(o => o.InnerText.Trim())
                                .Where(text => !string.IsNullOrEmpty(text))
                                .ToList() ?? new List<string>();
                        }

                        controls.Add(fieldControl);
                    }
                }
                else if (!dataLabel.StartsWith("lbl_", StringComparison.OrdinalIgnoreCase))
                {
                    // This is a field, look for matching label
                    var labelDataLabel = "lbl_" + dataLabel;
                    var labelNode = nodes.FirstOrDefault(n =>
                        n.GetAttributeValue("data-label", "").Equals( labelDataLabel, StringComparison.OrdinalIgnoreCase));

                    if (labelNode != null)
                    {
                        control.LabelId = labelNode.Id;
                        control.LabelText = labelNode.SelectSingleNode(".//div[@class='text ']//p//span")?.InnerText.Trim() ??
                                      labelNode.SelectSingleNode(".//div[@class='text ']//p")?.InnerText.Trim() ??
                                      labelNode.SelectSingleNode(".//div[@class='text ']")?.InnerText.Trim();

                        // Handle dropdown options
                        if (control.Type == "DropDown")
                        {
                            control.Options = node.SelectNodes(".//option")?
                                .Select(o => o.InnerText.Trim())
                                .Where(text => !string.IsNullOrEmpty(text))
                                .ToList() ?? new List<string>();
                        }
                    }
                    else if (control.Type == "Button")
                    {
                        control.LabelText = node.SelectSingleNode(".//div[@class='text']//span")?.InnerText.Trim() ??
                                          node.SelectSingleNode(".//div[@class='text']")?.InnerText.Trim();
                    }
                }

                controls.Add(control);
            }
        }

        // Second pass - match labels with controls
        foreach (var control in controls.Where(c => c.Type != "Label"))
        {
            var matchingLabel = controls.FirstOrDefault(l =>
                l.Type == "Label" &&
                l.DataLabel == control.DataLabel);

            if (matchingLabel != null)
            {
                control.LabelId = matchingLabel.Id;
                control.LabelText = matchingLabel.LabelText;
            }
        }

        return controls;
    }

    private string DetermineCustomControlType(string[] classes, string typeInput, string dataLabel)
    {
        if (dataLabel.Contains("pnl_")) return "Panel";
        if (typeInput.Contains("date")) return "Date";
        if (typeInput.Contains("number")) return "Number";
        if (typeInput.Contains("file")) return "File";
        if (typeInput.Contains("textarea")) return "TextArea";
        if (classes.Contains("label")) return "Label";
        if (classes.Contains("text_field")) return "TextBox";
        if (classes.Contains("date")) return "Date";
        if (classes.Contains("droplist")) return "DropDown";
        if (classes.Contains("button")) return "Button";
        return null;
    }
    #endregion
}
