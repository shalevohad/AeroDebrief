# DCS SRS Recording Player Client

## Overview

The DCS SRS Recording Player Client is a modern Windows Forms application built with .NET 9 that provides comprehensive playback, analysis, and visualization capabilities for DCS SRS recorded audio files. It features a modular architecture with advanced audio processing, real-time waveform visualization, and enhanced user interface components.

## Project Structure

### Folder Organization

```
DCS-SRS-PlayerClient/
??? Infrastructure/                 # Core application infrastructure
?   ??? ServiceConfiguration.cs    # Dependency injection configuration
?   ??? ServiceContainer.cs        # Custom DI container
?   ??? MainFormFactory.cs         # Main form factory pattern
?   ??? PlaybackCommands.cs        # Command pattern for playback operations
?
??? Views/                         # User interface layers
?   ??? MainPlayerForm.cs          # Main application window
?   ??? MainPlayerForm.Designer.cs # Generated designer file
?   ??? Tabs/                      # Tab view implementations
?   ?   ??? GeneralTabView.cs      # Main playback and analysis tab
?   ?   ??? AudioTestTabView.cs    # Audio system testing tab
?   ?   ??? PlayerTabView.cs       # Alternative player tab (legacy)
?   ?   ??? FileAnalyzerTabView.cs # File analysis tab (future)
?   ??? Components/                # Reusable UI components
?       ??? PlayerComponent.cs     # Main audio player controls
?       ??? AnalyzerComponent.cs   # File analysis interface
?       ??? FileSelectionComponent.cs # File browsing and selection
?       ??? FrequencyTreeViewComponent.cs # Frequency management
?       ??? RecentFilesComponent.cs # Recent files and bookmarks
?       ??? LiveAnalysisComponent.cs # Real-time analysis display
?
??? Controls/                      # Custom Windows Forms controls
?   ??? WaveformSeekBar.cs         # Interactive waveform visualization
?   ??? FrequencyFilterControl.cs  # Frequency selection tree view
?   ??? VolumeControl.cs           # Custom volume slider
?
??? Services/                      # Business logic services
?   ??? IServices.cs               # Service interface definitions
?   ??? InfrastructureServices.cs # Core infrastructure services
?   ??? RecordingInfoService.cs    # Recording file analysis
?   ??? PlaybackService.cs         # Audio playback management
?   ??? PlayerTooltipBuilder.cs    # UI tooltip generation
?
??? Models/                        # Data models and view models
?   ??? PlayerModels.cs            # Rich data models using records
?   ??? PlayerViewModel.cs         # View model for data binding
?   ??? PlayerViewModelController.cs # View model controller
?   ??? MainPlayerPresenter.cs     # Main presenter (MVP pattern)
?
??? Extensions/                    # Extension methods and utilities
?   ??? ControlExtensions.cs       # Windows Forms control extensions
?   ??? ExtensionMethods.cs        # General utility extensions
?   ??? FrequencyModulationExtensions.cs # Audio-specific extensions
?
??? Examples/                      # Example implementations and demos
?   ??? EnhancedUIFeaturesDemo.cs  # Demo of advanced UI features
?   ??? ComponentUsageExample.cs   # Component usage examples
?
??? Program.cs                     # Application entry point
```

## Architecture

### Core Components

#### 1. **Modular Service Architecture**
- **Dependency Injection**: Uses a custom `ServiceContainer` for managing service lifecycles
- **Factory Pattern**: `MainFormFactory` creates properly configured form instances
- **Service Configuration**: Centralized service registration and initialization

#### 2. **MVP (Model-View-Presenter) Pattern**
- **Models**: Rich data models with records and immutable structures
- **Views**: Modular tab-based UI with specialized components
- **Presenters**: Business logic separation from UI concerns

#### 3. **Component-Based UI Architecture**
- **Modular Components**: Self-contained UI components with specific responsibilities
- **Tab-Based Interface**: Main functionality organized into specialized tabs
- **Custom Controls**: Enhanced Windows Forms controls for audio-specific features

