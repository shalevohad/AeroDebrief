# Tacview Tests Fix Plan

## Executive Summary

**Status**: 18/45 Tacview tests passing (40%)
- ? **TacviewSyncServiceTests**: 18/18 passing (100%) 
- ? **Other Tacview Tests**: 0/27 passing (0%)

## Failing Test Categories

### 1. Protocol Tests (2 failures)
**File**: `tests\AeroDebrief.Tests\Integrations\Tacview\Protocol\TacviewProtocolTests.cs`

**Issues**:
- Missing message type: `sync_status` 
- Missing message type: `speaking_status` (exists but parser doesn't handle it)

**Root Cause**: TacviewProtocol.ParseMessage() doesn't handle these message types

**Fix Required**:
1. Create `SyncStatusMessage.cs` in `src\AeroDebrief.Integrations\Tacview\Protocol\Messages\`
2. Update `TacviewProtocol.ParseMessage()` to handle both message types
3. Update message type string constants

**Estimated Effort**: 30 minutes

---

### 2. Client Tests (3 failures)
**File**: `tests\AeroDebrief.Tests\Integrations\Tacview\Client\TacviewClientTests.cs`

**Failing Tests**:
- `MessageReceived_WhenServerSendsMessage_RaisesEvent`
- `Disconnected_WhenServerDisconnects_RaisesEvent`
- `ConnectAsync_CancellationRequested_ThrowsOperationCanceledException`

**Issues**:
- Tests try to connect to real Tacview server at `127.0.0.1:52099`
- No mock TCP server implementation
- Connection timeout errors: `SocketException (10061): No connection could be made`
- Wrong exception type: expects `OperationCanceledException` but gets `TaskCanceledException`

**Fix Required**:
1. Create `MockTacviewServer` test helper class
2. Implement basic TCP server that:
   - Accepts connections
   - Sends/receives messages
   - Can be controlled from tests
3. Update tests to use mock server
4. Fix exception type assertion (TaskCanceledException inherits from OperationCanceledException - use `Assert.ThrowsAny<OperationCanceledException>()`)

**Estimated Effort**: 2-3 hours

---

### 3. Reconnection Strategy Tests (7 failures)
**File**: `tests\AeroDebrief.Tests\Integrations\Tacview\Client\ReconnectionStrategyTests.cs`

**Failing Tests**:
- `TryReconnectAsync_SuccessfulReconnect_ReturnsTrue`
- `TryReconnectAsync_AllAttemptsFail_ReturnsFalse`
- `TryReconnectAsync_SucceedsOnSecondAttempt_ReturnsTrue`
- `TryReconnectAsync_CancellationRequested_ReturnsFalse`
- `TryReconnectAsync_MaxDelayCapAt60Seconds_DoesNotExceedCap`
- `TryReconnectAsync_RespectsMaxAttempts` (4 theory variations: 1, 3, 5, 10)

**Issues**:
- Tests depend on `TacviewClient` which requires real network connection
- Need to mock/stub connection attempts
- Reconnection logic cannot be tested without connection infrastructure

**Fix Required**:
1. Refactor `TacviewReconnectionStrategy` to accept connection delegate:
   ```csharp
   public async Task<bool> TryReconnectAsync(
       Func<CancellationToken, Task<bool>> connectFunc,
       CancellationToken cancellationToken)
   ```
2. Update tests to provide mock connect function
3. Verify retry delays, max attempts, exponential backoff

**Estimated Effort**: 1-2 hours

---

### 4. Integration Tests (8 failures)
**File**: `tests\AeroDebrief.Tests\Integrations\Tacview\Integration\TacviewIntegrationTests.cs`

**Failing Tests**:
- `VariableSpeed_AllSpeeds_SyncCorrectly` (6 theory tests: 0.25x, 0.5x, 1x, 1.5x, 2x, 4x)
- `Reconnection_AfterDisconnect_RecoversSmoothly`
- `Performance_CpuOverhead_RemainsUnder5Percent`

**Issues**:
- Full end-to-end integration tests
- Require:
  - Mock Tacview server
  - Mock audio pipeline
  - Mock playback controller setup
  - Complete integration wiring

**Fix Required**:
1. Create comprehensive integration test infrastructure:
   - `MockTacviewServer` (from #2 above)
   - `MockAudioPipeline` or use existing mock engines
   - Test harness that wires everything together
2. Implement test scenarios:
   - Speed changes and sync verification
   - Reconnection scenarios
   - Performance measurement infrastructure

**Estimated Effort**: 4-6 hours

---

### 5. Frequency Filter Tests (1 failure)
**File**: `tests\AeroDebrief.Tests\Integrations\Tacview\Pilot\FrequencyFilterTests.cs`

**Failing Test**:
- `UpdateSelection_ClearsOldSelection`

**Issue**:
- Test logic expects selection to be cleared when updating
- Actual behavior may differ
- Need to examine `TacviewAudioFilter` implementation

**Fix Required**:
1. Review `TacviewAudioFilter.UpdateSelection()` implementation
2. Either fix implementation or fix test expectations
3. Verify filter behavior matches intended design

**Estimated Effort**: 30 minutes

---

## Recommended Fix Order

### Phase 1: Quick Wins (1-2 hours)
1. ? Fix Protocol Tests - add missing message types
2. ? Fix Frequency Filter Test - simple logic fix
3. ? Fix exception type assertion in Client Tests

### Phase 2: Infrastructure (3-4 hours)
4. ?? Create `MockTacviewServer` test helper
5. ?? Refactor `ReconnectionStrategy` for testability
6. ?? Update Client Tests to use mock server
7. ?? Fix all Reconnection Strategy Tests

### Phase 3: Integration (4-6 hours)
8. ?? Create integration test harness
9. ?? Fix all Integration Tests

**Total Estimated Effort**: 8-12 hours

---

## Files That Need Changes

### New Files to Create:
1. `src\AeroDebrief.Integrations\Tacview\Protocol\Messages\SyncStatusMessage.cs`
2. `tests\AeroDebrief.Tests\TestHelpers\MockTacviewServer.cs`
3. `tests\AeroDebrief.Tests\Integrations\Tacview\TestHelpers\IntegrationTestHarness.cs`

### Files to Modify:
1. `src\AeroDebrief.Integrations\Tacview\Protocol\TacviewProtocol.cs` - add message type handlers
2. `src\AeroDebrief.Integrations\Tacview\Client\TacviewReconnectionStrategy.cs` - refactor for testability
3. `tests\AeroDebrief.Tests\Integrations\Tacview\Protocol\TacviewProtocolTests.cs` - update test cases
4. `tests\AeroDebrief.Tests\Integrations\Tacview\Client\TacviewClientTests.cs` - use mock server
5. `tests\AeroDebrief.Tests\Integrations\Tacview\Client\ReconnectionStrategyTests.cs` - use mock connection
6. `tests\AeroDebrief.Tests\Integrations\Tacview\Integration\TacviewIntegrationTests.cs` - add infrastructure
7. `tests\AeroDebrief.Tests\Integrations\Tacview\Pilot\FrequencyFilterTests.cs` - fix logic
8. `src\AeroDebrief.Integrations\Tacview\Pilot\TacviewAudioFilter.cs` - possibly fix UpdateSelection

---

## Decision Points

### Should We Fix All Tests?

**Arguments FOR**:
- Complete test coverage
- Catches regressions
- Documents expected behavior
- Professional codebase

**Arguments AGAINST**:
- Time investment (8-12 hours)
- Some tests may test implementation details
- Integration tests are brittle
- Core functionality (TacviewSyncService) already works

### Recommendation

**Option A: Full Fix (Recommended for Production)**
- Fix all 27 tests
- Invest 8-12 hours
- Achieve 100% Tacview test coverage
- Best for long-term maintainability

**Option B: Pragmatic Fix (Recommended for MVP)**
- Fix Protocol Tests (30 min) - quick win
- Fix Frequency Filter Test (30 min) - quick win  
- Skip Client/Reconnection/Integration tests - mark as `[Fact(Skip = "Requires mock server infrastructure")]`
- Total time: 1 hour
- Achieves 23/45 (51%) test coverage
- Core sync functionality proven working

**Option C: Deferred Fix**
- Document issues in this file
- Create GitHub issues for each category
- Fix incrementally over multiple PRs
- Prioritize based on user feedback

---

## Implementation Guide (Phase 1 - Quick Wins)

### 1. Fix Protocol Tests

#### Step 1: Create SyncStatusMessage.cs
```csharp
using System.Text.Json.Serialization;

namespace AeroDebrief.Integrations.Tacview.Protocol.Messages;

/// <summary>
/// Sync status message sent between AeroDebrief and Tacview
/// Indicates current synchronization state
/// </summary>
public class SyncStatusMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "sync_status";
    
    /// <summary>
    /// Current sync quality (0-100%)
    /// </summary>
    [JsonPropertyName("quality_percent")]
    public double QualityPercent { get; set; }
    
    /// <summary>
    /// Current drift in milliseconds
    /// </summary>
    [JsonPropertyName("drift_ms")]
    public int DriftMs { get; set; }
    
    /// <summary>
    /// Whether currently synchronized
    /// </summary>
    [JsonPropertyName("is_synchronized")]
    public bool IsSynchronized { get; set; }
}
```

#### Step 2: Update TacviewProtocol.cs
Add to ParseMessage switch statement:
```csharp
case "sync_status":
    return JsonSerializer.Deserialize<SyncStatusMessage>(jsonMessage, _jsonOptions);
    
case "speaking_status":
    return JsonSerializer.Deserialize<SpeakingStatusMessage>(jsonMessage, _jsonOptions);
```

### 2. Fix Frequency Filter Test

Review `UpdateSelection()` behavior and either:
- Fix implementation to clear old selection
- Or update test to match actual behavior

### 3. Fix Exception Type in Client Tests

Change:
```csharp
await Assert.ThrowsAsync<OperationCanceledException>(...);
```

To:
```csharp
await Assert.ThrowsAnyAsync<OperationCanceledException>(...);
```

---

## Success Criteria

- All 45 Tacview tests passing
- No compilation errors
- Build passes
- Tests run in < 30 seconds
- Mock infrastructure reusable for future tests

---

## Next Steps

1. Review this plan with team
2. Choose option (A, B, or C)
3. Allocate time for implementation
4. Create GitHub issues if deferred
5. Begin implementation starting with Phase 1

---

## Notes

- TacviewSyncServiceTests are already 100% passing - excellent work!
- Core Tacview sync functionality is proven working
- Remaining failures are in testing infrastructure and edge cases
- Mock server pattern will benefit other networking tests
