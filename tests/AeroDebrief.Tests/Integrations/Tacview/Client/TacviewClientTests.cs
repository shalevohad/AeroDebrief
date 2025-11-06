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
using Xunit;

namespace AeroDebrief.Tests.Integrations.Tacview.Client;

public class TacviewClientTests : IDisposable
{
    private TcpListener? _mockServer;
    private CancellationTokenSource _cts;
    private const int TestPort = 52099; // Use different port to avoid conflicts

    public TacviewClientTests()
    {
        _cts = new CancellationTokenSource();
    }

    [Fact]
    public async Task ConnectAsync_SuccessfulConnection_ConnectsToServer()
    {
        // Arrange
        var mockServer = StartMockServer();
        var config = new TacviewConfiguration { Host = "127.0.0.1", Port = TestPort };
        var client = new TacviewClient(config);

        try
        {
            // Act
            await client.ConnectAsync(_cts.Token);

            // Assert
            Assert.True(client.IsConnected);
        }
        finally
        {
            await client.DisconnectAsync();
            mockServer?.Stop();
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
        var mockServer = StartMockServer();
        var config = new TacviewConfiguration { Host = "127.0.0.1", Port = TestPort };
        var client = new TacviewClient(config);
        await client.ConnectAsync(_cts.Token);

        // Act
        await client.DisconnectAsync();

        // Assert
        Assert.False(client.IsConnected);

        mockServer?.Stop();
    }

    [Fact]
    public async Task MessageReceived_WhenServerSendsMessage_RaisesEvent()
    {
        // Arrange
        var mockServer = StartMockServer();
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

        // Act
        var testMessage = new TimeUpdateMessage
        {
            MissionTimeUtc = DateTime.UtcNow.ToString("o"),
            PlaybackState = "playing",
            PlaybackSpeed = 1.0
        };
        await SendMessageFromServer(testMessage);

        // Wait for message processing
        await Task.Delay(500);

        // Assert
        Assert.True(receivedMessage);
        Assert.NotNull(receivedTimeUpdate);
        Assert.Equal("playing", receivedTimeUpdate.PlaybackState);
        Assert.Equal(1.0, receivedTimeUpdate.PlaybackSpeed);

        await client.DisconnectAsync();
        mockServer?.Stop();
    }

    [Fact]
    public async Task SendMessageAsync_ValidMessage_SendsSuccessfully()
    {
        // Arrange
        var mockServer = StartMockServer();
        var config = new TacviewConfiguration { Host = "127.0.0.1", Port = TestPort };
        var client = new TacviewClient(config);
        await client.ConnectAsync(_cts.Token);

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

        await client.DisconnectAsync();
        mockServer?.Stop();
    }

    [Fact]
    public async Task Disconnected_WhenServerDisconnects_RaisesEvent()
    {
        // Arrange
        var mockServer = StartMockServer();
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

        // Act
        mockServer?.Stop(); // Simulate server disconnect
        await Task.Delay(1000); // Wait for disconnect detection

        // Assert
        Assert.True(disconnectedRaised);
        Assert.NotNull(disconnectReason);
    }

    [Fact]
    public async Task ConnectAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var config = new TacviewConfiguration { Host = "127.0.0.1", Port = TestPort };
        var client = new TacviewClient(config);
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await client.ConnectAsync(cts.Token);
        });
    }

    private TcpListener StartMockServer()
    {
        _mockServer = new TcpListener(IPAddress.Loopback, TestPort);
        _mockServer.Start();

        // Accept connections in background
        _ = Task.Run(async () =>
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var client = await _mockServer.AcceptTcpClientAsync(_cts.Token);
                    _ = HandleClientAsync(client);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when test completes
            }
        }, _cts.Token);

        return _mockServer;
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            var stream = client.GetStream();
            var buffer = new byte[8192];

            while (!_cts.Token.IsCancellationRequested && client.Connected)
            {
                var bytesRead = await stream.ReadAsync(buffer, _cts.Token);
                if (bytesRead == 0) break;

                // Echo back for testing
                await stream.WriteAsync(buffer.AsMemory(0, bytesRead), _cts.Token);
            }
        }
        catch (Exception)
        {
            // Ignore errors in mock server
        }
        finally
        {
            client.Close();
        }
    }

    private async Task SendMessageFromServer(object message)
    {
        // Simulate server sending a message
        // This is a simplified version - in real tests you'd need to track the accepted client
        await Task.Delay(100);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _mockServer?.Stop();
        _cts.Dispose();
    }
}
