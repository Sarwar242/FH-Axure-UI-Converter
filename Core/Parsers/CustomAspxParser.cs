using System.Text.RegularExpressions;
using Core.Models;
using HtmlAgilityPack;

namespace Core.Parsers;

public class CustomAspxParser
{
    private readonly string _mappingFilePath;
    private readonly List<string> _skipLabels = new List<string>
    {
        "master_background", "header_nav", "logo", "static_nav", "user_info",
        "setting", "logout", "content_pannel", "notification", "Menu",
        "search", "nav", "fav", "home", "square", "content_panel"
    };

    private List<FormSection> _sections = new();

    public CustomAspxParser(string mappingFilePath)
    {
        _mappingFilePath = mappingFilePath;
    }

    public async Task<AnalysisResult> ParseAspx(string filePath)
    {
        var doc = new HtmlDocument { OptionOutputOriginalCase = true };
        doc.Load(filePath);
        var root = FindContentRoot(doc.DocumentNode);

        // First pass - identify panels and their grids
        var panels = IdentifyPanels(root);
        var grids = IdentifyGrids(root);
        _sections = OrganizePanelsAndGrids(panels, grids);

        // Second pass - identify controls within panels
        var controls = new List<CustomControl>();
        foreach (var section in _sections)
        {
            var panelNode = root.SelectSingleNode($"//*[@id='{section.PanelId}']");
            if (panelNode != null)
            {
                var sectionControls = ProcessPanelControls(panelNode, panels);
                section.Fields.AddRange(sectionControls);
                controls.AddRange(sectionControls);
            }
        }

        return new AnalysisResult
        {
            OriginalFilePath = filePath,
            CustomControls = controls,
            Grids = grids,
            Sections = _sections
        };
    }

    private List<HtmlNode> IdentifyPanels(HtmlNode root)
    {
        var panels = new List<HtmlNode>();
        var potentialPanels = root.SelectNodes("//*[contains(@class, 'box_1') or contains(@data-label, 'pnl_')]");

        if (potentialPanels == null) return panels;

        foreach (var panel in potentialPanels)
        {
            // Skip if panel has a skip label
            var dataLabel = panel.GetAttributeValue("data-label", "").ToLower();
            if (_skipLabels.Contains(dataLabel)) continue;
            if (IsEffectivelyEmpty(panel)) continue;

            // Check if followed by empty box
            var nextSibling = panel.NextSibling;
            while (nextSibling != null && nextSibling.NodeType != HtmlNodeType.Element)
            {
                nextSibling = nextSibling.NextSibling;
            }

            if (nextSibling != null &&
                ((nextSibling.GetAttributeValue("class", "").Contains("box_") && 
                IsEffectivelyEmpty(nextSibling)) || 
                nextSibling.GetAttributeValue("class", "").Contains("label", StringComparison.OrdinalIgnoreCase)) && 
                !nextSibling.GetAttributeValue("id", "").Contains("-1") && 
                !nextSibling.GetAttributeValue("data-label", "").Contains("edit"))
            {
                panels.Add(panel);
            }
        }

        return panels;
    }

    private List<GridInfo> IdentifyGrids(HtmlNode root)
    {
        var grids = new List<GridInfo>();
        var gridNodes = root.SelectNodes("//div[contains(@data-label, 'grid')]");

        if (gridNodes == null) return grids;

        foreach (var gridNode in gridNodes)
        {
            // Look for repeater template
            var template = gridNode.SelectSingleNode(".//script[@type='axure-repeater-template']");
            if (template != null)
            {
                var scriptContent = template.InnerHtml;
                var tempDoc = new HtmlDocument();
                tempDoc.LoadHtml(scriptContent);
                var grid = new GridInfo
                {
                    Id = gridNode.Id,
                    DataLabel = gridNode.GetAttributeValue("data-label", ""),
                    Columns = ExtractGridColumns(tempDoc),
                    ColumnsDataLbls = ExtractGridColumnLabels(tempDoc)
                };
                grids.Add(grid);
            }
        }

        return grids;
    }

    private List<FormSection> OrganizePanelsAndGrids(List<HtmlNode> panels, List<GridInfo> grids)
    {
        var sections = new List<FormSection>();

        foreach (var panel in panels)
        {
            var section = new FormSection
            {
                PanelId = panel.Id,
                PanelLabel = ExtractPanelLabel(panel)
            };

            // Look for grid after panel
            var nextGrid = FindNextGrid(panels, panel, grids);
            if (nextGrid != null)
            {
                section.AssociatedGrid = nextGrid;

                // Look for add/update buttons between panel and grid
                var addButton = FindButtonBetween(panel, "ADD", "add");
                var updateButton = FindButtonBetween(panel, "UPDATE", "update");

                section.AddButtonId = addButton?.Id;
                section.UpdateButtonId = updateButton?.Id;
            }

            sections.Add(section);
        }

        return sections;
    }

