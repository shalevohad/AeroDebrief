# ?? Quick Start: DuckDB Pre-Refactoring Verification

## Current Status

? **Build**: Passing  
? **Phase 1-3**: Complete  
? **Phase 4 Code**: Exists  
? **Phase 4 Test**: **START HERE**

---

## Step 1: Manual Test (60 min) ?? **DO THIS FIRST**

### Test Scenario: Live Recording + Monitoring

1. **Start Recording**:
   ```
   ? Connect to SRS server
   ? Click "Start Recording"
   ? Verify temp .duckdb file created
   ```

2. **Verify Live Features**:
   ```
   ? Frequencies appear in real-time
   ? Players appear as they join
   ? Duration updates every 500ms
   ? Waveform extends in real-time
   ```

3. **Test Dual Playheads**:
   ```
   ? Scrub back to 30 seconds ago
   ? Audio plays from scrubbed position
   ? Recording playhead stays at end (red)
   ? Playback playhead at 30s (blue)
   ? Click "Go Live" ? jumps to recording position
   ```

4. **Stop Recording**:
   ```
   ? Click "Stop Recording"
   ? CVR file created
   ? Temp .duckdb deleted
   ? Can open CVR and play it
   ```

**Result**: ? All features work ? Proceed to Step 2  
**Result**: ? Issues found ? Fix and re-test

---

## Step 2: Quick Cleanup (30 min)

### Search & Remove:
```bash
Ctrl+Shift+F:
- "TODO Phase 4"
- "FIXME"
- "XXX"
- "HACK"
- "#if false"
```

### Actions:
- Remove commented-out code
- Delete obsolete methods
- Clean up debug logging
- Remove unused imports (Ctrl+R, Ctrl+G)
- Format all files (Ctrl+K, Ctrl+D)

---

## Step 3: Update Docs (30 min)

### Files to Update:
1. `DuckDB-Phase4-Complete.md` ? Add "Verified ?"
2. `DuckDB-Implementation-Complete-Plan.md` ? Update status
3. `DuckDB-Cleanup-Checklist.md` ? Check completed items

---

## Go/No-Go Decision

### ? GO (Ready to Refactor)
- Phase 4 works ?
- Cleanup done ?
- Docs updated ?

**Next**:
```bash
git add .
git commit -m "feat: Phase 4 verified, ready for refactoring"
git tag v1.0.0-duckdb-complete
git checkout -b refactor-viewmodel
```

### ? NO-GO (Issues Found)
- Document issues
- Fix critical bugs
- Re-test
- Then GO

---

## After Go ? Start Refactoring

Follow: `docs/UnifiedPlayerViewModel-Refactoring-Plan.md`

**Option B: Composition**
1. Create `IPlayerSession.cs`
2. Extract `PlaybackSessionManager.cs`
3. Create `RecordingSessionManager.cs` (composes Playback)
4. Simplify `UnifiedPlayerViewModel.cs`

---

**Priority**: ?? HIGH  
**Time**: 2 hours  
**Action**: Start manual test NOW  
**Branch**: `DuckDB-implementation`
