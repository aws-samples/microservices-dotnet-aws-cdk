using Amazon.XRay.Recorder.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;

namespace SampleWebApp.AppLogger;

/// <summary>
/// Custom Console Logging Formmater to add AWS X-Ray TraceId as suffix for logs 
/// </summary>
public class XrayCustomFormatter : ConsoleFormatter, IDisposable
{
    private bool isDisposed;
    private readonly string _padding = " ";
    private readonly IDisposable _optionsReloadToken;
    private XrayCustomFormatterOptions _formatterOptions;

    public XrayCustomFormatter(IOptionsMonitor<XrayCustomFormatterOptions> options)
        : base(nameof(XrayCustomFormatter)) =>
        (_optionsReloadToken, _formatterOptions) =
            (options.OnChange(ReloadLoggerOptions), options?.CurrentValue);

    private void ReloadLoggerOptions(XrayCustomFormatterOptions options) =>
        _formatterOptions = options;

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider scopeProvider,
        TextWriter textWriter)
    {

        string message =
            logEntry.Formatter(
                logEntry.State, logEntry.Exception);

        if (message == null || textWriter == null)
        {
            return;
        }

        //textWriter.Write(DateTime.UtcNow.ToString(_formatterOptions.TimestampFormat) + " ");
        textWriter.Write(logLevelString(logEntry.LogLevel));
        textWriter.Write(":");
        textWriter.Write(_padding);
        textWriter.Write(message);
        textWriter.Write(_padding);
        WriteTraceIdSuffix(textWriter);
        textWriter.Write(_padding);

        // exception message
        if (logEntry.Exception != null)
        {
            textWriter.Write("Exception: ");
            string newMessage = logEntry.Exception
                                         .ToString()?
                                         .Replace(Environment.NewLine, " ", StringComparison.Ordinal);
            textWriter.Write(newMessage);
        }

        textWriter.Write(Environment.NewLine);

    }

    private void WriteTraceIdSuffix(TextWriter textWriter)
    {
        if (_formatterOptions.EnableTraceIdInjection && AWSXRayRecorder.Instance.IsEntityPresent())
        {
            textWriter.Write($"TraceId: {AWSXRayRecorder.Instance?.GetEntity()?.TraceId}");
        }
    }

    private static string logLevelString(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
        };
    }

    // Dispose() calls Dispose(true)
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    // The bulk of the clean-up code is implemented in Dispose(bool)
    protected virtual void Dispose(bool disposing)
    {
        if (isDisposed) return;

        if (disposing)
        {
            _optionsReloadToken?.Dispose();
        }

        isDisposed = true;
    }

}