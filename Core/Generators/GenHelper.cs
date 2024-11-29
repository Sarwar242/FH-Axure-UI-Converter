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
}
