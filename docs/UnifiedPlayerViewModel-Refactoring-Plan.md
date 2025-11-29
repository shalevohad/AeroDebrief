# UnifiedPlayerViewModel Refactoring Plan - Option B (Composition)

## ?? Goal
Split the 1,473-line `UnifiedPlayerViewModel` into focused, maintainable components using **composition pattern** where Recording composes Playback, avoiding code duplication while maintaining clear separation of concerns.

---

## ?? Current State Analysis

### Problems:
1. **Too Long**: 1,473 lines (hard to navigate)
2. **Mixed Concerns**: Recording + Playback in one class
3. **Complex State**: Multiple modes with different behaviors
4. **Hard to Test**: Tightly coupled responsibilities
5. **Difficult to Extend**: Adding features touches too many areas

### Current Responsibilities:
- ? Server connection management
- ? File loading and playback
- ? Live recording with dual playheads
- ? Frequency analysis
- ? Mixer channel control
- ? Graph visualization
- ? Tacview integration
- ? Command routing

### ? Key Insight: Recording = Playback + Live Enhancements

**Recording Mode is NOT separate from Playback - it's an enhanced version that adds:**
1. Server connection (SRS)
2. Real-time packet capture
3. Live monitoring with dual playheads
4. Ability to scrub through past recordings while recording continues

---

## ??? Selected Architecture: Option B (Composition)

### Composition Structure:

```
???????????????????????????????????????????????????????????
?          UnifiedPlayerViewModel (Orchestrator)           ?
?                                                          ?
?  - Mode switching (Idle/Recording/Playback)             ?
?  - Service initialization & coordination                ?
?  - Event routing to active session                      ?
?  - Shared UI state                                      ?
?                                                          ?
?  Size: ~400 lines                                       ?
???????????????????????????????????????????????????????????
                        ?
        ?????????????????????????????????
        ?                               ?
????????????????????         ?????????????????????????
? PlaybackSession  ?         ?  RecordingSession     ?
?  Manager         ???????????   Manager             ?
?                  ? Composes?                       ?
? File loading     ?         ? All Playback features ?
? Frequency mgmt   ?         ?      +                ?
? Standard playback?         ? Server connection     ?
? Tacview integ    ?         ? Live monitoring       ?
?                  ?         ? Dual playheads        ?
? ~600 lines       ?         ? Live audio stream     ?
????????????????????         ?                       ?
                             ? ~500 lines            ?
                             ?????????????????????????
```

**Key Principle**: RecordingSessionManager **wraps** PlaybackSessionManager and delegates most operations to it, adding live recording features on top.

---

## ?? File Structure

```
src/AeroDebrief.UI/
??? ViewModels/
?   ??? UnifiedPlayerViewModel.cs          (Orchestrator - 400 lines)
?   ?   ?? Mode management
?   ?   ?? Service initialization
?   ?   ?? Event routing
?   ?   ?? Command delegation
?   ?
?   ??? Player/
?       ??? IPlayerSession.cs              (Interface - 50 lines)
?       ?   ?? Common contract for both sessions
?       ?
?       ??? PlaybackSessionManager.cs      (File playback - 600 lines)
?       ?   ?? File loading (CVR/DuckDB)
?       ?   ?? PlaybackSessionManager service
?       ?   ?? Frequency analysis
?       ?   ?? Standard playback controls
?       ?   ?? Tacview integration
?       ?   ?? Graph synchronization
?       ?
?       ??? RecordingSessionManager.cs     (Live recording - 500 lines)
?           ?? **COMPOSES** PlaybackSessionManager
?           ?? Server connection (SRS)
?           ?? Live packet recording
?           ?? Dual playhead tracking
?           ?? Live frequency detection
?           ?? Real-time audio streaming
?           ?? "Go Live" functionality
?
??? Services/
    ??? LivePlaybackManager.cs            (Existing)
    ??? LiveAudioPlaybackService.cs       (Existing)
    ??? LiveRecordingPlaybackPipeline.cs  (Existing)
```

**Total**: ~1,550 lines (slightly more, but well organized!)

---

## ?? Component Design

### 1. **IPlayerSession.cs** (Common Interface)

**Purpose**: Defines the contract that both session managers must implement.

**Size**: ~50 lines

