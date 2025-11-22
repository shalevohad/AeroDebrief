# CVR-Only Quick Reference

## For Developers: How to Use RecordingArchiveService

### Basic Usage Pattern

```csharp
using AeroDebrief.Core.Storage;
using AeroDebrief.Core.Storage.Abstractions;

// Create the service
var service = new RecordingArchiveService();
```

---

## Creating a New Recording

```csharp
// 1. Create metadata
var metadata = new RecordingMetadata
{
    Version = Constants.RECORDING_FILE_MAGIC,
    ServerIp = "127.0.0.1",
    ServerPort = 5002,
    StartTime = DateTime.UtcNow
};

// 2. Create the recording
// Database is stored in: %LOCALAPPDATA%\AeroDebrief\Sessions\{guid}\recording.db
var unitOfWork = await service.CreateNewRecordingAsync(metadata);

// 3. Use the unit of work to store data
await unitOfWork.Packets.InsertBatchAsync(packets);

// 4. Save as CVR (user-visible file)
await service.SaveAsCvrAsync("path/to/output.cvr", progress);

// 5. Close and cleanup
await service.CloseAsync();
// Hidden directory is now deleted
```

---

## Opening an Existing CVR File

```csharp
// 1. Open the CVR file
// CVR is extracted to: %LOCALAPPDATA%\AeroDebrief\Sessions\{guid}\recording.db
var unitOfWork = await service.OpenCvrAsync("path/to/recording.cvr", progress);

// 2. Read data from the unit of work
var packets = service.CurrentUnitOfWork.Packets.StreamAsync(...);
await foreach (var packet in packets)
{
    // Process packet
}

// 3. Close and cleanup
await service.CloseAsync();
// Hidden directory is now deleted
```

---

## File Dialog Integration

```csharp
// Use the service's file filter for Open File dialogs
var dialog = new OpenFileDialog
{
    Title = "Select Recording File",
    Filter = RecordingArchiveService.GetFileDialogFilter(),
    FilterIndex = 1  // Default to "All Recording Files"
};

if (dialog.ShowDialog() == true)
{
    var filePath = dialog.FileName;
    // ... open the file
}

// Filter returns:
// "All Recording Files|*.cvr;*.adb|
//  Combat Voice Recording (*.cvr)|*.cvr|
//  Legacy Recording (*.adb)|*.adb|
//  All Files|*.*"
```

---

## Recent Files Management

```csharp
// Before adding to recent files, validate the format
if (RecordingArchiveService.IsUserVisibleFormat(filePath))
{
    // Only .cvr and .adb files - safe to show to users
    AddToRecentFiles(filePath);
}
else
{
    // .db or .cvr-debug files - internal only, don't show to users
    Logger.Warn($"Attempted to add internal format to recent files: {filePath}");
}
```

---

## Progress Reporting

```csharp
// For opening CVR files (string progress)
var progress = new Progress<string>(status => 
{
    Console.WriteLine($"Status: {status}");
    // Example output:
    // "Extracting CVR archive..."
    // "Opening database..."
    // "Ready"
});

await service.OpenCvrAsync("recording.cvr", progress);

// For saving CVR files (percentage progress)
var progress = new Progress<int>(percent => 
{
    progressBar.Value = percent;
    // Progress: 0-10%:   Rebuilding frequency stats
    //          10-20%:  Rebuilding player stats
    //          20-30%:  Finalizing database
    //          30-100%: Compressing to CVR
});

await service.SaveAsCvrAsync("output.cvr", progress);
```

---

## Error Handling

```csharp
try
{
    await service.OpenCvrAsync("recording.cvr");
}
catch (FileNotFoundException)
{
    MessageBox.Show("Recording file not found.");
}
catch (ArgumentException ex) when (ex.Message.Contains("Not a CVR file"))
{
    MessageBox.Show("Invalid file format. Please select a .cvr file.");
}
catch (Exception ex)
{
    Logger.Error(ex, "Failed to open recording");
    MessageBox.Show($"Error opening recording: {ex.Message}");
}
finally
{
    // Always cleanup, even on error
    await service.CloseAsync();
}
```

---

## Best Practices

### ? DO

```csharp
// Always dispose the service
using var service = new RecordingArchiveService();

// Always close when done
await service.CloseAsync();

// Validate file paths before using
if (RecordingArchiveService.IsUserVisibleFormat(path))
{
    // Use the file
}

// Use the service's file filters
Filter = RecordingArchiveService.GetFileDialogFilter()
```

