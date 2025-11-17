using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NLog;

namespace AeroDebrief.UI.Services;

/// <summary>
/// Service for handling and displaying errors to the user.
/// </summary>
public class ErrorHandlingService : INotifyPropertyChanged, IErrorHandlingService
{
    private readonly Logger _logger;
    private readonly Dispatcher _dispatcher;
    private readonly ConcurrentDictionary<string, DateTime> _recentErrors = new();
    private readonly TimeSpan _errorDeduplicationWindow = TimeSpan.FromSeconds(5);
    private DispatcherTimer? _autoHideTimer;
    private TaskCompletionSource<ErrorResult>? _currentErrorTcs;

    // Observable properties for UI binding
    private bool _isVisible;
    private string _title = string.Empty;
    private string _message = string.Empty;
    private ErrorSeverity _severity;
    private Brush _backgroundBrush = Brushes.DarkRed;
    private Brush _borderBrush = Brushes.Red;
    private Brush _iconBrush = Brushes.White;
    private Geometry? _iconGeometry;
    private bool _showRetryButton;
    private Func<Task>? _retryAction;

    // Events
    public event EventHandler<ErrorEventArgs>? ErrorShown;
    public event EventHandler? ErrorsCleared;
    public event PropertyChangedEventHandler? PropertyChanged;

    // Properties
    public bool IsVisible
    {
        get => _isVisible;
        private set { _isVisible = value; OnPropertyChanged(); }
    }

    public string Title
    {
        get => _title;
        private set { _title = value; OnPropertyChanged(); }
    }

    public string Message
    {
        get => _message;
        private set { _message = value; OnPropertyChanged(); }
    }

    public ErrorSeverity Severity
    {
        get => _severity;
        private set { _severity = value; OnPropertyChanged(); }
    }

    public Brush BackgroundBrush
    {
        get => _backgroundBrush;
        private set { _backgroundBrush = value; OnPropertyChanged(); }
    }

    public Brush BorderBrush
    {
        get => _borderBrush;
        private set { _borderBrush = value; OnPropertyChanged(); }
    }

    public Brush IconBrush
    {
        get => _iconBrush;
        private set { _iconBrush = value; OnPropertyChanged(); }
    }

    public Geometry? IconGeometry
    {
        get => _iconGeometry;
        private set { _iconGeometry = value; OnPropertyChanged(); }
    }

    public bool ShowRetryButton
    {
        get => _showRetryButton;
        private set { _showRetryButton = value; OnPropertyChanged(); }
    }

    public ICommand RetryCommand { get; }
    public ICommand DismissCommand { get; }

    public ErrorHandlingService(Logger logger)
    {
        _logger = logger;
        // For unit tests, Dispatcher might be null
        _dispatcher = Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
        RetryCommand = new RelayCommand(async () => await Retry(), () => _retryAction != null);
        DismissCommand = new RelayCommand(Dismiss);
    }

    /// <inheritdoc/>
    public async Task<ErrorResult> ShowErrorAsync(
        string title,
        string message,
        Exception? exception = null,
        ErrorSeverity severity = ErrorSeverity.Error,
        params ErrorAction[] actions)
    {
        // Check for duplicate errors
        var errorKey = $"{title}:{message}";
        if (_recentErrors.TryGetValue(errorKey, out var lastShown))
        {
            if (DateTime.UtcNow - lastShown < _errorDeduplicationWindow)
            {
                _logger.Debug($"Suppressing duplicate error: {title}");
                return ErrorResult.Dismissed;
            }
        }
        _recentErrors[errorKey] = DateTime.UtcNow;

        // Log the error
        if (exception != null)
        {
            _logger.Error(exception, $"{title}: {message}");
        }
        else
        {
            _logger.Error($"{title}: {message}");
        }

        // Show on UI thread
        var result = await _dispatcher.InvokeAsync(() =>
        {
            _currentErrorTcs = new TaskCompletionSource<ErrorResult>();

            Title = title;
            Message = message;
            Severity = severity;
            UpdateBrushes(severity);
            UpdateIcon(severity);

            // Set up retry action if provided
            ShowRetryButton = actions.Length > 0;
            _retryAction = actions.Length > 0 ? actions[0].Action : null;

            IsVisible = true;

            // Raise event
            ErrorShown?.Invoke(this, new ErrorEventArgs(title, message, severity, exception));

            return _currentErrorTcs.Task;
        });

        return await result;
    }