```csharp
namespace AeroDebrief.UI.ViewModels.Player
{
    /// <summary>
    /// Common interface for player sessions (Playback and Recording).
    /// </summary>
    public interface IPlayerSession : IDisposable
    {
        #region State Properties
        PlaybackState PlaybackState { get; }
        TimeSpan CurrentPosition { get; }
        TimeSpan TotalDuration { get; }
        string StatusMessage { get; }
        bool IsBuffering { get; }
        string CurrentSourceName { get; }
        #endregion

        #region Collections
        ObservableCollection<FrequencyGroupViewModel> Frequencies { get; }
        ObservableCollection<MixerChannelViewModel> MixerChannels { get; }
        #endregion

        #region Lifecycle
        Task ActivateAsync();
        Task DeactivateAsync();
        #endregion

        #region Playback Control
        Task PlayAsync();
        Task PauseAsync();
        Task StopAsync();
        Task SeekAsync(TimeSpan position);
        #endregion

        #region Commands
        ICommand PlayCommand { get; }
        ICommand PauseCommand { get; }
        ICommand StopCommand { get; }
        ICommand SeekCommand { get; }
        #endregion

        #region Events
        event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;
        event EventHandler<TimeSpan>? PositionChanged;
        event EventHandler<string>? StatusMessageChanged;
        #endregion
    }
}
```

---

### 2. **PlaybackSessionManager.cs** (File Playback)

**Purpose**: Manages file-based playback sessions (CVR/DuckDB files). Can be used standalone OR composed by RecordingSessionManager.

**Size**: ~600 lines

**Key Features**:
- File loading and format detection
- Frequency analysis
- Standard playback controls (Play/Pause/Stop/Seek)
- Tacview integration
- Graph synchronization
- Mixer integration

**Composition Point**: This class is designed to be wrapped by RecordingSessionManager.

```csharp
public class PlaybackSessionManager : ViewModelBase, IPlayerSession
{
    // Services
    private readonly Core.Services.PlaybackSessionManager _coreSessionManager;
    private readonly FrequencyManager _frequencyManager;
    private readonly MixerController _mixerController;
    private readonly UnifiedGraphViewModel _graphViewModel;
    
    // All file playback logic from current UnifiedPlayerViewModel
    // ...
}
```

---

### 3. **RecordingSessionManager.cs** (Live Recording - Composes Playback)

**Purpose**: Manages live recording sessions. **COMPOSES** PlaybackSessionManager to inherit all file playback capabilities, then adds live recording features.

**Size**: ~500 lines

**Key Architecture Decision**: Uses composition, NOT inheritance
```csharp
public class RecordingSessionManager : ViewModelBase, IPlayerSession
{
    // ? COMPOSITION (The Key!)
    private readonly PlaybackSessionManager _playbackSession;
    
    // Recording-specific services
    private readonly ServerSourceViewModel _serverSource;
    private readonly LivePlaybackManager _livePlaybackManager;
    private readonly LiveAudioPlaybackService _liveAudioService;
    
    // Recording-specific state
    private TimeSpan _recordingPosition;  // Static playhead (red)
    private TimeSpan _playbackPosition;   // Dynamic playhead (blue)
    
    // Delegate most operations to composed playback session
    public PlaybackState PlaybackState => _playbackSession.PlaybackState;
    public TimeSpan CurrentPosition => _playbackSession.CurrentPosition;
    public Task PlayAsync() => _playbackSession.PlayAsync();
    public Task SeekAsync(TimeSpan pos) => _playbackSession.SeekAsync(pos);
    // ... etc
    
    // Add recording-specific features
    public async Task StartLivePlaybackAsync(string liveDbPath)
    {
        // Start live monitoring
        await _livePlaybackManager.StartLivePlaybackAsync(liveDbPath);
        
        // Load temp DB into playback session (enables scrubbing!)
        await _playbackSession.LoadFileAsync(liveDbPath);
    }
}
```

**Unique Properties**:
```csharp
// Recording-specific state
public bool IsLiveRecording { get; set; }
public TimeSpan RecordingPosition { get; set; }      // Static playhead (red)
public TimeSpan PlaybackPosition { get; set; }       // Dynamic playhead (blue)
public double RecordingPositionNormalized { get; }
public double PlaybackPositionNormalized { get; }

// Server connection
public string ServerIp { get; set; }
public int ServerPort { get; set; }
public bool IsConnected { get; set; }
```

**Unique Commands**:
```csharp
public ICommand ConnectCommand { get; }
public ICommand DisconnectCommand { get; }
public ICommand StartRecordingCommand { get; }
public ICommand StopRecordingCommand { get; }
public ICommand PlayLiveRecordingCommand { get; }
public ICommand PauseLiveRecordingCommand { get; }
public ICommand SeekLiveRecordingCommand { get; }
public ICommand GoToLivePositionCommand { get; }
```

