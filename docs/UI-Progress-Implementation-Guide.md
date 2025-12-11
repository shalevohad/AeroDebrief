# UI Progress Reporting for ADB Conversion

## Overview

This guide shows how to add conversion progress reporting to the AeroDebrief UI client, similar to what's already in the CLI.

## Implementation

### Step 1: Update RecordingFileLoader to Report Progress

The `RecordingFileLoader.LoadRecordingAsync` method already calls the converter. We need to pass through progress:

```csharp
// In RecordingFileLoader.cs - add IProgress parameter
public static async Task<IPacketSource> LoadRecordingAsync(
    string filePath,
    IProgress<ConversionProgress>? progress = null,  // ? NEW
    CancellationToken ct = default)
{
    // ...existing validation code...
    
    if (needsConversion)
    {
        // Pass progress to converter
        var result = await converter.ConvertAsync(adbPath, outputDbPath, false, 
            progress: progress,  // ? Pass through
            ct: ct);
        
        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to convert recording: {result.Error}");
        }
        
        // Continue with converted file...
    }
    
    // ...rest of loading code...
}
```

### Step 2: Update UnifiedPlayerViewModel to Wire Progress

The `UnifiedPlayerViewModel` subscribes to `FileSource.FileLoaded` event. Update it to handle progress:

```csharp
// In UnifiedPlayerViewModel.cs
private async void OnFileLoaded(string filePath)
{
    try
    {
        // Create progress reporter
        var progress = new Progress<ConversionProgress>(p =>
        {
            // Update FileSource view model with progress
            FileSource?.UpdateLoadingProgress(
                status: p.Message,
                progress: p.Percent,
                isIndeterminate: p.Percent == 0 && string.IsNullOrEmpty(p.Message)
            );
        });
        
        // Load file with progress reporting
        var source = await RecordingFileLoader.LoadRecordingAsync(filePath, progress, _cts.Token);
        
        // ...rest of loading code...
        
        // Complete loading
        FileSource?.CompleteLoading(true, "File loaded successfully");
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Failed to load file");
        FileSource?.CompleteLoading(false, $"Load failed: {ex.Message}");
    }
}
```

### Step 3: UI Already Has Progress Display!

The `FileSourceViewModel` already has these properties (no changes needed):

```csharp
public string LoadingStatus { get; set; }      // Status message
public double LoadingProgress { get; set; }     // 0-100
public bool IsIndeterminate { get; set; }       // Spinner vs progress bar
public bool IsLoading { get; set; }             // Show/hide progress
```

The XAML just needs to bind to these properties:

```xaml
<!-- In your FileSource control XAML -->
<ProgressBar Value="{Binding LoadingProgress}" 
             Visibility="{Binding IsLoading, Converter={StaticResource BoolToVisibility}}"
             IsIndeterminate="{Binding IsIndeterminate}"
             Height="4"/>

<TextBlock Text="{Binding LoadingStatus}" 
           Visibility="{Binding IsLoading, Converter={StaticResource BoolToVisibility}}"/>
```

## Progress Messages You'll See

```
Opening source file...                  [0%]
Creating database...                    [5%]
Converting packets: 15,000 packets      [25%]  (2,500 pkt/sec) + amplitude cache
Converting packets: 45,000 packets      [45%]  (2,500 pkt/sec) + amplitude cache
ETA: 2 minutes
Finalizing database...                  [85%]
Building indexes and statistics...     [90%]
Compressing to CVR format...            [95%]
Conversion complete!                    [100%]
```

## Benefits

? **Real-time feedback**: Users see conversion progress
? **Speed metrics**: Shows packets/sec processing speed
? **ETA display**: Shows estimated time remaining
? **Status messages**: Clear indication of current stage
? **Works for both**: ADB?SQLite conversion and CVR decompression

## Testing

1. Open a legacy `.adb` file in the UI
2. Watch the progress bar and status messages
3. Conversion happens in background with live updates
4. File opens automatically when conversion completes

That's it! The UI infrastructure is already there, just wire the progress callbacks.
