# AeroDebrief Quick Start

## ?? Immediate Next Steps

Your AeroDebrief solution is **built and ready**! Follow these steps to complete the migration:

## 1. Update Namespaces (Required)

Run the namespace update script to convert all C# files to use the new `AeroDebrief.*` namespaces:

```powershell
# Preview changes first
powershell -ExecutionPolicy Bypass -File "scripts\update-namespaces.ps1" -WhatIf

# Apply changes
powershell -ExecutionPolicy Bypass -File "scripts\update-namespaces.ps1"
```

This updates:
- `Core` ? `AeroDebrief.Core`
- `DCS_SRS_RecordingClient.CLI` ? `AeroDebrief.CLI`
- Old UI namespaces ? `AeroDebrief.UI`

## 2. Rebuild and Test

```powershell
# Clean previous builds
dotnet clean AeroDebrief.sln

# Restore dependencies
dotnet restore AeroDebrief.sln

# Build
dotnet build AeroDebrief.sln --configuration Release

# Run tests
dotnet test AeroDebrief.sln --configuration Release
```

## 3. Test Applications

### CLI Application
```powershell
# Run with server IP and port
dotnet run --project src/AeroDebrief.CLI/AeroDebrief.CLI.csproj -- 192.168.1.100 5002

# Or after publishing
cd src/AeroDebrief.CLI/bin/Release/net9.0
.\AeroDebrief.CLI.exe 192.168.1.100 5002
```

### UI Application
```powershell
# Run directly
dotnet run --project src/AeroDebrief.UI/AeroDebrief.UI.csproj

# Or after publishing
cd src/AeroDebrief.UI/bin/Release/net9.0-windows
.\AeroDebrief.UI.exe
```

## 4. Set Up Git Branches

```powershell
# Commit the new structure
git add -A
git commit -m "feat: Migrate to modular AeroDebrief structure

- Reorganized into src/tests directories
- Updated project structure for .NET 9
- Added CI/CD workflows and scripts
- Maintained all existing functionality"

git push origin main

# Create beta branch
git checkout -b beta
git push origin beta

# Create release branch
git checkout -b release
git push origin release

# Return to main
git checkout main
```

## 5. Enable GitHub Actions

GitHub Actions should now work automatically:

1. Go to your repository on GitHub
2. Click the "Actions" tab
3. Workflows will trigger on:
   - Push to main/beta/release
   - Pull requests
   - Manual triggers

## 6. Test Beta Release

```powershell
# Promote to beta (merges main ? beta)
powershell -ExecutionPolicy Bypass -File "scripts\promote-beta.ps1"
```

This will:
- Merge your main branch to beta
- Push to GitHub
- Trigger the beta release workflow
- Create a pre-release with downloadable ZIP files

## Project Structure

```
AeroDebrief/
??? src/
?   ??? AeroDebrief.Core/          ? Shared logic, audio processing, models
?   ??? AeroDebrief.CLI/           ? Command-line recording tool
?   ??? AeroDebrief.UI/            ? WPF playback and analysis UI
?   ??? AeroDebrief.Integrations/  ? TacView & Lua (add your code here)
??? tests/
?   ??? AeroDebrief.Tests/         ? Unit tests
??? External/SRS/                  ? Git submodule (don't touch)
??? scripts/                       ? Automation scripts
??? .github/workflows/             ? CI/CD workflows
```

## Common Tasks

### Build Everything
```powershell
dotnet build AeroDebrief.sln --configuration Release
```

### Run All Tests
```powershell
dotnet test AeroDebrief.sln --configuration Release --verbosity normal
```

### Publish Self-Contained Executables
```powershell
# CLI
dotnet publish src/AeroDebrief.CLI/AeroDebrief.CLI.csproj -c Release -r win-x64 --self-contained -o publish/cli

# UI
dotnet publish src/AeroDebrief.UI/AeroDebrief.UI.csproj -c Release -r win-x64 --self-contained -o publish/ui
```

### Clean Everything
```powershell
dotnet clean AeroDebrief.sln
Remove-Item -Recurse -Force src/*/bin,src/*/obj,tests/*/bin,tests/*/obj -ErrorAction SilentlyContinue
```

## Troubleshooting

### "Namespace not found" errors
? Run: `powershell -ExecutionPolicy Bypass -File "scripts\update-namespaces.ps1"`

### "Project not found" errors
? Check paths in `.csproj` files are relative to project location

### "Native library not found" (opus.dll)
? Rebuild: `dotnet build --no-incremental`

### Tests fail after migration
? Verify `InternalsVisibleTo` in `AeroDebrief.Core.csproj`

### GitHub Actions don't run
? Check workflows are in `.github/workflows/` directory
? Ensure YAML syntax is correct

## What's Next?

1. **Add TacView Integration**: Implement in `src/AeroDebrief.Integrations/TacView/`
2. **Add DCS Lua Scripts**: Create in `src/AeroDebrief.Integrations/Lua/`
3. **Expand Tests**: Add more tests in `tests/AeroDebrief.Tests/`
4. **Documentation**: Update README with usage examples
5. **Release**: Use `promote-release.ps1` when ready for production

## Key Files

| File | Purpose |
|------|---------|
| `AeroDebrief.sln` | Main solution file |
| `src/AeroDebrief.Core/AeroDebrief.Core.csproj` | Core library project |
| `src/AeroDebrief.CLI/AeroDebrief.CLI.csproj` | CLI application project |
| `src/AeroDebrief.UI/AeroDebrief.UI.csproj` | WPF UI project |
| `src/AeroDebrief.Integrations/AeroDebrief.Integrations.csproj` | Integrations project |
| `tests/AeroDebrief.Tests/AeroDebrief.Tests.csproj` | Test project |
| `.github/workflows/build.yml` | Build & test workflow |
| `.github/workflows/beta.yml` | Beta release workflow |
| `scripts/update-namespaces.ps1` | Namespace update script |
| `scripts/promote-beta.ps1` | Beta promotion script |
| `scripts/promote-release.ps1` | Release promotion script |

## Resources

- **SETUP_GUIDE.md**: Detailed setup and troubleshooting
- **MIGRATION_GUIDE.md**: Complete migration documentation
- **IMPLEMENTATION_SUMMARY.md**: What was done and why
- **README.md**: Project overview

## Need Help?

1. Check **SETUP_GUIDE.md** for detailed instructions
2. Review build output carefully
3. Check GitHub Actions logs
4. Open an issue on GitHub

---

**You're ready to go! Start with step 1 above.** ??
