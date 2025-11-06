# Testing Strategy - Tacview Integration

## Overview

Comprehensive testing strategy for the Tacview integration, covering unit tests, integration tests, manual tests, and long-duration stability tests.

## Test Pyramid

```
                    ?
                   / \
                  /   \
                 /  E2E \
                /  Manual \
               /???????????\
              / Integration \
             /   Tests (30)  \
            /?????????????????\
           /   Unit Tests (50) \
          /?????????????????????\
         /_______________________\
```

## 1. Unit Tests

### 1.1 Protocol Tests

**File**: `AeroDebrief.Integrations.Tests/Protocol/TacviewProtocolTests.cs`

```csharp
[TestClass]
public class TacviewProtocolTests
{
    [TestMethod]
    public void Serialize_TimeUpdateMessage_ProducesValidJson()
    {
        var message = new TimeUpdateMessage
        {
            MissionTimeUtc = new DateTime(2024, 1, 15, 14, 30, 45, DateTimeKind.Utc),
            PlaybackState = "playing",
            PlaybackSpeed = 1.0
        };
        
        var json = TacviewProtocol.Serialize(message);
        
        Assert.IsNotNull(json);
        Assert.IsTrue(json.Contains("time_update"));
        Assert.IsTrue(json.Contains("2024-01-15T14:30:45"));
    }
    
    [TestMethod]
    public void Deserialize_ValidTimeUpdate_ParsesCorrectly()
    {
        var json = @"{""type"":""time_update"",""mission_time_utc"":""2024-01-15T14:30:45Z"",""playback_state"":""playing"",""playback_speed"":1.0}";
        
        var message = TacviewProtocol.Deserialize(json) as TimeUpdateMessage;
        
        Assert.IsNotNull(message);
        Assert.AreEqual("playing", message.PlaybackState);
        Assert.AreEqual(1.0, message.PlaybackSpeed);
    }
    
    [TestMethod]
    public void Deserialize_InvalidJson_ReturnsNull()
    {
        var json = "{invalid json";
        
        var message = TacviewProtocol.Deserialize(json);
        
        Assert.IsNull(message);
    }
    
    [TestMethod]
    public void Serialize_PilotSelection_IncludesAllPilots()
    {
        var message = new PilotSelectionMessage
        {
            SelectedPilots = new List<TacviewPilot>
            {
                new TacviewPilot
                {
                    PilotId = "pilot-1",
                    PilotName = "Viper 1-1",
                    Frequencies = new[] { 251.0, 305.0 },
                    Pan = -0.8
                }
            }
        };
        
        var json = TacviewProtocol.Serialize(message);
        
        Assert.IsTrue(json.Contains("pilot-1"));
        Assert.IsTrue(json.Contains("Viper 1-1"));
        Assert.IsTrue(json.Contains("251.0"));
    }
}
```

---

### 1.2 Time Conversion Tests

**File**: `AeroDebrief.Integrations.Tests/Sync/TimeConverterTests.cs`

