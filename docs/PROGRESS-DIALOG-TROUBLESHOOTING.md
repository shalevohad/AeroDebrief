# ?? Progress Dialog Troubleshooting Guide

## Problem

You reported: **"I still can't see the new window progressbar"**

## Immediate Test

I've added a **test button** to help diagnose the issue:

### How to Test:

1. **Run your app**
2. **Click "File" ? "Test Progress Dialog"**
3. **Watch for the dialog to appear**

This will show a simulated progress dialog that updates every 200ms.

### What Should Happen:

```
??????????????????????????????????????????????
?  ?? Converting Recording File              ?
??????????????????????????????????????????????
?  Converting: test_recording.adb            ?
?                                             ?
?  Converting packets                         ?
?  Processed: 25,000 packets                  ?
?  Speed: 2,500 pkt/sec      ETA: 00:15     ?
?                                             ?
?  ????????????????????????????????         ?
?                  25%                        ?
??????????????????????????????????????????????
```

**Progress updates every 200ms, completes after 4 seconds**

## Diagnostic Steps

### Step 1: Test Button Works?

**If YES** ? The dialog window works, but there's an issue with the file loading path

**If NO** ? There's a fundamental issue with the dialog creation

### Step 2: Check Logs

Open your log file (usually in `logs/` folder) and search for:

```
=== LoadWithProgressAsync START ===
File: [your file path]
IsAdb: True/False, IsCvr: True/False
Creating progress window...
Progress window created
Starting background load task...
Showing progress dialog...
```

**If you see these logs:**
- Dialog is being created
- Check if it's appearing behind other windows
- Check if it's appearing on wrong monitor

**If you DON'T see these logs:**
- `RecordingLoaderService` is not being called
- The integration point is wrong

### Step 3: Check Integration Point

Look at `CoreApiService.cs` around line 131. Should be:

```csharp
var loadResult = await UI.Services.RecordingLoaderService.LoadWithProgressAsync(
    filePath, 
    System.Windows.Application.Current.MainWindow);
```

**If it's different**, the integration wasn't applied correctly.

### Step 4: Check File Type

The dialog only shows for **ADB and CVR files**, NOT for DB files.

**Are you opening:**
- ? `.adb` file ? Should show dialog
- ? `.cvr` file ? Should show dialog (briefly)
- ? `.db` file ? No dialog (opens instantly)

## Common Issues

### Issue 1: Dialog Behind Main Window

**Symptoms:** File loads but no dialog visible

**Solution:** I've added `Topmost="True"` to the XAML:

```xaml
<Window Topmost="True" ShowInTaskbar="True" ...>
```

This ensures the dialog appears on top.

### Issue 2: Wrong Monitor

**Symptoms:** Dialog appears on different monitor

**Solution:** Changed to `WindowStartupLocation="CenterScreen"` which shows on primary monitor.

### Issue 3: Not Called At All

**Symptoms:** No logs, file loads immediately

**Solution:** Check that `CoreApiService.LoadFileAsync` is calling `RecordingLoaderService`.

Search for this in `CoreApiService.cs`:
```csharp
await RecordingFileLoader.OpenAsync
```

Should be:
```csharp
await UI.Services.RecordingLoaderService.LoadWithProgressAsync
```

### Issue 4: XAML Not Compiled

**Symptoms:** Build succeeds but dialog crashes on creation

**Check:** 
1. `ConversionProgressWindow.xaml` exists
2. `ConversionProgressWindow.xaml.cs` has `partial class`
3. Build action is "Page" for XAML
4. `InitializeComponent()` is called in constructor

### Issue 5: Theme Resources Missing

**Symptoms:** Dialog shows but looks broken or crashes

**Check:** Look for errors like:
```
Cannot find resource named 'WindowBackgroundBrush'
```

**Solution:** Add fallback colors to XAML:
```xaml
<Window Background="{DynamicResource WindowBackgroundBrush, FallbackValue=#FF2D2D30}" ...>
```

## Debug Checklist

Run through this checklist:

- [ ] **Build succeeded** without errors
- [ ] **Test button** shows dialog (File ? Test Progress Dialog)
- [ ] **Opening ADB file** (not DB file)
- [ ] **Logs show** "LoadWithProgressAsync START"
- [ ] **Logs show** "Progress window created"
- [ ] **Logs show** "Showing progress dialog"
- [ ] **Main window is visible** when opening file
- [ ] **No exceptions in logs**

## Quick Fixes

### Fix 1: Force Dialog to Show

Add this to `RecordingLoaderService.cs` right before `ShowDialog()`:

```csharp
// Force show the window
progressWindow.Show();
progressWindow.Activate();
progressWindow.Focus();

// Then make it modal
var dialogResult = progressWindow.ShowDialog();
```

### Fix 2: Add Explicit Logging

Add logging to `ConversionProgressWindow` constructor:

```csharp
public ConversionProgressWindow()
{
    Logger.Info("?? ConversionProgressWindow CONSTRUCTOR CALLED");
    InitializeComponent();
    Logger.Info("?? ConversionProgressWindow INITIALIZED");
    
    // Make sure it's visible
    this.Topmost = true;
    this.ShowInTaskbar = true;
    Logger.Info("?? ConversionProgressWindow READY TO SHOW");
}
```

### Fix 3: Test Without Modal

Try showing the dialog non-modally first:

```csharp
// Instead of:
var dialogResult = progressWindow.ShowDialog();

// Try:
progressWindow.Show();
await Task.Delay(5000); // Keep it visible for 5 seconds
progressWindow.Close();
```

## What I Changed

### Changes Made:

1. ? **Added extensive logging** to `RecordingLoaderService`
2. ? **Changed XAML**: `ShowInTaskbar="True"`, `Topmost="True"`
3. ? **Changed XAML**: `WindowStartupLocation="CenterScreen"`
4. ? **Added test button**: File ? Test Progress Dialog
5. ? **Created test class**: `ProgressDialogTest.cs`

### Files Modified:

1. `src/AeroDebrief.UI/Services/RecordingLoaderService.cs` - Added logging
2. `src/AeroDebrief.UI/Windows/ConversionProgressWindow.xaml` - Made more visible
3. `src/AeroDebrief.UI/MainWindow.xaml` - Added test menu
4. `src/AeroDebrief.UI/MainWindow.xaml.cs` - Added test handler
5. `src/AeroDebrief.UI/Tests/ProgressDialogTest.cs` - Created test

## Next Steps

### 1. Test the Test Button

**Run your app** and click **"File" ? "Test Progress Dialog"**

- **If dialog shows**: Good! The dialog works, issue is in file loading path
- **If dialog doesn't show**: Fundamental issue with dialog/XAML

### 2. Check the Logs

Open your log file and search for the markers I added:

```
?? ConversionProgressWindow
=== LoadWithProgressAsync START ===
```

### 3. Report Back

Let me know:
- ? Does the test button show a dialog?
- ? What do the logs say?
- ? What file type are you opening (.adb, .cvr, .db)?
- ? Any error messages?

## If Nothing Works

If the test button doesn't show a dialog, there might be a deeper issue:

1. **Check WPF resources** are loading correctly
2. **Check for exceptions** in Output window
3. **Try a completely simple window** first:

```csharp
var testWindow = new Window
{
    Title = "Test",
    Width = 300,
    Height = 200,
    Content = new TextBlock { Text = "Hello!", FontSize = 30 }
};
testWindow.ShowDialog();
```

If even this doesn't show, there's a WPF configuration issue.

---

**Build Status:** ? Successful

**Test Button:** ? Added to File menu

**Run the test and report what you see!** ??
