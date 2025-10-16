# AeroDebrief Migration Guide

This guide will help you migrate your existing DCS-SRS-RecordingClient project to the new modular AeroDebrief structure.

## Overview

The refactoring reorganizes the project into a clean, modular structure:

```
AeroDebrief/
??? src/
?   ??? AeroDebrief.Core/          # Shared logic and models
?   ??? AeroDebrief.CLI/           # Command-line interface
?   ??? AeroDebrief.UI/            # WPF application
?   ??? AeroDebrief.Integrations/  # TacView & Lua integration
??? tests/
?   ??? AeroDebrief.Tests/         # Unit tests
??? External/
?   ??? SRS/                       # Git submodule (unchanged)
?       ??? Common/
?       ??? SharedAudio/
??? scripts/
?   ??? migrate-structure.ps1      # Migration script
?   ??? update-namespaces.ps1      # Namespace update script
?   ??? promote-beta.ps1           # Beta promotion script
?   ??? promote-release.ps1        # Release promotion script
??? .github/
?   ??? workflows/
?       ??? build.yml              # Build & test workflow
?       ??? beta.yml               # Beta release workflow
??? AeroDebrief.sln                # Solution file
```

## Migration Steps

### Step 1: Backup Your Work

```powershell
# Create a backup branch
git checkout -b backup-before-migration
git push origin backup-before-migration
```

### Step 2: Run the Migration Script (Dry Run First)

```powershell
# See what changes will be made without actually doing them
.\scripts\migrate-structure.ps1 -WhatIf
```

Review the output to ensure everything looks correct.

### Step 3: Execute the Migration

```powershell
# Perform the actual file reorganization
.\scripts\migrate-structure.ps1
```

This script will:
- Copy all Core files to `src/AeroDebrief.Core/`
- Copy all CLI files to `src/AeroDebrief.CLI/`
- Copy all UI files to `src/AeroDebrief.UI/`
- Create the Integrations project structure
- Copy test files to `tests/AeroDebrief.Tests/`

### Step 4: Update Namespaces (Dry Run First)

```powershell
# See what namespace changes will be made
.\scripts\update-namespaces.ps1 -WhatIf
```

### Step 5: Execute Namespace Updates

```powershell
# Update all namespaces in C# files
.\scripts\update-namespaces.ps1
```

This script will automatically update:
- `Core` ? `AeroDebrief.Core`
- `ShalevOhad.DCS.SRS.Recorder.Core` ? `AeroDebrief.Core`
- `DCS_SRS_RecordingClient.CLI` ? `AeroDebrief.CLI`
- `ShalevOhad.DCS.SRS.Recorder.PlayerClient.UI` ? `AeroDebrief.UI`

### Step 6: Build the Solution

```powershell
# Restore dependencies
dotnet restore AeroDebrief.sln

# Build the solution
dotnet build AeroDebrief.sln --configuration Release
```

### Step 7: Fix Any Remaining Issues

If there are build errors, they're likely due to:

1. **Missing using statements**: Add appropriate `using AeroDebrief.Core.*` statements
2. **Changed references**: Update any hardcoded references to old namespaces
3. **XAML namespaces**: Update XML namespaces in XAML files to use new namespaces

### Step 8: Run Tests

```powershell
# Run all unit tests
dotnet test AeroDebrief.sln --configuration Release
```

### Step 9: Clean Up Old Files (Optional)

Once everything builds and tests pass, you can remove the old project directories:

```powershell
# Remove old directories (BE CAREFUL!)
Remove-Item -Recurse -Force Core
Remove-Item -Recurse -Force DCS-SRS-RecordingClient.CLI
Remove-Item -Recurse -Force DCS-SRS-RecordingClient.UI
Remove-Item -Recurse -Force SimpleRawExport  # If not needed
```

**WARNING**: Only do this after confirming everything works in the new structure!

### Step 10: Commit Changes

```powershell
# Stage all changes
git add .

# Commit with a descriptive message
git commit -m "Refactor to modular AeroDebrief structure

- Reorganized into src/tests directory structure
- Updated all namespaces to AeroDebrief.*
- Created modular projects: Core, CLI, UI, Integrations
- Added CI/CD scripts and GitHub workflows
- Maintained all existing functionality"

# Push to GitHub
git push origin main
```

## Project Structure Details

### AeroDebrief.Core

