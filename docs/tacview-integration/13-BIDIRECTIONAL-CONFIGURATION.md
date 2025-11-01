# Bidirectional Configuration - Feature Specification

## Overview

This document describes the **bidirectional configuration** feature that allows users to modify Tacview integration settings from either Tacview's menu or AeroDebrief's UI, with changes automatically synchronized between both applications.

**Status**: ?? Design Complete  
**Version**: 1.0  
**Related**: Phase 3, Phase 5 of Implementation Guide

---

## Feature Summary

### What is Bidirectional Configuration?

**Traditional Flow** (Tacview ? AeroDebrief only):
```
User configures in Tacview ? Tacview broadcasts ? AeroDebrief applies
```

**Bidirectional Flow** (Both directions):
```
Configuration Source 1: Tacview Menu
?? User configures in Tacview
?? Tacview broadcasts to AeroDebrief
?? AeroDebrief applies and displays

Configuration Source 2: AeroDebrief UI
?? User configures in AeroDebrief
?? AeroDebrief sends to Tacview
?? Tacview applies to internal state
?? Tacview broadcasts confirmation
?? AeroDebrief receives and confirms
```

### Supported Configurations

| Configuration | Tacview ? AeroDebrief | AeroDebrief ? Tacview |
|---------------|----------------------|----------------------|
| **Pan Mode** (Auto/Manual) | ? Primary | ? Secondary |
| **Per-Pilot Pan Values** | ? Primary | ? Secondary |
| **Per-Pilot Frequencies** | ? Primary | ? Secondary |
| **General Frequencies** | ? Primary | ? Secondary |
| **TCP Port** | ? Config file only | ? Config file only |
| **Update Rate** | ? Tacview only | ? N/A |

---

## User Experience

### Scenario 1: Configure in AeroDebrief

