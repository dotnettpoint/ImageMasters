using ImageMaster.Core.Interfaces;

namespace ImageMaster.Infrastructure.Logging;

/// <summary>
/// Appends log lines to a local rolling log file under the user's AppData
/// folder. Deliberately logs only messages and exception type/message - no
/// file contents, no full stack of user data - to keep logs safe to attach
/// to a bug report.
/// </summary>
public sealed class FileAppLogger : IAppLogger, IDisposable
{
    private readonly object _writeLock = new();
    private readonly string _logFilePath;

    public FileAppLogger()
        : this(GetDefaultLogFilePath())
    {
    }

    public FileAppLogger(string logFilePath)
    {
        _logFilePath = logFilePath;
        var directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }

    public void LogInfo(string message) => Write("INFO", message);

    public void LogWarning(string message) => Write("WARN", message);

    public void LogError(string message, Exception? exception = null)
    {
        var full = exception is null ? message : $"{message} | {exception.GetType().Name}: {exception.Message}";
        Write("ERROR", full);
    }

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}";

        // Logging must never crash the app it's diagnosing, so any failure
        // to write is swallowed here (there is nowhere safer to report it).
        try
        {
            lock (_writeLock)
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Intentionally ignored - see comment above.
        }
    }

    private static string GetDefaultLogFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "ImageMaster", "logs", $"app-{DateTime.Now:yyyyMMdd}.log");
    }

    public void Dispose()
    {
        // No unmanaged resources are held open between writes; present for
        // symmetry and future extension (e.g. a buffered writer).
    }
}
