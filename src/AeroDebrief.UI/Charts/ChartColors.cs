using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using SkiaSharp;

namespace AeroDebrief.UI.Charts
{
    /// <summary>
    /// Provides deterministic color palette for frequency visualization.
    /// Phase 4: Ensures distinct colors per frequency with consistent assignment.
    /// Thread-safe for concurrent access.
    /// </summary>
    public static class ChartColors
    {
        /// <summary>
        /// Primary color palette optimized for visibility and distinction.
        /// Based on ColorBrewer qualitative schemes with accessibility considerations.
        /// </summary>
        private static readonly SKColor[] BasePalette =
        {
            // High contrast primary colors
            SKColor.Parse("#1f77b4"), // Blue
            SKColor.Parse("#ff7f0e"), // Orange
            SKColor.Parse("#2ca02c"), // Green
            SKColor.Parse("#d62728"), // Red
            SKColor.Parse("#9467bd"), // Purple
            SKColor.Parse("#8c564b"), // Brown
            SKColor.Parse("#e377c2"), // Pink
            SKColor.Parse("#7f7f7f"), // Gray
            SKColor.Parse("#bcbd22"), // Yellow-Green
            SKColor.Parse("#17becf"), // Cyan
            
            // Extended colors for more frequencies
            SKColor.Parse("#aec7e8"), // Light Blue
            SKColor.Parse("#ffbb78"), // Light Orange
            SKColor.Parse("#98df8a"), // Light Green
            SKColor.Parse("#ff9896"), // Light Red
            SKColor.Parse("#c5b0d5"), // Light Purple
            SKColor.Parse("#c49c94"), // Light Brown
            SKColor.Parse("#f7b6d2"), // Light Pink
            SKColor.Parse("#c7c7c7"), // Light Gray
            SKColor.Parse("#dbdb8d"), // Light Yellow-Green
            SKColor.Parse("#9edae5"), // Light Cyan
            
            // Additional saturated colors
            SKColor.Parse("#3366cc"), // Bright Blue
            SKColor.Parse("#dc3912"), // Bright Red
            SKColor.Parse("#ff9900"), // Bright Orange
            SKColor.Parse("#109618"), // Bright Green
            SKColor.Parse("#990099"), // Bright Magenta
            SKColor.Parse("#0099c6"), // Bright Teal
            SKColor.Parse("#dd4477"), // Bright Pink
            SKColor.Parse("#66aa00"), // Bright Lime
            SKColor.Parse("#b82e2e"), // Bright Crimson
            SKColor.Parse("#316395"), // Bright Navy
        };

        /// <summary>
        /// Thread-safe cache of frequency-to-color assignments for consistency.
        /// Uses ConcurrentDictionary to support parallel test execution.
        /// </summary>
        private static readonly ConcurrentDictionary<string, SKColor> _colorCache = new();

        /// <summary>
        /// Gets a deterministic color for the specified frequency ID.
        /// Same frequency ID always returns the same color.
        /// Thread-safe for concurrent access.
        /// </summary>
        /// <param name="frequencyId">Unique frequency identifier (e.g., "251.0-AM")</param>
        /// <returns>SKColor assigned to this frequency</returns>
        public static SKColor GetColorForFrequency(string frequencyId)
        {
            if (string.IsNullOrEmpty(frequencyId))
            {
                return SKColors.Gray;
            }

            // GetOrAdd is thread-safe and atomic
            return _colorCache.GetOrAdd(frequencyId, key =>
            {
                // Generate deterministic hash-based index
                var hash = GetStableHashCode(key);
                var index = Math.Abs(hash) % BasePalette.Length;
                return BasePalette[index];
            });
        }

        /// <summary>
        /// Gets a color with adjusted opacity.
        /// Useful for inactive/muted series or background elements.
        /// </summary>
        /// <param name="frequencyId">Unique frequency identifier</param>
        /// <param name="alpha">Opacity (0-255)</param>
        /// <returns>SKColor with specified opacity</returns>
        public static SKColor GetColorWithAlpha(string frequencyId, byte alpha)
        {
            var baseColor = GetColorForFrequency(frequencyId);
            return new SKColor(baseColor.Red, baseColor.Green, baseColor.Blue, alpha);
        }

        /// <summary>
        /// Clears the color cache. Used when resetting visualization or for testing.
        /// Thread-safe operation.
        /// </summary>
        public static void ClearCache()
        {
            _colorCache.Clear();
        }

        /// <summary>
        /// Gets the total number of unique colors in the palette.
        /// </summary>
        public static int PaletteSize => BasePalette.Length;

        /// <summary>
        /// Generate a stable hash code that's consistent across runs.
        /// The default GetHashCode() can vary between process runs.
        /// </summary>
        private static int GetStableHashCode(string str)
        {
            unchecked
            {
                int hash1 = 5381;
                int hash2 = hash1;

                for (int i = 0; i < str.Length && str[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i + 1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }

        /// <summary>
        /// Gets all available colors in the palette (for testing/debugging).
        /// </summary>
        public static IReadOnlyList<SKColor> GetAllColors() => BasePalette;
    }
}
