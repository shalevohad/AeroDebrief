using System;
using System.Collections.Generic;
using AeroDebrief.Core;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using AeroDebrief.Integrations.Tacview.Models;
using AeroDebrief.Integrations.Tacview.Pilot;
using AeroDebrief.Integrations.Tacview.Protocol.Messages;
using Xunit;

namespace AeroDebrief.Tests.Integrations.Tacview.Pilot;

public class FrequencyFilterTests
{
    [Fact]
    public void ShouldPlayPacket_NoSelection_ReturnsTrue()
    {
        // Arrange
        var filter = new TacviewAudioFilter();
        var packet = CreateTestPacket("pilot-1", 251000000.0);

        // Act
        var result = filter.ShouldPlayPacket(packet);

        // Assert
        Assert.True(result, "Should play all packets when no Tacview selection exists");
    }

    [Fact]
    public void ShouldPlayPacket_SelectedPilot_AllFrequenciesEnabled_ReturnsTrue()
    {
        // Arrange
        var filter = new TacviewAudioFilter();
        var selection = new PilotSelectionMessage
        {
            SelectedPilots = new List<PilotData>
            {
                new() 
                { 
                    PilotId = "pilot-1", 
                    PilotName = "Viper 1-1",
                    EnabledFrequencies = null // null = all frequencies enabled
                }
            },
            GeneralEnabledFrequencies = new List<double>()
        };
        filter.UpdateSelection(selection);

        var packet = CreateTestPacket("pilot-1", 251000000.0);

        // Act
        var result = filter.ShouldPlayPacket(packet);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldPlayPacket_SelectedPilot_SpecificFrequencyEnabled_ReturnsTrue()
    {
        // Arrange
        var filter = new TacviewAudioFilter();
        var selection = new PilotSelectionMessage
        {
            SelectedPilots = new List<PilotData>
            {
                new() 
                { 
                    PilotId = "pilot-1",
                    PilotName = "Viper 1-1",
                    EnabledFrequencies = new List<double> { 251000000.0, 305000000.0 }
                }
            },
            GeneralEnabledFrequencies = new List<double>()
        };
        filter.UpdateSelection(selection);

        var packet = CreateTestPacket("pilot-1", 251000000.0);

        // Act
        var result = filter.ShouldPlayPacket(packet);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldPlayPacket_SelectedPilot_FrequencyDisabled_ReturnsFalse()
    {
        // Arrange
        var filter = new TacviewAudioFilter();
        var selection = new PilotSelectionMessage
        {
            SelectedPilots = new List<PilotData>
            {
                new() 
                { 
                    PilotId = "pilot-1",
                    PilotName = "Viper 1-1",
                    EnabledFrequencies = new List<double> { 251000000.0 } // Only 251 MHz
                }
            },
            GeneralEnabledFrequencies = new List<double>()
        };
        filter.UpdateSelection(selection);

        var packet = CreateTestPacket("pilot-1", 305000000.0); // Different frequency

        // Act
        var result = filter.ShouldPlayPacket(packet);

        // Assert
        Assert.False(result, "Should filter out disabled frequency for selected pilot");
    }

    [Fact]
    public void ShouldPlayPacket_NonSelectedPilot_GeneralFrequenciesDisabled_ReturnsFalse()
    {
        // Arrange
        var filter = new TacviewAudioFilter();
        var selection = new PilotSelectionMessage
        {
            SelectedPilots = new List<PilotData>
            {
                new() { PilotId = "pilot-1", PilotName = "Viper 1-1" }
            },
            GeneralEnabledFrequencies = new List<double>() // Empty = all disabled (default)
        };
        filter.UpdateSelection(selection);

        var packet = CreateTestPacket("pilot-2", 251000000.0); // Non-selected pilot

        // Act
        var result = filter.ShouldPlayPacket(packet);

        // Assert
        Assert.False(result, "Should filter out non-selected pilots when general frequencies disabled (default)");
    }

    [Fact]
    public void ShouldPlayPacket_NonSelectedPilot_GeneralFrequencyEnabled_ReturnsTrue()
    {
        // Arrange
        var filter = new TacviewAudioFilter();
        var selection = new PilotSelectionMessage
        {
            SelectedPilots = new List<PilotData>
            {
                new() { PilotId = "pilot-1", PilotName = "Viper 1-1" }
            },
            GeneralEnabledFrequencies = new List<double> { 251000000.0 } // Enable 251 MHz for non-selected
        };
        filter.UpdateSelection(selection);

        var packet = CreateTestPacket("pilot-2", 251000000.0);

        // Act
        var result = filter.ShouldPlayPacket(packet);

        // Assert
        Assert.True(result, "Should play non-selected pilot when their frequency is in general enabled list");
    }

    [Fact]
    public void ShouldPlayPacket_NonSelectedPilot_FrequencyNotInGeneralList_ReturnsFalse()
    {
        // Arrange
        var filter = new TacviewAudioFilter();
        var selection = new PilotSelectionMessage
        {
            SelectedPilots = new List<PilotData>
            {
                new() { PilotId = "pilot-1", PilotName = "Viper 1-1" }
            },
            GeneralEnabledFrequencies = new List<double> { 251000000.0 } // Only 251 MHz
        };
        filter.UpdateSelection(selection);

        var packet = CreateTestPacket("pilot-2", 305000000.0); // Different frequency

        // Act
        var result = filter.ShouldPlayPacket(packet);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldPlayPacket_MultipleSelectedPilots_EachHasOwnFilters()
    {
        // Arrange
        var filter = new TacviewAudioFilter();
        var selection = new PilotSelectionMessage
        {
            SelectedPilots = new List<PilotData>
            {
                new() 
                { 
                    PilotId = "pilot-1",
                    PilotName = "Viper 1-1",
                    EnabledFrequencies = new List<double> { 251000000.0 }
                },
                new() 
                { 
                    PilotId = "pilot-2",
                    PilotName = "Viper 1-2",
                    EnabledFrequencies = new List<double> { 305000000.0 }
                }
            },
            GeneralEnabledFrequencies = new List<double>()
        };
        filter.UpdateSelection(selection);

        // Act & Assert
        Assert.True(filter.ShouldPlayPacket(CreateTestPacket("pilot-1", 251000000.0)));
        Assert.False(filter.ShouldPlayPacket(CreateTestPacket("pilot-1", 305000000.0)));
        Assert.True(filter.ShouldPlayPacket(CreateTestPacket("pilot-2", 305000000.0)));
        Assert.False(filter.ShouldPlayPacket(CreateTestPacket("pilot-2", 251000000.0)));
    }

    [Fact]
    public void UpdateSelection_ClearsOldSelection()
    {
        // Arrange
        var filter = new TacviewAudioFilter();
        var selection1 = new PilotSelectionMessage
        {
            SelectedPilots = new List<PilotData>
            {
                new() { PilotId = "pilot-1", PilotName = "Viper 1-1" }
            },
            GeneralEnabledFrequencies = new List<double>()
        };
        filter.UpdateSelection(selection1);

        var selection2 = new PilotSelectionMessage
        {
            SelectedPilots = new List<PilotData>
            {
                new() { PilotId = "pilot-2", PilotName = "Viper 1-2" }
            },
            GeneralEnabledFrequencies = new List<double>()
        };

        // Act
        filter.UpdateSelection(selection2);

        // Assert
        Assert.False(filter.ShouldPlayPacket(CreateTestPacket("pilot-1", 251000000.0)));
        Assert.False(filter.ShouldPlayPacket(CreateTestPacket("pilot-2", 251000000.0)));
    }

    [Fact]
    public void ShouldPlayPacket_EmptyEnabledFrequenciesList_EnablesAllFrequencies()
    {
        // Arrange
        var filter = new TacviewAudioFilter();
        var selection = new PilotSelectionMessage
        {
            SelectedPilots = new List<PilotData>
            {
                new() 
                { 
                    PilotId = "pilot-1",
                    PilotName = "Viper 1-1",
                    EnabledFrequencies = new List<double>() // Empty list
                }
            },
            GeneralEnabledFrequencies = new List<double>()
        };
        filter.UpdateSelection(selection);

        // Act & Assert - should enable all frequencies for selected pilot when list is empty
        Assert.True(filter.ShouldPlayPacket(CreateTestPacket("pilot-1", 251000000.0)));
        Assert.True(filter.ShouldPlayPacket(CreateTestPacket("pilot-1", 305000000.0)));
        Assert.True(filter.ShouldPlayPacket(CreateTestPacket("pilot-1", 127500000.0)));
    }

    [Theory]
    [InlineData(251000000.0)]
    [InlineData(264000000.0)]
    [InlineData(305000000.0)]
    [InlineData(127500000.0)]
    public void ShouldPlayPacket_CommonFrequencies_FilteredCorrectly(double frequency)
    {
        // Arrange
        var filter = new TacviewAudioFilter();
        var selection = new PilotSelectionMessage
        {
            SelectedPilots = new List<PilotData>
            {
                new() 
                { 
                    PilotId = "pilot-1",
                    PilotName = "Viper 1-1",
                    EnabledFrequencies = new List<double> { frequency }
                }
            },
            GeneralEnabledFrequencies = new List<double>()
        };
        filter.UpdateSelection(selection);

        var packet = CreateTestPacket("pilot-1", frequency);

        // Act
        var result = filter.ShouldPlayPacket(packet);

        // Assert
        Assert.True(result);
    }

    private AudioPacketMetadata CreateTestPacket(string transmitterGuid, double frequency)
    {
        var playerInfo = new PlayerInfo
        {
            Name = transmitterGuid,
            TransmitterGuid = transmitterGuid,
            Coalition = 2, // Blue
            Seat = 0,
            AllowRecord = true,
            Position = new Position(),
            AircraftInfo = new AircraftInfo()
        };

        return new AudioPacketMetadata(
            DateTime.UtcNow,                    // Timestamp
            frequency,                          // Frequency
            0,                                  // Modulation
            0,                                  // Encryption
            1,                                  // TransmitterUnitId
            1,                                  // PacketId
            transmitterGuid,                    // TransmitterGuid
            playerInfo,                         // PlayerData
            48000,                              // SampleRate
            1,                                  // ChannelCount
            2,                                  // Coalition
            new byte[] { 0x01, 0x02, 0x03 }     // AudioPayload
        );
    }
}
