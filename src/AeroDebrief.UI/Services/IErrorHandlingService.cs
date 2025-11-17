namespace AeroDebrief.UI.Services;

/// <summary>
/// Service for handling and displaying errors to the user with recovery options.
/// </summary>
public interface IErrorHandlingService
{
    /// <summary>
    /// Shows an error to the user with optional recovery actions.
    /// </summary>
    /// <param name="title">Error title</param>
    /// <param name="message">User-friendly error message</param>
    /// <param name="exception">Original exception (for logging)</param>
    /// <param name="severity">Error severity level</param>
    /// <param name="actions">Available recovery actions</param>
    /// <returns>Result of user action</returns>
    Task<ErrorResult> ShowErrorAsync(
        string title,
        string message,
        Exception? exception = null,
        ErrorSeverity severity = ErrorSeverity.Error,
        params ErrorAction[] actions);

    /// <summary>
    /// Shows a warning banner that auto-dismisses after a delay.
    /// </summary>
    /// <param name="message">Warning message</param>
    /// <param name="autoHideDelay">Delay before auto-hide (default: 5 seconds)</param>
    void ShowWarning(string message, TimeSpan? autoHideDelay = null);

    /// <summary>
    /// Clears any visible error/warning banners.
    /// </summary>
    void ClearErrors();

    /// <summary>
    /// Event raised when an error is shown.
    /// </summary>
    event EventHandler<ErrorEventArgs>? ErrorShown;

    /// <summary>
    /// Event raised when errors are cleared.
    /// </summary>
    event EventHandler? ErrorsCleared;
}

/// <summary>
/// Severity level for errors.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>Informational message (blue)</summary>
    Info,
    
    /// <summary>Warning message (yellow, auto-hides)</summary>
    Warning,
    
    /// <summary>Error message (red, requires user action)</summary>
    Error,
    
    /// <summary>Critical error (dark red, blocks operations)</summary>
    Critical
}

/// <summary>
/// Represents an action the user can take in response to an error.
/// </summary>
public class ErrorAction
{
    /// <summary>
    /// Label to display on the action button.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Async action to execute when the button is clicked.
    /// </summary>
    public Func<Task>? Action { get; set; }

    /// <summary>
    /// Creates a new error action.
    /// </summary>
    public ErrorAction(string label, Func<Task>? action = null)
    {
        Label = label;
        Action = action;
    }
}

/// <summary>
/// Result of an error dialog.
/// </summary>
public enum ErrorResult
{
    /// <summary>User dismissed the error</summary>
    Dismissed,
    
    /// <summary>User chose to retry the operation</summary>
    Retry,
    
    /// <summary>User chose to cancel the operation</summary>
    Cancel,
    
    /// <summary>User chose a custom action</summary>
    Custom
}

/// <summary>
/// Event args for error events.
/// </summary>
public class ErrorEventArgs : EventArgs
{
    public string Title { get; }
    public string Message { get; }
    public ErrorSeverity Severity { get; }
    public Exception? Exception { get; }

    public ErrorEventArgs(string title, string message, ErrorSeverity severity, Exception? exception)
    {
        Title = title;
        Message = message;
        Severity = severity;
        Exception = exception;
    }
}
