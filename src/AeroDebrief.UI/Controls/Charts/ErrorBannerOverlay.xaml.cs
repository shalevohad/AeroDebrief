using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace AeroDebrief.UI.Controls.Charts;

/// <summary>
/// Overlay control for displaying error messages with recovery actions.
/// Phase 9 Step 4: Enhanced with auto-dismiss animations and icon pulse.
/// Phase 9 Step 5: Added accessibility support with focus management and keyboard navigation.
/// </summary>
public partial class ErrorBannerOverlay : UserControl
{
    private IInputElement? _previousFocus;
    
    public ErrorBannerOverlay()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
    }
    
    /// <summary>
    /// Starts the icon pulse animation and manages focus when loaded.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Store previous focus for restoration
        _previousFocus = Keyboard.FocusedElement;
        
        // Start icon pulse animation if the icon element exists
        var errorIcon = this.FindName("ErrorIcon") as FrameworkElement;
        if (errorIcon != null && Resources["IconPulseAnimation"] is Storyboard pulseStoryboard)
        {
            pulseStoryboard.Begin(errorIcon);
        }
        
        // Focus the first interactive element (Retry or Dismiss button)
        FocusFirstButton();
    }
    
    /// <summary>
    /// Handles keyboard navigation within the error banner.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Escape key dismisses the error
        if (e.Key == Key.Escape)
        {
            var dismissButton = this.FindName("DismissButton") as Button;
            dismissButton?.Command?.Execute(null);
            e.Handled = true;
        }
        
        // Tab key cycles focus within banner (focus trap)
        if (e.Key == Key.Tab)
        {
            HandleTabNavigation(e);
        }
    }
    
    /// <summary>
    /// Implements focus trap: Tab cycles between Retry and Dismiss buttons.
    /// </summary>
    private void HandleTabNavigation(KeyEventArgs e)
    {
        var retryButton = this.FindName("RetryButton") as Button;
        var dismissButton = this.FindName("DismissButton") as Button;
        
        if (retryButton == null || dismissButton == null)
            return;
        
        var focusedElement = Keyboard.FocusedElement;
        
        // Forward tab: Retry ? Dismiss ? Retry
        if (!e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            if (focusedElement == retryButton && dismissButton.IsVisible)
            {
                dismissButton.Focus();
                e.Handled = true;
            }
            else if (focusedElement == dismissButton && retryButton.IsVisible)
            {
                retryButton.Focus();
                e.Handled = true;
            }
        }
        // Backward tab (Shift+Tab): Dismiss ? Retry ? Dismiss
        else
        {
            if (focusedElement == dismissButton && retryButton.IsVisible)
            {
                retryButton.Focus();
                e.Handled = true;
            }
            else if (focusedElement == retryButton && dismissButton.IsVisible)
            {
                dismissButton.Focus();
                e.Handled = true;
            }
        }
    }
    
    /// <summary>
    /// Focuses the first available button (Retry if visible, otherwise Dismiss).
    /// </summary>
    private void FocusFirstButton()
    {
        var retryButton = this.FindName("RetryButton") as Button;
        var dismissButton = this.FindName("DismissButton") as Button;
        
        if (retryButton?.IsVisible == true)
        {
            retryButton.Focus();
        }
        else if (dismissButton?.IsVisible == true)
        {
            dismissButton.Focus();
        }
    }
    
    /// <summary>
    /// Triggers the slide-out animation for dismissing.
    /// Called by the ViewModel when auto-dismissing or manually dismissing.
    /// Also restores previous focus.
    /// </summary>
    public void TriggerDismissAnimation()
    {
        if (Resources["SlideOutAnimation"] is Storyboard slideOutStoryboard)
        {
            slideOutStoryboard.Completed += (s, e) =>
            {
                Visibility = Visibility.Collapsed;
                RestorePreviousFocus();
            };
            
            var errorBanner = this.FindName("ErrorBanner") as FrameworkElement;
            if (errorBanner != null)
            {
                slideOutStoryboard.Begin(errorBanner);
            }
        }
    }
    
    /// <summary>
    /// Restores keyboard focus to the element that had focus before the error appeared.
    /// </summary>
    private void RestorePreviousFocus()
    {
        if (_previousFocus is UIElement element && element.IsVisible && element.Focusable)
        {
            element.Focus();
        }
    }
}
