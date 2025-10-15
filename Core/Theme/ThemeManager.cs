using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ShalevOhad.DCS.SRS.Recorder.Core
{
    /// <summary>
    /// Manages color scheme files placed in the runtime themes folder.
    /// Users can drop JSON files describing a color scheme and select them via UI.
    /// This file lives under Core/Theme.
    /// </summary>
    public static class ThemeManager
    {
        public record ColorSchemeModel(
            string Name,
            string Primary,
            string Accent,
            string Background,
            string Panel,
            string PanelAlt,
            string TextPrimary,
            string TextSecondary,
            string Disabled,
            string Warning,
            string Error
        );

        public static string ThemesFolder { get; private set; }

        // Active scheme
        private static ColorSchemeModel? _activeSchemeModel;

        static ThemeManager()
        {
            // Default themes folder is next to the application executable in "themes"
            ThemesFolder = System.IO.Path.Combine(AppContext.BaseDirectory, "themes");
            if (!Directory.Exists(ThemesFolder)) Directory.CreateDirectory(ThemesFolder);

            // Ensure built-in defaults exist
            EnsureDefaultSchemes();

            // Try to load default scheme (light.json) if available
            var defaultPath = System.IO.Path.Combine(ThemesFolder, "light.json");
            if (File.Exists(defaultPath))
            {
                try { SetActiveSchemeFromFile("light.json"); } catch { /* ignore */ }
            }
        }

        private static void EnsureDefaultSchemes()
        {
            // Light
            var light = new ColorSchemeModel(
                "Light",
                "#46AF46",
                "#9664FF",
                "#F0F8FF",
                "#E6F5FF",
                "#EAF8FF",
                "#283040",
                "#5A6570",
                "#505050",
                "#FFA500",
                "#C03030"
            );

            // Dark
            var dark = new ColorSchemeModel(
                "Dark",
                "#4CAF50",
                "#7C5CFF",
                "#0F1724",
                "#111827",
                "#0B1220",
                "#E6EEF6",
                "#9AA6B2",
                "#6B7280",
                "#FFA500",
                "#FF6B6B"
            );

            var lightPath = System.IO.Path.Combine(ThemesFolder, "light.json");
            var darkPath = System.IO.Path.Combine(ThemesFolder, "dark.json");

            if (!File.Exists(lightPath)) File.WriteAllText(lightPath, JsonSerializer.Serialize(light, new JsonSerializerOptions { WriteIndented = true }));
            if (!File.Exists(darkPath)) File.WriteAllText(darkPath, JsonSerializer.Serialize(dark, new JsonSerializerOptions { WriteIndented = true }));
        }

        public static IEnumerable<string> GetAvailableSchemeFiles()
        {
            if (!Directory.Exists(ThemesFolder)) return Enumerable.Empty<string>();
            return Directory.GetFiles(ThemesFolder, "*.json").Select(Path.GetFileName);
        }

        public static ColorSchemeModel? LoadSchemeFromFile(string fileName)
        {
            var path = Path.Combine(ThemesFolder, fileName);
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            try
            {
                var model = JsonSerializer.Deserialize<ColorSchemeModel>(json);
                return model;
            }
            catch
            {
                return null;
            }
        }

        public static bool SetActiveSchemeFromFile(string fileName)
        {
            var model = LoadSchemeFromFile(fileName);
            if (model == null) return false;
            _activeSchemeModel = model;
            return true;
        }

        public static ColorSchemeModel? ActiveSchemeModel => _activeSchemeModel;

        public static bool SaveSchemeToFile(ColorSchemeModel model, string fileName)
        {
            try
            {
                var path = Path.Combine(ThemesFolder, fileName);
                var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Utility to convert hex to Color (used by UI)
        public static Color HexToColor(string hex)
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
    }
}
