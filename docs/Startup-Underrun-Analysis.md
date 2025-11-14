# Pre-Buffering Strategy: Eliminating Startup Underruns

## Current Status

**Test Results After Pre-Buffering Implementation:**
```
FramesMixed: 1510
FPS: 48.4
Underruns: 51 (constant - same as before pre-buffering)
Status: ? PASSED
```

## Analysis: Where Are The 51 Underruns Coming From?

### Pre-Buffering Added (TestAudioSource)
```
[TestAudioSource] Pre-buffering 10 packets (200ms) before starting generation...
[TestAudioSource] Pre-buffer complete: 10 packets ready
```

? **UserWorkers have immediate packet availability**
? **No waiting for packet generation to start**

### Yet Underruns Remain

Looking at the timeline:
```
Initial stats: FramesMixed=25, ActiveFrequencies=5
[1s] FramesMixed=75, FPS=29.8, Underruns=51
[2s] FramesMixed=125, FPS=35.5, Underruns=51 (no change!)
[30s] FramesMixed=1510, FPS=48.4, Underruns=51 (still no change!)
```

**Conclusion**: The 51 underruns occur in the **first 500ms** while:
1. MasterMixer starts its 50 FPS loop immediately
2. We're still registering workers (5 workers × ~20ms each = 100ms)
3. Workers' internal Channel readers are starting up (~100ms)
4. Initial 200ms delay for "pipeline warm-up"

**Total startup time: ~500ms = 25 frames = 51 underruns makes sense!**

---

## Root Cause: MasterMixer Timing

The issue isn't packet availability—it's that **MasterMixer starts mixing before workers are ready**.

### Current Initialization Sequence

```csharp
[TestInitialize]
public async Task Setup()
{
    _audioOutput = new TestAudioCapture();
    await _audioOutput.InitializeAsync();
    _audioOutput.Start();
    
    _mixer = new MasterMixer(_audioOutput);  // ? Mixing loop starts IMMEDIATELY!
    
    await Task.Delay(1000);  // Mixer already ran for 1 second with no workers!
}

// Then in test...
for (int i = 0; i < frequencies.Length; i++)
{
    testSource.StartContinuousGeneration(...);  // Packets ready
    var worker = new UserWorker(...);           // Worker processing starts
    _mixer.RegisterUserWorker(...);             // ? But mixer already running for 1+ seconds!
}
```

**Problem**: MasterMixer's mixing loop starts in its constructor and immediately begins trying to pull from workers that don't exist yet.

---

## Solution Options

### Option 1: Defer Mixer Initialization (Recommended)

Register workers **before** creating MasterMixer:

```csharp
[TestInitialize]
public async Task Setup()
{
    _audioOutput = new TestAudioCapture();
    await _audioOutput.InitializeAsync();
    _audioOutput.Start();
    
    // DON'T create mixer yet!
    _mixer = null;
    
    await Task.Delay(100);  // Let audio output stabilize
}

// In test...
// Step 1: Pre-buffer packets
for (int i = 0; i < frequencies.Length; i++)
{
    testSource.StartContinuousGeneration(..., preBufferPackets: 10);
}

// Step 2: Create workers (but DON'T register yet - no mixer!)
for (int i = 0; i < frequencies.Length; i++)
{
    var worker = new UserWorker(...);
    workers.Add(worker);
}

// Step 3: Brief delay for worker internal channels to start
await Task.Delay(100);

// Step 4: Create mixer (mixing loop starts)
_mixer = new MasterMixer(_audioOutput);

// Step 5: Immediately register all workers (before first mix iteration)
foreach (var worker in workers)
{
    _mixer.RegisterUserWorker(...);
    _mixer.SetFrequencyGate(..., FrequencyGateMode.Allow);
}

// Step 6: No additional delay needed - workers ready, packets buffered
// Start measurement immediately
```

**Expected Result:**
- Underruns: **0-5** (down from 51)
- FPS: **~49.5** (up from 48.4)
- Startup overhead eliminated

---

### Option 2: Add Mixer Warm-Up Mode (Alternative)

Modify MasterMixer to have a "paused" state before starting mixing:

```csharp
public class MasterMixer
{
    private bool _isPaused = true;  // Start paused
    
    public MasterMixer(IAudioOutputEngine audioOutput, bool startPaused = false)
    {
        _isPaused = startPaused;
        _mixingTask = Task.Run(() => MixingLoopAsync(_cts.Token));
    }
    
    public void Resume()
    {
        _isPaused = false;
    }
    
    private async Task MixingLoopAsync(CancellationToken cancellationToken)
    {
        // Wait until resumed
        while (_isPaused && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(10, cancellationToken);
        }
        
        // Now start actual mixing loop...
    }
}
```

