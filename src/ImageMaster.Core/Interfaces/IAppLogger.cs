namespace ImageMaster.Core.Interfaces;

/// <summary>
/// Local file logger. Implementations must never log file contents, full
/// user paths beyond what's useful for diagnosis, or any other
/// user-identifying data - just enough to diagnose a bug.
/// </summary>
public interface IAppLogger
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? exception = null);
}