### UI Architecture Overview

#### Main Application Structure

```
MainPlayerForm (Windows Form)
??? TabControl (Main navigation)
?   ??? GeneralTabView (Primary interface)
?   ?   ??? FileSelectionComponent (Top panel)
?   ?   ??? TabControl (Functionality tabs)
?   ?       ??? PlayerComponent (Audio playback)
?   ?       ??? AnalyzerComponent (File analysis)
?   ??? AudioTestTabView (System diagnostics)
??? StatusStrip (Status information)
```

#### General Tab Detailed Structure

```
GeneralTabView
??? FileSelectionComponent (Dock: Top, Height: 55px)
?   ??? File browser controls
?   ??? Recent files dropdown
?   ??? File information display
?
??? FunctionalityTabControl (Dock: Fill)
    ??? PlayerComponent Tab (Audio Player)
    ?   ??? EnhancedFeaturesPanel (Dock: Right, Width: 400px)
    ?   ?   ??? Recent Files & Bookmarks Tab
    ?   ?   ??? Live Analysis Tab
    ?   ?
    ?   ??? BottomInfoPanel (Dock: Bottom, Height: 140px)
    ?   ?   ??? PlaybackControlsPanel (Dock: Left, Width: 180px)
    ?   ?   ?   ??? Enhanced playback buttons (2 rows)
    ?   ?   ?   ??? Volume control
    ?   ?   ??? CurrentPacketInfo (Dock: Fill)
    ?   ?       ??? RichTextBox (Packet details)
    ?   ?
    ?   ??? MiddleContentPanel (Dock: Fill)
    ?       ??? FrequencyPanel (Dock: Right, Width: 350px)
    ?       ?   ??? FrequencyFilterControl
    ?       ?       ??? Coalition grouping tree
    ?       ?       ??? Player details
    ?       ?       ??? Filter controls
    ?       ?
    ?       ??? Splitter (Dock: Right, Width: 8px)
    ?       ?
    ?       ??? WaveformPanel (Dock: Fill)
    ?           ??? WaveformSeekBar
    ?               ??? Interactive waveform display
    ?               ??? Time labels (start/end/current)
    ?               ??? Zoom controls
    ?               ??? Position tracking
    ?
    ??? AnalyzerComponent Tab (File Analyzer)
        ??? Analysis controls
        ??? Results display
        ??? Export options
```

## Key Features

### ?? **Advanced Audio Playback**
- **Multi-format Support**: Native SRS recording format (.raw) and standard audio formats
- **Frequency Filtering**: Real-time filtering by selected frequencies and modulations
- **Volume Control**: Master volume with visual feedback
- **Seek Operations**: Precise waveform-based seeking with position tracking

### ?? **Real-time Waveform Visualization**
- **Interactive Waveform**: Visual representation with zoom and seek capabilities
- **Frequency-based Filtering**: Visual filtering based on selected frequencies
- **Time Labels**: Start/end times and current position display
- **Progress Indication**: Real-time playback position tracking

### ?? **Enhanced Frequency Management**
- **Hierarchical Display**: Coalition-based frequency grouping (Red, Blue, Spectator)
- **Player Information**: Individual player details under each frequency
- **Smart Selection**: Select All/None operations with tree view expansion
- **Color Coding**: Coalition-specific color schemes

### ?? **Live Audio Analysis**
- **Configurable Analysis Windows**: 10s, 30s, 1m, 2m, or 5m analysis periods
- **Multiple Chart Types**: Bar charts, pie charts, and statistics panels
- **Real-time Updates**: Live analysis during playback
- **Player Activity Tracking**: Most active players and transmission patterns

### ??? **Audio System Testing**
- **Test Tone Generation**: Verify audio output functionality
- **Device Information**: Comprehensive audio device diagnostics
- **Compatibility Testing**: Multiple audio engine testing (WASAPI, etc.)