**Usage:**
```csharp
_mixer = new MasterMixer(_audioOutput, startPaused: true);
// Register workers...
_mixer.Resume();  // Start mixing with workers ready
```

**Pros:**
- Cleaner API
- No timing dependencies
- Works for production code too

**Cons:**
- Requires MasterMixer code changes
- More complex implementation

---

### Option 3: Increase Setup Delay (Current Workaround)

Simply increase the Setup delay to let mixer "burn through" startup underruns:

```csharp
[TestInitialize]
public async Task Setup()
{
    _audioOutput = new TestAudioCapture();
    await _audioOutput.InitializeAsync();
    _audioOutput.Start();
    
    _mixer = new MasterMixer(_audioOutput);
    
    // Increased from 1000ms to 2000ms
    // Let mixer run through startup phase before any workers exist
    await Task.Delay(2000);
}
```

**Result:**
- Underruns: **51** (still present, but "hidden" before measurement)
- FPS: **~49.9** (better average because startup excluded)
- Measurement starts after mixer stabilizes

**Pros:**
- Simplest solution
- No code changes needed

**Cons:**
- Doesn't actually eliminate underruns
- Just moves them outside measurement window
- Slower test execution

---

## Recommendation: Option 1 (Deferred Initialization)

### Implementation for Prolonged Operation Test

```csharp
[TestInitialize]
public async Task Setup()
{
    var settings = AeroDebrief.Core.Settings.PlayerSettingsStore.Instance;
    settings.SaveAGCSettings(..., enabled: false);
    
    _audioOutput = new TestAudioCapture();
    await _audioOutput.InitializeAsync();
    _audioOutput.Start();
    
    // NOTE: DON'T create mixer yet - let tests create it when ready
    _mixer = null;
    
    await Task.Delay(100);  // Brief stabilization
}

[TestMethod]
public async Task ProlongedOperation_NoMemoryLeaks()
{
    var testSources = new List<TestAudioSource>();
    var workers = new List<UserWorker>();

    // Step 1: Pre-buffer packets
    for (int i = 0; i < frequencies.Length; i++)
    {
        var testSource = new TestAudioSource(frequencies[i], 48000);
        testSource.StartContinuousGeneration(..., preBufferPackets: 10);
        testSources.Add(testSource);
    }

    // Step 2: Create workers
    for (int i = 0; i < frequencies.Length; i++)
    {
        var worker = new UserWorker($"PILOT-{i:D3}", frequencies[i], testSources[i]);
        workers.Add(worker);
    }

    // Step 3: Brief delay for worker channels to start
    await Task.Delay(100);

    // Step 4: Create mixer NOW (mixing loop starts)
    _mixer = new MasterMixer(_audioOutput);

    // Step 5: Immediately register all workers
    for (int i = 0; i < frequencies.Length; i++)
    {
        _mixer.RegisterUserWorker(frequencies[i], $"PILOT-{i:D3}", workers[i]);
        _mixer.SetFrequencyGate(frequencies[i], FrequencyGateMode.Allow);
    }

    // Step 6: No additional delay - start measurement immediately
    var initialStats = _mixer.GetStats();
    
    // Run test for 30 seconds...
}
```

### Expected Improvements

**Before (Current):**
```
Initial: FramesMixed=25 (from setup delay)
[1s] Underruns=51 (startup overhead)
[30s] Underruns=51 (constant)
FPS: 48.4
```

**After (Deferred Init):**
```
Initial: FramesMixed=0 (mixer just created)
[1s] Underruns=0-2 (minimal startup)
[30s] Underruns=0-2 (constant)
FPS: 49.8-49.9
```

---

## Alternative: Increase Setup Delay (Simpler)

If we don't want to restructure tests, simply increase the setup delay:

```csharp
[TestInitialize]
public async Task Setup()
{
    //... same setup...
    
    _mixer = new MasterMixer(_audioOutput);
    
    // INCREASED: 1000ms ? 2000ms
    // Let mixer run through startup underruns before tests begin
    await Task.Delay(2000);
}
```

This will move the 51 underruns **outside** the measurement window, giving better FPS numbers without actually eliminating the underruns.

---

## Conclusion

The **51 constant underruns** are a **timing artifact** from MasterMixer starting before workers are ready, not a packet availability issue.

**Best Solution:** Defer MasterMixer creation until workers are registered (Option 1)

**Quick Fix:** Increase setup delay to 2000ms (Option 3)

The pre-buffering we implemented **is still valuable** because it ensures:
- ? UserWorkers never block waiting for packets
- ? Consistent packet availability throughout test
- ? No packet generation timing issues

Combined with deferred mixer initialization, we should achieve **near-zero underruns** and **~50 FPS** performance.
