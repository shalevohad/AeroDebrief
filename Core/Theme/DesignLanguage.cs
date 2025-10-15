using System.Drawing;

namespace ShalevOhad.DCS.SRS.Recorder.Core
{
    /// <summary>
    /// Centralized design language for the whole solution.
    /// UI components should reference values from this class for colors, sizes and fonts.
    /// Provides neutral representations and helpers for WinForms.
    /// WPF projects should convert hex values to their own brushes if needed.
    /// </summary>
    public static class DesignLanguage
    {
        /// <summary>
        /// Supported theme schemes
        /// </summary>
        public enum ThemeScheme
        {
            Light,
            Dark,
            Custom // reserved for user-defined schemes
        }

        // Internal color-scheme container
        private class ColorScheme
        {
            public string PrimaryHex { get; init; } = "#46AF46";
            public string AccentHex { get; init; } = "#9664FF";
            public string BackgroundHex { get; init; } = "#F0F8FF";
            public string PanelHex { get; init; } = "#E6F5FF";
            public string PanelAltHex { get; init; } = "#EAF8FF";
            public string TextPrimaryHex { get; init; } = "#283040";
            public string TextSecondaryHex { get; init; } = "#5A6570";
            public string DisabledHex { get; init; } = "#505050";
            public string WarningHex { get; init; } = "#FFA500";
            public string ErrorHex { get; init; } = "#C03030";

            public Color Primary => ToDrawingColor(PrimaryHex);
            public Color Accent => ToDrawingColor(AccentHex);
            public Color Background => ToDrawingColor(BackgroundHex);
            public Color Panel => ToDrawingColor(PanelHex);
            public Color PanelAlt => ToDrawingColor(PanelAltHex);
            public Color TextPrimary => ToDrawingColor(TextPrimaryHex);
            public Color TextSecondary => ToDrawingColor(TextSecondaryHex);
            public Color Disabled => ToDrawingColor(DisabledHex);
            public Color Warning => ToDrawingColor(WarningHex);
            public Color Error => ToDrawingColor(ErrorHex);
        }

        // Define the two schemes
        private static readonly ColorScheme LightScheme = new ColorScheme
        {
            PrimaryHex = "#46AF46",
            AccentHex = "#9664FF",
            BackgroundHex = "#F0F8FF",
            PanelHex = "#E6F5FF",
            PanelAltHex = "#EAF8FF",
            TextPrimaryHex = "#283040",
            TextSecondaryHex = "#5A6570",
            DisabledHex = "#505050",
            WarningHex = "#FFA500",
            ErrorHex = "#C03030"
        };

        private static readonly ColorScheme DarkScheme = new ColorScheme
        {
            PrimaryHex = "#4CAF50",
            AccentHex = "#7C5CFF",
            BackgroundHex = "#0F1724",
            PanelHex = "#111827",
            PanelAltHex = "#0B1220",
            TextPrimaryHex = "#E6EEF6",
            TextSecondaryHex = "#9AA6B2",
            DisabledHex = "#6B7280",
            WarningHex = "#FFA500",
            ErrorHex = "#FF6B6B"
        };

        // Active scheme (default to Light)
        private static ColorScheme _activeScheme = LightScheme;
        private static ThemeScheme _activeTheme = ThemeScheme.Light;

