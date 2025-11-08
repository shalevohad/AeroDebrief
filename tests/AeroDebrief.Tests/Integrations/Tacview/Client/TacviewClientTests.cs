using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Integrations.Tacview.Client;
using AeroDebrief.Integrations.Tacview.Models;
using AeroDebrief.Integrations.Tacview.Protocol.Messages;
using AeroDebrief.Tests.TestHelpers;
using Xunit;

namespace AeroDebrief.Tests.Integrations.Tacview.Client;

public class TacviewClientTests : IDisposable
{
    private MockTacviewServer? _mockServer;
    private CancellationTokenSource _cts;
    private const int TestPort = 52199; // Use different port to avoid conflicts

    public TacviewClientTests()
    {
        _cts = new CancellationTokenSource();
    }

    [Fact]
    public async Task ConnectAsync_SuccessfulConnection_ConnectsToServer()
    {
        // Arrange
        _mockServer = new MockTacviewServer(TestPort);
        _mockServer.Start();
        
        var config = new TacviewConfiguration { Host = "127.0.0.1", Port = TestPort };
        var client = new TacviewClient(config);

        try
        {
            // Act
            await client.ConnectAsync(_cts.Token);
            
            // Wait for server to fully accept connection
            var clientConnected = await _mockServer.WaitForClientAsync();

            // Assert
            Assert.True(clientConnected, "Client failed to connect to mock server");
            Assert.True(client.IsConnected);
            Assert.Equal(1, _mockServer.ConnectedClientCount);
        }
        finally
        {
            await client.DisconnectAsync();
            _mockServer.Stop();
        }
    }

    [Fact]
    public async Task ConnectAsync_ServerNotRunning_ThrowsException()
    {
        // Arrange
        var config = new TacviewConfiguration { Host = "127.0.0.1", Port = TestPort };
        var client = new TacviewClient(config);

        // Act & Assert
        await Assert.ThrowsAsync<SocketException>(async () =>
        {
            await client.ConnectAsync(_cts.Token);
        });
    }

    [Fact]
    public async Task DisconnectAsync_WhenConnected_DisconnectsCleanly()
    {
        // Arrange
        _mockServer = new MockTacviewServer(TestPort);
        _mockServer.Start();
        
        var config = new TacviewConfiguration { Host = "127.0.0.1", Port = TestPort };
        var client = new TacviewClient(config);
        await client.ConnectAsync(_cts.Token);
        
        // Wait for server to fully accept connection
        await _mockServer.WaitForClientAsync();

        // Act
        await client.DisconnectAsync();
        
        // Give server time to process disconnect
        await Task.Delay(200);

        // Assert
        Assert.False(client.IsConnected);
        Assert.Equal(0, _mockServer.ConnectedClientCount);

        _mockServer.Stop();
    }

    [Fact]
    public async Task MessageReceived_WhenServerSendsMessage_RaisesEvent()
    {
        // Arrange
        _mockServer = new MockTacviewServer(TestPort);
        _mockServer.Start();
        
        var config = new TacviewConfiguration { Host = "127.0.0.1", Port = TestPort };
        var client = new TacviewClient(config);
        var receivedMessage = false;
        TimeUpdateMessage? receivedTimeUpdate = null;

        client.MessageReceived += (sender, message) =>
        {
            receivedMessage = true;
            receivedTimeUpdate = message as TimeUpdateMessage;
        };

        await client.ConnectAsync(_cts.Token);
        
        // Wait for server to fully accept client and client receive loop to be ready
        var clientConnected = await _mockServer.WaitForClientAsync();
        Assert.True(clientConnected, "Client failed to connect to mock server");

        // Act
        var testMessage = new TimeUpdateMessage
        {
            MissionTimeUtc = DateTime.UtcNow.ToString("o"),
            PlaybackState = "playing",
            PlaybackSpeed = 1.0
        };
        await _mockServer.SendMessageToAllAsync(testMessage);

        // Wait for message processing
        await Task.Delay(500);

        // Assert
        Assert.True(receivedMessage, "Message was not received by client");
        Assert.NotNull(receivedTimeUpdate);
        Assert.Equal("playing", receivedTimeUpdate.PlaybackState);
        Assert.Equal(1.0, receivedTimeUpdate.PlaybackSpeed);

        await client.DisconnectAsync();
        _mockServer.Stop();
    }

    [Fact]
    public async Task SendMessageAsync_ValidMessage_SendsSuccessfully()
    {
        // Arrange
        _mockServer = new MockTacviewServer(TestPort);
        _mockServer.Start();
        
        var config = new TacviewConfiguration { Host = "127.0.0.1", Port = TestPort };
        var client = new TacviewClient(config);
        await client.ConnectAsync(_cts.Token);
        
        // Wait for server to fully accept connection
        await _mockServer.WaitForClientAsync();

        var message = new SpeakingStatusMessage
        {
            PilotId = "test-pilot",
            Frequency = 251000000.0,
            IsSpeaking = true,
            TimestampUtc = DateTime.UtcNow.ToString("o")
        };

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            await client.SendMessageAsync(message, _cts.Token);
        });

        // Assert
        Assert.Null(exception);
        
        // Wait for server to receive message
        await Task.Delay(200);
        
        // Verify server received the message
        Assert.NotEmpty(_mockServer.ReceivedMessages);

        await client.DisconnectAsync();
        _mockServer.Stop();
    }

    [Fact]
    public async Task Disconnected_WhenServerDisconnects_RaisesEvent()
    {
        // Arrange
        _mockServer = new MockTacviewServer(TestPort);
        _mockServer.Start();
        
        var config = new TacviewConfiguration { Host = "127.0.0.1", Port = TestPort };
        var client = new TacviewClient(config);
        var disconnectedRaised = false;
        string? disconnectReason = null;

        client.Disconnected += (sender, reason) =>
        {
            disconnectedRaised = true;
            disconnectReason = reason;
        };

        await client.ConnectAsync(_cts.Token);
        
        // Wait for server to fully accept connection
        await _mockServer.WaitForClientAsync();

        // Act
        _mockServer.DisconnectAllClients(); // Simulate server disconnect
        await Task.Delay(1000); // Wait for disconnect detection

        // Assert
        Assert.True(disconnectedRaised);
        Assert.NotNull(disconnectReason);
        
        _mockServer.Stop();
    }

    [Fact]
    public async Task ConnectAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var config = new TacviewConfiguration { Host = "127.0.0.1", Port = TestPort };
        var client = new TacviewClient(config);
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert - TaskCanceledException inherits from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await client.ConnectAsync(cts.Token);
        });
    }

    public void Dispose()
    {
        _cts.Cancel();
        _mockServer?.Dispose();
        _cts.Dispose();
    }
}
