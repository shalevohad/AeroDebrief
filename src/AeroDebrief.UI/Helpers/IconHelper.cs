using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FontAwesome.WPF;

namespace AeroDebrief.UI.Helpers
{
    /// <summary>
    /// Helper class for WPF icon display. Use ASCII fallbacks to avoid emoji/unicode rendering issues in some environments.
    /// Also provides FontAwesome helper for richer icons when available.
    /// </summary>
    public static class IconHelper
    {
        public static FontFamily SymbolFont { get; } = new FontFamily("Segoe UI Symbol");
        public static FontFamily StandardFont { get; } = new FontFamily("Segoe UI");

        // ASCII fallbacks (kept for compatibility)
        public const string GpuActive = "GPU";
        public const string CpuActive = "CPU";
        public const string Settings = "SET";
        public const string Reset = "R";

        public const string Play = ">";
        public const string Pause = "||";
        public const string Stop = "[]";

        public const string Mixer = "MIX";

        // Audio fallback icons
        public const string VolumeOn = "VOL";
        public const string VolumeMuted = "VOL-X";

        // Star selection fallbacks
        public const string StarEmpty = "*";
        public const string StarFilled = "[*]";

        /// <summary>
        /// Creates a FontAwesome icon control.
        /// Returns as FrameworkElement to avoid type name conflicts.
        /// </summary>
        /// <param name="icon">FontAwesome icon enum</param>
        /// <param name="size">Size in pixels</param>
        /// <param name="foreground">Foreground brush (null = default)</param>
        /// <returns>FrameworkElement (FontAwesome control)</returns>
        public static FrameworkElement CreateFaIcon(FontAwesomeIcon icon, double size = 16, Brush? foreground = null)
        {
            var fa = new FontAwesome.WPF.FontAwesome
            {
                Icon = icon,
                Width = size,
                Height = size,
                Foreground = foreground ?? Brushes.Black,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            return fa;
        }

        /// <summary>
        /// Create a TextBlock used as an icon (symbol font or simple ASCII)
        /// </summary>
        public static TextBlock CreateIconTextBlock(string glyph, double fontSize = 14, Brush? foreground = null)
        {
            return new TextBlock
            {
                Text = glyph,
                FontFamily = GetFontForIcon(glyph),
                FontSize = fontSize,
                Foreground = foreground ?? Brushes.Black,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
        }

        /// <summary>
        /// Gets the appropriate font family for a given icon string
        /// </summary>
        public static FontFamily GetFontForIcon(string iconChar)
        {
            if (string.IsNullOrEmpty(iconChar))
                return StandardFont;

            // If the string is simple ASCII (all chars < 128), use standard font
            var ascii = true;
            foreach (var c in iconChar)
            {
                if (c >= 128)
                {
                    ascii = false;
                    break;
                }
            }

            return ascii ? StandardFont : SymbolFont;
        }
    }
}