    private List<CustomControl> ProcessPanelControls(HtmlNode panelNode, List<HtmlNode> allPanels)
    {
        var controls = new List<CustomControl>();

        // Find the next panel node to establish boundary
        var nextPanel = allPanels
            .SkipWhile(p => p.Id != panelNode.Id)
            .Skip(1)
            .FirstOrDefault();

        // First try to find labels inside the panel
        var labelNodes = panelNode.SelectNodes(".//div[contains(@class, 'label')]");

        // If no labels found inside, look between this panel and next panel
        if (labelNodes == null || !labelNodes.Any())
        {
            var xpath = "following::div[contains(@class, 'label')]";
            if (nextPanel != null)
            {
                // Only get labels before the next panel
                xpath = $"following::div[contains(@class, 'label')]" +
                       $"[count(.|//*[@id='{nextPanel.Id}']/preceding::*) = " +
                       $"count(//*[@id='{nextPanel.Id}']/preceding::*)]";
            }
            labelNodes = panelNode.SelectNodes(xpath);
        }

        if (labelNodes == null) return controls;

        foreach (var labelNode in labelNodes)
        {
            if (ShouldSkipLabel(labelNode)) continue;

            // Find the next input but stop at next panel boundary
            var nextInput = FindNextInputWithinBoundary(labelNode, nextPanel);
            if (nextInput != null)
            {
                var control = CreateCustomControl(labelNode, nextInput);
                if (control != null)
                {
                    controls.Add(control);
                }
            }
        }

        return controls;
    }

