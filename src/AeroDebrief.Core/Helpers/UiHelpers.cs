using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using AeroDebrief.Core.Settings;

namespace AeroDebrief.Core.Helpers
{
    /// <summary>
    /// Helper methods for UI rendering and theming
    /// </summary>
    public static class UiHelpers
    {
        /// <summary>
        /// Creates a rounded rectangle GraphicsPath for drawing rounded panels and buttons
        /// </summary>
        public static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int cornerRadius)
        {
            var path = new GraphicsPath();
            var diameter = cornerRadius * 2;

            // Top left arc
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            // Top right arc
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            // Bottom right arc
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            // Bottom left arc
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);

            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Draws a filled rounded panel using the provided Graphics instance
        /// </summary>
        public static void DrawRoundedPanel(Graphics g, Rectangle bounds, int cornerRadius, Color fillColor)
        {
            using (var path = CreateRoundedRectanglePath(bounds, cornerRadius))
            using (var brush = new SolidBrush(fillColor))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(brush, path);
            }
        }

        /// <summary>
        /// Theme helpers: expose available theme files and apply/save a selected theme
        /// These bridge ThemeManager and RecorderSettingsStore for the UI.
        /// </summary>
        public static IEnumerable<string> GetAvailableThemeFiles()
        {
            return ThemeManager.GetAvailableSchemeFiles();
        }
        
        public static string GetRecorderThemeFile()
        {
            // Return recorder setting only. Delegate/owner should fetch from the correct store (player or recorder).
            return RecorderSettingsStore.Instance.GetRecorderSettingString(RecorderSettingKeys.ThemeFile);
        }

        public static string GetPlayerThemeFile()
        {
            // Return Player setting only. Delegate/owner should fetch from the correct store (player or recorder).
            return PlayerSettingsStore.Instance.GetPlayerSettingString(PlayerSettingKeys.ThemeFile);
        }

        public static bool ApplyThemeFile(string fileName)
        {
            var model = ThemeManager.LoadSchemeFromFile(fileName);
            if (model == null) return false;

            // Apply to design language only (do not persist here).
            DesignLanguage.ApplyUserScheme(model);

            return true;
        }
    }
}