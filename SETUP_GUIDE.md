# AeroDebrief - Complete Setup & Migration Guide

## Current Status

Your AeroDebrief project has the following structure already in place:

### ? Completed
- Project structure (`src/`, `tests/`, `External/`, `scripts/`, `.github/workflows/`)
- Project files (`.csproj`) for all modules with correct references
- Solution file (`AeroDebrief.sln`)
- Migration scripts (`migrate-structure.ps1`, `update-namespaces.ps1`)
- Promotion scripts (`promote-beta.ps1`, `promote-release.ps1`)
- Files have been copied to new structure

### ?? Needs Completion
1. Namespace updates in C# files
2. Workflow files need proper YAML formatting
3. Old project directories cleanup
4. Build verification

## Step-by-Step Completion Guide

### Step 1: Update Namespaces

Run the namespace update script to change all C# file namespaces:

```powershell
# Dry run to see what will change
.\scripts\update-namespaces.ps1 -WhatIf

# Execute the updates
.\scripts\update-namespaces.ps1
```

This will update:
- `Core` ? `AeroDebrief.Core`
- `DCS_SRS_RecordingClient.CLI` ? `AeroDebrief.CLI`
- `ShalevOhad.DCS.SRS.Recorder.*` ? `AeroDebrief.*`

### Step 2: Fix GitHub Workflows

The workflow files need proper YAML formatting. Here's what should be in each:

#### `.github/workflows/build.yml`

```yaml
name: Build & Test

on:
  push:
    branches: [ main, beta, release ]
  pull_request:
    branches: [ main, beta, release ]

jobs:
  build:
    runs-on: windows-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v4
        with:
          submodules: recursive
          
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Restore dependencies
        run: dotnet restore AeroDebrief.sln

      - name: Build
        run: dotnet build AeroDebrief.sln --configuration Release --no-restore

      - name: Test
        run: dotnet test AeroDebrief.sln --configuration Release --no-build --verbosity normal

      - name: Upload CLI artifact
        if: success()
        uses: actions/upload-artifact@v4
        with:
          name: AeroDebrief-CLI-${{ github.ref_name }}
          path: src/AeroDebrief.CLI/bin/Release/net9.0/

      - name: Upload UI artifact
        if: success()
        uses: actions/upload-artifact@v4
        with:
          name: AeroDebrief-UI-${{ github.ref_name }}
          path: src/AeroDebrief.UI/bin/Release/net9.0-windows/
```

#### `.github/workflows/beta.yml`

```yaml
name: Beta Release

on:
  push:
    branches: [ beta ]

permissions:
  contents: write
  actions: read

jobs:
  beta-build:
    name: Build & Publish Beta
    runs-on: windows-latest

    steps:
      - name: Checkout repository
        uses: actions/checkout@v4
        with:
          submodules: recursive

      - name: Setup .NET 9
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Restore dependencies
        run: dotnet restore AeroDebrief.sln

      - name: Build solution
        run: dotnet build AeroDebrief.sln --configuration Release --no-restore

      - name: Run tests
        run: dotnet test AeroDebrief.sln --configuration Release --no-build --verbosity normal

      - name: Publish CLI (Self-contained)
        run: dotnet publish src/AeroDebrief.CLI/AeroDebrief.CLI.csproj -c Release -r win-x64 --self-contained -o ./publish/cli

      - name: Publish UI (Self-contained)
        run: dotnet publish src/AeroDebrief.UI/AeroDebrief.UI.csproj -c Release -r win-x64 --self-contained -o ./publish/ui

      - name: Create ZIP archives
        run: |
          Compress-Archive -Path publish/cli/* -DestinationPath AeroDebrief-CLI-Beta.zip
          Compress-Archive -Path publish/ui/* -DestinationPath AeroDebrief-UI-Beta.zip

      - name: Generate beta tag
        id: version
        run: |
          $VERSION = 'beta-' + (Get-Date -Format 'yyyy.MM.dd.HHmm')
          echo "version=$VERSION" >> $env:GITHUB_OUTPUT

      - name: Create GitHub Pre-Release
        uses: softprops/action-gh-release@v2
        with:
          tag_name: ${{ steps.version.outputs.version }}
          name: 'AeroDebrief Beta ${{ steps.version.outputs.version }}'
          body: |
            **Beta build for internal validation**
            Generated automatically from **beta** branch.
            
            Includes:
            - AeroDebrief CLI (Command-line interface)
            - AeroDebrief UI (WPF Application)
          draft: false
          prerelease: true
          files: |
            AeroDebrief-CLI-Beta.zip
            AeroDebrief-UI-Beta.zip
```

