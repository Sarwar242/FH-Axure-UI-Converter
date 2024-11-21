

using Microsoft.Extensions.Logging;

namespace Core.Logging;

public class ConverterLogger : IConverterLogger
{
    private readonly ILogger _logger;

    public ConverterLogger(ILogger<ConverterLogger> logger)
    {
        _logger = logger;
    }

    public void LogInformation(string message)
    {
        _logger.LogInformation(message);
    }

    public void LogWarning(string message)
    {
        _logger.LogWarning(message);
    }

    public void LogError(string message, Exception exception = null)
    {
        _logger.LogError(exception, message);
    }

    public void LogDebug(string message)
    {
        _logger.LogDebug(message);
    }
}