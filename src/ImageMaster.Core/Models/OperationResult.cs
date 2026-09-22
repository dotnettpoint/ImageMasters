namespace ImageMaster.Core.Models;

/// <summary>
/// Wraps the outcome of a service operation so failures (corrupt files,
/// invalid input, disk errors, etc.) travel back to the UI as data instead of
/// as exceptions the UI has to catch. The UI layer turns
/// <see cref="ErrorMessage"/> into a user-friendly dialog; raw exception
/// details are logged, never shown to the user.
/// </summary>
/// <typeparam name="T">The type of value returned on success.</typeparam>
public sealed class OperationResult<T>
{
    public bool Success { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }

    /// <summary>Non-fatal issues worth surfacing (e.g. "EXIF block was corrupt and was skipped").</summary>
    public IReadOnlyList<string> Warnings { get; }

    private OperationResult(bool success, T? value, string? errorMessage, IReadOnlyList<string>? warnings)
    {
        Success = success;
        Value = value;
        ErrorMessage = errorMessage;
        Warnings = warnings ?? Array.Empty<string>();
    }

    public static OperationResult<T> Ok(T value, IReadOnlyList<string>? warnings = null) =>
        new(true, value, null, warnings);

    public static OperationResult<T> Fail(string errorMessage) =>
        new(false, default, errorMessage, null);
}