```csharp
[TestClass]
public class TimeConverterTests
{
    [TestMethod]
    public void ToTimeSpan_ValidUtcTime_ReturnsCorrectOffset()
    {
        var converter = new TimeConverter();
        var recordingStart = new DateTime(2024, 1, 15, 14, 0, 0, DateTimeKind.Utc);
        var recordingEnd = recordingStart.AddHours(2);
        
        converter.SetRecordingBounds(recordingStart, recordingEnd);
        
        var utcTime = recordingStart.AddMinutes(30);
        var offset = converter.ToTimeSpan(utcTime);
        
        Assert.AreEqual(TimeSpan.FromMinutes(30), offset);
    }
    
    [TestMethod]
    public void ToTimeSpan_BeforeRecordingStart_ReturnsZero()
    {
        var converter = new TimeConverter();
        var recordingStart = new DateTime(2024, 1, 15, 14, 0, 0, DateTimeKind.Utc);
        
        converter.SetRecordingBounds(recordingStart, recordingStart.AddHours(2));
        
        var utcTime = recordingStart.AddMinutes(-10); // Before start
        var offset = converter.ToTimeSpan(utcTime);
        
        Assert.AreEqual(TimeSpan.Zero, offset);
    }
    
    [TestMethod]
    public void ToUtcTime_ValidOffset_ReturnsCorrectUtc()
    {
        var converter = new TimeConverter();
        var recordingStart = new DateTime(2024, 1, 15, 14, 0, 0, DateTimeKind.Utc);
        
        converter.SetRecordingBounds(recordingStart, recordingStart.AddHours(2));
        
        var offset = TimeSpan.FromMinutes(45);
        var utcTime = converter.ToUtcTime(offset);
        
        Assert.AreEqual(recordingStart.AddMinutes(45), utcTime);
    }
    
    [TestMethod]
    public void IsTimeInRange_InsideRange_ReturnsTrue()
    {
        var converter = new TimeConverter();
        var recordingStart = new DateTime(2024, 1, 15, 14, 0, 0, DateTimeKind.Utc);
        var recordingEnd = recordingStart.AddHours(2);
        
        converter.SetRecordingBounds(recordingStart, recordingEnd);
        
        var utcTime = recordingStart.AddMinutes(30);
        
        Assert.IsTrue(converter.IsTimeInRange(utcTime));
    }
}
```

---

### 1.3 Sync Algorithm Tests

**File**: `AeroDebrief.Integrations.Tests/Sync/SyncAlgorithmTests.cs`

```csharp
[TestClass]
public class SyncAlgorithmTests
{
    [TestMethod]
    public void CalculateSyncAction_SmallDrift_ReturnsNone()
    {
        var algorithm = new SyncAlgorithm();
        var tacviewTime = DateTime.UtcNow;
        var aeroDebriefTime = tacviewTime.AddMilliseconds(50); // 50ms drift
        
        var action = algorithm.CalculateSyncAction(tacviewTime, aeroDebriefTime);
        
        Assert.AreEqual(SyncActionType.None, action.Type);
    }
    
    [TestMethod]
    public void CalculateSyncAction_MediumDrift_ReturnsSpeedAdjust()
    {
        var algorithm = new SyncAlgorithm();
        var tacviewTime = DateTime.UtcNow;
        var aeroDebriefTime = tacviewTime.AddMilliseconds(200); // 200ms drift
        
        var action = algorithm.CalculateSyncAction(tacviewTime, aeroDebriefTime);
        
        Assert.AreEqual(SyncActionType.SpeedAdjust, action.Type);
        Assert.IsTrue(action.SpeedMultiplier < 1.0); // Slow down to catch up
    }
    
    [TestMethod]
    public void CalculateSyncAction_LargeDrift_ReturnsSeek()
    {
        var algorithm = new SyncAlgorithm();
        var tacviewTime = DateTime.UtcNow;
        var aeroDebriefTime = tacviewTime.AddSeconds(2); // 2s drift
        
        var action = algorithm.CalculateSyncAction(tacviewTime, aeroDebriefTime);
        
        Assert.AreEqual(SyncActionType.Seek, action.Type);
        Assert.AreEqual(tacviewTime, action.SeekTarget);
    }
    
    [TestMethod]
    public void CalculateSpeedAdjustment_PositiveDrift_ReturnsSlowdown()
    {
        var algorithm = new SyncAlgorithm();
        var drift = TimeSpan.FromMilliseconds(300); // Ahead by 300ms
        
        var speed = algorithm.CalculateSpeedAdjustment(drift);
        
        Assert.IsTrue(speed < 1.0); // Should slow down
        Assert.IsTrue(speed >= 0.98); // Within limits
    }
}
```

---

### 1.4 Pilot Mapper Tests

**File**: `AeroDebrief.Integrations.Tests/Pilot/PilotMapperTests.cs`

