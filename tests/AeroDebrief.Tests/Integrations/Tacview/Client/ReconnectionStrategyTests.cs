using System;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Integrations.Tacview.Client;
using AeroDebrief.Integrations.Tacview.Models;
using Xunit;

namespace AeroDebrief.Tests.Integrations.Tacview.Client;

public class ReconnectionStrategyTests
{
    [Fact]
    public async Task TryReconnectAsync_SuccessfulReconnect_ReturnsTrue()
    {
        // Arrange
        var config = new TacviewConfiguration
        {
            Host = "127.0.0.1",
            Port = 52001,
            MaxReconnectAttempts = 3,
            ReconnectIntervalSeconds = 1
        };

        var connectFunc = (CancellationToken ct) => Task.FromResult(true);
        var strategy = new TacviewReconnectionStrategy();

        // Act
        var result = await strategy.TryReconnectAsync(connectFunc, config, CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task TryReconnectAsync_AllAttemptsFail_ReturnsFalse()
    {
        // Arrange
        var config = new TacviewConfiguration
        {
            Host = "127.0.0.1",
            Port = 52001,
            MaxReconnectAttempts = 3,
            ReconnectIntervalSeconds = 1
        };

        var attemptCount = 0;
        Func<CancellationToken, Task<bool>> connectFunc = (ct) =>
        {
            attemptCount++;
            throw new Exception("Connection failed");
        };

        var strategy = new TacviewReconnectionStrategy();

        // Act
        var result = await strategy.TryReconnectAsync(connectFunc, config, CancellationToken.None);

        // Assert
        Assert.False(result);
        Assert.Equal(3, attemptCount);
    }

    [Fact]
    public async Task TryReconnectAsync_SucceedsOnSecondAttempt_ReturnsTrue()
    {
        // Arrange
        var config = new TacviewConfiguration
        {
            Host = "127.0.0.1",
            Port = 52001,
            MaxReconnectAttempts = 5,
            ReconnectIntervalSeconds = 1
        };

        var attemptCount = 0;
        Func<CancellationToken, Task<bool>> connectFunc = (ct) =>
        {
            attemptCount++;
            if (attemptCount < 2)
                throw new Exception("Connection failed");
            return Task.FromResult(true);
        };

        var strategy = new TacviewReconnectionStrategy();

        // Act
        var result = await strategy.TryReconnectAsync(connectFunc, config, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task TryReconnectAsync_CancellationRequested_ReturnsFalse()
    {
        // Arrange
        var config = new TacviewConfiguration
        {
            Host = "127.0.0.1",
            Port = 52001,
            MaxReconnectAttempts = 10,
            ReconnectIntervalSeconds = 5
        };

        Func<CancellationToken, Task<bool>> connectFunc = (ct) =>
        {
            throw new Exception("Connection failed");
        };

        var strategy = new TacviewReconnectionStrategy();
        var cts = new CancellationTokenSource();

        // Cancel after 2 seconds
        cts.CancelAfter(2000);

        // Act
        var result = await strategy.TryReconnectAsync(connectFunc, config, cts.Token);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task TryReconnectAsync_ExponentialBackoff_IncreasesDelay()
    {
        // Arrange
        var config = new TacviewConfiguration
        {
            Host = "127.0.0.1",
            Port = 52001,
            MaxReconnectAttempts = 3,
            ReconnectIntervalSeconds = 1
        };

        Func<CancellationToken, Task<bool>> connectFunc = (ct) =>
        {
            throw new Exception("Connection failed");
        };

        var strategy = new TacviewReconnectionStrategy();
        var startTime = DateTime.UtcNow;

        // Act
        await strategy.TryReconnectAsync(connectFunc, config, CancellationToken.None);

        var totalTime = (DateTime.UtcNow - startTime).TotalSeconds;

        // Assert
        // With exponential backoff: 1s + 2s + 3s = 6s minimum
        Assert.True(totalTime >= 5, $"Expected at least 5 seconds with backoff, got {totalTime}");
    }

    [Fact]
    public async Task TryReconnectAsync_MaxDelayCapAt60Seconds_DoesNotExceedCap()
    {
        // Arrange
        var config = new TacviewConfiguration
        {
            Host = "127.0.0.1",
            Port = 52001,
            MaxReconnectAttempts = 2, // Only 2 attempts to keep test fast
            ReconnectIntervalSeconds = 50 // Large base interval to hit 60s cap
        };

        Func<CancellationToken, Task<bool>> connectFunc = (ct) =>
        {
            throw new Exception("Connection failed");
        };

        var strategy = new TacviewReconnectionStrategy();
        var startTime = DateTime.UtcNow;

        // Act
        await strategy.TryReconnectAsync(connectFunc, config, CancellationToken.None);

        var totalTime = (DateTime.UtcNow - startTime).TotalSeconds;

        // Assert
        // With cap at 60s: attempt 1: 50s, attempt 2: 60s (capped from 100s)
        // Total should be around 110s, but at least 50s
        Assert.True(totalTime >= 50, $"Expected at least 50 seconds, got {totalTime}");
        Assert.True(totalTime <= 125, $"Expected at most 125 seconds (with some margin), got {totalTime}");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task TryReconnectAsync_RespectsMaxAttempts(int maxAttempts)
    {
        // Arrange
        var config = new TacviewConfiguration
        {
            Host = "127.0.0.1",
            Port = 52001,
            MaxReconnectAttempts = maxAttempts,
            ReconnectIntervalSeconds = 1
        };

        var attemptCount = 0;
        Func<CancellationToken, Task<bool>> connectFunc = (ct) =>
        {
            attemptCount++;
            throw new Exception("Connection failed");
        };

        var strategy = new TacviewReconnectionStrategy();

        // Act
        var result = await strategy.TryReconnectAsync(connectFunc, config, CancellationToken.None);

        // Assert
        Assert.False(result);
        Assert.Equal(maxAttempts, attemptCount);
    }

    [Fact]
    public async Task TryReconnectAsync_ZeroMaxAttempts_InfiniteRetries()
    {
        // Arrange - MaxReconnectAttempts = 0 means infinite retries
        var config = new TacviewConfiguration
        {
            Host = "127.0.0.1",
            Port = 52001,
            MaxReconnectAttempts = 0, // 0 = infinite
            ReconnectIntervalSeconds = 1
        };

        var attemptCount = 0;
        Func<CancellationToken, Task<bool>> connectFunc = (ct) =>
        {
            attemptCount++;
            // Succeed after 3 attempts to avoid infinite loop
            if (attemptCount >= 3)
                return Task.FromResult(true);
            throw new Exception("Connection failed");
        };

        var strategy = new TacviewReconnectionStrategy();

        // Act
        var result = await strategy.TryReconnectAsync(connectFunc, config, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.True(attemptCount >= 3, "Should keep retrying indefinitely until success");
    }
}
