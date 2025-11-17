using Xunit;
using FluentAssertions;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Services.Graphs;
using AeroDebrief.UI.Services;
using AeroDebrief.Tests.TestHelpers;
using System;
using System.Threading.Tasks;

namespace AeroDebrief.Tests.Phase9.ViewModels
{
    /// <summary>
    /// Tests for UnifiedGraphViewModel Phase 9 features.
    /// Tests loading indicators, error handling, performance monitoring, and related commands.
    /// </summary>
    public class UnifiedGraphViewModelPhase9Tests
    {
        private UnifiedGraphViewModel CreateViewModel(
            IAmplitudeSeriesProvider? provider = null,
            IDataTileCache? cache = null,
            IDataTileManager? tileManager = null,
            IErrorHandlingService? errorHandler = null)
        {
            provider ??= new MockAmplitudeSeriesProvider();
            return new UnifiedGraphViewModel(provider, cache, null, tileManager, errorHandler);
        }

        [Fact]
        public void IsLoadingTiles_InitiallyFalse()
        {
            // Arrange & Act
            var vm = CreateViewModel();

            // Assert
            Assert.False(vm.IsLoadingTiles);
        }

        [Fact]
        public void LoadingStatusText_HasDefaultValue()
        {
            // Arrange & Act
            var vm = CreateViewModel();

            // Assert
            Assert.NotNull(vm.LoadingStatusText);
            Assert.Contains("Loading", vm.LoadingStatusText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CancelLoadingCommand_Exists()
        {
            // Arrange & Act
            var vm = CreateViewModel();

            // Assert
            Assert.NotNull(vm.CancelLoadingCommand);
        }

        [Fact]
        public void CancelLoadingCommand_CannotExecute_WhenNotLoading()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            var canExecute = vm.CancelLoadingCommand.CanExecute(null);

            // Assert
            Assert.False(canExecute);
        }

        [Fact]
        public void ShowPerformanceStats_InitiallyFalse()
        {
            // Arrange & Act
            var vm = CreateViewModel();

            // Assert
            Assert.False(vm.ShowPerformanceStats);
        }

        [Fact]
        public void ShowPerformanceStats_CanBeToggled()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.ShowPerformanceStats = true;

            // Assert
            Assert.True(vm.ShowPerformanceStats);
        }

        [Fact]
        public void ShowPerformanceStats_RaisesPropertyChanged()
        {
            // Arrange
            var vm = CreateViewModel();
            var propertyChangedRaised = false;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(vm.ShowPerformanceStats))
                    propertyChangedRaised = true;
            };

            // Act
            vm.ShowPerformanceStats = true;

            // Assert
            Assert.True(propertyChangedRaised);
        }

        [Fact]
        public void MemoryUsageMB_InitiallyZero()
        {
            // Arrange & Act
            var vm = CreateViewModel();

            // Assert
            Assert.Equal(0.0, vm.MemoryUsageMB);
        }

        [Fact]
        public void CurrentFPS_InitiallyZero()
        {
            // Arrange & Act
            var vm = CreateViewModel();

            // Assert
            Assert.Equal(0.0, vm.CurrentFPS);
        }

        [Fact]
        public void LastLoadTimeMs_InitiallyZero()
        {
            // Arrange & Act
            var vm = CreateViewModel();

            // Assert
            Assert.Equal(0.0, vm.LastLoadTimeMs);
        }

        [Fact]
        public void ViewModel_WithErrorHandler_IntegratesCorrectly()
        {
            // Arrange
            var errorHandler = new ErrorHandlingService(NLog.LogManager.GetCurrentClassLogger());

            // Act
            var vm = CreateViewModel(errorHandler: errorHandler);

            // Assert
            Assert.NotNull(vm);
        }

        [Fact]
        public async Task Dispose_CleansUpResources()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.Dispose();
            await Task.Delay(50); // Allow cleanup

            // Assert - Should not throw
            Assert.NotNull(vm);
        }
    }
}