```csharp
[TestClass]
public class PilotMapperTests
{
    [TestMethod]
    public void ShouldPlayPacket_NoPilotsSelected_ReturnsTrue()
    {
        var mapper = new PilotMapper();
        var packet = CreateTestPacket("pilot-1", 251.0);
        
        var result = mapper.ShouldPlayPacket(packet);
        
        Assert.IsTrue(result); // No filter = play all
    }
    
    [TestMethod]
    public void ShouldPlayPacket_MatchingPilotAndFrequency_ReturnsTrue()
    {
        var mapper = new PilotMapper();
        mapper.UpdateSelection(new[]
        {
            new TacviewPilot
            {
                PilotId = "pilot-1",
                Frequencies = new[] { 251.0, 305.0 }
            }
        });
        
        var packet = CreateTestPacket("pilot-1", 251.0);
        
        Assert.IsTrue(mapper.ShouldPlayPacket(packet));
    }
    
    [TestMethod]
    public void ShouldPlayPacket_WrongFrequency_ReturnsFalse()
    {
        var mapper = new PilotMapper();
        mapper.UpdateSelection(new[]
        {
            new TacviewPilot
            {
                PilotId = "pilot-1",
                Frequencies = new[] { 251.0 }
            }
        });
        
        var packet = CreateTestPacket("pilot-1", 305.0); // Different frequency
        
        Assert.IsFalse(mapper.ShouldPlayPacket(packet));
    }
    
    [TestMethod]
    public void GetPanForPilot_ValidPilot_ReturnsCorrectPan()
    {
        var mapper = new PilotMapper();
        mapper.UpdateSelection(new[]
        {
            new TacviewPilot
            {
                PilotId = "pilot-1",
                Pan = -0.8
            }
        });
        
        var pan = mapper.GetPanForPilot("pilot-1");
        
        Assert.AreEqual(-0.8, pan);
    }
    
    private AudioPacketMetadata CreateTestPacket(string pilotId, double frequency)
    {
        return new AudioPacketMetadata(
            DateTime.UtcNow,
            frequency,
            0, 0, 0, 0,
            pilotId,
            new PlayerInfo { TransmitterGuid = pilotId },
            48000, 1, 0,
            Array.Empty<byte>()
        );
    }
}
```

---

## 2. Integration Tests

### 2.1 TCP Communication Tests

**File**: `AeroDebrief.Integrations.Tests/Client/TacviewClientTests.cs`

```csharp
[TestClass]
public class TacviewClientTests
{
    private MockTcpServer _mockServer;
    
    [TestInitialize]
    public void Setup()
    {
        _mockServer = new MockTcpServer(52001);
        _mockServer.Start();
    }
    
    [TestCleanup]
    public void Cleanup()
    {
        _mockServer.Stop();
    }
    
    [TestMethod]
    public async Task Connect_ValidServer_EstablishesConnection()
    {
        var client = new TacviewClient();
        
        var connected = await client.ConnectAsync("127.0.0.1", 52001, CancellationToken.None);
        
        Assert.IsTrue(connected);
        Assert.IsTrue(client.IsConnected);
    }
    
    [TestMethod]
    public async Task SendMessage_ValidMessage_ReceivedByServer()
    {
        var client = new TacviewClient();
        await client.ConnectAsync("127.0.0.1", 52001, CancellationToken.None);
        
        var message = new ReadyMessage { Version = "1.0.0" };
        await client.SendMessageAsync(message);
        
        var received = await _mockServer.WaitForMessageAsync(TimeSpan.FromSeconds(1));
        Assert.IsNotNull(received);
        Assert.IsTrue(received.Contains("ready"));
    }
    
    [TestMethod]
    public async Task ReceiveMessage_ServerSends_RaisesEvent()
    {
        var client = new TacviewClient();
        await client.ConnectAsync("127.0.0.1", 52001, CancellationToken.None);
        
        var messageReceived = false;
        client.MessageReceived += (s, m) => messageReceived = true;
        
        await _mockServer.SendMessageAsync(@"{""type"":""time_update""}");
        await Task.Delay(100);
        
        Assert.IsTrue(messageReceived);
    }
}
```

---

### 2.2 End-to-End Sync Tests

**File**: `AeroDebrief.Integrations.Tests/E2E/SyncE2ETests.cs`