### ?? **Enhanced File Management**
- **Recent Files**: Quick access to previously opened recordings
- **Bookmarks**: Audio position bookmarks with descriptions
- **Batch Operations**: Multi-file processing capabilities (future)

## Technical Implementation

### Service Layer

#### Core Services

```csharp
public interface IRecordingInfoService
{
    Task<RecordingFileInfo> LoadRecordingInfoAsync(string filePath);
    bool IsValidRecordingFile(string filePath);
}

public interface IAudioPlaybackService : IDisposable
{
    // Playback control
    Task StartAsync(string filePath, FrequencyFilterConfig? frequencyFilter = null);
    Task StopAsync();
    Task PauseAsync();
    Task SeekToAsync(TimeSpan position);
    
    // Audio configuration
    Task SetVolumeAsync(float volume);
    Task SetFrequencyFilterAsync(FrequencyFilterConfig config);
    
    // Events
    event EventHandler<PlaybackState>? PlaybackStateChanged;
    event EventHandler<Core.AudioPacketMetadata>? PacketStarted;
}
```

#### UI Services

```csharp
public interface IWaveformService
{
    Task LoadWaveformAsync(string filePath);
    Task ApplyFrequencyFilterAsync(FrequencyFilterConfig config);
    void UpdatePlaybackPosition(int position);
    bool IsUserSeeking { get; }
    event EventHandler<int>? SeekRequested;
}

public interface IUIService
{
    void ShowError(string message, string title = "Error");
    Task<string?> ShowOpenFileDialogAsync(string filter, string title);
    void InvokeOnUIThread(Action action);
}
```

### Data Models

#### Rich Data Models with Records

```csharp
public record RecordingFileInfo(
    string FilePath,
    TimeSpan TotalDuration,
    List<FrequencyModulationInfo> FrequencyModulations,
    RecordingStatistics Statistics
);

public record PlaybackState(
    bool IsPlaying,
    bool IsPaused,
    TimeSpan CurrentPosition,
    TimeSpan TotalDuration,
    double ProgressPercent
);

public record FrequencyFilterConfig(
    bool IsEnabled,
    List<FrequencyModulationInfo> SelectedFrequencies
);
```

### UI Components Detail

#### Core Components

##### **PlayerComponent** (`Views/Components/PlayerComponent.cs`)
- **Layout**: Multi-panel modern interface with rounded corners
- **Playback Controls**: 
  - Compact circular buttons (Play, Pause, Stop)
  - Bookmark creation (Ctrl+B)
  - Enhanced features toggle
- **Enhanced Features Panel**: 
  - Recent files and bookmarks management
  - Live analysis during playback
  - Dockable right panel (400px width)
- **Packet Information**: Real-time display of current audio packet details
- **Volume Control**: Integrated volume slider with persistence
- **Keyboard Shortcuts**: Full keyboard navigation support

##### **WaveformSeekBar** (`Controls/WaveformSeekBar.cs`)
- **Interactive Visualization**: Click/drag seeking with visual feedback
- **Time Display**: Integrated start/end times and current position
- **Frequency Filtering**: Visual representation of filtered frequencies
- **Zoom Capabilities**: Mouse wheel + Ctrl for detailed inspection
- **Position Tracking**: Real-time playback position indicator

##### **FrequencyFilterControl** (`Controls/FrequencyFilterControl.cs`)
- **Hierarchical Tree**: Coalition-based grouping (Red, Blue, Spectator)
- **Player Details**: Individual player information under frequencies
- **Smart Selection**: Select All/None with tree expansion
- **Color Coding**: Coalition-specific visual themes
- **Filter State**: Persistent enable/disable functionality

##### **FileSelectionComponent** (`Views/Components/FileSelectionComponent.cs`)
- **File Browser**: Enhanced file selection with filters
- **Recent Files**: Quick access dropdown
- **File Information**: Format validation and metadata display
- **Drag & Drop**: File dropping support (future)