        // Helper to convert hex string to System.Drawing.Color
        private static Color ToDrawingColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.Black;
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.Length == 6)
            {
                var r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                var g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                var b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                return Color.FromArgb(255, r, g, b);
            }
            return Color.Black;
        }

        /// <summary>
        /// Set the active theme scheme used by the application UI.
        /// </summary>
        public static void SetActiveTheme(ThemeScheme scheme)
        {
            _activeTheme = scheme;
            _activeScheme = scheme == ThemeScheme.Dark ? DarkScheme : LightScheme;
        }

        /// <summary>
        /// Apply a theme model loaded from ThemeManager (user-defined JSON). If null, keep current scheme.
        /// </summary>
        public static void ApplyUserScheme(ThemeManager.ColorSchemeModel? model)
        {
            if (model == null) return;
            _activeTheme = ThemeScheme.Custom;
            _activeScheme = new ColorScheme
            {
                PrimaryHex = model.Primary,
                AccentHex = model.Accent,
                BackgroundHex = model.Background,
                PanelHex = model.Panel,
                PanelAltHex = model.PanelAlt,
                TextPrimaryHex = model.TextPrimary,
                TextSecondaryHex = model.TextSecondary,
                DisabledHex = model.Disabled,
                WarningHex = model.Warning,
                ErrorHex = model.Error
            };
        }

        /// <summary>
        /// Get the currently active theme
        /// </summary>
        public static ThemeScheme ActiveTheme => _activeTheme;

        /// <summary>
        /// Color definitions. These properties delegate to the currently active scheme.
        /// Kept as static properties to avoid changing existing consumer code.
        /// </summary>
        public static class Colors
        {
            // Exposed typed properties for convenience (delegate to active scheme)
            public static Color PrimaryDrawing => _activeScheme.Primary;
            public static Color AccentDrawing => _activeScheme.Accent;
            public static Color BackgroundDrawing => _activeScheme.Background;
            public static Color PanelDrawing => _activeScheme.Panel;
            public static Color PanelAltDrawing => _activeScheme.PanelAlt;
            public static Color TextPrimaryDrawing => _activeScheme.TextPrimary;
            public static Color TextSecondaryDrawing => _activeScheme.TextSecondary;
            public static Color DisabledDrawing => _activeScheme.Disabled;
            public static Color WarningDrawing => _activeScheme.Warning;
            public static Color ErrorDrawing => _activeScheme.Error;

            // Note: conversion helper retained locally for convenience when needed
            private static Color ToDrawingColor(string hex)
            {
                if (hex.StartsWith("#")) hex = hex.Substring(1);
                if (hex.Length == 6)
                {
                    var r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                    var g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                    var b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    return Color.FromArgb(255, r, g, b);
                }
                return Color.Black;
            }
        }

        /// <summary>
        /// Size tokens used by UI.
        /// </summary>
        public static class Sizes
        {
            public const int CornerRadius = 12;
            public const int SmallCornerRadius = 8;
            public const int Spacing = 8;
            public const int ControlPadding = 8;

            // Button sizes (default compact)
            public static readonly Size ButtonSmall = new Size(64, 28);
            public static readonly Size ButtonNormal = new Size(96, 32);
            public static readonly Size ButtonLarge = new Size(120, 40);

            public const int IconSize = 16;
        }

        /// <summary>
        /// Font tokens and helpers. Uses the same family name across WinForms and WPF.
        /// </summary>
        public static class Fonts
        {
            public const string Family = "Segoe UI";

            // default font sizes (in points)
            public const float Small = 9f;
            public const float Normal = 10f;
            public const float Large = 12f;

            public static Font GetDrawingFont(float size, FontStyle style = FontStyle.Regular)
            {
                return new Font(Family, size, style, GraphicsUnit.Point);
            }
        }

        /// <summary>
        /// Common padding presets.
        /// </summary>
        public static class LayoutPadding
        {
            // Expose padding values as integers. UI layers should construct framework-specific Padding types.
            public static int Default => Sizes.ControlPadding;

            public static int Dialog => 16;
        }

        /// <summary>
        /// Convenience helpers for WinForms brushes/pen equivalents
        /// </summary>
        public static class WinForms
        {
            public static Brush PrimaryBrush => new SolidBrush(Colors.PrimaryDrawing);
            public static Brush PanelBrush => new SolidBrush(Colors.PanelDrawing);
            public static Pen BorderPen => new Pen(Color.FromArgb(150, Colors.TextSecondaryDrawing.R, Colors.TextSecondaryDrawing.G, Colors.TextSecondaryDrawing.B));
        }
    }
}
