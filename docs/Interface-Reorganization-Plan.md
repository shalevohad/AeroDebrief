# Interface Reorganization Plan

## ?? Objective
Reorganize all interfaces into dedicated `/Interfaces/` folders with domain-based subfolders to clearly emphasize their role as contracts and indicate what subsystem they connect to.

---

## ?? New Structure

### AeroDebrief.Core ? `src/AeroDebrief.Core/Interfaces/`

```
Interfaces/
??? Audio/                          # Audio processing contracts
?   ??? IAudioOutputEngine.cs      # Audio output abstraction (WASAPI, Test)
?   ??? IAudioProcessingEngine.cs  # Audio decode/process pipeline
?   ??? IAudioSource.cs            # Audio data source for testing
?
??? Storage/                        # Data access contracts
?   ??? IPacketSource.cs           # Packet source abstraction (FilePacketSource, DuckDBPacketSource)
?
??? Playback/                       # Playback integration contracts
    ??? IExternalTimeSource.cs     # External time sync (Tacview integration)
```

### AeroDebrief.UI ? `src/AeroDebrief.UI/Interfaces/`

```
Interfaces/
??? Visualization/                         # Chart/graph rendering contracts
?   ??? IUnifiedChartRenderer.cs          # Chart rendering abstraction (LiveCharts2)
?   ??? IAmplitudeSeriesProvider.cs       # Amplitude data provider for waveform
?   ??? IDataTileManager.cs               # Tile-based data loading manager
?   ??? IPlayheadSyncService.cs           # Playhead synchronization service
?   ??? IErrorHandlingService.cs          # Error handling for visualization
```

---

## ?? Migration Steps

### Phase 1: AeroDebrief.Core Interfaces

#### Audio Interfaces (3 files)
1. Move `src/AeroDebrief.Core/Audio/IAudioOutputEngine.cs`
   ? `src/AeroDebrief.Core/Interfaces/Audio/IAudioOutputEngine.cs`

2. Move `src/AeroDebrief.Core/Audio/IAudioProcessingEngine.cs`
   ? `src/AeroDebrief.Core/Interfaces/Audio/IAudioProcessingEngine.cs`

3. Move `src/AeroDebrief.Core/Audio/IAudioSource.cs`
   ? `src/AeroDebrief.Core/Interfaces/Audio/IAudioSource.cs`

#### Storage Interfaces (1 file)
4. Move `src/AeroDebrief.Core/IO/IPacketSource.cs`
   ? `src/AeroDebrief.Core/Interfaces/Storage/IPacketSource.cs`

#### Playback Interfaces (1 file)
5. Move `src/AeroDebrief.Core/Playback/IExternalTimeSource.cs`
   ? `src/AeroDebrief.Core/Interfaces/Playback/IExternalTimeSource.cs`

### Phase 2: AeroDebrief.UI Interfaces

#### Visualization Interfaces (5 files)
1. Move `src/AeroDebrief.UI/Charts/IUnifiedChartRenderer.cs`
   ? `src/AeroDebrief.UI/Interfaces/Visualization/IUnifiedChartRenderer.cs`

2. Move `src/AeroDebrief.UI/Services/Visualization/Graphs/IAmplitudeSeriesProvider.cs`
   ? `src/AeroDebrief.UI/Interfaces/Visualization/IAmplitudeSeriesProvider.cs`

3. Move `src/AeroDebrief.UI/Services/Visualization/Graphs/IDataTileManager.cs`
   ? `src/AeroDebrief.UI/Interfaces/Visualization/IDataTileManager.cs`

4. Move `src/AeroDebrief.UI/Services/Visualization/Graphs/IPlayheadSyncService.cs`
   ? `src/AeroDebrief.UI/Interfaces/Visualization/IPlayheadSyncService.cs`

5. Move `src/AeroDebrief.UI/Services/Visualization/Graphs/IErrorHandlingService.cs`
   ? `src/AeroDebrief.UI/Interfaces/Visualization/IErrorHandlingService.cs`

---

## ?? Namespace Changes

### AeroDebrief.Core

