using System.Windows.Controls;

namespace AeroDebrief.UI.Controls.Charts;

/// <summary>
/// Overlay control for displaying performance statistics.
/// Phase 9 Step 3: Shows memory usage, cache stats, FPS, and load times.
/// Toggle with F3 key.
/// </summary>
public partial class PerformanceStatsOverlay : UserControl
{
    public PerformanceStatsOverlay()
    {
        InitializeComponent();
    }
}