Contains all shared logic:
- **Audio/**: Audio processing, recording, playback engines
- **Settings/**: Settings management
- **Helpers/**: Utility functions
- **Models/**: Data models
- **Analysis/**: File and frequency analysis
- **Filtering/**: Audio filtering logic
- **Playback/**: Playback controllers
- **Singleton/**: Shared state management
- **Theme/**: Theme and design language

### AeroDebrief.CLI

Command-line interface:
- **Program.cs**: Entry point with command-line parsing
- **AudioDiagnostics.cs**: Audio testing and diagnostics
- Recording mode
- File analysis tools

### AeroDebrief.UI

WPF application:
- **Controls/**: Custom WPF controls (waveform viewer, spectrum analyzer, etc.)
- **ViewModels/**: MVVM view models
- **Services/**: UI services and integrations
- **MainWindow.xaml**: Main application window
- **App.xaml**: Application definition

### AeroDebrief.Integrations

TacView and DCS Lua integrations:
- **Lua/**: DCS Lua export scripts
- **TacView/**: TacView synchronization logic
- Integration adapters

### AeroDebrief.Tests

Unit tests for all modules:
- Core functionality tests
- Opus decoding tests
- Audio processing tests
- Integration tests

## CI/CD Pipeline

### Build Workflow (`build.yml`)

Triggers on push/PR to main, beta, or release branches:
1. Checkout code with submodules
2. Setup .NET 9
3. Restore dependencies
4. Build solution
5. Run tests
6. Upload artifacts

### Beta Release Workflow (`beta.yml`)

Triggers on tags matching `v*-beta`:
1. Build and test
2. Publish CLI and UI as self-contained executables
3. Create ZIP archives
4. Create GitHub pre-release with binaries

### Promotion Scripts

**promote-beta.ps1**:
- Merges `main` ? `beta`
- Creates beta tag (e.g., `v1.0.0-beta`)
- Triggers beta release workflow

**promote-release.ps1**:
- Merges `beta` ? `release`
- Creates release tag (e.g., `v1.0.0`)
- Ready for manual GitHub Release creation

## Usage Examples

### Promote to Beta

```powershell
# Merge main to beta and create beta tag
.\scripts\promote-beta.ps1

# With specific version
.\scripts\promote-beta.ps1 -Version "1.2.0"
```

### Promote to Release

```powershell
# Merge beta to release and create release tag
.\scripts\promote-release.ps1

# With specific version
.\scripts\promote-release.ps1 -Version "1.2.0"
```

### Build and Run

```powershell
# Build everything
dotnet build AeroDebrief.sln --configuration Release

# Run CLI
dotnet run --project src/AeroDebrief.CLI/AeroDebrief.CLI.csproj -- 192.168.1.100 5002

# Run UI
dotnet run --project src/AeroDebrief.UI/AeroDebrief.UI.csproj

# Run tests
dotnet test AeroDebrief.sln
```

## Troubleshooting

### Build Errors After Migration

1. **Check namespace imports**: Ensure all files have correct `using AeroDebrief.*` statements
2. **Verify project references**: Check all `.csproj` files have correct `<ProjectReference>` paths
3. **Clean and rebuild**: `dotnet clean && dotnet build`
4. **Check for orphaned obj/bin folders**: Delete them manually if needed

### XAML Errors

Update XML namespace declarations in XAML files:

```xml
<!-- Old -->
xmlns:local="clr-namespace:ShalevOhad.DCS.SRS.Recorder.PlayerClient.UI"

<!-- New -->
xmlns:local="clr-namespace:AeroDebrief.UI"
```

### Submodule Issues

If External/SRS submodules aren't updating:

```powershell
git submodule update --init --recursive
```

### Test Failures

If tests fail after migration:
1. Check that test project references are correct
2. Verify `InternalsVisibleTo` in Core project includes `AeroDebrief.Tests`
3. Update any test namespaces

## Benefits of New Structure

1. **Modularity**: Clear separation of concerns
2. **Testability**: Easy to test individual modules
3. **Maintainability**: Organized, predictable structure
4. **Scalability**: Easy to add new modules
5. **CI/CD Ready**: Automated build, test, and release pipelines
6. **Professional**: Industry-standard project layout

## Support

If you encounter issues during migration:
1. Check the [GitHub Issues](https://github.com/shalevohad/AeroDebrief/issues)
2. Review the build logs carefully
3. Compare with the backup branch
4. Ask for help in discussions

## Next Steps After Migration

1. **Add TacView Integration**: Implement in `AeroDebrief.Integrations`
2. **Add Lua Scripts**: Place DCS export scripts in `AeroDebrief.Integrations/Lua/`
3. **Expand Tests**: Add more unit and integration tests
4. **Documentation**: Update README with new project structure
5. **Release**: Use promotion scripts to create beta/release builds

---

**Happy Debriefing! ??**
