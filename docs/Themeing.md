# UI Theme Guide

This document explains how to create custom UI color themes for the application, where to place them, how to select them from the UI, and how the system applies them behind the scenes.

## Overview

- Themes are simple JSON files stored in the `themes` folder next to the application executable (path: `AppContext.BaseDirectory/themes`).
- Two built-in themes are included by default: `light.json` and `dark.json`.
- The Core provides a `ThemeManager` (under `Core/Theme/ThemeManager.cs`) to enumerate, load and save theme files.
- The Core exposes `DesignLanguage` (under `Core/Theme/DesignLanguage.cs`) to provide color tokens to UI code. `DesignLanguage` can apply a user theme at runtime via `ApplyUserScheme`.
- The selected theme filename is persisted in the recorder configuration using `RecorderSettingsStore` with key `ThemeFile` (default `light.json`).
- Helper methods to enumerate and apply themes from UI are available in `Core/Helpers.cs`:
  - `Helpers.GetAvailableThemeFiles()`
  - `Helpers.GetCurrentThemeFile()`
  - `Helpers.ApplyThemeFile(string fileName)`

## Theme JSON schema

A theme JSON file is a plain JSON object matching the `ThemeManager.ColorSchemeModel` record. Example `my-theme.json`:

```json
{
  "Name": "My Theme",
  "Primary": "#46AF46",
  "Accent": "#9664FF",
  "Background": "#F0F8FF",
  "Panel": "#E6F5FF",
  "PanelAlt": "#EAF8FF",
  "TextPrimary": "#283040",
  "TextSecondary": "#5A6570",
  "Disabled": "#505050",
  "Warning": "#FFA500",
  "Error": "#C03030"
}
```

- All color values are hex RGB strings (`#RRGGBB`).
- `Name` is a friendly display name for the theme (optional for system operation but useful for UI listing).

## Creating a theme file

1. Create a JSON file following the schema above.
2. Place the file in the `themes` folder next to the application executable (the app will create the folder automatically on first run). Example path: `C:\Program Files\MyApp\themes\my-theme.json` or in development `bin/Debug/net9.0/themes/my-theme.json`.
3. The theme becomes available immediately to the application's UI listing (UI may need to refresh listing to show new files).

## Using from the UI

UI code should:

1. List available files:

```csharp
var files = Helpers.GetAvailableThemeFiles();
// Bind `files` to a dropdown/list for user selection
```

2. Read the current selection (to mark current in UI):

```csharp
var current = Helpers.GetCurrentThemeFile();
```

3. Apply user selection when changed:

```csharp
bool ok = Helpers.ApplyThemeFile(selectedFileName);
if (!ok) {
    // show error to user
}
```

`Helpers.ApplyThemeFile` does three things:
- Loads the JSON with `ThemeManager.LoadSchemeFromFile`.
- Calls `DesignLanguage.ApplyUserScheme(...)` with the loaded model so runtime color tokens are updated.
- Persists the filename in `RecorderSettingsStore` under the `ThemeFile` key.

## Apply theme on startup

To make the persisted theme active on startup, call the helper early in the application initialization (e.g., in the app startup code):

```csharp
var themeFile = RecorderSettingsStore.Instance.GetRecorderSettingString(RecorderSettingKeys.ThemeFile);
if (!string.IsNullOrEmpty(themeFile))
{
    Helpers.ApplyThemeFile(themeFile);
}
```

This will set `DesignLanguage` to use the user-specified theme for the rest of the session.

## How it works behind the scenes

- `ThemeManager`:
  - Provides file operations and JSON (de)serialization.
  - Ensures default `light.json` and `dark.json` are present in the `themes` folder.
  - Offers `GetAvailableSchemeFiles()`, `LoadSchemeFromFile()` and `SaveSchemeToFile()`.

- `DesignLanguage`:
  - Maintains internal color tokens and exposes them as `System.Drawing.Color` values through `DesignLanguage.Colors` static properties.
  - Built-in `Light` and `Dark` schemes are available via `SetActiveTheme`.
  - `ApplyUserScheme` builds a runtime color scheme from the `ThemeManager.ColorSchemeModel` and replaces the active scheme.

- `Helpers.ApplyThemeFile` ties the pieces together and persists the chosen filename using `RecorderSettingsStore`.

## WPF vs WinForms

- Core exposes `System.Drawing.Color` because some parts of the solution (PlayerClient controls) are WinForms based. WPF expects `System.Windows.Media.Brush` / `Color`.
- For WPF, convert hex / drawing color to WPF brushes. Example conversion helper in WPF code:

```csharp
using System.Windows.Media;

public static SolidColorBrush ToMediaBrush(string hex)
{
    if (hex.StartsWith("#")) hex = hex.Substring(1);
    byte r = Convert.ToByte(hex.Substring(0,2), 16);
    byte g = Convert.ToByte(hex.Substring(2,2), 16);
    byte b = Convert.ToByte(hex.Substring(4,2), 16);
    return new SolidColorBrush(Color.FromArgb(255, r, g, b));
}

// Or convert System.Drawing.Color to Media.Color
public static System.Windows.Media.Color ToMediaColor(System.Drawing.Color c)
{
    return System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
}
```

When the UI applies a user theme via `Helpers.ApplyThemeFile`, `DesignLanguage.Colors` values update. WPF code should read `DesignLanguage.Colors` (or convert from the JSON directly) and set ResourceDictionary entries or control properties accordingly.

## Previewing themes (optional)

UI can preview a theme before applying by:

1. Loading the JSON with `ThemeManager.LoadSchemeFromFile(fileName)`.
2. Converting the hex color values or `ThemeManager.ColorSchemeModel` to preview brushes/colors in the UI.
3. Only call `Helpers.ApplyThemeFile` when the user confirms.

## Example: Adding a theme and selecting it

1. Create `themes/solarized.json` with the color fields.
2. Start the app.
3. Open Settings -> Theme list -> refresh list (calls `Helpers.GetAvailableThemeFiles`).
4. Choose `solarized.json` and click `Apply` (calls `Helpers.ApplyThemeFile`).
5. The UI will switch colors immediately. The chosen filename is stored in `recorder.cfg` and applied on next startup.

## Troubleshooting

- If the theme file fails to load, the UI should show an error and keep the current theme.
- Theme JSON parsing errors will be handled by `ThemeManager.LoadSchemeFromFile` returning `null`.
- If colors look wrong in WPF, confirm the conversion from hex to WPF brushes is correct (alpha and byte order).

## Summary

- Themes are simple JSON files dropped into a `themes` folder.
- Use the Core helpers to list and apply themes; applied theme is persisted in settings.
- `DesignLanguage` exposes the active color tokens to UI code.
- WPF needs conversion to Media brushes; WinForms can use `System.Drawing.Color` directly.

Place this document in the repository `docs` folder so it is available to developers and users who want to create custom UI themes.