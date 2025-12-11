# Quick Integration Guide - Show Progress Dialog NOW!

## Problem
You created the progress dialog files but they're not being called yet, so you don't see the progress bar when opening files.

## Solution
Update your file loading code to use `RecordingLoaderService` instead of direct `RecordingFileLoader`.

## Quick Fix - Copy This Code

### Option 1: Update MainWindow.xaml.cs (Simplest)

Replace your `OpenRecording_Click` method with this:

```csharp
// In MainWindow.xaml.cs
using AeroDebrief.UI.Services;  // ? ADD THIS
using Microsoft.Win32;           // ? ADD THIS

private async void OpenRecording_Click(object sender, RoutedEventArgs e)
{
    var dialog = new OpenFileDialog
    {
        Title = "Open Recording",
        Filter = RecordingFileLoader.GetFileFilters(),
        FilterIndex = 1
    };
    
    if (dialog.ShowDialog() == true)
    {
        // ? NEW: Use RecordingLoaderService to show progress dialog
        var uow = await RecordingLoaderService.LoadAsync(dialog.FileName, this);
        
        if (uow != null)
        {
            // Tell ViewModel to use this loaded file
            // (You'll need to add this method to your ViewModel)
            _viewModel?.LoadFromUnitOfWork(uow);
        }
    }
}
```

### Option 2: Update FileSourceViewModel (Better)

Find where `ExecuteLoadFileAsync()` calls `RecordingFileLoader.OpenAsync` and replace it:

```csharp
// In FileSourceViewModel.cs
private async Task ExecuteLoadFileAsync()
{
    if (string.IsNullOrEmpty(SelectedFilePath))
        return;

    try
    {
        IsLoading = true;
        FileInfo = "Loading...";
        
        Logger.Info($"Loading file: {SelectedFilePath}");

        // ? REPLACE THIS OLD CODE:
        // var (uow, tempPath) = await RecordingFileLoader.OpenAsync(
        //     SelectedFilePath, progress, detailedProgress, _cts.Token);
        
        // ? WITH THIS NEW CODE:
        var owner = Application.Current.MainWindow;
        var uow = await RecordingLoaderService.LoadAsync(SelectedFilePath, owner);
        
        if (uow == null)
        {
            // User cancelled or error occurred
            FileInfo = "Load cancelled";
            IsLoading = false;
            return;
        }

        // Save as last used file
        SaveLastFileToSettings();
        AddToRecentFiles(SelectedFilePath);

        // Notify that file is loaded
        FileLoaded?.Invoke(SelectedFilePath);

        FileInfo = $"Loaded: {Path.GetFileName(SelectedFilePath)}";
        Logger.Info($"File loaded successfully: {SelectedFilePath}");
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Failed to load file");
        FileInfo = $"Load error: {ex.Message}";
    }
    finally
    {
        IsLoading = false;
    }
}
```

### Option 3: Direct Test (Test it immediately!)

Create a button in your UI to test:

```xaml
<!-- Add this button anywhere in your MainWindow.xaml -->
<Button Content="Test Progress Dialog" Click="TestProgress_Click"/>
```

```csharp
// Add this to MainWindow.xaml.cs
using AeroDebrief.UI.Services;

private async void TestProgress_Click(object sender, RoutedEventArgs e)
{
    var dialog = new Microsoft.Win32.OpenFileDialog
    {
        Title = "Select ADB or CVR file",
        Filter = "Recording Files|*.adb;*.cvr|All Files|*.*"
    };
    
    if (dialog.ShowDialog() == true)
    {
        // This will show the progress dialog!
        var uow = await RecordingLoaderService.LoadAsync(dialog.FileName, this);
        
        if (uow != null)
        {
            MessageBox.Show("File loaded successfully with progress dialog!");
        }
        else
        {
            MessageBox.Show("Load cancelled or failed");
        }
    }
}
```

## What You'll See

When you select an ADB file, a modal dialog will appear:

```
????????????????????????????????????????????
?  ?? Converting Recording File            ?
????????????????????????????????????????????
?  Converting: my_recording.adb            ?
?                                           ?
?  Converting packets                       ?
?  Processed: 45,000 packets                ?
?  Speed: 2,500 pkt/sec    ETA: 02:15      ?
?                                           ?
?  ????????????????????????????????       ?
?                 45%                       ?
?                                           ?
?                          [Cancel]         ?
????????????????????????????????????????????
```

## Current Files vs What Needs to Change

### ? Already Created (You have these):
- `ConversionProgressWindow.xaml` - The dialog UI
- `ConversionProgressWindow.xaml.cs` - The dialog code
- `RecordingLoaderService.cs` - The service that shows the dialog

### ? Not Yet Updated (You need to change these):
- `MainWindow.xaml.cs` - Still using old file opening
- `FileSourceViewModel.cs` - Still using `RecordingFileLoader.OpenAsync` directly
- OR wherever you handle file opening in your ViewModels

## Why You Don't See It

The progress dialog files exist, but **nothing is calling them yet**. It's like having a beautiful car in your garage but never driving it.

You need to change from:
```csharp
// OLD - Direct loading (no progress dialog)
var (uow, tempPath) = await RecordingFileLoader.OpenAsync(filePath);
```

To:
```csharp
// NEW - Uses progress dialog automatically
var uow = await RecordingLoaderService.LoadAsync(filePath, ownerWindow);
```

## Quick Test Steps

1. **Add using statement** at top of `MainWindow.xaml.cs`:
   ```csharp
   using AeroDebrief.UI.Services;
   ```

2. **Replace your file opening code** with the examples above

3. **Run the app**

4. **Open an ADB file** - You'll see the beautiful progress dialog!

5. **Open a CVR file** - Quick progress, auto-closes

## Need Help Finding the File Opening Code?

Search your solution for:
- `RecordingFileLoader.OpenAsync`
- `FileLoaded?.Invoke`
- `OpenFileDialog.ShowDialog`
- `ExecuteLoadFileAsync`

Those are the places you need to update to use `RecordingLoaderService.LoadAsync` instead.

## TL;DR - Just Do This

**Find wherever you have:**
```csharp
await RecordingFileLoader.OpenAsync(filePath, ...)
```

**Replace with:**
```csharp
await RecordingLoaderService.LoadAsync(filePath, ownerWindow)
```

**That's it!** The progress dialog will appear automatically! ??
