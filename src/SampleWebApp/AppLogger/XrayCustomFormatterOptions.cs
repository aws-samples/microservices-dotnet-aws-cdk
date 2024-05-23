using Microsoft.Extensions.Logging.Console;

namespace SampleWebApp.AppLogger;

/// <summary>
/// Options to Enable AWS X-Ray TraceId injection
/// </summary>
public class XrayCustomFormatterOptions : ConsoleFormatterOptions
{
    /// <summary>
    /// Enable X-Ray TraceId Injection as Log Suffix
    /// </summary>
    /// <value></value>
    public bool EnableTraceIdInjection { get; set; } = true;
}
