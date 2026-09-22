using ImageMaster.Core.Interfaces;

namespace ImageMaster.Core.Tests.TestDoubles;

/// <summary>In-memory logger for tests: captures messages instead of writing to disk.</summary>
public sealed class FakeAppLogger : IAppLogger
{
    public List<string> InfoMessages { get; } = new();
    public List<string> WarningMessages { get; } = new();
    public List<string> ErrorMessages { get; } = new();

    public void LogInfo(string message) => InfoMessages.Add(message);
    public void LogWarning(string message) => WarningMessages.Add(message);
    public void LogError(string message, Exception? exception = null) => ErrorMessages.Add(message);
}