```csharp
[TestClass]
public class SyncE2ETests
{
    [TestMethod]
    public async Task FullSync_FromConnectionToPlayback_WorksCorrectly()
    {
        // Arrange
        var mockServer = new MockTcpServer(52001);
        mockServer.Start();
        
        var syncService = new TacviewSyncService();
        var config = new TacviewConfiguration
        {
            Host = "127.0.0.1",
            Port = 52001
        };
        
        // Act
        await syncService.StartAsync(config);
        
        // Simulate Tacview sending messages
        await mockServer.SendTimeUpdateAsync(DateTime.UtcNow, "playing", 1.0);
        await Task.Delay(200);
        
        // Assert
        Assert.IsTrue(syncService.IsConnected);
        Assert.AreEqual(ConnectionState.Synchronized, syncService.State);
        
        // Cleanup
        await syncService.StopAsync();
        mockServer.Stop();
    }
}
```

---

## 3. Manual Tests

### 3.1 Real Tacview Connection

**Test Case**: Connect to real Tacview addon

**Steps**:
1. Install Tacview addon in `%APPDATA%\Tacview\AddOns\`
2. Start Tacview and load a mission
3. Start AeroDebrief with integration enabled
4. Verify connection indicator turns green
5. Select aircraft in Tacview
6. Verify pilot list updates in AeroDebrief
7. Play mission in Tacview
8. Verify audio plays synchronized

**Expected Results**:
- ? Connection established within 1 second
- ? Pilot selection appears in <100ms
- ? Audio synchronized within ±1 second
- ? No audio dropouts or stuttering

---

### 3.2 Pilot Selection & Filtering

**Test Case**: Verify pilot filtering works

**Steps**:
1. Load recording with 4+ pilots on multiple frequencies
2. Connect to Tacview
3. Select 2 pilots in Tacview
4. Verify only those pilots' audio plays
5. Select different pilot
6. Verify audio switches immediately
7. Deselect all pilots
8. Verify all audio plays

**Expected Results**:
- ? Only selected pilots audible
- ? Filter switches in <100ms
- ? All frequencies for selected pilots included
- ? No selected pilots = hear all

---

### 3.3 Spatial Audio (Pan)

**Test Case**: Verify spatial audio positioning

**Steps**:
1. Connect to Tacview with 3 pilots selected
2. Verify pan indicators show: ???, ??, ???
3. Play audio
4. Verify pilot 1 in left ear
5. Verify pilot 2 in both ears (center)
6. Verify pilot 3 in right ear
7. Change selection order
8. Verify pan adjusts dynamically

**Expected Results**:
- ? Distinct left/right positioning
- ? Center pilot audible in both ears
- ? Clear spatial separation
- ? Pan updates immediately on selection change

---

### 3.4 Time Synchronization

**Test Case**: Verify time sync accuracy

**Steps**:
1. Load 30-minute recording
2. Connect to Tacview
3. Play mission
4. Monitor drift display
5. Seek to different times in Tacview
6. Verify AeroDebrief follows immediately
7. Pause/resume in Tacview
8. Verify AeroDebrief syncs

**Expected Results**:
- ? Drift stays <500ms during normal playback
- ? Seek completes in <200ms
- ? Pause/resume works instantly
- ? No gradual drift over time

---

### 3.5 Speaking Indicators

**Test Case**: Verify visual feedback in Tacview

**Steps**:
1. Connect to Tacview
2. Play audio
3. Watch for transmissions
4. Verify speaking pilot highlighted in Tacview
5. Verify highlight disappears after transmission
6. Test with multiple simultaneous transmissions

**Expected Results**:
- ? Pilot aircraft changes color when speaking
- ? Highlight appears within 100ms of audio
- ? Highlight removed when transmission ends
- ? Multiple pilots can be highlighted simultaneously

---

## 4. Long-Duration Tests

### 4.1 Stability Test (2 Hours)

**Test Case**: Verify 2+ hour stability

**Setup**:
- 2-hour recording
- 6 pilots
- Multiple frequencies
- All features enabled

**Test Duration**: 2 hours real-time

**Metrics to Track**:
| Metric | Target | Actual |
|--------|--------|--------|
| Average Drift | <500ms | |
| Max Drift | <2s | |
| Connection Uptime | 99.5% | |
| Message Success Rate | 99% | |
| CPU Usage | <10% | |
| Memory Leak | 0 MB/hour | |

**Monitoring**:
- Log drift every 10 seconds
- Track connection state changes
- Monitor memory usage
- Record any errors

**Pass Criteria**:
- ? No disconnections
- ? Drift stays within target
- ? No memory leaks
- ? No audio glitches

---

### 4.2 Reconnection Test

**Test Case**: Verify automatic reconnection

**Steps**:
1. Start playback with Tacview connected
2. Kill Tacview addon (simulate crash)
3. Verify AeroDebrief detects disconnection
4. Restart Tacview addon
5. Verify AeroDebrief reconnects automatically
6. Verify playback continues correctly
7. Repeat 5 times

**Expected Results**:
- ? Disconnection detected within 5 seconds
- ? Reconnection within 10 seconds
- ? State restored correctly
- ? No audio interruption

---

### 4.3 Stress Test (High Message Rate)

**Test Case**: Verify handling of high message rate

**Setup**:
- Simulate Tacview sending 100 messages/second
- Run for 10 minutes
- Monitor performance

**Metrics**:
- Message processing latency
- CPU usage
- Memory usage
- Dropped messages

**Pass Criteria**:
- ? <10ms message processing
- ? <15% CPU usage
- ? No memory leaks
- ? <1% message drop rate

---

## 5. Performance Benchmarks

### 5.1 Latency Benchmarks

| Operation | Target | Method |
|-----------|--------|--------|
| TCP message send | <1ms | Stopwatch around send |
| TCP message receive | <5ms | Timestamp on arrival |
| JSON serialization | <1ms | Benchmark serialize |
| JSON deserialization | <2ms | Benchmark deserialize |
| Time sync check | <0.1ms | Benchmark sync check |
| Pilot filter check | <0.05ms | Benchmark filter |
| Pan calculation | <0.01ms | Benchmark pan |

### 5.2 Memory Benchmarks

| Component | Target | Method |
|-----------|--------|--------|
| TacviewClient | <1 MB | Memory profiler |
| TacviewSyncService | <500 KB | Memory profiler |
| Message queue | <100 KB | Track queue size |
| Total overhead | <2 MB | Total integration |

---

## 6. Test Automation

### 6.1 CI/CD Pipeline

```yaml
# .github/workflows/integration-tests.yml
name: Integration Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: windows-latest
    
    steps:
      - uses: actions/checkout@v2
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v1
        with:
          dotnet-version: 9.0.x
      
      - name: Restore dependencies
        run: dotnet restore
      
      - name: Build
        run: dotnet build --no-restore
      
      - name: Run unit tests
        run: dotnet test --filter Category=Unit
      
      - name: Run integration tests
        run: dotnet test --filter Category=Integration
      
      - name: Upload test results
        uses: actions/upload-artifact@v2
        with:
          name: test-results
          path: TestResults/
```

### 6.2 Nightly Tests

- Run full test suite nightly
- Include long-duration tests
- Email results to team
- Track metrics over time

---

## 7. Test Coverage Goals

| Component | Target Coverage |
|-----------|-----------------|
| Protocol | 95% |
| Time Conversion | 100% |
| Sync Algorithm | 95% |
| Pilot Mapper | 90% |
| TCP Client | 80% |
| Integration | 70% |
| **Overall** | **85%** |

---

## 8. Test Documentation

### 8.1 Test Report Template

```markdown
# Test Report - Tacview Integration

## Test Session
- Date: YYYY-MM-DD
- Tester: Name
- Version: 1.0.0
- Duration: X hours

## Test Results
- Total Tests: X
- Passed: X
- Failed: X
- Skipped: X

## Failed Tests
1. Test Name
   - Expected: ...
   - Actual: ...
   - Logs: ...

## Performance Metrics
- Average Drift: X ms
- Max Drift: X ms
- CPU Usage: X%
- Memory: X MB

## Notes
...
```

---

**Document Version**: 1.0  
**Last Updated**: 2024-01-XX  
**Status**: ?? Planning
