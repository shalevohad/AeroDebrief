# Performance Benchmarks Guide

## Overview

The AeroDebrief performance benchmark suite provides comprehensive testing of critical performance metrics across file I/O, audio processing, analysis, and memory usage. These benchmarks help maintain performance standards and detect regressions early.

**Location:** `tests\AeroDebrief.Tests\Performance\PerformanceBenchmarks.cs`

---

## Performance Targets

| Category | Metric | Target | Notes |
|----------|--------|--------|-------|
| **File Indexing** | Throughput | >10,000 packets/sec | Measures packet index creation speed |
| **Audio Decoding** | Opus Decoding | >5,000 packets/sec | Real Opus codec performance |
| **Audio Decoding** | Real-time Ratio | >10x | Should decode >10x faster than real-time |
| **File Analysis** | Complete Analysis | <2s for 10MB file | Full frequency + activity + duration |
| **Memory Usage** | File Loading | <500MB for 100MB file | Memory footprint during operation |
| **PCM Conversion** | Throughput | >10M samples/sec | Raw audio conversion speed |

---

## Running Benchmarks

### Run All Performance Benchmarks
```bash
dotnet test --filter "Category=Performance"
```

### Run Specific Benchmark Categories

**File I/O Benchmarks Only:**
```bash
dotnet test --filter "FullyQualifiedName~Benchmark_FileIndexing"
```

**Audio Decoding Benchmarks Only:**
```bash
dotnet test --filter "FullyQualifiedName~Benchmark_Opus"
```

**Memory Benchmarks Only:**
```bash
dotnet test --filter "FullyQualifiedName~Benchmark_Memory"
```

**Stress Tests Only:**
```bash
dotnet test --filter "Category=Stress"
```

### Run with Detailed Logging
```bash
dotnet test --filter "Category=Performance" --logger "console;verbosity=detailed"
```

---

## Benchmark Categories

### 1. File I/O Benchmarks

Tests file indexing and loading performance across different file sizes.

**Tests:**
- `Benchmark_FileIndexing_SmallFile` - 1K packets
- `Benchmark_FileIndexing_MediumFile` - 10K packets
- `Benchmark_FileIndexing_LargeFile` - 50K packets (~3MB)
- `Benchmark_IndexCache_SecondLoad` - Index cache performance

**Key Metrics:**
- Packets per second
- Data rate (MB/s)
- Indexing time
- Cache speedup

**Example Output:**
```
?? File Indexing Benchmark (Large):
   File size: 2.87 MB
   Packets: 50,000
   Time: 1,234ms
   Rate: 40,519 packets/sec
   Data rate: 2.33 MB/s
```

---

### 2. Audio Decoding Benchmarks

Tests audio processing performance with real Opus codec and PCM conversion.

**Tests:**
- `Benchmark_OpusDecoding` - Real Opus decoder performance
- `Benchmark_PcmConversion` - PCM byte array conversion
- `Benchmark_AudioAmplitudeCalculation` - Signal analysis speed

**Key Metrics:**
- Packets per second
- Real-time decoding ratio
- Samples per second
- Memory efficiency

**Example Output:**
```
?? Opus Decoding Benchmark:
   Packets decoded: 1,000
   Samples decoded: 960,000
   Audio duration: 20.00s
   Time elapsed: 187ms
   Rate: 5,348 packets/sec
   Real-time ratio: 106.95x (higher is better)
```

---

### 3. File Analysis Benchmarks

Tests analysis operations that scan recording files.

**Tests:**
- `Benchmark_FileAnalysis_FrequencyExtraction` - Extract all frequencies
- `Benchmark_FileAnalysis_DurationCalculation` - Calculate total duration
- `Benchmark_FileAnalysis_ActivityAnalysis` - Detect activity periods
- `Benchmark_FileAnalysis_CompleteAnalysis` - Full analysis pipeline

**Key Metrics:**
- Processing time
- Packets per second
- Analysis completeness
- Memory usage

**Example Output:**
```
?? Complete File Analysis Benchmark:
   File size: 1.25 MB
   Packets: 25,000
   Frequencies: 8
   Duration: 00:16:40
   Activity periods: 15
   Total time: 1,523ms
   Overall rate: 16,414 packets/sec
```

---

### 4. Memory Usage Benchmarks

Tests memory footprint during various operations.

**Tests:**
- `Benchmark_Memory_FileLoading` - Memory usage during file loading
- `Benchmark_Memory_AudioDecoding` - Memory usage during decoding

**Key Metrics:**
- Memory used (MB)
- Memory per packet (bytes)
- GC pressure
- Peak memory

