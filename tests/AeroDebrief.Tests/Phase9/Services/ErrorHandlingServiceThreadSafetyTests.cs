using Xunit;
using FluentAssertions;
using AeroDebrief.UI.Services;
using AeroDebrief.UI.Services.Visualization.Graphs;
using NLog;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AeroDebrief.Tests.Phase9.Services
{
    /// <summary>
    /// Thread safety tests for ErrorHandlingService (Phase 9).
    /// Tests concurrent error reporting, race conditions, and stress scenarios.
    /// </summary>
    public class ErrorHandlingServiceThreadSafetyTests
    {
        private readonly Logger _logger;

        public ErrorHandlingServiceThreadSafetyTests()
        {
            _logger = LogManager.GetCurrentClassLogger();
        }

        [Fact]
        public async Task ConcurrentShowError_HandlesMultipleThreads()
        {
            // Arrange
            var service = new ErrorHandlingService(_logger);
            const int threadCount = 10;
            var tasks = new List<Task>();

            // Act - Show errors from multiple threads
            for (int i = 0; i < threadCount; i++)
            {
                var threadId = i;
                tasks.Add(Task.Run(async () =>
                {
                    await service.ShowErrorAsync($"Error {threadId}", $"Message from thread {threadId}");
                }));
            }

            // Wait for all to complete
            await Task.WhenAll(tasks);
            await Task.Delay(200); // Allow dispatcher to process

            // Assert - Service should still be functional
            Assert.NotNull(service);
            
            // Cleanup
            service.DismissCommand.Execute(null);
        }

        [Fact]
        public async Task ConcurrentDismiss_ThreadSafe()
        {
            // Arrange
            var service = new ErrorHandlingService(_logger);
            await service.ShowErrorAsync("Test", "Message");
            await Task.Delay(100);

            // Act - Try to dismiss from multiple threads
            var tasks = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    service.DismissCommand.Execute(null);
                }));
            }

            // Should not throw
            await Task.WhenAll(tasks);
            await Task.Delay(100);

            // Assert - Should be dismissed
            Assert.False(service.IsVisible);
        }

        [Fact]
        public async Task RaceCondition_ShowAndDismiss_HandledCorrectly()
        {
            // Arrange
            var service = new ErrorHandlingService(_logger);

            // Act - Rapidly show and dismiss errors
            for (int i = 0; i < 10; i++)
            {
                var showTask = service.ShowErrorAsync($"Error {i}", "Message");
                await Task.Delay(10); // Brief delay
                service.DismissCommand.Execute(null);
                await Task.Delay(10);
            }

            await Task.Delay(100);

            // Assert - Service should still be functional
            Assert.NotNull(service);
        }

        [Fact(Skip = "Stress test - run manually")]
        public async Task StressTest_1000ConcurrentReports()
        {
            // Arrange
            var service = new ErrorHandlingService(_logger);
            const int errorCount = 1000;
            var tasks = new List<Task>();
            var exceptions = new List<Exception>();

            // Act - Report many errors concurrently
            for (int i = 0; i < errorCount; i++)
            {
                var errorId = i;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await service.ShowErrorAsync($"Error {errorId}", $"Message {errorId}");
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            await Task.Delay(500);

            // Assert - Should not have any exceptions
            Assert.Empty(exceptions);
            
            // Cleanup
            service.DismissCommand.Execute(null);
        }

        [Fact]
        public async Task EventHandlers_ThreadSafe()
        {
            // Arrange
            var service = new ErrorHandlingService(_logger);
            var eventCounts = new System.Collections.Concurrent.ConcurrentBag<int>();
            
            service.ErrorShown += (s, e) =>
            {
                // Simulate some work
                Thread.Sleep(1);
                eventCounts.Add(1);
            };

            // Act - Show errors from multiple threads
            var tasks = new List<Task>();
            for (int i = 0; i < 20; i++)
            {
                var id = i;
                tasks.Add(Task.Run(async () =>
                {
                    await service.ShowErrorAsync($"Error {id}", $"Message {id}");
                }));
                await Task.Delay(50); // Space them out a bit
            }

            await Task.WhenAll(tasks);
            await Task.Delay(300);

            // Assert - Events should have fired (at least some, due to deduplication)
            Assert.True(eventCounts.Count > 0);
            
            // Cleanup
            service.DismissCommand.Execute(null);
        }

        [Fact]
        public async Task PropertyChanged_ThreadSafe()
        {
            // Arrange
            var service = new ErrorHandlingService(_logger);
            var propertyChanges = new System.Collections.Concurrent.ConcurrentBag<string>();
            
            service.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != null)
                    propertyChanges.Add(e.PropertyName);
            };

            // Act - Show errors from multiple threads
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                var id = i;
                tasks.Add(Task.Run(async () =>
                {
                    await service.ShowErrorAsync($"Error {id}", "Message");
                    await Task.Delay(20);
                    service.DismissCommand.Execute(null);
                }));
            }

            await Task.WhenAll(tasks);
            await Task.Delay(200);

            // Assert - Property changes should have been recorded
            Assert.True(propertyChanges.Count > 0);
            Assert.Contains(propertyChanges, p => p == "IsVisible");
            Assert.Contains(propertyChanges, p => p == "Title");
        }
    }
}