| Old Namespace | New Namespace |
|--------------|---------------|
| `AeroDebrief.Core.Audio` | `AeroDebrief.Core.Interfaces.Audio` |
| `AeroDebrief.Core.IO` | `AeroDebrief.Core.Interfaces.Storage` |
| `AeroDebrief.Core.Playback` | `AeroDebrief.Core.Interfaces.Playback` |

### AeroDebrief.UI

| Old Namespace | New Namespace |
|--------------|---------------|
| `AeroDebrief.UI.Charts` | `AeroDebrief.UI.Interfaces.Visualization` |
| `AeroDebrief.UI.Services.Visualization.Graphs` | `AeroDebrief.UI.Interfaces.Visualization` |

---

## ?? Benefits

### 1. Clear Architectural Intent
- ? Interfaces are immediately recognizable as contracts
- ? Folder structure shows what domain they belong to
- ? Easy to find all interfaces in one place

### 2. Better Organization
```
Before:
src/AeroDebrief.Core/Audio/
  ??? AudioOutputEngine.cs (implementation)
  ??? IAudioOutputEngine.cs (interface)
  ??? AudioProcessingEngine.cs (implementation)
  ??? IAudioProcessingEngine.cs (interface)
  ??? ... (mixed)

After:
src/AeroDebrief.Core/Interfaces/Audio/
  ??? IAudioOutputEngine.cs ? (contract only)
  ??? IAudioProcessingEngine.cs ? (contract only)
  ??? IAudioSource.cs ? (contract only)

src/AeroDebrief.Core/Audio/
  ??? AudioOutputEngine.cs (implementation)
  ??? AudioProcessingEngine.cs (implementation)
  ??? ... (implementations only)
```

### 3. Domain-Based Subfolders
- **Audio/** - Audio processing contracts
- **Storage/** - Data access contracts  
- **Playback/** - Playback integration contracts
- **Visualization/** - UI rendering contracts

This makes it clear:
- `IAudioOutputEngine` ? Audio subsystem
- `IPacketSource` ? Storage subsystem
- `IExternalTimeSource` ? Playback subsystem
- `IUnifiedChartRenderer` ? Visualization subsystem

### 4. Follows .NET Best Practices
- Interfaces in dedicated folders (like .NET Core's `Microsoft.Extensions.DependencyInjection.Abstractions`)
- Clear separation of contracts from implementations
- Easier to create NuGet packages with just interfaces

---

## ?? Implementation Commands

### Create Directories
```powershell
# AeroDebrief.Core
New-Item -Path "src\AeroDebrief.Core\Interfaces\Audio" -ItemType Directory -Force
New-Item -Path "src\AeroDebrief.Core\Interfaces\Storage" -ItemType Directory -Force
New-Item -Path "src\AeroDebrief.Core\Interfaces\Playback" -ItemType Directory -Force

# AeroDebrief.UI
New-Item -Path "src\AeroDebrief.UI\Interfaces\Visualization" -ItemType Directory -Force
```

### Move Files (Use Copilot tools)
Then use Copilot's file moving tools to:
1. Move each interface file to its new location
2. Update namespace declarations
3. Update all using directives in dependent files
4. Verify build succeeds

---

## ? Verification Checklist

- [ ] All interface files moved to `/Interfaces/` folders
- [ ] Namespaces updated in interface files
- [ ] All `using` statements updated in implementation files
- [ ] All `using` statements updated in test files
- [ ] Build successful (zero errors)
- [ ] All tests passing
- [ ] Update architecture documentation

---

## ?? Summary

### Files to Move: **11 total**

**AeroDebrief.Core (5 files):**
- 3 Audio interfaces
- 1 Storage interface
- 1 Playback interface

**AeroDebrief.UI (5 files):**
- 5 Visualization interfaces

### New Structure Benefits:
- ? Clear separation of contracts and implementations
- ? Domain-based organization (Audio, Storage, Playback, Visualization)
- ? Easy to find and understand all interfaces
- ? Better architectural clarity
- ? Follows .NET best practices

---

**Status**: Ready for execution  
**Estimated Time**: 1-2 hours (with namespace updates)  
**Risk Level**: Low (mostly mechanical refactoring)  
**Impact**: High (much clearer architecture)