    private HtmlNode FindNextInputWithinBoundary(HtmlNode labelNode, HtmlNode boundaryNode)
    {
        var xpath = "following::*[self::input or self::select or self::textarea][1]";

        if (boundaryNode != null)
        {
            // Only get input before the boundary node
            xpath = $"following::*[self::input or self::select or self::textarea]" +
                   $"[count(.|//*[@id='{boundaryNode.Id}']/preceding::*) = " +
                   $"count(//*[@id='{boundaryNode.Id}']/preceding::*)]" +
                   "[1]";
        }

        var nextNode = labelNode.SelectSingleNode(xpath);
        if (nextNode != null && !IsDescendantOfLabel(nextNode))
        {
            return nextNode;
        }
        return null;
    }
    private CustomControl CreateCustomControl(HtmlNode labelNode, HtmlNode inputNode)
    {
        var control = new CustomControl
        {
            Id = inputNode.ParentNode.Id,
            LabelId = labelNode.Id,
            DataLabel = inputNode.ParentNode.GetAttributeValue("data-label", ""),
            LabelText = ExtractLabelText(labelNode),
            IsDisabled = inputNode.ParentNode.GetAttributeValue("class", "").Contains("disabled"),
            Name = DetermineControlType(inputNode),
            Type = DetermineControlType(inputNode)
        };

        if (control.Type == "DropDown")
        {
            control.Options = inputNode.SelectNodes(".//option")?
                .Select(o => o.InnerText.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList() ?? new List<string>();
        }

        return control;
    }

    private string DetermineControlType(HtmlNode node)
    {
        var tagName = node.Name.ToLower();
        var type = node.GetAttributeValue("type", "").ToLower();
        var classes = node.GetAttributeValue("class", "").ToLower().Split(' ');

        return (tagName, type, classes) switch
        {
            var (_, t, _) when t == "date" => "Date",
            var (_, t, _) when t == "number" => "Number",
            var (_, t, _) when t == "file" => "File",
            var (tag, _, _) when tag == "textarea" => "TextArea",
            var (_, _, c) when c.Contains("droplist") => "DropDown",
            var (tag, _, _) when tag.Contains("select") => "DropDown",
            var (_, _, c) when c.Contains("checkbox") => "Switch",
            var (_, _, c) when c.Contains("text_field") => "TextBox",
            _ => "TextBox"
        };
    }

    // Helper methods
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

    private List<string> ExtractGridColumns(HtmlDocument template)
    {
        var columns = new List<string>();
        var headerCells = template.DocumentNode.SelectNodes(".//div[contains(@class, 'box_1')]")?
            .Where(n => n.SelectSingleNode(".//div[contains(@class, 'text')]") != null);

        if (headerCells != null)
        {
            foreach (var cell in headerCells)
            {
                var text = cell.SelectSingleNode(".//div[contains(@class, 'text')]")?.InnerText?.Trim();
                if (!string.IsNullOrEmpty(text) && !text.Equals("Edit", StringComparison.OrdinalIgnoreCase)
                    && !text.Equals("Delete", StringComparison.OrdinalIgnoreCase))
                {
                    columns.Add(text);
                }
            }
        }

        return columns;
    }

    private List<string> ExtractGridColumnLabels(HtmlDocument template)
    {
        var labels = new List<string>();
        var cells = template.DocumentNode.SelectNodes(".//div[contains(@class, 'box_1')]")
            ?.Where(n => n.GetAttributeValue("data-label", "").StartsWith("cell_"));

        if (cells != null)
        {
            foreach (var cell in cells)
            {
                var label = cell.GetAttributeValue("data-label", "");
                if (!string.IsNullOrEmpty(label) && !label.Contains("edit") && !label.Contains("delete"))
                {
                    labels.Add(label.Substring(5)); // Remove "cell_" prefix
                }
            }
        }

        return labels;
    }

    private string ExtractPanelLabel(HtmlNode panel) =>
        panel.SelectSingleNode(".//div[contains(@class, 'text')]")?
            .InnerText?.Trim() ?? string.Empty;

    private GridInfo FindNextGrid(List<HtmlNode> panels, HtmlNode currentPanel, List<GridInfo> grids)
    {
        // Get the next panel if it exists
        var nextPanel = panels
            .SkipWhile(p => p.Id != currentPanel.Id)
            .Skip(1)
            .FirstOrDefault();

        // Find the next grid node after current panel
        var nextGridNode = currentPanel.SelectSingleNode("following::div[contains(@data-label, 'grid')][1]");
        if (nextGridNode == null) return null;

        // If there's no next panel, this grid belongs to current panel
        if (nextPanel == null)
        {
            return grids.FirstOrDefault(g => g.Id == nextGridNode.Id);
        }

        // Check if grid comes before next panel
        // Convert node IDs to integers for comparison (assuming format uXXX)
        var currentPanelNum = ExtractNumberFromId(currentPanel.Id);
        var nextPanelNum = ExtractNumberFromId(nextPanel.Id);
        var gridNum = ExtractNumberFromId(nextGridNode.Id);

        // If grid number is between current and next panel numbers, it belongs to current panel
        if (gridNum > currentPanelNum && gridNum < nextPanelNum)
        {
            return grids.FirstOrDefault(g => g.Id == nextGridNode.Id);
        }

        return null;
    }

    private int ExtractNumberFromId(string id)
    {
        // Remove 'u' prefix and parse remaining number
        if (id != null && id.StartsWith("u", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(id.Substring(1), out int number))
            {
                return number;
            }
        }
        return -1;
    }

    private HtmlNode FindButtonBetween(HtmlNode panel, string buttonText, string buttonLabel)
    {
        var xpath = $"following::div[contains(@class, 'button') or contains(@class, 'primary_button')]" +
                   $"[contains(translate(., 'abcdefghijklmnopqrstuvwxyz', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'), '{buttonText}') " +
                   $"or contains(@data-label, '{buttonLabel}')][1]";
        return panel.SelectSingleNode(xpath);
    }

    private bool ShouldSkipLabel(HtmlNode labelNode)
    {
        var dataLabel = labelNode.GetAttributeValue("data-label", "").ToLower();
        return _skipLabels.Any(skip => dataLabel.Contains(skip));
    }

    private HtmlNode FindNextInput(HtmlNode labelNode)
    {
        var nextNode = labelNode.SelectSingleNode("following::*[self::input or self::select or self::textarea][1]");
        if (nextNode != null && !IsDescendantOfLabel(nextNode))
        {
            return nextNode;
        }
        return null;
    }

    private bool IsDescendantOfLabel(HtmlNode node)
    {
        var parent = node.ParentNode;
        while (parent != null)
        {
            if (parent.GetAttributeValue("class", "").Contains("label"))
            {
                return true;
            }
            parent = parent.ParentNode;
        }
        return false;
    }

    private string ExtractLabelText(HtmlNode labelNode) =>
        labelNode.SelectSingleNode(".//div[contains(@class, 'text')]//p//span")?.InnerText?.Trim() ??
        labelNode.SelectSingleNode(".//div[contains(@class, 'text')]//p")?.InnerText?.Trim() ??
        labelNode.SelectSingleNode(".//div[contains(@class, 'text')]")?.InnerText?.Trim() ?? string.Empty;

    private HtmlNode FindContentRoot(HtmlNode rootNode) =>
        rootNode.Descendants("div")
            .FirstOrDefault(n => n.GetAttributeValue("id", "")
                .Contains("base", StringComparison.OrdinalIgnoreCase)) ??
        rootNode.Descendants("body").FirstOrDefault() ?? rootNode;
}