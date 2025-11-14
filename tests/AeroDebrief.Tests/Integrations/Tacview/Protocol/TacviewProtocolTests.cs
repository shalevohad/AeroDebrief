using System;
using System.Text.Json;
using AeroDebrief.Integrations.Tacview.Protocol;
using AeroDebrief.Integrations.Tacview.Protocol.Messages;
using Xunit;

namespace AeroDebrief.Tests.Integrations.Tacview.Protocol;

public class TacviewProtocolTests
{
    [Fact]
    public void SerializeMessage_TimeUpdateMessage_ProducesValidJson()
    {
        // Arrange
        var message = new TimeUpdateMessage
        {
            MissionTimeUtc = "2024-01-15T14:30:45.000Z",
            PlaybackState = "playing",
            PlaybackSpeed = 1.0
        };

        // Act
        var json = TacviewProtocol.SerializeMessage(message);

        // Assert
        Assert.Contains("\"type\":\"time_update\"", json);
        Assert.Contains("\"mission_time_utc\":\"2024-01-15T14:30:45.000Z\"", json);
        Assert.Contains("\"playback_state\":\"playing\"", json);
        Assert.Contains("\"playback_speed\":1", json);
        Assert.EndsWith("\n", json); // Should have newline delimiter
    }

    [Fact]
    public void ParseMessage_TimeUpdateMessage_DeserializesCorrectly()
    {
        // Arrange
        var json = "{\"type\":\"time_update\",\"mission_time_utc\":\"2024-01-15T14:30:45.000Z\",\"playback_state\":\"playing\",\"playback_speed\":2.0}\n";

        // Act
        var message = TacviewProtocol.ParseMessage(json);

        // Assert
        Assert.IsType<TimeUpdateMessage>(message);
        var timeUpdate = (TimeUpdateMessage)message;
        Assert.Equal("2024-01-15T14:30:45.000Z", timeUpdate.MissionTimeUtc);
        Assert.Equal("playing", timeUpdate.PlaybackState);
        Assert.Equal(2.0, timeUpdate.PlaybackSpeed);
    }

    [Fact]
    public void ParseMessage_PilotSelectionMessage_DeserializesCorrectly()
    {
        // Arrange
        var json = @"{
            ""type"": ""pilot_selection"",
            ""selected_pilots"": [
                {
                    ""pilot_id"": ""test-1"",
                    ""pilot_name"": ""Viper 1-1"",
                    ""coalition"": ""blue"",
                    ""unit_type"": ""F-16C"",
                    ""frequencies"": [251000000.0, 305000000.0],
                    ""enabled_frequencies"": [251000000.0],
                    ""pan"": -0.5
                }
            ],
            ""pan_mode"": ""manual"",
            ""general_enabled_frequencies"": []
        }
";

        // Act
        var message = TacviewProtocol.ParseMessage(json);

