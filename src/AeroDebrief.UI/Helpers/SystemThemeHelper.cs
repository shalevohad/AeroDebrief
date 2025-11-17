using System;
using System.Windows;

namespace AeroDebrief.UI.Helpers;

/// <summary>
/// Helper class for detecting and responding to system theme changes,
/// particularly high contrast mode for accessibility support.
/// Phase 9 Step 5: Accessibility - High Contrast Support
/// </summary>
public static class SystemThemeHelper
{
    /// <summary>
    /// Gets whether Windows high contrast mode is currently enabled.
    /// </summary>
    /// <returns>True if high contrast mode is active; otherwise, false.</returns>
    public static bool IsHighContrastMode()
    {
        return SystemParameters.HighContrast;
    }
    
    /// <summary>
    /// Occurs when system parameters change, including high contrast mode.
    /// Subscribe to this event to respond to theme changes.
    /// </summary>
    /// <example>
    /// <code>
    /// SystemThemeHelper.HighContrastChanged += (s, e) =>
    /// {
    ///     if (SystemThemeHelper.IsHighContrastMode())
    ///     {
    ///         // Apply high contrast styles
    ///     }
    /// };
    /// </code>
    /// </example>
    public static event EventHandler? HighContrastChanged
    {
        add
        {
            if (value != null)
            {
                SystemParameters.StaticPropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(SystemParameters.HighContrast))
                    {
                        value(sender, EventArgs.Empty);
                    }
                };
            }
        }
        remove
        {
            // Note: Cannot reliably unsubscribe from StaticPropertyChanged
            // This is acceptable as system theme changes are rare
        }
    }
    
    /// <summary>
    /// Gets the current system window background brush for high contrast mode.
    /// </summary>
    public static System.Windows.Media.Brush WindowBrush => 
        SystemColors.WindowBrush;
    
    /// <summary>
    /// Gets the current system window text brush for high contrast mode.
    /// </summary>
    public static System.Windows.Media.Brush WindowTextBrush => 
        SystemColors.WindowTextBrush;
    
    /// <summary>
    /// Gets the current system highlight background brush for high contrast mode.
    /// </summary>
    public static System.Windows.Media.Brush HighlightBrush => 
        SystemColors.HighlightBrush;
    
    /// <summary>
    /// Gets the current system highlight text brush for high contrast mode.
    /// </summary>
    public static System.Windows.Media.Brush HighlightTextBrush => 
        SystemColors.HighlightTextBrush;
}
