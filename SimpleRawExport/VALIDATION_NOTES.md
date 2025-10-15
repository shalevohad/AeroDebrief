# SimpleRawExport Validation Notes

## Fixed Issues

### 1. Missing Native Opus Library
**Problem**: The Opus decoder requires native `opus.dll` (Windows) or `opus.so` (Linux) library.

**Solution**: Added `SharedAudio` project reference to `SimpleRawExport.csproj`:
```xml
<ProjectReference Include="..\External\SRS\SharedAudio\SharedAudio.csproj" />
```

The SharedAudio project automatically copies native libraries to the output directory:
- `opus.dll` / `opus.so` - Opus codec
- `libmp3lame.dll` / `libmp3lame.so` - LAME MP3 encoder

### 2. Unused Variable Warning
**Problem**: Compiler warning CS0219 for unused `hasEnhancedData` variable.

**Solution**: Removed the unused variable from the packet reading logic.

## Build Verification

? **Build Status**: Successful
? **Native Libraries**: Copied to output directory
? **Warnings**: Resolved

## Native Library Verification

After building, the following files should exist in `SimpleRawExport\bin\Debug\net9.0\`:
- ? `opus.dll` (454,656 bytes)
- ? `opus.so` (514,064 bytes)  
- ? `libmp3lame.dll` (1,081,856 bytes)
- ? `libmp3lame.so` (301,192 bytes)
- ? `libmp3lame.32.dll` (697,344 bytes)
- ? `libmp3lame.64.dll` (1,081,856 bytes)

## Runtime Testing

To test the tool at runtime:

### 1. Create Test Recording
Use the main recording client to create a `.srs` file.

### 2. Run SimpleRawExport
```bash
cd SimpleRawExport\bin\Debug\net9.0
SimpleRawExport.exe <path-to-recording.srs>
```

### 3. Expected Behavior
The tool should:
1. Read packets from the recording file
2. Decode Opus audio using the native library
3. Mix all audio into a timeline
4. Normalize the audio
5. Export to MP3 format

### 4. Common Runtime Errors

**Error**: "Unable to load DLL 'opus'"
- **Cause**: opus.dll is missing from the output directory
- **Fix**: Rebuild the project to copy native libraries

**Error**: "Exception occurred while creating decoder"
- **Cause**: Invalid sample rate or channel count
- **Fix**: Verify SAMPLE_RATE=48000 and channels=1 (mono)

**Error**: "Decoding failed - [error code]"
- **Cause**: Corrupted Opus packets or invalid format
- **Fix**: Check if the .srs file is valid and not corrupted

## Code Quality

### Static Analysis
- No critical issues
- All warnings resolved
- Clean build output

### Performance Considerations
- Sequential packet processing (memory efficient)
- Progress indicators for user feedback
- Graceful error handling for corrupted packets

### Memory Usage
- Minimal allocations during packet reading
- Reuses decoder instance
- Efficient float buffer management

## Platform Compatibility

### Windows
- ? Uses `opus.dll` and `libmp3lame.dll`
- ? Tested on .NET 9

### Linux
- ?? Should use `opus.so` and `libmp3lame.so`
- ?? Not yet tested, but libraries are included

## Future Enhancements

1. **Add runtime library check**: Verify native libraries exist before attempting to use them
2. **Better error messages**: Provide user-friendly errors if libraries are missing
3. **Cross-platform testing**: Verify functionality on Linux
4. **Progress percentage**: Show detailed progress during long exports
5. **Multiple format support**: Add WAV, FLAC output options
