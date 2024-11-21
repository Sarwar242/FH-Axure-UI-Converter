namespace Core.Models;

public class KeywordReplacementRuleUI
{
    public string OldKeyword { get; set; }
    public string NewKeyword { get; set; }

    public KeywordReplacementRuleUI(string oldKeyword, string newKeyword)
    {
        OldKeyword = oldKeyword;
        NewKeyword = newKeyword;
    }

    public KeywordReplacementRuleUI() { }
}