**Steps**:
1. User opens AeroDebrief with Tacview connected
2. User expands "Spatial Audio (Pan)" section
3. User switches from Auto to Manual mode
4. User drags pan slider for "Viper 1-1" to -0.5
5. AeroDebrief sends `pan_configuration` message to Tacview
6. Tacview updates internal state
7. Tacview broadcasts updated `pilot_selection`
8. AeroDebrief confirms change (UI doesn't flicker)
9. User opens Tacview menu ? sees "Manual" mode and -0.5 pan

**Result**: ? Change made in AeroDebrief is reflected in Tacview

---

### Scenario 2: Configure in Tacview

**Steps**:
1. User opens Tacview menu ? AeroDebrief Sync ? Configure Audio Pan
2. User sets "Viper 1-1" pan to 0.8
3. Tacview broadcasts `pilot_selection` with new pan
4. AeroDebrief receives message
5. AeroDebrief updates UI (pan slider moves to 0.8)
6. Audio output applies new pan immediately

**Result**: ? Change made in Tacview is reflected in AeroDebrief (existing behavior)

---

### Scenario 3: Rapid Changes (Slider Drag)

**Problem**: Slider generates many events while dragging

**Solution**: Debouncing

**Steps**:
1. User drags pan slider in AeroDebrief from 0.0 to -0.8
2. Slider generates 20+ value change events
3. Debounce timer (500ms) prevents sending each change
4. User releases slider
5. Timer expires ? Send single `pan_configuration` message
6. Tacview receives one update instead of 20+

**Result**: ? Network efficient, no spam

---

### Scenario 4: Conflicting Changes

**Problem**: User changes setting in both UIs simultaneously

**Example**:
- User A sets pan to -0.5 in AeroDebrief
- User B sets pan to 0.8 in Tacview
- Both happen within 100ms

**Resolution**: Last Write Wins + Confirmation Loop

**Steps**:
1. AeroDebrief sends `pan_configuration` with -0.5
2. Tacview sends `pilot_selection` with 0.8 (before receiving -0.5)
3. AeroDebrief receives 0.8 ? Updates UI to 0.8
4. Tacview receives -0.5 ? Updates to -0.5
5. Tacview broadcasts `pilot_selection` with -0.5
6. AeroDebrief receives -0.5 ? Updates UI to -0.5

**Result**: ? Final state: -0.5 (last write wins)

---

## Technical Implementation

### New Message Types

#### 1. frequency_filter_update

**AeroDebrief ? Tacview**

**Purpose**: Update enabled frequencies for a pilot or general frequencies

**Format**:
```json
{
  "type": "frequency_filter_update",
  "pilot_id": "F-16C-001-GUID",  // null for general
  "enabled_frequencies": [251.0, 305.0]
}
```

**Tacview Handler** (`main.lua`):
```lua
if message.type == "frequency_filter_update" then
    if message.pilot_id then
        -- Update per-pilot frequencies
        for _, freq in ipairs(message.enabled_frequencies) do
            panManager.SetPilotFrequencyEnabled(message.pilot_id, freq, true)
        end
        -- Disable frequencies not in list
        local allFreqs = panManager.GetAllFrequencies()
        for _, freq in ipairs(allFreqs) do
            if not contains(message.enabled_frequencies, freq) then
                panManager.SetPilotFrequencyEnabled(message.pilot_id, freq, false)
            end
        end
    else
        -- Update general frequencies
        for _, freq in ipairs(message.enabled_frequencies) do
            panManager.SetGeneralFrequencyEnabled(freq, true)
        end
        local allFreqs = panManager.GetAllFrequencies()
        for _, freq in ipairs(allFreqs) do
            if not contains(message.enabled_frequencies, freq) then
                panManager.SetGeneralFrequencyEnabled(freq, false)
            end
        end
    end
    
    -- Broadcast updated state
    self:OnSelectionChange()
end
```

---

#### 2. pan_configuration

**AeroDebrief ? Tacview**

**Purpose**: Update pan mode and per-pilot pan values

**Format**:
```json
{
  "type": "pan_configuration",
  "pan_mode": "manual",
  "pilot_pan_settings": {
    "F-16C-001-GUID": -0.8,
    "F-16C-002-GUID": 0.8
  }
}
```

**Tacview Handler** (`main.lua`):
```lua
if message.type == "pan_configuration" then
    -- Update pan mode
    panManager.SetMode(message.pan_mode or "auto")
    
    -- Update per-pilot settings (if manual mode)
    if message.pilot_pan_settings then
        for pilotId, panValue in pairs(message.pilot_pan_settings) do
            panManager.SetPilotPan(pilotId, panValue)
        end
    end
    
    -- Save configuration
    config.Save()
    
    -- Broadcast updated state
    self:OnSelectionChange()
    
    Tacview.Log.Info(string.format("Pan config updated from AeroDebrief: %s mode", message.pan_mode))
end
```

---

### AeroDebrief UI Changes

#### View Model Commands

**File**: `TacviewIntegrationViewModel.cs`

```csharp
// NEW: Commands for configuration updates
public ICommand UpdatePilotFrequenciesCommand { get; }
public ICommand UpdateGeneralFrequenciesCommand { get; }
public ICommand UpdatePanConfigurationCommand { get; }
public ICommand ToggleFrequencyCommand { get; }

private async void UpdatePilotFrequencies(TacviewPilotViewModel pilot)
{
    var message = new FrequencyFilterUpdateMessage
    {
        PilotId = pilot.PilotId,
        EnabledFrequencies = pilot.EnabledFrequencies.ToList()
    };
    
    await _integrationService.SendMessageAsync(message);
}

private async void UpdateGeneralFrequencies()
{
    var message = new FrequencyFilterUpdateMessage
    {
        PilotId = null,
        EnabledFrequencies = GeneralEnabledFrequencies.ToList()
    };
    
    await _integrationService.SendMessageAsync(message);
}

private async void UpdatePanConfiguration()
{
    var pilotPanSettings = new Dictionary<string, double>();
    foreach (var pilot in SelectedPilots)
    {
        pilotPanSettings[pilot.PilotId] = pilot.Pan;
    }
    
    var message = new PanConfigurationMessage
    {
        PanMode = PanMode,
        PilotPanSettings = pilotPanSettings
    };
    
    await _integrationService.SendMessageAsync(message);
}
```

---

#### UI Controls

**Frequency Checkboxes**:
```xaml
<CheckBox Content="{Binding Display}"
         IsChecked="{Binding IsEnabled, Mode=TwoWay}"
         Command="{Binding DataContext.ToggleFrequencyCommand, 
                         RelativeSource={RelativeSource AncestorType=UserControl}}"
         CommandParameter="{Binding}"/>
```

**Pan Slider** (with debouncing):
```xaml
<Slider Value="{Binding Pan, Mode=TwoWay}"
       Minimum="-1" Maximum="1"
       ValueChanged="OnPanSliderValueChanged"/>
```

**Code-Behind**:
```csharp
private Timer _panUpdateTimer;

private void OnPanSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    _panUpdateTimer?.Stop();
    _panUpdateTimer = new Timer(500);
    _panUpdateTimer.Elapsed += (s, args) => 
    {
        Dispatcher.Invoke(() => ViewModel.UpdatePanConfigurationCommand.Execute(null));
        _panUpdateTimer?.Stop();
    };
    _panUpdateTimer.Start();
}
```

---

### Tacview Lua Changes

#### Message Handler

**File**: `main.lua`

```lua
function AeroDebriefSync:OnInitialize()
    -- ... existing code ...
    
    -- Set message handler for incoming messages
    tcpServer.SetMessageHandler(function(message)
        self:OnMessageReceived(message)
    end)
end

function AeroDebriefSync:OnMessageReceived(message)
    local decoded = protocol.Decode(message)
    
    if decoded.type == "frequency_filter_update" then
        self:HandleFrequencyFilterUpdate(decoded)
    elseif decoded.type == "pan_configuration" then
        self:HandlePanConfiguration(decoded)
    elseif decoded.type == "speaking_status" then
        self:HandleSpeakingStatus(decoded)
    end
end

function AeroDebriefSync:HandleFrequencyFilterUpdate(message)
    if message.pilot_id then
        -- Per-pilot update
        local enabledSet = {}
        for _, freq in ipairs(message.enabled_frequencies or {}) do
            enabledSet[freq] = true
        end
        
        local allFreqs = panManager.GetAllFrequencies()
        for _, freq in ipairs(allFreqs) do
            panManager.SetPilotFrequencyEnabled(message.pilot_id, freq, enabledSet[freq] == true)
        end
        
        Tacview.Log.Info(string.format("Updated frequencies for pilot %s from AeroDebrief", message.pilot_id))
    else
        -- General update
        local enabledSet = {}
        for _, freq in ipairs(message.enabled_frequencies or {}) do
            enabledSet[freq] = true
        end
        
        local allFreqs = panManager.GetAllFrequencies()
        for _, freq in ipairs(allFreqs) do
            panManager.SetGeneralFrequencyEnabled(freq, enabledSet[freq] == true)
        end
        
        Tacview.Log.Info("Updated general frequencies from AeroDebrief")
    end
    
    -- Broadcast updated state
    self:OnSelectionChange()
end

function AeroDebriefSync:HandlePanConfiguration(message)
    panManager.SetMode(message.pan_mode or "auto")
    
    if message.pilot_pan_settings then
        for pilotId, panValue in pairs(message.pilot_pan_settings) do
            panManager.SetPilotPan(pilotId, panValue)
        end
    end
    
    config.Save()
    self:OnSelectionChange()
    
    Tacview.Log.Info(string.format("Pan configuration updated from AeroDebrief: %s mode", message.pan_mode))
end
```

---

## Testing Strategy

### Unit Tests

#### C# Side

**Test**: Send configuration update message
```csharp
[Fact]
public async Task ViewModel_SendsPanConfigurationMessage()
{
    var viewModel = CreateViewModel();
    var mockService = new Mock<ITacviewIntegrationService>();
    
    viewModel.PanMode = "manual";
    viewModel.SelectedPilots[0].Pan = -0.5;
    
    await viewModel.UpdatePanConfigurationCommand.ExecuteAsync(null);
    
    mockService.Verify(s => s.SendMessageAsync(
        It.Is<PanConfigurationMessage>(m => 
            m.PanMode == "manual" && 
            m.PilotPanSettings["Test-1"] == -0.5)), 
        Times.Once);
}
```

**Test**: Debouncing works
```csharp
[Fact]
public async Task PanSlider_DebouncesRapidChanges()
{
    var viewModel = CreateViewModel();
    var mockService = new Mock<ITacviewIntegrationService>();
    
    // Simulate rapid slider drag (10 changes in 100ms)
    for (int i = 0; i < 10; i++)
    {
        viewModel.SelectedPilots[0].Pan = i * 0.1;
        await Task.Delay(10);
    }
    
    // Wait for debounce timer
    await Task.Delay(600);
    
    // Should only send one message
    mockService.Verify(s => s.SendMessageAsync(It.IsAny<PanConfigurationMessage>()), Times.Once);
}
```

---

#### Lua Side

**Test**: Handle frequency filter update
```lua
function TestFrequencyFilterUpdate()
    local message = {
        type = "frequency_filter_update",
        pilot_id = "Test-1",
        enabled_frequencies = {251.0, 305.0}
    }
    
    addon:OnMessageReceived(protocol.Encode(message))
    
    assert(panManager.IsPilotFrequencyEnabled("Test-1", 251.0) == true)
    assert(panManager.IsPilotFrequencyEnabled("Test-1", 305.0) == true)
    assert(panManager.IsPilotFrequencyEnabled("Test-1", 133.0) == false)
end
```

**Test**: Handle pan configuration update
```lua
function TestPanConfigurationUpdate()
    local message = {
        type = "pan_configuration",
        pan_mode = "manual",
        pilot_pan_settings = {
            ["Test-1"] = -0.8,
            ["Test-2"] = 0.8
        }
    }
    
    addon:OnMessageReceived(protocol.Encode(message))
    
    assert(panManager.GetMode() == "manual")
    assert(panManager.GetPilotPan("Test-1") == -0.8)
    assert(panManager.GetPilotPan("Test-2") == 0.8)
end
```

---

### Integration Tests

**Test**: End-to-end configuration flow
```csharp
[Fact]
public async Task EndToEnd_ConfigurationRoundTrip()
{
    // Setup
    var mockTacviewServer = new MockTacviewServer(52001);
    await mockTacviewServer.StartAsync();
    
    var integrationService = CreateIntegrationService();
    await integrationService.StartAsync();
    
    var viewModel = new TacviewIntegrationViewModel(integrationService);
    
    // Act: Change pan in AeroDebrief
    viewModel.SelectedPilots[0].Pan = -0.5;
    await viewModel.UpdatePanConfigurationCommand.ExecuteAsync(null);
    
    // Assert: Tacview received message
    await Task.Delay(100);
    var receivedMessage = mockTacviewServer.GetLastReceivedMessage<PanConfigurationMessage>();
    Assert.NotNull(receivedMessage);
    Assert.Equal(-0.5, receivedMessage.PilotPanSettings["Test-1"]);
    
    // Act: Tacview broadcasts confirmation
    mockTacviewServer.BroadcastPilotSelection(new[] 
    {
        new TacviewPilot { PilotId = "Test-1", Pan = -0.5 }
    });
    
    // Assert: AeroDebrief confirms
    await Task.Delay(100);
    Assert.Equal(-0.5, viewModel.SelectedPilots[0].Pan);
}
```

---

### Manual Testing Checklist

**Frequency Filtering**:
- [ ] Toggle frequency checkbox in AeroDebrief ? Reflects in Tacview
- [ ] Toggle frequency in Tacview menu ? Reflects in AeroDebrief
- [ ] Enable All / Disable All in AeroDebrief ? Works in Tacview
- [ ] Enable All / Disable All in Tacview ? Works in AeroDebrief
- [ ] General frequencies work bidirectionally

**Pan Configuration**:
- [ ] Switch Auto/Manual in AeroDebrief ? Reflects in Tacview
- [ ] Switch Auto/Manual in Tacview ? Reflects in AeroDebrief
- [ ] Drag pan slider in AeroDebrief ? Reflects in Tacview
- [ ] Set pan in Tacview menu ? Reflects in AeroDebrief
- [ ] Rapid slider drag doesn't spam messages

**Conflict Resolution**:
- [ ] Simultaneous changes ? Last write wins
- [ ] UI doesn't flicker during confirmation
- [ ] No message loops or infinite updates

---

## Performance Considerations

### Network Overhead

**Without Debouncing**:
- Slider drag: 20+ messages per second
- Checkbox spam: 10+ messages per second

**With Debouncing**:
- Slider drag: 1-2 messages per drag operation
- Checkbox: 1 message per toggle

**Throttling Strategy**:
```csharp
// Maximum 10 configuration updates per second
private readonly RateLimiter _configUpdateLimiter = new(maxPerSecond: 10);

private async void UpdateConfiguration()
{
    if (!_configUpdateLimiter.TryAcquire())
    {
        Logger.Warn("Configuration update rate limited");
        return;
    }
    
    await SendConfigurationMessage();
}
```

---

### CPU/Memory Impact

**Negligible**: Configuration updates are rare (user-initiated only)

**Measurements**:
- Message size: ~100-500 bytes
- Frequency: <10 per second (debounced)
- CPU overhead: <0.1%
- Memory overhead: <1 MB

---

## Security Considerations

### Validation

**Tacview Side** (Lua):
```lua
function ValidateFrequencyFilterUpdate(message)
    if message.pilot_id and type(message.pilot_id) ~= "string" then
        return false
    end
    
    if not message.enabled_frequencies or type(message.enabled_frequencies) ~= "table" then
        return false
    end
    
    for _, freq in ipairs(message.enabled_frequencies) do
        if type(freq) ~= "number" or freq < 0 or freq > 400 then
            return false
        end
    end
    
    return true
end
```

**AeroDebrief Side** (C#):
```csharp
private bool ValidatePanConfiguration(PanConfigurationMessage message)
{
    if (message.PanMode != "auto" && message.PanMode != "manual")
        return false;
    
    if (message.PilotPanSettings != null)
    {
        foreach (var (pilotId, panValue) in message.PilotPanSettings)
        {
            if (panValue < -1.0 || panValue > 1.0)
                return false;
        }
    }
    
    return true;
}
```

---

## Future Enhancements

### Configuration Presets

**Feature**: Save/load configuration profiles

**Example**:
- "Combat Focus" preset: Only combat frequencies, manual pan
- "Full Context" preset: All frequencies, auto pan
- "Tower Only" preset: Tower frequencies only

**UI**:
```
Presets: [Combat Focus ?]
         [Save Current As...]
         [Delete Preset]
```

---

### Configuration History

**Feature**: Undo/redo configuration changes

**Implementation**:
```csharp
private Stack<ConfigurationSnapshot> _undoStack = new();
private Stack<ConfigurationSnapshot> _redoStack = new();

public void UndoConfiguration()
{
    if (_undoStack.Count == 0) return;
    
    var previous = _undoStack.Pop();
    _redoStack.Push(CurrentConfiguration);
    
    ApplyConfiguration(previous);
    SendConfigurationUpdate(previous);
}
```

---

### Smart Conflict Resolution

**Feature**: Detect and resolve conflicts intelligently

**Strategy**:
- Track modification timestamps
- Merge non-conflicting changes
- Prompt user for conflicting changes

**Example**:
```
User A changed pan for Pilot 1
User B changed frequencies for Pilot 2
? Merge both changes (no conflict)

User A changed pan for Pilot 1 to -0.5
User B changed pan for Pilot 1 to 0.8
? Prompt: "Pilot 1 pan was changed by another user. Keep yours (-0.5) or use theirs (0.8)?"
```

---

## Summary

### Benefits

? **Flexibility**: Configure from either UI  
? **Convenience**: No need to switch between apps  
? **Consistency**: Always synchronized  
? **Performance**: Debounced and throttled  
? **Reliability**: Confirmation loop prevents desync

### Implementation Checklist

- [ ] Add new message types to protocol
- [ ] Implement Tacview message handlers
- [ ] Add AeroDebrief UI controls
- [ ] Implement debouncing/throttling
- [ ] Add validation on both sides
- [ ] Write unit tests
- [ ] Write integration tests
- [ ] Manual testing
- [ ] Documentation
- [ ] User guide

---

**Feature Status**: ? Design Complete  
**Ready for**: Implementation (Phase 3 & 5)  
**Estimated Effort**: +2-3 days to base implementation  
**Complexity**: Medium  
**Risk**: Low (graceful fallback if not supported)

---

**Document Version**: 1.0  
**Date**: 2024-01-XX  
**Author**: AeroDebrief Team