---

### 4. **UnifiedPlayerViewModel.cs** (Simplified Orchestrator)

**Purpose**: Coordinates mode switching and delegates to appropriate session manager.

**Size**: ~400 lines (down from 1,473!)

**Responsibilities**:
- Mode switching (Idle/Playback/Recording)
- Session initialization
- Event routing
- Command delegation
- Dispose coordination

```csharp
public class UnifiedPlayerViewModel : ViewModelBase, IDisposable
{
    // Session managers
    private readonly PlaybackSessionManager _playbackSession;
    private readonly RecordingSessionManager _recordingSession;
    private IPlayerSession? _activeSession;
    
    // Shared services
    private readonly FrequencyManager _frequencyManager;
    private readonly MixerController _mixerController;
    private readonly UnifiedGraphViewModel _graphViewModel;
    
    public UnifiedPlayerViewModel()
    {
        // Initialize shared services
        _frequencyManager = new FrequencyManager();
        _mixerController = new MixerController();
        _graphViewModel = new UnifiedGraphViewModel(...);

        // Initialize session managers
        _playbackSession = new PlaybackSessionManager(
            _frequencyManager, _mixerController, _graphViewModel);

        // ? Recording COMPOSES playback!
        _recordingSession = new RecordingSessionManager(
            _playbackSession,  // Pass playback to recording!
            _frequencyManager);
    }
    
    public async void SwitchToRecordingMode()
    {
        await _activeSession?.DeactivateAsync();
        _activeSession = _recordingSession;
        await _recordingSession.ActivateAsync();
        CurrentMode = PlayerMode.Recording;
    }
    
    // Delegate method calls to active session
    public void HandleFrequencySelectionChanged(double freq, bool selected)
        => (_activeSession as PlaybackSessionManager)?.HandleFrequencySelectionChanged(freq, selected);
}
```

---

## ?? Composition Flow

### Key Insight: Recording **Wraps** Playback

```
????????????????????????????????????????
?   RecordingSessionManager            ?
?                                      ?
?  ?????????????????????????????????? ?
?  ?  PlaybackSessionManager        ? ?
?  ?                                ? ?
?  ?  - File loading                ? ?
?  ?  - Frequency analysis          ? ?
?  ?  - Playback controls           ? ?
?  ?  - Mixer integration           ? ?
?  ?  - Graph sync                  ? ?
?  ?????????????????????????????????? ?
?              ?                       ?
?              ? Delegates to          ?
?              ?                       ?
?  + Server connection                ?
?  + Live monitoring                  ?
?  + Dual playheads                   ?
?  + Real-time audio                  ?
????????????????????????????????????????
```

### When Recording Starts:

1. **Connect to SRS** ? RecordingSessionManager handles
2. **Start Recording** ? RecordingSessionManager handles
3. **Create Temp DuckDB** ? RecordingSessionManager handles
4. **Load Temp DB** ? ? Delegates to `_playbackSession.LoadFileAsync()`
5. **Analyze Frequencies** ? ? PlaybackSessionManager handles
6. **Setup Mixer** ? ? PlaybackSessionManager handles
7. **Enable Dual Playheads** ? RecordingSessionManager adds on top
8. **User Scrubs Back** ? ? Delegates to `_playbackSession.SeekAsync()`
9. **User Clicks "Go Live"** ? RecordingSessionManager handles

---

## ?? Migration Checklist

### Phase 1: Create Interface & Playback Session
- [ ] Create `IPlayerSession.cs` interface
- [ ] Create `PlaybackSessionManager.cs`
- [ ] Move all file playback code from `UnifiedPlayerViewModel`
- [ ] Move file loading logic
- [ ] Move frequency analysis
- [ ] Move Tacview integration
- [ ] Move standard playback controls
- [ ] Test playback session independently
- [ ] Verify all file playback features work

### Phase 2: Create Recording Session (with Composition)
- [ ] Create `RecordingSessionManager.cs`
- [ ] **COMPOSE** `PlaybackSessionManager` inside (key step!)
- [ ] Move server connection code
- [ ] Move live playback manager
- [ ] Move dual playhead logic
- [ ] Move live event handlers
- [ ] Delegate file operations to composed playback session
- [ ] Test recording session independently
- [ ] Verify composition works (recording uses playback internally)

### Phase 3: Simplify Orchestrator
- [ ] Refactor `UnifiedPlayerViewModel` to orchestrator
- [ ] Initialize both session managers (playback first, then recording with playback)
- [ ] Implement mode switching (Idle/Playback/Recording)
- [ ] Delegate method calls to active session
- [ ] Remove moved code
- [ ] Test mode switching
- [ ] Verify no regressions