        // Assert
        Assert.IsType<PilotSelectionMessage>(message);
        var pilotSelection = (PilotSelectionMessage)message;
        Assert.Single(pilotSelection.SelectedPilots);
        Assert.Equal("test-1", pilotSelection.SelectedPilots[0].PilotId);
        Assert.Equal("Viper 1-1", pilotSelection.SelectedPilots[0].PilotName);
        Assert.Equal("manual", pilotSelection.PanMode);
        Assert.Equal(-0.5, pilotSelection.SelectedPilots[0].Pan);
    }

    [Fact]
    public void ParseMessage_PlaybackCommandMessage_DeserializesCorrectly()
    {
        // Arrange
        var json = "{\"type\":\"playback_command\",\"command\":\"pause\"}\n";

        // Act
        var message = TacviewProtocol.ParseMessage(json);

        // Assert
        Assert.IsType<PlaybackCommandMessage>(message);
        var command = (PlaybackCommandMessage)message;
        Assert.Equal("pause", command.Command);
    }

    [Fact]
    public void ParseMessage_SeekMessage_DeserializesCorrectly()
    {
        // Arrange
        var json = "{\"type\":\"seek\",\"target_time_utc\":\"2024-01-15T14:35:00.000Z\"}\n";

        // Act
        var message = TacviewProtocol.ParseMessage(json);

        // Assert
        Assert.IsType<SeekMessage>(message);
        var seek = (SeekMessage)message;
        Assert.Equal("2024-01-15T14:35:00.000Z", seek.TargetTimeUtc);
    }

    [Fact]
    public void SerializeMessage_SpeakingStatusMessage_ProducesValidJson()
    {
        // Arrange
        var message = new SpeakingStatusMessage
        {
            PilotId = "test-pilot",
            PilotName = "Viper 1-1",
            Frequency = 251000000.0,
            IsSpeaking = true,
            TimestampUtc = "2024-01-15T14:30:45.000Z"
        };

        // Act
        var json = TacviewProtocol.SerializeMessage(message);

        // Assert
        Assert.Contains("\"type\":\"speaking_status\"", json);
        Assert.Contains("\"pilot_id\":\"test-pilot\"", json);
        Assert.Contains("\"frequency\":251000000", json);
        Assert.Contains("\"is_speaking\":true", json);
    }

    [Fact]
    public void SerializeMessage_FrequencyFilterUpdateMessage_ProducesValidJson()
    {
        // Arrange
        var message = new FrequencyFilterUpdateMessage
        {
            PilotId = "test-pilot",
            EnabledFrequencies = new System.Collections.Generic.List<double> { 251000000.0, 305000000.0 }
        };

        // Act
        var json = TacviewProtocol.SerializeMessage(message);

        // Assert
        Assert.Contains("\"type\":\"frequency_filter_update\"", json);
        Assert.Contains("\"pilot_id\":\"test-pilot\"", json);
        Assert.Contains("\"enabled_frequencies\":[251000000,305000000]", json);
    }

    [Fact]
    public void SerializeMessage_PanConfigurationMessage_ProducesValidJson()
    {
        // Arrange
        var message = new PanConfigurationMessage
        {
            PanMode = "manual",
            PilotPanSettings = new System.Collections.Generic.Dictionary<string, double>
            {
                { "pilot-1", -0.5 },
                { "pilot-2", 0.5 }
            }
        };

        // Act
        var json = TacviewProtocol.SerializeMessage(message);

        // Assert
        Assert.Contains("\"type\":\"pan_configuration\"", json);
        Assert.Contains("\"pan_mode\":\"manual\"", json);
        Assert.Contains("\"pilot_pan_settings\"", json);
    }

    [Fact]
    public void ParseMessage_InvalidJson_ReturnsNull()
    {
        // Arrange
        var json = "{ invalid json }";

        // Act
        var message = TacviewProtocol.ParseMessage(json);

        // Assert
        Assert.Null(message);
    }

    [Fact]
    public void ParseMessage_UnknownMessageType_ReturnsNull()
    {
        // Arrange
        var json = "{\"type\":\"unknown_message_type\",\"data\":\"test\"}\n";

        // Act
        var message = TacviewProtocol.ParseMessage(json);

        // Assert
        Assert.Null(message);
    }

    [Fact]
    public void ParseMessage_MissingTypeField_ReturnsNull()
    {
        // Arrange
        var json = "{\"data\":\"test\"}\n";

        // Act
        var message = TacviewProtocol.ParseMessage(json);

        // Assert
        Assert.Null(message);
    }

    [Theory]
    [InlineData("time_update")]
    [InlineData("pilot_selection")]
    [InlineData("playback_command")]
    [InlineData("seek")]
    [InlineData("speaking_status")]
    [InlineData("sync_status")]
    [InlineData("ready")]
    public void ParseMessage_AllValidMessageTypes_DeserializeWithoutError(string messageType)
    {
        // Arrange
        var json = CreateMinimalValidJson(messageType);

        // Act
        var message = TacviewProtocol.ParseMessage(json);

        // Assert
        Assert.NotNull(message);
    }

    [Fact]
    public void SerializeDeserialize_RoundTrip_PreservesData()
    {
        // Arrange
        var original = new TimeUpdateMessage
        {
            MissionTimeUtc = "2024-01-15T14:30:45.123Z",
            PlaybackState = "playing",
            PlaybackSpeed = 1.5
        };

        // Act
        var json = TacviewProtocol.SerializeMessage(original);
        var deserialized = TacviewProtocol.ParseMessage(json) as TimeUpdateMessage;

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.MissionTimeUtc, deserialized.MissionTimeUtc);
        Assert.Equal(original.PlaybackState, deserialized.PlaybackState);
        Assert.Equal(original.PlaybackSpeed, deserialized.PlaybackSpeed);
    }

    private string CreateMinimalValidJson(string messageType)
    {
        return messageType switch
        {
            "time_update" => "{\"type\":\"time_update\",\"mission_time_utc\":\"2024-01-15T14:30:45.000Z\",\"playback_state\":\"playing\",\"playback_speed\":1.0}\n",
            "pilot_selection" => "{\"type\":\"pilot_selection\",\"selected_pilots\":[],\"pan_mode\":\"auto\",\"general_enabled_frequencies\":[]}\n",
            "playback_command" => "{\"type\":\"playback_command\",\"command\":\"play\"}\n",
            "seek" => "{\"type\":\"seek\",\"target_time_utc\":\"2024-01-15T14:30:45.000Z\"}\n",
            "speaking_status" => "{\"type\":\"speaking_status\",\"pilot_id\":\"test\",\"frequency\":251000000.0,\"is_speaking\":true,\"timestamp_utc\":\"2024-01-15T14:30:45.000Z\"}\n",
            "sync_status" => "{\"type\":\"sync_status\",\"sync_quality\":100.0,\"drift_ms\":0}\n",
            "ready" => "{\"type\":\"ready\",\"client_version\":\"1.0.0\"}\n",
            _ => "{}"
        };
    }
}