**To apply these**: Copy the YAML content above into the respective files using your editor, or recreate them.

### Step 3: Build and Test

```powershell
# Clean old build artifacts
dotnet clean AeroDebrief.sln

# Restore dependencies
dotnet restore AeroDebrief.sln

# Build
dotnet build AeroDebrief.sln --configuration Release

# Run tests
dotnet test AeroDebrief.sln --configuration Release
```

### Step 4: Fix Any Compilation Errors

Common issues and fixes:

**Missing using statements**:
```csharp
// Add to files that need them
using AeroDebrief.Core;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core.Models;
// etc.
```

**XAML namespace errors**:
```xml
<!-- Update in XAML files -->
xmlns:local="clr-namespace:AeroDebrief.UI"
xmlns:controls="clr-namespace:AeroDebrief.UI.Controls"
```

**Project reference issues**:
- Already fixed in `.csproj` files
- Verify paths are correct relative to solution root

### Step 5: Clean Up Old Directories (Optional)

**WARNING**: Only do this after verifying everything works!

```powershell
# Create a backup first!
git add -A
git commit -m "Backup before cleanup"

# Remove old directories
Remove-Item -Recurse -Force "Core"
Remove-Item -Recurse -Force "Core.Tests"
Remove-Item -Recurse -Force "DCS-SRS-RecordingClient.CLI"
Remove-Item -Recurse -Force "DCS-SRS-RecordingClient.UI"
Remove-Item -Recurse -Force "SimpleRawExport"  # If not needed
```

### Step 6: Commit and Push

```powershell
# Stage all changes
git add -A

# Commit
git commit -m "Refactor: Migrate to modular AeroDebrief structure

- Reorganized into src/tests directory structure
- Updated all namespaces to AeroDebrief.*
- Created modular projects: Core, CLI, UI, Integrations
- Added CI/CD scripts and GitHub workflows
- Updated project references and dependencies
- Maintained all existing functionality"

# Push to GitHub
git push origin main
```

### Step 7: Set Up Branch Strategy

```powershell
# Create beta branch
git checkout -b beta
git push origin beta

# Create release branch
git checkout -b release
git push origin release

# Back to main
git checkout main
```

## Testing the CI/CD Pipeline

### Test Build Workflow

```powershell
# Make a small change and push to main
git checkout main
echo "# Test" >> TEST.md
git add TEST.md
git commit -m "Test: Trigger build workflow"
git push origin main
```

Check GitHub Actions tab to see the build workflow run.

### Test Beta Workflow

```powershell
# Promote to beta
.\scripts\promote-beta.ps1
```

This will:
1. Merge main ? beta
2. Push to beta branch
3. Trigger beta release workflow
4. Create a GitHub pre-release with ZIP files

## Project File Details

### AeroDebrief.Core.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Platforms>x64</Platforms>
    <RootNamespace>AeroDebrief.Core</RootNamespace>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="AeroDebrief.Tests" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="NAudio.Wasapi" Version="2.2.1" />
    <PackageReference Include="NLog" Version="6.0.0" />
    <PackageReference Include="sharpconfig" Version="3.2.9.1" />
    <PackageReference Include="System.Drawing.Common" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\External\SRS\Common\Common.csproj" />
    <ProjectReference Include="..\..\External\SRS\SharedAudio\SharedAudio.csproj" />
  </ItemGroup>
</Project>
```

### AeroDebrief.CLI.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <RootNamespace>AeroDebrief.CLI</RootNamespace>
    <Platforms>x64</Platforms>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\AeroDebrief.Core\AeroDebrief.Core.csproj" />
  </ItemGroup>
</Project>
```

