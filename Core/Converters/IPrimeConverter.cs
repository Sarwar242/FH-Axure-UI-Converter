

using Core.Models;

namespace Core.Converters;

public interface IPrimeConverter
{
    Task<string> ConvertToBlazor(string aspxFilePath, bool isPopup, string componentName, List<string> popupComponents);
    Task<AnalysisResult?> AspxAnalysis(string aspxFilePath);
}