### Phase 4: Update UI
- [ ] Update `UnifiedPlayerControl.xaml` bindings
- [ ] Bind to `PlaybackSession` for file playback mode
- [ ] Bind to `RecordingSession` for live recording mode
- [ ] Create `PlaybackSessionPanel.xaml` (optional)
- [ ] Create `RecordingSessionPanel.xaml` (optional)
- [ ] Test UI with new architecture
- [ ] Verify all features work in both modes

### Phase 5: Cleanup & Polish
- [ ] Remove dead code
- [ ] Update documentation
- [ ] Run full test suite
- [ ] Verify performance (should be same or better)
- [ ] Code review
- [ ] Merge to main branch

---

## ? Benefits of Composition Approach

### 1. **No Code Duplication**
- Recording doesn't reimplement file loading - it uses `_playbackSession.LoadFileAsync()`
- All frequency analysis code is in one place
- Mixer integration is shared automatically
- Tacview integration is reused

### 2. **Clear Responsibilities**
- `PlaybackSessionManager`: "How to load and play a file"
- `RecordingSessionManager`: "How to record from SRS + play the result"
- `UnifiedPlayerViewModel`: "Which mode are we in?"

### 3. **Easy Testing**
```csharp
[Test]
public async Task RecordingSession_DelegatesToPlayback()
{
    var mockPlayback = new Mock<PlaybackSessionManager>();
    var recording = new RecordingSessionManager(mockPlayback.Object, ...);
    
    await recording.SeekAsync(TimeSpan.FromSeconds(30));
    
    mockPlayback.Verify(p => p.SeekAsync(TimeSpan.FromSeconds(30)), Times.Once);
}
```

### 4. **Flexible Extension**
Want to add a new mode? Just implement `IPlayerSession` and compose existing sessions!

```csharp
public class NetworkStreamingSessionManager : IPlayerSession
{
    private readonly PlaybackSessionManager _playback;  // Reuse!
    private readonly NetworkStreamSource _network;       // New!
    // ... implementation ...
}
```

### 5. **Natural Architecture**
- Recording is literally Playback + Live features
- No artificial base class
- No deep inheritance hierarchies
- Clear composition relationships

---

## ?? Data Flow Examples

### Example 1: User Loads File in Playback Mode

```
1. User clicks "Browse File"
   ?
2. UnifiedPlayerViewModel.ActiveSession = PlaybackSession
   ?
3. PlaybackSession.LoadFileAsync(path)
   ?
4. Analyze frequencies
   ?
5. Setup mixer
   ?
6. Ready to play
```

### Example 2: User Starts Recording

```
1. User clicks "Connect to Server"
   ?
2. UnifiedPlayerViewModel.ActiveSession = RecordingSession
   ?
3. RecordingSession.ConnectToServerAsync()
   ?
4. User clicks "Start Recording"
   ?
5. RecordingSession starts packet capture
   ?
6. Create temp DuckDB
   ?
7. RecordingSession.StartLivePlaybackAsync(tempDbPath)
   ?
8. ? _playbackSession.LoadFileAsync(tempDbPath)  (Composition!)
   ?
9. PlaybackSession analyzes frequencies
   ?
10. RecordingSession adds dual playheads
    ?
11. User can now scrub + listen to live audio
```

### Example 3: User Scrubs During Recording

```
1. User drags scrubber to 30s
   ?
2. RecordingSession.SeekAsync(30s)
   ?
3. ? _playbackSession.SeekAsync(30s)  (Delegation!)
   ?
4. Playback pipeline seeks to 30s
   ?
5. PlaybackPosition updated (blue playhead)
   ?
6. RecordingPosition stays at current (red playhead)
```

---

## ?? UI Binding Strategy

### XAML Data Context:

```xml
<!-- Main control -->
<UserControl DataContext="{Binding UnifiedPlayerViewModel}">
    
    <!-- Mode-specific panels -->
    <Grid>
        <!-- Playback Mode UI -->
        <local:PlaybackSessionPanel 
            DataContext="{Binding PlaybackSession}"
            Visibility="{Binding IsPlaybackMode, Converter={StaticResource BoolToVisibilityConverter}}"/>
        
        <!-- Recording Mode UI -->
        <local:RecordingSessionPanel 
            DataContext="{Binding RecordingSession}"
            Visibility="{Binding IsRecordingMode, Converter={StaticResource BoolToVisibilityConverter}}"/>
        
        <!-- Shared graph (always visible) -->
        <local:UnifiedGraphControl 
            DataContext="{Binding GraphViewModel}"/>
    </Grid>
</UserControl>
```

