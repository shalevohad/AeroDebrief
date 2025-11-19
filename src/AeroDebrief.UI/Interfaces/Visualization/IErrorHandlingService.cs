using System;
using System.Threading.Tasks;

namespace AeroDebrief.UI.Interfaces.Visualization
{
    /// <summary>
    /// Error handling contract for visualization subsystem.
    /// Provides user-friendly error display with recovery options.
    /// 
    /// Responsibilities:
    /// - Display errors/warnings to user
    /// - Offer recovery actions (Retry, Cancel, etc.)
    /// - Auto-dismiss warnings
    /// - Clear error state
    /// - Event notification for error lifecycle
    /// 
    /// Design goals:
    /// - Non-blocking (async)
    /// - User-friendly messages
    /// - Actionable recovery options
    /// - Severity-based styling (Info/Warning/Error/Critical)
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
        public string Label { get; set; } = string.Empty;
        public Func<Task>? Action { get; set; }
        public bool IsPrimary { get; set; }
        
        public ErrorAction() { }
        
        public ErrorAction(string label, Func<Task> action, bool isPrimary = false)
        {
            Label = label;
            Action = action;
            IsPrimary = isPrimary;
        }
    }

    /// <summary>
    /// Result of error handling action.
    /// </summary>
    public class ErrorResult
    {
        public bool Success { get; set; }
        public string? ActionTaken { get; set; }
        
        public static ErrorResult Dismissed => new() { Success = false, ActionTaken = "Dismissed" };
        public static ErrorResult Retry => new() { Success = true, ActionTaken = "Retry" };
        public static ErrorResult Cancel => new() { Success = false, ActionTaken = "Cancel" };
    }

    /// <summary>
    /// Event args for error shown event.
    /// </summary>
    public class ErrorEventArgs : EventArgs
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public ErrorSeverity Severity { get; set; }
        public Exception? Exception { get; set; }
        
        public ErrorEventArgs() { }
        
        public ErrorEventArgs(string title, string message, ErrorSeverity severity, Exception? exception)
        {
            Title = title;
            Message = message;
            Severity = severity;
            Exception = exception;
        }
    }
}