##### **AnalyzerComponent** (`Views/Components/AnalyzerComponent.cs`)
- **File Analysis**: Comprehensive recording analysis
- **Statistics Display**: Packet counts, duration, frequencies
- **Export Options**: Analysis results export
- **Progress Tracking**: Long-running analysis progress

#### Custom Controls

##### **VolumeControl** (`Controls/VolumeControl.cs`)
- **Modern Slider**: Custom-drawn volume control
- **Visual Feedback**: Real-time volume indication
- **Persistence**: Automatic settings saving
- **Accessibility**: Keyboard and mouse support

##### **RecentFilesComponent** (`Views/Components/RecentFilesComponent.cs`)
- **File History**: Recently opened files management
- **Bookmarks**: Audio position bookmarks with descriptions
- **Quick Navigation**: One-click file loading and position jumping
- **Persistence**: Settings storage and retrieval

##### **LiveAnalysisComponent** (`Views/Components/LiveAnalysisComponent.cs`)
- **Real-time Statistics**: Live analysis during playback
- **Configurable Windows**: Multiple time window options (10s, 30s, 1m, 2m, 5m)
- **Multiple Charts**: Bar charts, pie charts, statistics panels
- **Activity Tracking**: Frequency, player, and modulation activity

#### Tab Views

##### **GeneralTabView** (`Views/Tabs/GeneralTabView.cs`)
- **Component Integration**: Orchestrates all main components
- **Layout Management**: Proper docking and sizing
- **Event Coordination**: Inter-component communication
- **Service Injection**: Dependency management

##### **AudioTestTabView** (`Views/Tabs/AudioTestTabView.cs`)
- **Audio Testing**: Test tone generation and playback
- **Device Information**: Audio system diagnostics
- **Compatibility Testing**: Multiple audio engine testing
- **Troubleshooting**: Audio issue diagnosis

### Visual Design System

#### Color Scheme
- **Primary Background**: Light blue theme (`Color.FromArgb(240, 248, 255)`)
- **Panel Backgrounds**: Graduated blue tones for depth
- **Control Accents**: Coalition-specific colors (Red, Blue, Green)
- **Text Colors**: High contrast for readability

#### Layout Principles
- **Responsive Design**: Components adapt to window resizing
- **Visual Hierarchy**: Clear information organization
- **Modern Styling**: Rounded corners, subtle shadows
- **Consistent Spacing**: 8px, 12px, 16px grid system

#### Interactive Elements
- **Hover Effects**: Subtle color changes and highlights
- **Focus Indicators**: Clear keyboard navigation feedback
- **Status Feedback**: Real-time status updates
- **Progressive Disclosure**: Advanced features hidden by default

## Key Classes and Responsibilities

### Infrastructure Layer

```csharp
// Service configuration and dependency injection
public static class ServiceConfiguration
{
    public static ServiceContainer ConfigureServices(MainPlayerForm mainForm)
    // Registers all application services with proper dependencies
}

// Main form factory with proper initialization
public static class MainFormFactory
{
    public static MainPlayerForm CreateMainForm()
    // Creates fully configured main form instance
}

// Custom dependency injection container
public class ServiceContainer : IDisposable
{
    // Lightweight DI container for service management
}
```

### Main Application

```csharp
// Modern program entry point with exception handling
internal static class Program
{
    static void Main() // STAThread with global exception handling
}

// Main form with modular tab architecture
public partial class MainPlayerForm : Form
{
    public async Task InitializeAsync(ServiceContainer serviceContainer)
    // Async initialization with service container
}
```

### Models and ViewModels

```csharp
// Comprehensive player models with immutable records
public record AudioBookmark(string FilePath, TimeSpan Position, string Description, DateTime Created);
public record LiveAnalysisStats(int ProcessedPackets, Dictionary<double, int> FrequencyActivity, ...);
public record WaveformData(float[] Peaks, float[] RMS, TimeSpan Duration, int SampleRate);

// Main presenter for business logic
public class MainPlayerPresenter
{
    // Coordinates between services and views
}
```

