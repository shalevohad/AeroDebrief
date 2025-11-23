using Xunit;
using FluentAssertions;
using AeroDebrief.UI.Services;
using AeroDebrief.UI.Services.Visualization.Graphs;
using AeroDebrief.UI.Interfaces.Visualization;
using NLog;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace AeroDebrief.Tests.Phase9.Services
{
    /// <summary>
    /// Tests for ErrorHandlingService (Phase 9).
    /// Tests error reporting, deduplication, event notifications, and basic thread safety.
    /// </summary>
    public class ErrorHandlingServiceTests
    {
        private readonly Logger _logger;

        public ErrorHandlingServiceTests()
        {
            _logger = LogManager.GetCurrentClassLogger();
        }

        [Fact]
        public void Service_CanBeCreated()
        {
            // Arrange & Act
            var service = new ErrorHandlingService(_logger);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public void Service_InitiallyNotVisible()
        {
            // Arrange & Act
            var service = new ErrorHandlingService(_logger);

            // Assert
            Assert.False(service.IsVisible);
        }

        [Fact]
        public void Service_HasRetryCommand()
        {
            // Arrange & Act
            var service = new ErrorHandlingService(_logger);

            // Assert
            Assert.NotNull(service.RetryCommand);
        }

        [Fact]
        public void Service_HasDismissCommand()
        {
            // Arrange & Act
            var service = new ErrorHandlingService(_logger);

            // Assert
            Assert.NotNull(service.DismissCommand);
        }

        [Fact]
        public async Task ShowErrorAsync_UpdatesTitle()
        {
            // Arrange
            var service = new ErrorHandlingService(_logger);
            const string testTitle = "Test Error";
            const string testMessage = "This is a test error message";

            // Act
            var task = service.ShowErrorAsync(testTitle, testMessage);
            
            // Give it a moment to update (it runs on dispatcher)
            await Task.Delay(100);

            // Assert
            Assert.Equal(testTitle, service.Title);
            
            // Cleanup - dismiss the error
            service.DismissCommand.Execute(null);
        }

        [Fact]
        public async Task ShowErrorAsync_UpdatesMessage()
        {
            // Arrange
            var service = new ErrorHandlingService(_logger);
            const string testTitle = "Test Error";
            const string testMessage = "This is a test error message";

            // Act
            var task = service.ShowErrorAsync(testTitle, testMessage);
            await Task.Delay(100);

            // Assert
            Assert.Equal(testMessage, service.Message);
            
            // Cleanup
            service.DismissCommand.Execute(null);
        }

        [Fact]
        public async Task ShowErrorAsync_UpdatesSeverity()
        {
            // Arrange
            var service = new ErrorHandlingService(_logger);

            // Act
            var task = service.ShowErrorAsync("Test", "Message", severity: ErrorSeverity.Warning);
            await Task.Delay(100);

            // Assert
            Assert.Equal(ErrorSeverity.Warning, service.Severity);
            
            // Cleanup
            service.DismissCommand.Execute(null);
        }

        [Fact]
        public async Task ShowErrorAsync_BecomesVisible()
        {
            // Arrange
            var service = new ErrorHandlingService(_logger);

            // Act
            var task = service.ShowErrorAsync("Test", "Message");
            await Task.Delay(100);

            // Assert
            Assert.True(service.IsVisible);
            
            // Cleanup
            service.DismissCommand.Execute(null);
        }

        [Fact]
        public async Task ShowErrorAsync_RaisesErrorShownEvent()
        {
            // Arrange
            var service = new ErrorHandlingService(_logger);
            var eventRaised = false;
            service.ErrorShown += (s, e) => eventRaised = true;

            // Act
            var task = service.ShowErrorAsync("Test", "Message");
            await Task.Delay(100);

            // Assert
            Assert.True(eventRaised);
            
            // Cleanup
            service.DismissCommand.Execute(null);
        }

        [Fact]
        public async Task ShowErrorAsync_DeduplicatesSameError()
        {
            // Arrange
            var service = new ErrorHandlingService(_logger);
            var eventCount = 0;
            service.ErrorShown += (s, e) => eventCount++;

            // Act - Show same error twice quickly
            var task1 = service.ShowErrorAsync("Test", "Message");
            await Task.Delay(50);
            var task2 = service.ShowErrorAsync("Test", "Message");
            await Task.Delay(100);

            // Assert - Should only show once
            Assert.Equal(1, eventCount);
            
            // Cleanup
            service.DismissCommand.Execute(null);
        }

        [Fact]
        public async Task ShowErrorAsync_AllowsDifferentErrors()
        {
            // Arrange
            var service = new ErrorHandlingService(_logger);
            var eventCount = 0;
            service.ErrorShown += (s, e) => eventCount++;

            // Act - Show different errors
            var task1 = service.ShowErrorAsync("Error 1", "Message 1");
            await Task.Delay(100);
            service.DismissCommand.Execute(null);
            await Task.Delay(50);
            
            var task2 = service.ShowErrorAsync("Error 2", "Message 2");
            await Task.Delay(100);

            // Assert - Should show both
            Assert.Equal(2, eventCount);
            
            // Cleanup
            service.DismissCommand.Execute(null);
        }

        [Fact]
        public async Task DismissCommand_HidesError()
        {
            // Arrange
            var service = new ErrorHandlingService(_logger);
            var task = service.ShowErrorAsync("Test", "Message");
            await Task.Delay(100);

            // Act
            service.DismissCommand.Execute(null);
            await Task.Delay(50);

            // Assert
            Assert.False(service.IsVisible);
        }

        [Fact]
        public async Task DismissCommand_RaisesErrorsClearedEvent()
        {
            // Arrange
            var service = new ErrorHandlingService(_logger);
            var task = service.ShowErrorAsync("Test", "Message");
            await Task.Delay(100);

            var eventRaised = false;
            service.ErrorsCleared += (s, e) => eventRaised = true;

            // Act
            service.DismissCommand.Execute(null);
            await Task.Delay(50);

            // Assert
            Assert.True(eventRaised);
        }

        [Fact]
        public void Service_ImplementsINotifyPropertyChanged()
        {
            // Arrange & Act
            var service = new ErrorHandlingService(_logger);

            // Assert
            Assert.IsAssignableFrom<System.ComponentModel.INotifyPropertyChanged>(service);
        }

        [Fact]
        public async Task Service_PropertyChanged_RaisedForTitle()
        {
            // Arrange
            var service = new ErrorHandlingService(_logger);
            var propertyChangedRaised = false;
            service.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(service.Title))
                    propertyChangedRaised = true;
            };

            // Act
            var task = service.ShowErrorAsync("Test Title", "Message");
            await Task.Delay(100);

            // Assert
            Assert.True(propertyChangedRaised);
            
            // Cleanup
            service.DismissCommand.Execute(null);
        }

        [Fact]
        public async Task Service_PropertyChanged_RaisedForIsVisible()
        {
            // Arrange
            var service = new ErrorHandlingService(_logger);
            var propertyChangedCount = 0;
            service.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(service.IsVisible))
                    propertyChangedCount++;
            };

            // Act
            var task = service.ShowErrorAsync("Test", "Message");
            await Task.Delay(100);
            service.DismissCommand.Execute(null);
            await Task.Delay(50);

            // Assert - Should raise twice (show and hide)
            Assert.True(propertyChangedCount >= 2);
        }

        [Fact]
        public async Task ErrorSeverity_UpdatesBrushes()
        {
            // Arrange
            var service = new ErrorHandlingService(_logger);

            // Act - Show error with Warning severity
            var task = service.ShowErrorAsync("Test", "Message", severity: ErrorSeverity.Warning);
            await Task.Delay(100);

            // Assert - Brushes should be set
            Assert.NotNull(service.BackgroundBrush);
            Assert.NotNull(service.BorderBrush);
            Assert.NotNull(service.IconBrush);
            
            // Cleanup
            service.DismissCommand.Execute(null);
        }

        [Fact]
        public async Task ErrorSeverity_UpdatesIconGeometry()
        {
            // Arrange
            var service = new ErrorHandlingService(_logger);

            // Act
            var task = service.ShowErrorAsync("Test", "Message", severity: ErrorSeverity.Error);
            await Task.Delay(100);

            // Assert - Icon geometry should be set
            Assert.NotNull(service.IconGeometry);
            
            // Cleanup
            service.DismissCommand.Execute(null);
        }
    }
}