**Example Output:**
```
?? Memory Usage Benchmark (File Loading):
   Packets: 100,000
   Initial memory: 45.23 MB
   Final memory: 127.85 MB
   Memory used: 82.62 MB
   Memory per packet: 862.20 bytes
```

---

### 5. Stress Tests

Tests system behavior under heavy load.

**Tests:**
- `Stress_LargeFileProcessing` - Process ~100MB file with 50 frequencies

**Key Metrics:**
- Total processing time
- Overall throughput
- Data rate (MB/s)
- System stability
- Multi-frequency handling
- Concurrency performance
- Memory efficiency under load

**Performance Target:**
- Process 100MB in under 60 seconds
- Throughput: >1.5 MB/s
- Memory usage: <500MB

**File Characteristics:**
- 100MB .adb file ? 2 hours of recording
- Includes audio payload + metadata (positions, player info, timestamps)
- Audio: ~80-85% of file size
- Metadata: ~15-20% of file size

**Example Output:**
```
?? Large File Stress Test (Multi-Frequency - 100MB):
   File size: 98.47 MB
   Packets: 512,000
   Frequencies: 3
   Avg packets per frequency: 170,667
   Duration: 02:13:20  (2 hours, 13 minutes)
   Activity periods: 234
   Processing time: 32.15s
   Overall rate: 15,923 packets/sec
   Data rate: 3.06 MB/s
   Concurrency factor: 3 simultaneous channels
```

**Interpretation:**
- **Processing time < 60s** ? Pass - Meets performance target
- **Data rate > 1.5 MB/s** ? Pass - Good throughput
- **Memory stable** ? Pass - No excessive allocations
- **All frequencies detected** ? Pass - Metadata extraction working
- **Duration accurate** ? Pass - ~2 hours for 100MB file

---

## Understanding Results

### .ADB File Format

**AeroDebrief recording files (.adb) contain:**
1. **Audio payload** (80-85% of file size)
   - Opus-encoded voice data
   - Compressed at 48kHz, 16-bit mono
   - ~40ms frames (typical)

2. **Metadata** (15-20% of file size)
   - Player information (name, GUID, coalition)
   - Position data (latitude, longitude, altitude)
   - Aircraft information (type, unit ID)
   - Timestamps (precise packet timing)
   - Frequency and modulation data

**Size relationship:**
- 100MB .adb file ? 2 hours of recording
- 50MB .adb file ? 1 hour of recording
- 200MB .adb file ? 4 hours of recording

**Why metadata matters:**
- Enables post-flight analysis
- Supports position-based playback
- Facilitates frequency filtering
- Provides context for debriefing

### Performance Indicators

? **PASS** - Performance meets or exceeds target
- Green output
- Meets threshold
- No action needed

?? **WARNING** - Performance below target but acceptable
- Yellow output
- Within 20% of threshold
- Monitor for regression

? **FAIL** - Performance significantly below target
- Red output
- Below threshold
- Investigate immediately

### Common Performance Issues

**Slow File Indexing (<5K packets/sec):**
- Check disk I/O performance
- Verify SSD vs HDD
- Check antivirus interference
- Ensure sufficient disk space

**Slow Audio Decoding (<2K packets/sec):**
- Check CPU usage
- Verify Opus codec installation
- Check for CPU throttling
- Verify .NET optimization level

**High Memory Usage (>500MB for 100MB file):**
- Check for memory leaks
- Verify object disposal
- Monitor GC pressure
- Profile memory allocations

**Slow Analysis (<5K packets/sec):**
- Check file I/O bottlenecks
- Verify algorithm efficiency
- Check for repeated file scans
- Profile hotspots

---

## Continuous Integration

### CI Pipeline Integration

Benchmarks are automatically run in the CI pipeline with:
- Performance threshold checks
- Historical trend tracking
- Regression detection
- Automated alerts

### GitHub Actions Example
```yaml
- name: Run Performance Benchmarks
  run: dotnet test --filter "Category=Performance" --logger "trx;LogFileName=benchmark-results.trx"
  
- name: Check Performance Thresholds
  run: |
    # Parse results and check thresholds
    # Fail build if performance degrades >20%
```

---

## Interpreting Trends

### Baseline Establishment

**First run on new hardware:**
1. Run all benchmarks 3 times
2. Record median values
3. Establish baseline thresholds
4. Document hardware specs

