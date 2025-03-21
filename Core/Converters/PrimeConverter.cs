﻿using Core.Generators;
using Core.Logging;
using Core.Models;
using Core.Parsers;

namespace Core.Converters;

public class PrimeConverter : IPrimeConverter
{
    private readonly IConverterLogger _logger;
    private readonly AspxParser _aspxParser;
    private readonly CustomAspxParser _aspxParserCustom;
    private readonly BlazorComponentGenerator _blazorComponentGenerator;
    private readonly CustomComponentGenerator _customComponentGenerator;

    public PrimeConverter(string mappingFilePath, IConverterLogger logger)
    {
        _logger = logger;
        _aspxParser = new AspxParser(mappingFilePath);
        _aspxParserCustom = new CustomAspxParser(mappingFilePath);
        _blazorComponentGenerator = new BlazorComponentGenerator(mappingFilePath);
        _customComponentGenerator = new CustomComponentGenerator(mappingFilePath);
    }

    public async Task<string> ConvertToBlazor(string aspxFilePath, bool isPopup, string componentName, List<string> popupComponents)
    {
        _logger.LogInformation($"Starting conversion for ASPX file: {aspxFilePath}");
        try
        {
            //var codeBehindContent = await File.ReadAllTextAsync(codeBehindFilePath);
            var analysisResult = await _aspxParserCustom.ParseAspx(aspxFilePath);
            _logger.LogInformation("ASPX parsing completed successfully");

            //var codeBehindAnalysis = _codeBehindAnalyzer.AnalyzeCodeBehind(codeBehindContent);
            _logger.LogInformation("Code-behind analysis completed successfully");
            string pageName = Path.GetFileNameWithoutExtension(aspxFilePath);

            //var blazorComponent = _blazorComponentGenerator.GenerateComponent(analysisResult, componentName, isPopup);
            var blazorComponent = _customComponentGenerator.GenerateComponent(analysisResult, componentName);
            _logger.LogInformation("Conversion completed successfully");
            return blazorComponent;
        }catch (Exception ex) {
            _logger.LogError("An error occurred during conversion", ex);
            throw;
        }
    }
    
    public async Task<AnalysisResult?> AspxAnalysis(string aspxFilePath)
    {
        try
        {
            //var codeBehindContent = await File.ReadAllTextAsync(codeBehindFilePath);
            AnalysisResult aspxAnalysisResult = await _aspxParser.ParseAspx(aspxFilePath);

            return aspxAnalysisResult;
        }catch (Exception ex) {
            _logger.LogError("An error occurred during analysis", ex);
            throw;
        }
    }
}