### ? DON'T

```csharp
// Don't expose database paths to users
// BAD: MessageBox.Show($"Database at: {hiddenPath}");

// Don't allow users to select .db files directly
// BAD: Filter = "Database Files|*.db"

// Don't skip CloseAsync() - leaves hidden directories
// BAD: service = null; // Without calling CloseAsync()

// Don't reference session directories in UI
// BAD: label.Text = workingDirectory;
```

---

## Common Scenarios

### Scenario 1: Recording Live Audio

```csharp
var service = new RecordingArchiveService();

// Start recording
var metadata = CreateMetadata();
var uow = await service.CreateNewRecordingAsync(metadata);

// While recording...
while (isRecording)
{
    var packets = GetAudioPackets();
    await uow.Packets.InsertBatchAsync(packets);
}

// Stop and save
await service.SaveAsCvrAsync(userSelectedPath);
await service.CloseAsync();
```

### Scenario 2: Browsing Recordings

```csharp
var service = new RecordingArchiveService();

// Let user select file
var dialog = new OpenFileDialog
{
    Filter = RecordingArchiveService.GetFileDialogFilter()
};

if (dialog.ShowDialog() == true)
{
    // Open for playback
    var uow = await service.OpenCvrAsync(dialog.FileName);
    
    // Read and play
    await foreach (var packet in uow.Packets.StreamAsync(TimeSpan.Zero))
    {
        PlayAudio(packet);
    }
    
    // Close when done
    await service.CloseAsync();
}
```

### Scenario 3: Converting Legacy ADB

```csharp
// The service handles this automatically!
var service = new RecordingArchiveService();

// This works even though it's an .adb file
// It will be converted and extracted to hidden directory
var uow = await service.OpenCvrAsync("legacy.adb");

// Save as modern CVR format
await service.SaveAsCvrAsync("converted.cvr");
await service.CloseAsync();

// User now has legacy.adb and converted.cvr
// Both are user-visible, no .db files exposed
```

---

## Checking Service State

```csharp
// Check if a recording is open
if (service.IsOpen)
{
    // Safe to use CurrentUnitOfWork
    var count = await service.CurrentUnitOfWork.Packets.GetCountAsync();
}

// Get current session ID (for logging only, never show to users)
if (service.CurrentSessionId != null)
{
    Logger.Debug($"Session: {service.CurrentSessionId}");
}
```

---

## Testing

```csharp
[Fact]
public async Task MyTest()
{
    // Arrange
    var service = new RecordingArchiveService();
    var testCvrPath = Path.Combine(_tempDir, "test.cvr");
    
    // Act
    var uow = await service.CreateNewRecordingAsync(metadata);
    await uow.Packets.InsertBatchAsync(testPackets);
    await service.SaveAsCvrAsync(testCvrPath);
    await service.CloseAsync();
    
    // Assert
    File.Exists(testCvrPath).Should().BeTrue();
    Path.GetExtension(testCvrPath).Should().Be(".cvr");
    
    // Verify no .db files in test directory
    var dbFiles = Directory.GetFiles(_tempDir, "*.db");
    dbFiles.Should().BeEmpty();
}
```

---

## Important Notes

### Session Directory Cleanup
- Cleanup is **automatic** when calling `CloseAsync()`
- Cleanup is **best-effort** (won't throw on failure)
- Cleanup uses **retry logic** (handles antivirus file locks)
- If cleanup fails, directories remain in `%LOCALAPPDATA%` but are benign

### File Extensions
- `.cvr` - Primary format, compressed archive (Zstandard)
- `.adb` - Legacy format, auto-converts on open
- `.db` - Internal format, not shown in dialogs but can be opened programmatically
- `.cvr-debug` - Development format (uncompressed), not shown in dialogs

### Performance
- Creating session: < 10ms
- Extracting CVR: ~1-2 seconds (typical file)
- Compressing CVR: ~2-3 seconds (typical file)
- Cleanup: < 500ms (with retry)

---

## Related Documentation

- [CVR-Only Architecture Implementation](CVR-Only-Architecture-Implementation.md)
- [CVR User Interface Requirements](CVR-User-Interface-Requirements.md)
- [CVR-Only Implementation Complete](CVR-Only-Implementation-Complete.md)

---

**Quick Reference v1.0**