**Example baseline:**
```
Hardware: Intel i7-12700K, 32GB RAM, NVMe SSD
Baseline Results:
- File Indexing: 45,000 pkt/s
- Opus Decoding: 8,500 pkt/s (120x real-time)
- Complete Analysis: 0.8s (25,000 packets)
- Memory Usage: 150MB (100K packets)
```

### Regression Detection

**Performance regression indicators:**
- >20% decrease in throughput
- >50% increase in memory usage
- >30% increase in processing time
- Consistent failures across runs

**Action steps:**
1. Verify hardware consistency
2. Check recent code changes
3. Profile hot code paths
4. Compare algorithm complexity
5. Review recent dependencies

---

## Hardware Considerations

### Minimum Requirements
- **CPU:** 4+ cores, 2.5+ GHz
- **RAM:** 8GB+ available
- **Storage:** SSD (recommended)
- **.NET:** .NET 9 SDK

### Recommended Configuration
- **CPU:** 8+ cores, 3.5+ GHz
- **RAM:** 16GB+
- **Storage:** NVMe SSD
- **.NET:** Latest .NET 9

### Performance Scaling

**Expected performance by hardware:**

| Hardware Class | File Indexing | Opus Decoding | Analysis |
|---------------|---------------|---------------|----------|
| Entry (4C/8GB) | 15K pkt/s | 3K pkt/s | 3s |
| Mid (8C/16GB) | 35K pkt/s | 7K pkt/s | 1.5s |
| High (16C/32GB) | 60K+ pkt/s | 12K+ pkt/s | <1s |

---

## Adding New Benchmarks

### Benchmark Template

```csharp
[Fact]
[Trait("Category", "Performance")]
[Trait("Category", "Benchmark")]
public async Task Benchmark_YourOperation()
{
    // Arrange
    const int itemCount = 10_000;
    var testData = CreateTestData(itemCount);
    
    // Act
    var stopwatch = Stopwatch.StartNew();
    
    // Your operation here
    foreach (var item in testData)
    {
        ProcessItem(item);
    }
    
    stopwatch.Stop();
    
    // Assert & Report
    var itemsPerSec = itemCount / stopwatch.Elapsed.TotalSeconds;
    
    Logger.Info($"?? Your Operation Benchmark:");
    Logger.Info($"   Items processed: {itemCount:N0}");
    Logger.Info($"   Time: {stopwatch.ElapsedMilliseconds}ms");
    Logger.Info($"   Rate: {itemsPerSec:N0} items/sec");
    
    // Assert performance threshold
    itemsPerSec.Should().BeGreaterThan(YOUR_THRESHOLD);
}
```

### Best Practices

1. **Warm-up:** Include warm-up runs for JIT compilation
2. **Isolation:** Test one operation at a time
3. **Realistic Data:** Use representative test data
4. **Multiple Runs:** Run 3+ times and take median
5. **Clear Metrics:** Report actionable metrics
6. **Thresholds:** Set realistic, hardware-aware thresholds

---

## Troubleshooting

### Benchmark Failures

**"Performance below threshold"**
- Run benchmark 3 times to verify consistency
- Check system load (Task Manager)
- Close background applications
- Verify disk speed (CrystalDiskMark)
- Check .NET runtime version

**"Out of memory during benchmark"**
- Increase available RAM
- Reduce test data size
- Check for memory leaks
- Profile memory allocations
- Force GC between tests

**"Benchmark times out"**
- Reduce test data size
- Check for infinite loops
- Verify file system access
- Check network connectivity (if applicable)

### Performance Analysis Tools

**Windows:**
- Visual Studio Profiler
- PerfView
- dotTrace
- Windows Performance Recorder

**Cross-platform:**
- dotnet-trace
- dotnet-counters
- BenchmarkDotNet

---

## Related Documentation

- [TEST_CODE_AUDIT.md](./TEST_CODE_AUDIT.md) - Test suite audit report
- [Technical-Architecture.md](./Technical-Architecture.md) - System architecture
- [GPU-Waveform-Rendering-Implementation.md](./GPU-Waveform-Rendering-Implementation.md) - GPU performance

---

## Future Enhancements

**Planned benchmark additions:**
1. GPU waveform rendering benchmarks
2. Network I/O benchmarks (live recording)
3. Concurrent playback benchmarks
4. Database query benchmarks (if added)
5. UI rendering benchmarks

**Infrastructure improvements:**
1. Automated baseline tracking
2. Performance trend visualization
3. Comparative analysis (vs. previous versions)
4. Hardware-normalized scoring
5. Cloud-based benchmark repository

---

*This benchmark suite is continuously evolving. Contributions and improvements are welcome!*