### AeroDebrief.UI.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows</TargetFramework>
    <RootNamespace>AeroDebrief.UI</RootNamespace>
    <UseWPF>true</UseWPF>
    <Platforms>x64</Platforms>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\AeroDebrief.Core\AeroDebrief.Core.csproj" />
  </ItemGroup>
</Project>
```

### AeroDebrief.Integrations.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <RootNamespace>AeroDebrief.Integrations</RootNamespace>
    <Platforms>x64</Platforms>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\AeroDebrief.Core\AeroDebrief.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="Lua\**\*.lua">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

### AeroDebrief.Tests.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <RootNamespace>AeroDebrief.Tests</RootNamespace>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <Platforms>x64</Platforms>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="MSTest.TestAdapter" Version="3.6.3" />
    <PackageReference Include="MSTest.TestFramework" Version="3.6.3" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\AeroDebrief.Core\AeroDebrief.Core.csproj" />
    <ProjectReference Include="..\..\src\AeroDebrief.Integrations\AeroDebrief.Integrations.csproj" />
  </ItemGroup>
</Project>
```

## Common Issues and Solutions

### Issue: "Cannot find namespace AeroDebrief"

**Solution**: Run the namespace update script:
```powershell
.\scripts\update-namespaces.ps1
```

### Issue: "Project reference not found"

**Solution**: Check that paths in `.csproj` files are correct:
- From `src/AeroDebrief.Core`: `..\..\External\SRS\Common\Common.csproj`
- From `src/AeroDebrief.CLI`: `..\AeroDebrief.Core\AeroDebrief.Core.csproj`
- From `tests/AeroDebrief.Tests`: `..\..\src\AeroDebrief.Core\AeroDebrief.Core.csproj`

### Issue: "Native library not found (opus.dll)"

**Solution**: Native libraries are copied from SharedAudio automatically during build. If missing:
```powershell
# Rebuild with verbosity to see copy operations
dotnet build AeroDebrief.sln -v detailed
```

### Issue: "Build succeeds but tests fail"

**Solution**: Check that `InternalsVisibleTo` is set in Core project:
```xml
<ItemGroup>
  <InternalsVisibleTo Include="AeroDebrief.Tests" />
</ItemGroup>
```

### Issue: "XAML designer shows errors"

**Solution**: 
1. Restart Visual Studio
2. Clean and rebuild solution
3. Check XML namespaces in XAML files match new namespace structure

## Next Steps After Migration

### 1. Add TacView Integration
Create `src/AeroDebrief.Integrations/TacView/TacViewSynchronizer.cs`:
```csharp
namespace AeroDebrief.Integrations.TacView
{
    public class TacViewSynchronizer
    {
        // TODO: Implement TacView timeline synchronization
    }
}
```

### 2. Add DCS Lua Scripts
Create `src/AeroDebrief.Integrations/Lua/Export.lua`:
```lua
-- DCS Export script for AeroDebrief
-- Place in: DCS World\Scripts\Export.lua
```

### 3. Expand Test Coverage
Add more tests in `tests/AeroDebrief.Tests/`:
- Audio processing tests
- File I/O tests
- Integration tests

### 4. Documentation
Update:
- XML documentation comments in code
- User guides
- API documentation

### 5. Continuous Improvement
- Add code coverage reporting
- Set up automated releases
- Add performance benchmarks

## Verification Checklist

- [ ] All namespaces updated to `AeroDebrief.*`
- [ ] Solution builds without errors
- [ ] All tests pass
- [ ] CLI runs successfully
- [ ] UI launches and works
- [ ] GitHub workflows are properly formatted
- [ ] Old directories removed (after backup)
- [ ] Changes committed and pushed
- [ ] Beta branch created
- [ ] Release branch created
- [ ] CI/CD pipeline tested

## Support

If you encounter issues:
1. Check this guide first
2. Review MIGRATION_GUIDE.md
3. Check build output carefully
4. Open an issue on GitHub

---

**You're ready to complete the migration! Follow the steps above and your modular AeroDebrief solution will be ready for development and deployment.**