**Key Point**: Recording panel can access both recording-specific AND playback features through composition!

---

## ?? Potential Challenges & Solutions

### 1. **Shared State Management**
**Problem**: CurrentPosition, TotalDuration shared between modes

**Solution**: Recording delegates to Playback's state properties:
```csharp
public TimeSpan CurrentPosition => _playbackSession.CurrentPosition;
public TimeSpan TotalDuration => _playbackSession.TotalDuration;
```

### 2. **Event Routing**
**Problem**: FrequencyManager events need to reach correct session

**Solution**: Orchestrator subscribes once, delegates to active session

### 3. **Ownership of PlaybackSession**
**Problem**: Who owns PlaybackSessionManager - orchestrator or recording?

**Solution**: Orchestrator creates both and passes playback to recording:
```csharp
_playbackSession = new PlaybackSessionManager(...);
_recordingSession = new RecordingSessionManager(_playbackSession, ...);
```
Recording doesn't dispose playback - orchestrator does.

### 4. **Dual Playheads in Recording**
**Problem**: Recording needs its own position tracking + playback's position

**Solution**: Recording adds RecordingPosition property, delegates CurrentPosition to playback:
```csharp
public TimeSpan RecordingPosition { get; set; }  // Static (red)
public TimeSpan CurrentPosition => _playbackSession.CurrentPosition;  // Dynamic (blue)
```

### 5. **Testing Complexity**
**Problem**: Need to test composition works correctly

**Solution**: 
- Unit test PlaybackSession in isolation
- Unit test RecordingSession with mocked PlaybackSession
- Integration test full orchestrator with both sessions

---

## ?? Before/After Comparison

| Metric | Before | After (Option B) |
|--------|--------|------------------|
| **Lines per file** | 1,473 | 400 / 600 / 500 / 50 |
| **Number of files** | 1 | 4 |
| **Code duplication** | High (recording reimplements playback) | **None** (composition) |
| **Responsibilities per class** | 8+ | 2-3 |
| **Test complexity** | High (tightly coupled) | **Low** (isolated with composition) |
| **Merge conflicts** | Frequent (one file) | Rare (separate files) |
| **Extensibility** | Difficult | **Easy** (compose existing) |
| **Onboarding time** | Hours (complex) | Minutes (clear structure) |

---

## ?? Implementation Order

### Step 1: Create Interface (30 min)
Create `IPlayerSession.cs` with common contract

### Step 2: Extract Playback (4-6 hours)
Create `PlaybackSessionManager.cs` and move all file playback code

### Step 3: Create Recording with Composition (3-4 hours)
Create `RecordingSessionManager.cs` that composes PlaybackSessionManager

### Step 4: Simplify Orchestrator (2-3 hours)
Refactor `UnifiedPlayerViewModel` to orchestrator

### Step 5: Update UI (2-3 hours)
Update XAML bindings for new architecture

### Step 6: Test & Polish (2-4 hours)
Full testing, documentation, code review

**Total Estimated Time**: 1-2 days

---

## ?? Why Option B is Superior

### vs Option A (Keep as-is):
? Much better maintainability  
? Easier to test  
? Clear separation of concerns  

### vs Option C (Extract live features only):
? Cleaner architecture (composition > extraction)  
? Recording naturally composes playback  
? More flexible for future extensions  
? Better encapsulation  

### vs Full Split (Option from original plan):
? No code duplication (recording uses playback)  
? Simpler than maintaining two parallel implementations  
? Natural relationship (recording IS-A-KIND-OF playback)  

---

## ?? Next Steps

**Ready to start?**

1. ? **Create `IPlayerSession.cs`** - Common interface
2. ? **Extract `PlaybackSessionManager.cs`** - File playback
3. ? **Create `RecordingSessionManager.cs`** - Compose playback + add recording
4. ? **Simplify `UnifiedPlayerViewModel.cs`** - Orchestrator
5. ? **Update UI** - Bind to sessions
6. ? **Test Everything** - Verify composition works

---

**Status**: ?? Plan Updated with Option B (Composition)  
**Selected Approach**: Composition Pattern  
**Timeline**: 1-2 days  
**Risk**: Low (clear separation, testable)  
**Benefit**: High (maintainable, extensible, no duplication)

---

**Author**: GitHub Copilot  
**Date**: 2025-01-18  
**Updated**: 2025-01-18 (Option B Selected)  
**Status**: ? Ready for Implementation
