using System.Diagnostics;
using System.Text.RegularExpressions;
using Core.Converters;
using Core.Models;
using FH_Axure_UI_Converter.Models;
using Microsoft.AspNetCore.Mvc;

namespace FH_Axure_UI_Converter.Controllers;

public class HomeController : Controller
{
    #region Initialization and Construction
    private readonly IConfiguration _configuration;
    private readonly IPrimeConverter _converter;
    private readonly IWebHostEnvironment _environment;
    private string _uiSaveDirectory;
    public HomeController(IConfiguration configuration, IPrimeConverter converter, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _converter = converter;
        _environment = environment;
        _uiSaveDirectory = String.Empty;
    }
    #endregion

    #region Page Routing Methods
    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.Message = TempData["Message"];
        ViewBag.ConvertedFiles = TempData["ConvertedFiles"];
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UxcConverter(FileUploadViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }
        if (model.HtmlFiles == null || model.HtmlFiles.Count == 0)
        {
            ModelState.AddModelError("", "Please select valid files.");
            return View("Index", model);
        }
        string saveDirectory = Path.Combine(Directory.GetCurrentDirectory(), model.SaveDirectory);
        if (!Directory.Exists(saveDirectory))
        {
            ModelState.AddModelError("", "The specified directory does not exist.");
            return Json(new
            {
                success = true,
                message = $"{saveDirectory} The specified directory does not exist.",
            });
        }

        if (!String.IsNullOrEmpty(model.UiSaveDirectory))
        {
            _uiSaveDirectory = Path.Combine(Directory.GetCurrentDirectory(), model.UiSaveDirectory);
        }
        List<string> convertedFilePaths = new List<string>();
        Dictionary<string, string> componentNameMap = new Dictionary<string, string>();
        List<string> popupComponents = new List<string>();
        Dictionary<string, IFormFile> fileAnalysis = new Dictionary<string, IFormFile>();

        foreach (var uploadedFile in model.HtmlFiles.Where(f => f.FileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)))
        {
            var analysis = await AnalyzeAspxFile(uploadedFile);
            fileAnalysis[uploadedFile.FileName] = (uploadedFile);
        }

        // Second pass: Identify popups
        foreach (var entry in fileAnalysis)
        {
            string componentName = Path.GetFileNameWithoutExtension(entry.Key);
            componentName = Regex.Replace(componentName, "^(?:BU_|bu_)", "", RegexOptions.IgnoreCase);

            componentNameMap[entry.Key] = componentName;
        }

        // Third pass: Convert files
        foreach (var entry in fileAnalysis)
        {
            var aspxFile = entry.Value;
            bool isPopup = popupComponents.Contains(componentNameMap[entry.Key]);

            var blazorContent = await ConvertAspx(aspxFile, isPopup, componentNameMap[entry.Key], popupComponents);
            var convertedFilePath = "";
            if (!String.IsNullOrEmpty(_uiSaveDirectory))
            {
                convertedFilePath = await SaveConvertedFile(blazorContent, componentNameMap[entry.Key] + ".razor", _uiSaveDirectory, true);
            }
            else
            {
                convertedFilePath = await SaveConvertedFile(blazorContent, componentNameMap[entry.Key] + ".razor", saveDirectory, true);
            }
            convertedFilePaths.Add(convertedFilePath);
        }

        return Json(new
        {
            success = true,
            message = $"{saveDirectory}\n{_uiSaveDirectory}",
            files = convertedFilePaths
        });
    }

    private async Task<string> SaveConvertedFile(string content, string originalFileName, string saveDirectory, bool isRazor)
    {
        string _SaveDirectory = saveDirectory;

        // Ensure the directory exists
        if (!Directory.Exists(_SaveDirectory))
        {
            Directory.CreateDirectory(_SaveDirectory);
        }

        string newFileName;
        if (isRazor)
        {
            newFileName = Path.Combine(_SaveDirectory, Path.GetFileNameWithoutExtension(originalFileName) + ".razor");
        }
        else
        {
            newFileName = Path.Combine(_SaveDirectory, Path.GetFileNameWithoutExtension(originalFileName) + ".cs");
        }

        // Ensure the file path is valid
        if (!Path.IsPathFullyQualified(newFileName))
        {
            newFileName = Path.GetFullPath(newFileName);
        }

        await System.IO.File.WriteAllTextAsync(newFileName, content);
        return newFileName;
    }
    #endregion

       

    #region Aspx Related Methods
    private async Task<AnalysisResult> AnalyzeAspxFile(IFormFile aspxFile)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var aspxPath = Path.Combine(tempDir, aspxFile.FileName);
        //var codeBehindPath = Path.Combine(tempDir, codeBehindFile.FileName);

        using (var aspxStream = new FileStream(aspxPath, FileMode.Create))
        {
            await aspxFile.CopyToAsync(aspxStream);
        }
        //using (var codeBehindStream = new FileStream(codeBehindPath, FileMode.Create))
        //{
        //    await codeBehindFile.CopyToAsync(codeBehindStream);
        //}

        var analysisResult = await _converter.AspxAnalysis(aspxPath);
        Directory.Delete(tempDir, true);

        return analysisResult;
    }

    private bool DetermineIfPopup(AnalysisResult analysisResult, string fileName, Dictionary<string, (IFormFile aspx, IFormFile codeBehind, AnalysisResult analysis)> fileAnalysis)
    {
        bool containsPopupFunctions = false;
        bool isTargetOfPopup = false;

        foreach (var functionInfo in analysisResult.ScriptAnalysis)
        {
            if (functionInfo.IsPopup)
            {
                containsPopupFunctions = true;
                break;
            }
        }

        // Check if this file is the target of a popup in any other file
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        foreach (var otherFile in fileAnalysis.Where(f => f.Key != fileName))
        {
            foreach (var functionInfo in otherFile.Value.analysis.ScriptAnalysis)
            {
                if (functionInfo.IsPopup && functionInfo.Url != null &&
                    functionInfo.Url.Contains(fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
                {
                    isTargetOfPopup = true;
                    break;
                }
            }
            if (isTargetOfPopup) break;
        }

        // A file is considered a popup if it's the target of a popup function
        // and doesn't contain popup functions itself
        return isTargetOfPopup && !containsPopupFunctions;
    }

    private async Task<string> ConvertAspx(IFormFile htmlFile, bool isPopup, string componentName, List<string> popupComponents)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var aspxPath = Path.Combine(tempDir, htmlFile.FileName);
        //var codeBehindPath = Path.Combine(tempDir, codeBehindFile.FileName);

        using (var aspxStream = new FileStream(aspxPath, FileMode.Create))
        {
            await htmlFile.CopyToAsync(aspxStream);
        }
         
        var blazorContent = await _converter.ConvertToBlazor(aspxPath, isPopup, componentName, popupComponents);
        Directory.Delete(tempDir, true);

        return blazorContent;
    }
    #endregion
}
