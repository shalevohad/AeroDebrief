# Test Builders

This directory contains builder classes for creating test data objects in unit tests. The builder pattern provides a fluent API for constructing complex test objects with sensible defaults, making tests more readable and maintainable.

## Available Builders

### PositionBuilder

Creates `Position` objects for testing geographical coordinates.

**Example:**
```csharp
var position = new PositionBuilder()
    .WithLatitude(40.7128)
    .WithLongitude(-74.0060)
    .WithAltitude(100)
    .Build();

// Or use preset positions
var validPosition = new PositionBuilder()
    .WithValidPosition()
    .Build();

var invalidPosition = new PositionBuilder()
    .WithInvalidPosition()
    .Build();
```

### AircraftInfoBuilder

Creates `AircraftInfo` objects for testing aircraft/unit data.

**Example:**
```csharp
var aircraft = new AircraftInfoBuilder()
    .WithUnitType("F-16C_50")
    .WithUnitId(1001)
    .Build();

// Or use preset aircraft types
var f16 = new AircraftInfoBuilder().WithF16().Build();
var a10 = new AircraftInfoBuilder().WithA10().Build();
var fa18 = new AircraftInfoBuilder().WithFA18().Build();
```

### PlayerInfoBuilder

Creates `PlayerInfo` objects for testing player/pilot data.

**Example:**
```csharp
var player = new PlayerInfoBuilder()
    .WithName("Maverick")
    .WithBlueCoalition()
    .WithF16Pilot()
    .Build();

// Or configure nested objects
var player = new PlayerInfoBuilder()
    .WithName("Iceman")
    .WithPosition(p => p.WithValidPosition())
    .WithAircraftInfo(a => a.WithA10())
    .Build();
```

**Presets:**
- `WithF16Pilot()` - Creates an F-16 pilot
- `WithA10Pilot()` - Creates an A-10 pilot
- `WithRedCoalition()` - Sets coalition to Red (1)
- `WithBlueCoalition()` - Sets coalition to Blue (2)
- `WithSpectatorCoalition()` - Sets coalition to Spectator (0)

### AudioPacketMetadataBuilder

Creates `AudioPacketMetadata` objects for testing audio packet data.

**Example:**
```csharp
var packet = new AudioPacketMetadataBuilder()
    .WithFrequencyMHz(251.0)
    .WithAMModulation()
    .WithOpusSilence()
    .WithPlayerData(p => p
        .WithName("Viper")
        .WithF16Pilot())
    .Build();

// Create with PCM audio
var packet = new AudioPacketMetadataBuilder()
    .WithFrequencyMHz(305.0)
    .WithPcmTone(440.0, 0.02) // 440 Hz tone, 20ms duration
    .Build();
```

**Audio Presets:**
- `WithOpusSilence()` - Creates minimal Opus silence frame
- `WithPcmSilence()` - Creates 1 second of PCM silence
- `WithPcmTone(frequency, duration)` - Creates a PCM tone at specified frequency

**Modulation:**
- `WithAMModulation()` - Sets AM modulation
- `WithFMModulation()` - Sets FM modulation

**Coalition:**
- `WithRedCoalition()` - Sets coalition to Red
- `WithBlueCoalition()` - Sets coalition to Blue

## Design Principles

1. **Fluent API**: All builder methods return `this` to allow method chaining
2. **Sensible Defaults**: Builders provide reasonable default values for all fields
3. **Presets**: Common configurations are available as preset methods (e.g., `WithF16()`, `WithValidPosition()`)
4. **Composability**: Builders can be composed using Action delegates (e.g., `WithPlayerData(p => ...)`)
5. **Readability**: Method names are descriptive and self-documenting

## Usage in Tests

Here's a complete example showing how to use builders in a unit test:

```csharp
[TestClass]
public class MyTests
{
    [TestMethod]
    public void Test_AudioPacket_Serialization()
    {
        // Arrange
        var packet = new AudioPacketMetadataBuilder()
            .WithFrequencyMHz(251.0)
            .WithAMModulation()
            .WithPlayerData(p => p
                .WithF16Pilot()
                .WithName("Maverick")
                .WithBlueCoalition()
                .WithPosition(pos => pos.WithValidPosition())
                .WithAircraftInfo(ac => ac.WithF16()))
            .WithOpusSilence()
            .Build();

        // Act
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            packet.TryWriteMetadata(writer);
        }

        // Assert
        Assert.IsTrue(stream.Length > 0);
    }
}
```

## Benefits

- **Reduced Boilerplate**: No need to manually set every field for every test
- **Maintainable**: Changes to object constructors only require updates to builders
- **Clear Intent**: Tests clearly show what data is important for the test case
- **Consistent Defaults**: All tests use the same reasonable defaults
- **Type Safety**: Compile-time checking of builder method calls

## Adding New Builders

When creating a new builder:

1. Follow the naming convention: `{ClassName}Builder`
2. Place the builder in the `TestBuilders` directory
3. Provide sensible defaults in private fields
4. Create fluent methods that return `this`
5. Add preset methods for common configurations
6. Include a `Build()` method that returns the constructed object
7. Document usage in this README
