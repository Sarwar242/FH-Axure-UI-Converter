using Core.Models;
using Core.Models.Enums;
using System.Text.RegularExpressions;

namespace Core.Generators;

public static class GenHelper
{
    public static string CapitalizeFirstLetter(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return str;
        }

        return char.ToUpper(str[0]) + str.Substring(1);
    }
    
    public static string LowerValue(string v)
    {
        if (string.IsNullOrEmpty(v))
        {
            return v;
        }

        return v.ToLower();
    }
    public static string GetColumnPropName(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return str;
        }

        // Remove special characters using regular expression
        string cleanedStr = Regex.Replace(str, "[^a-zA-Z0-9 ]+", "");

        // Split, trim, and join words with underscores
        return string.Join("_", cleanedStr.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
    }

    public static ModelInfo CreateGridModel(GridInfo gridInfo)
    {
        if (gridInfo == null) { return default; }
        var model = new ModelInfo();
        model.Id = gridInfo.Id;
        model.Name = CapitalizeFirstLetter(gridInfo.Id) + "Model";
        model.ClassType = ClassTypes.Class;
        model.AccessModifier = "public";
        model.Properties = new List<PropertyInfo>();
        var propInfos = new List<PropertyInfo>();
        foreach (var prop in gridInfo.Columns)
        {
            var propinfo = new PropertyInfo();
            propinfo.Name = GetColumnPropName(prop);
            propinfo.DataType = "string";
            propinfo.AccessModifier = "public";
            propinfo.HasGetter = true;
            propinfo.HasSetter = true;
            propInfos.Add(propinfo);
        }

        if (gridInfo.Columns.Where(_ => _.Equals("id", StringComparison.OrdinalIgnoreCase)).Count() == 0)
        {
            model.Properties.Add(new PropertyInfo
            {
                Name = "Id",
                DataType = "string",
                AccessModifier = "public",
                HasGetter = true,
                HasSetter = true
            });
        }
        
        if (propInfos!=null)
            model.Properties.AddRange(propInfos);

        return model;
    }

    public static string GetPageName(string OriginalFilePath, bool isPopup)
    {
        // Extract page title from analysis or generate from path
        var fileName = Path.GetFileNameWithoutExtension(OriginalFilePath ?? "");
        var words = fileName.Split('_')
    .Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower())
    .ToList();
        var combined = string.Join("", words);

        return combined + "UI";
    }

    public static string GetPageTitle(string OriginalFilePath, bool isPopup)
    {
        // Extract page title from analysis or generate from path
        var fileName = Path.GetFileNameWithoutExtension(OriginalFilePath ?? "");
        var words = fileName.Split('_')
    .Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower())
    .ToList();
        var combined = string.Join(" ", words);

        return Regex.Replace(combined, "(?<=[a-z])(?=[A-Z])", " ");
    }

    // Jaro-Winkler similarity calculation
    public static double CalculateStringSimilarity(string str1, string str2)
    {
        // Implementation of Jaro-Winkler distance
        double jaroDistance = CalculateJaroDistance(str1, str2);

        // Prefix scaling factor
        int prefixLength = GetCommonPrefixLength(str1, str2);
        double winklerAdjustment = 0.1 * prefixLength * (1 - jaroDistance);

        return jaroDistance + winklerAdjustment;
    }

    public static double CalculateJaroDistance(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
            return 0.0;

        int len1 = str1.Length;
        int len2 = str2.Length;

        // Maximum distance for matching
        int matchDistance = Math.Max(len1, len2) / 2 - 1;

        // Matching flags
        bool[] str1Matches = new bool[len1];
        bool[] str2Matches = new bool[len2];

        int matches = 0;
        int transpositions = 0;

        // Find matching characters
        for (int i = 0; i < len1; i++)
        {
            for (int j = Math.Max(0, i - matchDistance);
                 j < Math.Min(len2, i + matchDistance + 1);
                 j++)
            {
                if (!str2Matches[j] && str1[i] == str2[j])
                {
                    str1Matches[i] = true;
                    str2Matches[j] = true;
                    matches++;
                    break;
                }
            }
        }

        // If no matches
        if (matches == 0) return 0.0;

        // Count transpositions
        int k = 0;
        for (int i = 0; i < len1; i++)
        {
            if (str1Matches[i])
            {
                while (!str2Matches[k]) k++;

                if (str1[i] != str2[k])
                    transpositions++;

                k++;
            }
        }

        // Calculate Jaro distance
        return ((double)matches / len1 +
                (double)matches / len2 +
                (double)(matches - transpositions / 2.0) / matches) / 3.0;
    }

    public static int GetCommonPrefixLength(string str1, string str2)
    {
        int maxLength = Math.Min(str1.Length, str2.Length);
        for (int i = 0; i < maxLength; i++)
        {
            if (char.ToLower(str1[i]) != char.ToLower(str2[i]))
                return i;
        }
        return maxLength;
    }

    public static ComponentMapping FindComponentMapping(CustomControl control, ComponentMappingConfig _customControlMappings)
    {
        var componentType = _customControlMappings.Components.FirstOrDefault(m =>
            control.Type.Contains(m.Name, StringComparison.OrdinalIgnoreCase));

        return componentType ?? _customControlMappings.Components.FirstOrDefault(m =>
            control.Name.Equals("TextBox", StringComparison.OrdinalIgnoreCase)) ?? new ComponentMapping();
    }

}