    /// <inheritdoc/>
    public void ShowWarning(string message, TimeSpan? autoHideDelay = null)
    {
        _dispatcher.InvokeAsync(() =>
        {
            Title = "Warning";
            Message = message;
            Severity = ErrorSeverity.Warning;
            UpdateBrushes(ErrorSeverity.Warning);
            UpdateIcon(ErrorSeverity.Warning);
            ShowRetryButton = false;
            IsVisible = true;

            _logger.Warn(message);

            // Raise event
            ErrorShown?.Invoke(this, new ErrorEventArgs("Warning", message, ErrorSeverity.Warning, null));

            // Auto-hide after delay
            var delay = autoHideDelay ?? TimeSpan.FromSeconds(5);
            _autoHideTimer?.Stop();
            _autoHideTimer = new DispatcherTimer { Interval = delay };
            _autoHideTimer.Tick += (s, e) =>
            {
                _autoHideTimer.Stop();
                ClearErrors();
            };
            _autoHideTimer.Start();
        });
    }

    /// <inheritdoc/>
    public void ClearErrors()
    {
        _dispatcher.InvokeAsync(() =>
        {
            IsVisible = false;
            _autoHideTimer?.Stop();
            _currentErrorTcs?.TrySetResult(ErrorResult.Dismissed);
            _currentErrorTcs = null;
            
            ErrorsCleared?.Invoke(this, EventArgs.Empty);
        });
    }

    private async Task Retry()
    {
        if (_retryAction != null)
        {
            _currentErrorTcs?.TrySetResult(ErrorResult.Retry);
            _currentErrorTcs = null;
            IsVisible = false;

            try
            {
                await _retryAction();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Retry action failed");
                await ShowErrorAsync(
                    "Retry Failed",
                    "The retry operation failed. Please try again or contact support.",
                    ex,
                    ErrorSeverity.Error);
            }
        }
    }

    private void Dismiss()
    {
        _currentErrorTcs?.TrySetResult(ErrorResult.Dismissed);
        _currentErrorTcs = null;
        ClearErrors();
    }

    private void UpdateBrushes(ErrorSeverity severity)
    {
        switch (severity)
        {
            case ErrorSeverity.Info:
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(30, 144, 255)); // DodgerBlue
                BorderBrush = new SolidColorBrush(Color.FromRgb(65, 105, 225)); // RoyalBlue
                IconBrush = Brushes.White;
                break;

            case ErrorSeverity.Warning:
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(255, 165, 0)); // Orange
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 140, 0)); // DarkOrange
                IconBrush = Brushes.White;
                break;

            case ErrorSeverity.Error:
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(220, 20, 60)); // Crimson
                BorderBrush = new SolidColorBrush(Color.FromRgb(178, 34, 34)); // FireBrick
                IconBrush = Brushes.White;
                break;

            case ErrorSeverity.Critical:
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(139, 0, 0)); // DarkRed
                BorderBrush = new SolidColorBrush(Color.FromRgb(128, 0, 0)); // Maroon
                IconBrush = Brushes.White;
                break;
        }
    }

    private void UpdateIcon(ErrorSeverity severity)
    {
        // Icon geometries (Material Design Icons)
        var infoIcon = Geometry.Parse("M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20A8,8 0 0,0 20,12A8,8 0 0,0 12,4M11,16.5L6.5,12L7.91,10.59L11,13.67L16.59,8.09L18,9.5L11,16.5Z");
        var warningIcon = Geometry.Parse("M12,2L1,21H23M12,6L19.53,19H4.47M11,10V14H13V10M11,16V18H13V16");
        var errorIcon = Geometry.Parse("M12,2C17.53,2 22,6.47 22,12C22,17.53 17.53,22 12,22C6.47,22 2,17.53 2,12C2,6.47 6.47,2 12,2M15.59,7L12,10.59L8.41,7L7,8.41L10.59,12L7,15.59L8.41,17L12,13.41L15.59,17L17,15.59L13.41,12L17,8.41L15.59,7Z");

        IconGeometry = severity switch
        {
            ErrorSeverity.Info => infoIcon,
            ErrorSeverity.Warning => warningIcon,
            ErrorSeverity.Error => errorIcon,
            ErrorSeverity.Critical => errorIcon,
            _ => errorIcon
        };
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Simple RelayCommand implementation
    private class RelayCommand : ICommand
    {
        private readonly Func<Task>? _executeAsync;
        private readonly Action? _execute;
        private readonly Func<bool>? _canExecute;

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
        {
            _executeAsync = executeAsync;
            _canExecute = canExecute;
        }

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public async void Execute(object? parameter)
        {
            if (_executeAsync != null)
                await _executeAsync();
            else
                _execute?.Invoke();
        }
    }
}