## Configuration and Settings

### Player Settings

The application uses `PlayerSettingsStore` for persisting user preferences:

- **Window Settings**: Position, size, and state
- **Audio Settings**: Default volume, audio device preferences
- **UI Settings**: Selected tab, frequency filter preferences
- **File Settings**: Recent files, last opened file path

### Keyboard Shortcuts

- **Ctrl+O**: Open file dialog
- **Spacebar**: Play/Pause toggle
- **Escape**: Stop playback
- **F5/F6/F7**: Quick function keys (context-dependent)
- **Ctrl+B**: Add bookmark at current position
- **Ctrl+E**: Toggle enhanced features panel

## Getting Started

### Installation and Launch

1. **Prerequisites**: Ensure .NET 9 Runtime is installed on your system
2. **Launch Application**: Run `DCS-SRS-PlayerClient.exe`
3. **First Launch**: Application will initialize services and create default settings

### Basic Usage

#### Opening and Playing Files

1. **File Selection**: 
   - Click "Browse" in the file selection panel (top of General tab)
   - Use Ctrl+O keyboard shortcut
   - Select from recent files dropdown
   
2. **File Loading**: 
   - Application validates the SRS recording format
   - Extracts frequency and player information
   - Loads waveform visualization data
   - Displays file statistics in status bar

3. **Basic Playback**:
   - Click the green play button (?) or press Spacebar
   - Use pause button (?) or Spacebar to pause/resume
   - Click stop button (?) or press Escape to stop
   - Click anywhere on the waveform to seek to that position

#### Frequency Filtering

1. **Frequency Tree Navigation**:
   - Right panel shows hierarchical frequency organization
   - Frequencies grouped by coalition (Red, Blue, Spectator)
   - Individual players listed under each frequency

2. **Filter Selection**:
   - Check/uncheck specific frequencies or entire coalitions
   - Use "Select All" / "Select None" buttons for quick operations
   - Enable/disable filtering with the master toggle

3. **Real-time Filtering**:
   - Changes apply immediately during playback
   - Waveform updates to show only selected frequencies
   - Audio output filters to selected transmissions only

### Advanced Features

#### Waveform Interaction

1. **Navigation**:
   - **Click**: Jump to specific time position
   - **Drag**: Scrub through audio for preview
   - **Mouse Wheel + Ctrl**: Zoom in/out at cursor position
   - **Double-click**: Zoom in at clicked position
   - **Ctrl + Double-click**: Reset zoom to fit

2. **Time Display**:
   - Start/end times shown at waveform corners
   - Current position time follows playback cursor
   - Total duration and current position in status bar

#### Enhanced Features Panel

1. **Access**: Click the enhanced features button (?) in playback controls
2. **Recent Files & Bookmarks Tab**:
   - View recently opened files
   - Create bookmarks at current position (Ctrl+B)
   - Jump to saved bookmarks
   - Manage bookmark descriptions

3. **Live Analysis Tab**:
   - Real-time statistics during playback
   - Configurable analysis window (10s, 30s, 1m, 2m, 5m)
   - Frequency activity charts
   - Player activity tracking
   - Transmission pattern analysis

#### Audio System Testing

1. **Switch to Audio Test Tab**: Second tab in main interface
2. **Test Audio Output**:
   - Generate test tones at various frequencies
   - Test different audio devices
   - Verify WASAPI compatibility
   - Diagnose audio issues

3. **Device Information**:
   - View available audio devices
   - Check driver compatibility
   - Audio format support details

### Keyboard Shortcuts Reference

#### Global Shortcuts
- **Ctrl+O**: Open file dialog
- **Spacebar**: Play/Pause toggle
- **Escape**: Stop playback
- **Ctrl+B**: Add bookmark at current position
- **Ctrl+E**: Toggle enhanced features panel

