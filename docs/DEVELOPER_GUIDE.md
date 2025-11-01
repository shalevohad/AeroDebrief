# AeroDebrief Developer Guide

Welcome to the AeroDebrief developer documentation. This guide will help you understand the architecture, build the project, and contribute effectively.

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Development Environment Setup](#development-environment-setup)
4. [Building the Project](#building-the-project)
5. [Project Structure](#project-structure)
6. [Core Components](#core-components)
7. [Development Workflow](#development-workflow)
8. [Testing](#testing)
9. [Extending AeroDebrief](#extending-aerodebrief)
10. [Performance Considerations](#performance-considerations)
11. [Debugging Tips](#debugging-tips)
12. [Contributing](#contributing)

## Overview

AeroDebrief is a modular .NET 9 application designed to capture, analyze, and play back voice communications from SRS (Simple Radio Standalone) servers used in DCS World multiplayer sessions.

### Key Technologies

- **.NET 9**: Modern, high-performance runtime
- **WPF**: Rich desktop UI framework for Windows
- **NAudio**: Professional audio processing library
- **OPUS Codec**: High-quality audio compression
- **Vortice.Direct3D11**: GPU compute shaders for waveform rendering
- **xUnit**: Comprehensive test framework

### Design Principles

- **Modularity**: Clear separation of concerns across projects
- **Performance**: GPU acceleration, efficient buffering, optimized algorithms
- **Extensibility**: Plugin-style integrations, event-driven architecture
- **Testability**: Comprehensive unit and integration tests
- **Maintainability**: Clean code, SOLID principles, documentation

## Architecture

AeroDebrief follows a layered architecture with clear boundaries between components.

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Presentation Layer                    │
│  ┌────────────────┐  ┌─────────────────────────────┐   │
│  │  AeroDebrief   │  │    AeroDebrief.CLI          │   │
│  │  .UI (WPF)     │  │   (Command Line)            │   │
│  └────────────────┘  └─────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
                         │
┌─────────────────────────────────────────────────────────┐
│                    Business Logic Layer                  │
│  ┌──────────────────────────────────────────────────┐  │
│  │         AeroDebrief.Core                          │  │
│  │  • Audio Recording & Playback                     │  │
│  │  • Frequency Analysis                             │  │
│  │  • Signal Processing                              │  │
│  │  • GPU-Accelerated Waveform Generation           │  │
│  └──────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
                         │
┌─────────────────────────────────────────────────────────┐
│                   Integration Layer                      │
│  ┌──────────────────────────────────────────────────┐  │
│  │      AeroDebrief.Integrations                     │  │
│  │  • TacView Export (planned)                       │  │
│  │  • DCS Lua Integration (planned)                  │  │
│  │  • External APIs                                  │  │
│  └──────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
                         │
┌─────────────────────────────────────────────────────────┐
│                    External Libraries                    │
│  ┌──────────────────────────────────────────────────┐  │
│  │  External/SRS (Simple Radio Standalone)           │  │
│  │  • Network Protocol                               │  │
│  │  • Audio Codecs                                   │  │
│  │  • Common Utilities                               │  │
│  └──────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

### Data Flow

1. **Recording Flow**:
   - SRS Server → UDP/TCP Handlers → AudioPacketRecorder → .srs File

2. **Playback Flow**:
   - .srs File → FileAnalyzer → AudioPacketReader → Audio Processing → Output

3. **Analysis Flow**:
   - AudioPacketMetadata → FrequencyAnalysisService → UI View Models → WPF Controls

For detailed architecture diagrams, see [Technical Architecture](../DOC/architecture.md).

## Development Environment Setup

### Prerequisites

1. **Operating System**: Windows 10/11 (64-bit)
   - AeroDebrief uses Windows-specific APIs (WPF, DirectX)
   - Linux/macOS development is not currently supported

2. **Development Tools**:
   - [Visual Studio 2022](https://visualstudio.microsoft.com/) or later
   - [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
   - [Git](https://git-scm.com/) for version control

3. **Optional Tools**:
   - [ReSharper](https://www.jetbrains.com/resharper/) or [Rider](https://www.jetbrains.com/rider/) for enhanced productivity
   - [PlantUML](https://plantuml.com/) for viewing/editing architecture diagrams
   - [WinDbg](https://docs.microsoft.com/en-us/windows-hardware/drivers/debugger/) for deep debugging

4. **Visual Studio Workloads**:
   - .NET desktop development
   - Desktop development with C++
   - Windows desktop development

### Initial Setup

1. **Clone the Repository**:
   ```bash
   git clone https://github.com/shalevohad/AeroDebrief.git
   cd AeroDebrief
   ```

2. **Initialize Submodules**:
   ```bash
   git submodule update --init --recursive
   ```

3. **Restore NuGet Packages**:
   ```bash
   dotnet restore
   ```

4. **Build the Solution**:
   ```bash
   dotnet build --configuration Debug
   ```

5. **Run Tests**:
   ```bash
   dotnet test
   ```

### IDE Configuration

#### Visual Studio Setup

1. Open `AeroDebrief.sln` in Visual Studio
2. Set `AeroDebrief.UI` as the startup project
3. Configure build settings:
   - Configuration: Debug
   - Platform: x64
4. Enable NuGet package restore on build
5. Configure code style:
   - Follow existing .editorconfig settings
   - Use tabs for indentation
   - 4 spaces per indent level

#### Recommended Extensions

- **ReSharper** or **CodeRush**: Code analysis and refactoring
- **Visual Studio Spell Checker**: Catch typos in comments/docs
- **GitHub Extension**: Integrated Git operations
- **XAML Designer**: Enhanced WPF development

## Building the Project

### Build Configurations

- **Debug**: Development builds with full debug symbols
- **Release**: Optimized builds for distribution

### Build Commands

```bash
# Restore dependencies
dotnet restore

# Build Debug
dotnet build --configuration Debug

# Build Release
dotnet build --configuration Release

# Clean build
dotnet clean
dotnet build --configuration Release

# Build specific project
dotnet build src/AeroDebrief.Core/AeroDebrief.Core.csproj

# Build and run
dotnet run --project src/AeroDebrief.UI
```

### Build Output

Build artifacts are placed in:
- `src/{ProjectName}/bin/{Configuration}/net9.0-windows/`

### Publishing

To create a distributable package:

```bash
# Publish self-contained (includes .NET runtime)
dotnet publish src/AeroDebrief.UI/AeroDebrief.UI.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output ./publish

# Publish framework-dependent (requires .NET 9 installed)
dotnet publish src/AeroDebrief.UI/AeroDebrief.UI.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained false `
  --output ./publish
```

## Project Structure

The solution is organized into several focused projects:

```
AeroDebrief/
├── src/
│   ├── AeroDebrief.Core/          # Core audio processing engine
│   ├── AeroDebrief.UI/            # WPF user interface
│   ├── AeroDebrief.CLI/           # Command-line interface
│   ├── AeroDebrief.DevTools/      # Development utilities
│   └── AeroDebrief.Integrations/  # External integrations
├── tests/
│   └── AeroDebrief.Tests/         # Unit and integration tests
├── External/
│   └── SRS/                       # SRS common libraries (submodule)
├── DOC/                           # Technical documentation
├── docs/                          # User and developer guides
└── TestData/                      # Sample recordings for testing
```

### Project Dependencies

```
AeroDebrief.UI → AeroDebrief.Core → External/SRS
AeroDebrief.CLI → AeroDebrief.Core → External/SRS
AeroDebrief.Integrations → AeroDebrief.Core
AeroDebrief.Tests → All Projects
```

## Core Components

### AeroDebrief.Core

The heart of the application, providing all audio processing functionality.

#### Key Classes

**AudioPacketRecorder** (`Audio/AudioPacketRecorder.cs`)
- Connects to SRS server via TCP/UDP
- Captures audio packets with metadata
- Writes to `.srs` binary format
- Thread-safe concurrent queue for disk I/O

**AudioPacketReader** (`Audio/AudioPacketReader.cs`)
- Reads `.srs` files
- Manages playback state and timeline
- Integrates with audio processing pipeline
- Supports seeking and export

**AudioProcessingEngine** (`Audio/AudioProcessingEngine.cs`)
- OPUS decoding
- Signal processing
- Volume control and effects
- Multi-frequency mixing

**FrequencyAnalysisService** (`Analysis/FrequencyAnalysisService.cs`)
- Real-time frequency analysis
- Player activity tracking
- Event-driven updates
- Thread-safe concurrent operations

**GpuWaveformGenerator** (`Audio/Waveform/GpuWaveformGenerator.cs`)
- DirectX 11 compute shader acceleration
- 10-50x faster than CPU rendering
- Automatic fallback to CPU
- Batch processing for efficiency

#### File Format (.srs)

The `.srs` format is a custom binary format:

```
Header:
  - Magic bytes: "AERO"
  - Version: uint32
  - Metadata: variable

Packets: [Repeating]
  - Timestamp: uint64 (ticks)
  - Frequency: double
  - Modulation: uint8
  - PlayerGUID: Guid (16 bytes)
  - AudioLength: uint32
  - AudioData: byte[]
  - PlayerInfo: variable (optional)
```

See `AudioPacketMetadata.cs` for detailed structure.

### AeroDebrief.UI

WPF application providing visualization and user interaction.

#### Architecture Pattern: MVVM

- **Models**: Core domain objects (from AeroDebrief.Core)
- **ViewModels**: UI state and commands (MainViewModel, FrequencyViewModel)
- **Views**: XAML and code-behind (MainWindow, custom controls)

#### Key Components

**MainViewModel** (`ViewModels/MainViewModel.cs`)
- Central UI state management
- Command bindings
- Observable collections for data binding

**FrequencyAnalysisIntegrationService** (`Services/FrequencyAnalysisIntegrationService.cs`)
- Bridge between Core and UI
- Event marshaling to UI thread
- State synchronization

**Custom Controls**:
- **WaveformViewer**: Interactive waveform display with zoom/pan
- **WaveformMiniMap**: Overview and navigation
- **FrequencyTreeView**: Hierarchical frequency display
- **FrequencyMixer**: Per-channel gain/pan controls
- **PresenceGraphView**: Network visualization

### AeroDebrief.CLI

Command-line interface for headless operations.

#### Use Cases

- Automated recording on servers
- Batch processing of recordings
- CI/CD pipeline integration
- Scripted analysis

#### Example Usage

```bash
# Record from SRS server
AeroDebrief.CLI.exe record --server 192.168.1.100 --port 5002 --output mission.srs

# Analyze recording
AeroDebrief.CLI.exe analyze --input mission.srs --report report.json

# Export audio
AeroDebrief.CLI.exe export --input mission.srs --frequency 251.0 --output audio.wav
```

## Development Workflow

### Typical Development Cycle

1. **Create Feature Branch**:
   ```bash
   git checkout -b feature/my-new-feature
   ```

2. **Implement Changes**:
   - Write code following existing patterns
   - Add/update tests
   - Document public APIs

3. **Test Locally**:
   ```bash
   dotnet test
   dotnet run --project src/AeroDebrief.UI
   ```

4. **Commit Changes**:
   ```bash
   git add .
   git commit -m "feat: add new feature"
   ```

5. **Push and Create PR**:
   ```bash
   git push origin feature/my-new-feature
   ```

### Coding Standards

- Follow existing code style (use .editorconfig)
- Use meaningful variable/method names
- Add XML documentation comments for public APIs
- Keep methods focused and single-purpose
- Prefer composition over inheritance
- Use dependency injection where appropriate

### Git Workflow

- **Branch Naming**:
  - `feature/description` for new features
  - `fix/description` for bug fixes
  - `docs/description` for documentation
  - `refactor/description` for refactoring

- **Commit Messages**:
  - Follow [Conventional Commits](https://www.conventionalcommits.org/)
  - Examples: `feat:`, `fix:`, `docs:`, `refactor:`, `test:`

## Testing

### Test Organization

Tests are located in `tests/AeroDebrief.Tests/`:

```
AeroDebrief.Tests/
├── Unit/              # Unit tests for individual components
├── Integration/       # Integration tests for workflows
├── Performance/       # Performance benchmarks
├── Playback/          # Playback-specific tests
└── TestData/          # Sample .srs files for testing
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/AeroDebrief.Tests

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run tests matching filter
dotnet test --filter "FullyQualifiedName~Playback"

# Run tests in parallel
dotnet test --parallel
```

### Writing Tests

```csharp
using Xunit;
using FluentAssertions;

public class AudioPacketReaderTests
{
    [Fact]
    public void LoadFile_ValidFile_ShouldSucceed()
    {
        // Arrange
        var reader = new AudioPacketReader();
        var filePath = "test.srs";

        // Act
        var result = reader.LoadFile(filePath);

        // Assert
        result.Should().BeTrue();
        reader.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void LoadFile_InvalidPath_ShouldThrow(string path)
    {
        // Arrange
        var reader = new AudioPacketReader();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => reader.LoadFile(path));
    }
}
```

### Test Best Practices

- **AAA Pattern**: Arrange, Act, Assert
- **One Assert Per Test**: Focus on single behavior
- **Descriptive Names**: Test names should describe the scenario
- **Use FluentAssertions**: More readable assertions
- **Mock External Dependencies**: Use Moq for isolation
- **Test Edge Cases**: Null, empty, boundary values

## Extending AeroDebrief

### Adding a New Integration

1. **Create Integration Class** in `AeroDebrief.Integrations`:
   ```csharp
   public class TacViewExporter
   {
       public void Export(string srsFilePath, string outputPath)
       {
           // Implementation
       }
   }
   ```

2. **Add Tests** in `AeroDebrief.Tests/Integration/`:
   ```csharp
   public class TacViewExporterTests
   {
       [Fact]
       public void Export_ValidFile_CreatesOutput()
       {
           // Test implementation
       }
   }
   ```

3. **Document** in `DOC/integrations.md`

### Adding a New Analytics View

1. **Create ViewModel**:
   ```csharp
   public class MyAnalyticsViewModel : INotifyPropertyChanged
   {
       // Properties and commands
   }
   ```

2. **Create View (XAML)**:
   ```xml
   <UserControl x:Class="AeroDebrief.UI.Views.MyAnalyticsView">
       <!-- UI elements -->
   </UserControl>
   ```

3. **Wire up in MainViewModel**:
   ```csharp
   public MyAnalyticsViewModel MyAnalytics { get; }
   ```

4. **Add to UI** in MainWindow.xaml

### Adding GPU Compute Shaders

1. **Create HLSL Shader** in `AeroDebrief.Core/Shaders/`:
   ```hlsl
   [numthreads(256, 1, 1)]
   void CSMain(uint3 id : SV_DispatchThreadID)
   {
       // Compute shader code
   }
   ```

2. **Compile Shader** (Visual Studio does this automatically)

3. **Load and Execute** in C#:
   ```csharp
   var shader = LoadComputeShader("MyShader.cso");
   context.Dispatch(threadGroupsX, 1, 1);
   ```

## Performance Considerations

### Critical Performance Areas

1. **Audio Processing**:
   - Use buffering to smooth processing
   - Decode OPUS frames efficiently
   - Minimize allocations in hot paths

2. **Waveform Rendering**:
   - GPU acceleration for large datasets
   - Batch processing
   - LOD (Level of Detail) for zoom levels

3. **File I/O**:
   - Async operations for disk access
   - Buffer reads/writes
   - Memory-mapped files for large recordings

4. **UI Responsiveness**:
   - Dispatch work to background threads
   - Use Task.Run for CPU-intensive operations
   - Throttle UI updates (e.g., 60 FPS max)

### Profiling Tools

- **Visual Studio Profiler**: CPU and memory profiling
- **dotTrace**: JetBrains profiling tool
- **PerfView**: Performance analysis
- **GPU Profiling**: RenderDoc, PIX

### Optimization Tips

- Avoid LINQ in hot paths
- Use `Span<T>` and `Memory<T>` for performance-critical code
- Pool large objects to reduce GC pressure
- Cache computed values when appropriate
- Profile before optimizing

## Debugging Tips

### Common Debugging Scenarios

**Audio Issues**:
- Use NAudio's diagnostic logging
- Check audio device selection
- Verify sample rates match
- Inspect audio buffer states

**File Format Issues**:
- Validate magic bytes and version
- Check for corruption with hex editor
- Verify packet boundaries
- Use FileAnalyzer to inspect structure

**GPU Issues**:
- Check for DirectX errors in debug output
- Verify shader compilation
- Inspect GPU resources with PIX
- Test CPU fallback path

**Threading Issues**:
- Use ThreadSanitizer or similar tools
- Add logging to trace thread execution
- Check for race conditions
- Verify thread-safe patterns

### Debug Logging

```csharp
// Add diagnostic logging
#if DEBUG
System.Diagnostics.Debug.WriteLine($"Processing packet: {metadata.Timestamp}");
#endif
```

### Visual Studio Debugger

- **Breakpoints**: Set conditional breakpoints in hot paths
- **Watch Windows**: Monitor variable states
- **Call Stack**: Trace execution flow
- **Threads Window**: Debug concurrent operations
- **Memory Windows**: Inspect raw memory

## Contributing

For contribution guidelines, see [CONTRIBUTING.md](CONTRIBUTING.md).

### Quick Contribution Checklist

- [ ] Code follows existing style and conventions
- [ ] All tests pass (`dotnet test`)
- [ ] New features have tests
- [ ] Public APIs have XML documentation
- [ ] Breaking changes are documented
- [ ] Commit messages follow conventional commits
- [ ] PR description explains the change

## Additional Resources

### Documentation

- [Technical Architecture](../DOC/architecture.md) - Detailed architecture diagrams
- [Core Audio Systems](../DOC/core-audio.md) - Audio processing details
- [UI Integration](../DOC/ui.md) - UI architecture and patterns
- [Analysis Systems](../DOC/analysis.md) - Frequency analysis implementation
- [DevOps Guide](../DOC/devops.md) - Build and CI/CD

### External Resources

- [.NET 9 Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [WPF Documentation](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
- [NAudio Documentation](https://github.com/naudio/NAudio)
- [Vortice.Direct3D11 Documentation](https://github.com/amerkoleci/Vortice.Windows)
- [OPUS Codec](https://opus-codec.org/)
- [SRS Documentation](https://github.com/ciribob/DCS-SimpleRadioStandalone)

### Community

- [GitHub Issues](https://github.com/shalevohad/AeroDebrief/issues) - Bug reports and feature requests
- [GitHub Discussions](https://github.com/shalevohad/AeroDebrief/discussions) - Community discussions
- [Pull Requests](https://github.com/shalevohad/AeroDebrief/pulls) - Code contributions

---

**Happy Coding!**  
Thank you for contributing to AeroDebrief!
