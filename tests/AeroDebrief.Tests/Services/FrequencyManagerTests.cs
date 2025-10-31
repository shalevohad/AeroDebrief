using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AeroDebrief.Core.IO;
using AeroDebrief.Core.Models;
using AeroDebrief.Core.Playback;
using AeroDebrief.UI.Services;
using Xunit;

namespace AeroDebrief.Tests.Services
{
    /// <summary>
    /// Unit tests for FrequencyManager service.
    /// Demonstrates how Separation of Concerns makes testing easier.
    /// </summary>
    public class FrequencyManagerTests : IDisposable
    {
        private FrequencyManager _manager;

        public FrequencyManagerTests()
        {
            _manager = new FrequencyManager();
        }

        [Fact]
        public void Constructor_InitializesEmptyCollections()
        {
            // Assert
            Assert.Empty(_manager.Frequencies);
            Assert.Empty(_manager.SelectedFrequencies);
        }

        [Fact]
        public void SelectFrequency_AddsToSelectedSet()
        {
            // Arrange
            var frequency = 251000000.0; // 251 MHz

            // Act
            _manager.SelectFrequency(frequency);

            // Assert
            Assert.Contains(frequency, _manager.SelectedFrequencies);
        }

        [Fact]
        public void SelectFrequency_RaisesSelectionChangedEvent()
        {
            // Arrange
            var frequency = 251000000.0;
            bool eventRaised = false;
            double? eventFrequency = null;
            bool? eventIsSelected = null;

            _manager.SelectionChanged += (s, e) =>
            {
                eventRaised = true;
                eventFrequency = e.Frequency;
                eventIsSelected = e.IsSelected;
            };

            // Act
            _manager.SelectFrequency(frequency);

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(frequency, eventFrequency);
            Assert.True(eventIsSelected);
        }

        [Fact]
        public void DeselectFrequency_RemovesFromSelectedSet()
        {
            // Arrange
            var frequency = 251000000.0;
            _manager.SelectFrequency(frequency);

            // Act
            _manager.DeselectFrequency(frequency);

            // Assert
            Assert.DoesNotContain(frequency, _manager.SelectedFrequencies);
        }

        [Fact]
        public void DeselectFrequency_RaisesSelectionChangedEvent()
        {
            // Arrange
            var frequency = 251000000.0;
            _manager.SelectFrequency(frequency);

            bool eventRaised = false;
            bool? eventIsSelected = null;

            _manager.SelectionChanged += (s, e) =>
            {
                eventRaised = true;
                eventIsSelected = e.IsSelected;
            };

            // Act
            _manager.DeselectFrequency(frequency);

            // Assert
            Assert.True(eventRaised);
            Assert.False(eventIsSelected);
        }

        [Fact]
        public void SelectFrequency_WhenAlreadySelected_DoesNotRaiseEvent()
        {
            // Arrange
            var frequency = 251000000.0;
            _manager.SelectFrequency(frequency);

            int eventCount = 0;
            _manager.SelectionChanged += (s, e) => eventCount++;

            // Act
            _manager.SelectFrequency(frequency);

            // Assert
            Assert.Equal(0, eventCount);
        }

        [Fact]
        public void Clear_RemovesAllFrequenciesAndSelections()
        {
            // Arrange
            _manager.SelectFrequency(251000000.0);
            _manager.SelectFrequency(305000000.0);

            // Act
            _manager.Clear();

            // Assert
            Assert.Empty(_manager.Frequencies);
            Assert.Empty(_manager.SelectedFrequencies);
        }

        [Fact]
        public void Dispose_ClearsAllData()
        {
            // Arrange
            _manager.SelectFrequency(251000000.0);

            // Act
            _manager.Dispose();

            // Assert
            Assert.Empty(_manager.Frequencies);
            Assert.Empty(_manager.SelectedFrequencies);
        }

        public void Dispose()
        {
            _manager?.Dispose();
        }
    }
}