#### Playback Control
- **F5**: Quick play (context-dependent)
- **F6**: Quick pause (context-dependent)  
- **F7**: Quick stop (context-dependent)

#### Waveform Navigation
- **Left/Right Arrow**: Fine position adjustment (when waveform focused)
- **Ctrl + Mouse Wheel**: Zoom in/out
- **Double-click**: Zoom to position
- **Ctrl + Double-click**: Reset zoom

### File Format Support

#### Supported Formats
- **Primary**: SRS Recording format (.raw) - Full feature support
- **Audio Analysis**: Standard audio formats for basic analysis
- **Export**: Future support for WAV, MP3 export

#### File Requirements
- **Valid SRS Format**: Must contain proper SRS packet structure
- **Metadata**: Player, frequency, and timing information required for full features
- **Audio Data**: Valid audio payload for waveform generation

## Development

### Building the Application

```bash
# Restore dependencies
dotnet restore

# Build the application
dotnet build --configuration Release

# Run the application
dotnet run --project DCS-SRS-PlayerClient
```

### Architecture Principles

1. **Separation of Concerns**: Clear separation between UI, business logic, and data access
2. **Dependency Injection**: Services are injected rather than created directly
3. **Async/Await**: Proper async patterns for I/O operations
4. **Event-Driven**: Loose coupling through events and messaging
5. **Immutable Data**: Using records for data transfer objects
6. **Resource Management**: Proper disposal patterns for resources

### Extension Points

The application is designed for extensibility:

- **New Tab Views**: Implement `UserControl` and integrate into main form
- **Additional Services**: Register new services in `ServiceConfiguration`
- **Custom Components**: Create specialized UI components inheriting from base classes
- **Analysis Plugins**: Extend analysis capabilities through service interfaces

## Troubleshooting

### Common Issues

#### Audio Playback Problems
- **No Audio**: Use the Audio Test tab to verify system compatibility
- **Stuttering**: Check system resources and adjust buffer settings
- **Wrong Device**: Verify audio output device in system settings

#### File Loading Issues
- **Invalid Format**: Ensure file is a valid SRS recording format
- **Corrupted Data**: Use CLI analysis tools to verify file integrity
- **Missing Metadata**: Some analysis features require complete metadata

#### Performance Issues
- **Large Files**: Consider memory usage when loading very large recordings
- **Real-time Analysis**: Disable analysis features if experiencing performance issues
- **UI Responsiveness**: Ensure background operations don't block UI thread

### Logging

The application uses NLog for comprehensive logging:
- **Debug Level**: Detailed operation logging
- **Info Level**: General application flow
- **Warn Level**: Recoverable issues
- **Error Level**: Serious problems requiring attention
- **Fatal Level**: Critical errors causing application shutdown

## Integration with Recording Client

The Player Client is designed to work seamlessly with recorded files from the DCS SRS Recording Client:

1. **Compatible Formats**: Reads raw SRS recording format directly
2. **Metadata Preservation**: Maintains all player, frequency, and timing information
3. **Enhanced Playback**: Provides features not available during live recording
4. **Analysis Tools**: Offers post-recording analysis capabilities

## Future Enhancements

### Planned Features

1. **Batch Processing**: Multi-file analysis and conversion capabilities
2. **Export Functions**: Export to standard audio formats (WAV, MP3)
3. **Advanced Analysis**: Frequency spectrum analysis, communication patterns
4. **Plugin System**: Extensible architecture for custom analysis tools
5. **Network Playback**: Remote file access and streaming capabilities

### Technical Improvements

1. **Performance Optimization**: Better memory management for large files
2. **UI Enhancements**: More responsive and modern interface elements
3. **Accessibility**: Improved keyboard navigation and screen reader support
4. **Cross-Platform**: Potential .NET MAUI migration for cross-platform support

---

*For more technical details, see the source code documentation and inline comments throughout the codebase.*