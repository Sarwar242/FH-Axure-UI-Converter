using Core.Models;

namespace Core.Models.Request;

public class GenerateAnyBlazorMethodRequest
{
    public MethodInfo? Method { get; set; }
    public HashSet<VariableInfo>? VariableInfos{ get; set; }
    public List<CodeBehindAnalysis>? CodeAnalysisList { get; set; }
    public List<KeywordReplacementRuleUI>? SessionMappingRules { get; set; }
    public List<string> PanelIDList { get; set; }
    public GenerateAnyBlazorMethodRequest()
    {
        
    }

    public GenerateAnyBlazorMethodRequest(MethodInfo? method, HashSet<VariableInfo>? variableInfos, List<CodeBehindAnalysis>? codeAnalysisList, List<KeywordReplacementRuleUI>? sessionMappingRules, List<string> panelIDList)
    {
        Method = method;
        VariableInfos = variableInfos;
        CodeAnalysisList = codeAnalysisList;
        SessionMappingRules = sessionMappingRules;
        PanelIDList = panelIDList;
    }
}
