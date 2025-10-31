using System;
using System.Linq;
using AeroDebrief.UI.Controls;
using AeroDebrief.UI.Controls.Player;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace AeroDebrief.UI.Tests.Components
{
    /// <summary>
    /// Unit tests for FrequencyMixerPanel component.
    /// Tests bulk selection, mixer controls, event propagation, and frequency counting.
    /// </summary>
    public class FrequencyMixerPanelTests : IDisposable
    {
        private readonly FrequencyMixerPanel _control;

        public FrequencyMixerPanelTests()
        {
            _control = ComponentTestHelper.CreateComponent<FrequencyMixerPanel>();
        }

        [Fact]
        public void Frequencies_WhenSet_PopulatesTree()
        {
            // Arrange
            var frequencies = TestDataFactory.CreateMockFrequencies();

            // Act
            ComponentTestHelper.SetProperty(_control, FrequencyMixerPanel.FrequenciesProperty, frequencies);

            // Assert
            var result = ComponentTestHelper.GetProperty<System.Collections.ObjectModel.ObservableCollection<FrequencyGroupViewModel>>(
                _control, FrequencyMixerPanel.FrequenciesProperty);
            result.Should().NotBeNull();
            result.Count.Should().Be(frequencies.Count);
        }

        [Fact]
        public void SelectAllButton_WhenClicked_SelectsAllFrequencies()
        {
            // Arrange
            var frequencies = TestDataFactory.CreateMockFrequencies();
            ComponentTestHelper.SetProperty(_control, FrequencyMixerPanel.FrequenciesProperty, frequencies);

            bool eventRaised = false;
            _control.AllFrequenciesSelected += (sender, args) => eventRaised = true;

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                var selectAllButton = FindButton(_control, "SelectAllButton");
                selectAllButton?.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            });

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => eventRaised, TimeSpan.FromSeconds(2));
            eventRaised.Should().BeTrue();
        }

        [Fact]
        public void SelectNoneButton_WhenClicked_DeselectsAllFrequencies()
        {
            // Arrange
            var frequencies = TestDataFactory.CreateMockFrequencies();
            ComponentTestHelper.SetProperty(_control, FrequencyMixerPanel.FrequenciesProperty, frequencies);

            bool eventRaised = false;
            _control.AllFrequenciesDeselected += (sender, args) => eventRaised = true;

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                var selectNoneButton = FindButton(_control, "SelectNoneButton");
                selectNoneButton?.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            });

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => eventRaised, TimeSpan.FromSeconds(2));
            eventRaised.Should().BeTrue();
        }

        [Fact]
        public void FrequencyCheckbox_WhenChecked_RaisesSelectionChangedEvent()
        {
            // Arrange
            var frequencies = TestDataFactory.CreateMockFrequencies();
            ComponentTestHelper.SetProperty(_control, FrequencyMixerPanel.FrequenciesProperty, frequencies);

            FrequencySelectionChangedEventArgs? capturedArgs = null;
            _control.FrequencySelectionChanged += (sender, args) => capturedArgs = args;

            // Act - Simulate frequency selection
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                // Simulate selection event from frequency tree
                var method = typeof(FrequencyMixerPanel).GetMethod("FrequencyTree_SelectionChanged",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                
                var frequency = frequencies.First().Frequencies.First();
                var eventArgs = new FrequencySelectionChangedEventArgs(frequency, true);
                method?.Invoke(_control, new object[] { _control, eventArgs });
            });

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => capturedArgs != null, TimeSpan.FromSeconds(2));
            capturedArgs.Should().NotBeNull();
            capturedArgs?.IsSelected.Should().BeTrue();
        }

        [Fact]
        public void VolumeSlider_WhenChanged_RaisesMixerValueChangedEvent()
        {
            // Arrange
            var frequencies = TestDataFactory.CreateMockFrequencies();
            ComponentTestHelper.SetProperty(_control, FrequencyMixerPanel.FrequenciesProperty, frequencies);

            MixerValueChangedEventArgs? capturedArgs = null;
            _control.MixerValueChanged += (sender, args) => capturedArgs = args;

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                var frequency = frequencies.First().Frequencies.First();
                var eventArgs = new MixerValueChangedEventArgs(frequency, "Volume", 0.75f);
                
                var method = typeof(FrequencyMixerPanel).GetMethod("FrequencyTree_MixerValueChanged",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                method?.Invoke(_control, new object[] { _control, eventArgs });
            });

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => capturedArgs != null, TimeSpan.FromSeconds(2));
            capturedArgs.Should().NotBeNull();
            capturedArgs?.Property.Should().Be("Volume");
            capturedArgs?.Value.Should().Be(0.75f);
        }

        [Fact]
        public void PanSlider_WhenChanged_RaisesMixerValueChangedEvent()
        {
            // Arrange
            var frequencies = TestDataFactory.CreateMockFrequencies();
            ComponentTestHelper.SetProperty(_control, FrequencyMixerPanel.FrequenciesProperty, frequencies);

            MixerValueChangedEventArgs? capturedArgs = null;
            _control.MixerValueChanged += (sender, args) => capturedArgs = args;

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                var frequency = frequencies.First().Frequencies.First();
                var eventArgs = new MixerValueChangedEventArgs(frequency, "Pan", -0.5f);
                
                var method = typeof(FrequencyMixerPanel).GetMethod("FrequencyTree_MixerValueChanged",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                method?.Invoke(_control, new object[] { _control, eventArgs });
            });

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => capturedArgs != null, TimeSpan.FromSeconds(2));
            capturedArgs.Should().NotBeNull();
            capturedArgs?.Property.Should().Be("Pan");
            capturedArgs?.Value.Should().Be(-0.5f);
        }

        [Fact]
        public void MuteButton_WhenClicked_RaisesMixerBooleanChangedEvent()
        {
            // Arrange
            var frequencies = TestDataFactory.CreateMockFrequencies();
            ComponentTestHelper.SetProperty(_control, FrequencyMixerPanel.FrequenciesProperty, frequencies);

            MixerBooleanChangedEventArgs? capturedArgs = null;
            _control.MixerBooleanChanged += (sender, args) => capturedArgs = args;

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                var frequency = frequencies.First().Frequencies.First();
                var eventArgs = new MixerBooleanChangedEventArgs(frequency, "Mute", true);
                
                var method = typeof(FrequencyMixerPanel).GetMethod("FrequencyTree_MixerBooleanChanged",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                method?.Invoke(_control, new object[] { _control, eventArgs });
            });

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => capturedArgs != null, TimeSpan.FromSeconds(2));
            capturedArgs.Should().NotBeNull();
            capturedArgs?.Property.Should().Be("Mute");
            capturedArgs?.Value.Should().BeTrue();
        }

        [Fact]
        public void SoloButton_WhenClicked_RaisesMixerBooleanChangedEvent()
        {
            // Arrange
            var frequencies = TestDataFactory.CreateMockFrequencies();
            ComponentTestHelper.SetProperty(_control, FrequencyMixerPanel.FrequenciesProperty, frequencies);

            MixerBooleanChangedEventArgs? capturedArgs = null;
            _control.MixerBooleanChanged += (sender, args) => capturedArgs = args;

            // Act
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                var frequency = frequencies.First().Frequencies.First();
                var eventArgs = new MixerBooleanChangedEventArgs(frequency, "Solo", true);
                
                var method = typeof(FrequencyMixerPanel).GetMethod("FrequencyTree_MixerBooleanChanged",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                method?.Invoke(_control, new object[] { _control, eventArgs });
            });

            // Assert
            ComponentTestHelper.WaitForCondition(_control, () => capturedArgs != null, TimeSpan.FromSeconds(2));
            capturedArgs.Should().NotBeNull();
            capturedArgs?.Property.Should().Be("Solo");
            capturedArgs?.Value.Should().BeTrue();
        }

        [Fact]
        public void GetTotalFrequencyCount_ReturnsCorrectCount()
        {
            // Arrange
            var frequencies = TestDataFactory.CreateMockFrequencies();
            ComponentTestHelper.SetProperty(_control, FrequencyMixerPanel.FrequenciesProperty, frequencies);

            // Act
            int count = 0;
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                count = _control.GetTotalFrequencyCount();
            });

            // Assert
            var expectedCount = frequencies.Sum(g => g.Frequencies.Count);
            count.Should().Be(expectedCount);
        }

        [Fact]
        public void GetSelectedFrequencyCount_ReturnsCorrectCount()
        {
            // Arrange
            var frequencies = TestDataFactory.CreateMockFrequencies();
            frequencies.First().Frequencies.First().IsSelected = true;
            frequencies.First().Frequencies.Last().IsSelected = true;
            ComponentTestHelper.SetProperty(_control, FrequencyMixerPanel.FrequenciesProperty, frequencies);

            // Act
            int count = 0;
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                count = _control.GetSelectedFrequencyCount();
            });

            // Assert
            count.Should().Be(2);
        }

        [Fact]
        public void AreAllFrequenciesSelected_WhenAllSelected_ReturnsTrue()
        {
            // Arrange
            var frequencies = TestDataFactory.CreateMockFrequencies();
            foreach (var group in frequencies)
            {
                foreach (var freq in group.Frequencies)
                {
                    freq.IsSelected = true;
                }
            }
            ComponentTestHelper.SetProperty(_control, FrequencyMixerPanel.FrequenciesProperty, frequencies);

            // Act
            bool allSelected = false;
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                allSelected = _control.AreAllFrequenciesSelected();
            });

            // Assert
            allSelected.Should().BeTrue();
        }

        [Fact]
        public void AreNoFrequenciesSelected_WhenNoneSelected_ReturnsTrue()
        {
            // Arrange
            var frequencies = TestDataFactory.CreateMockFrequencies();
            foreach (var group in frequencies)
            {
                foreach (var freq in group.Frequencies)
                {
                    freq.IsSelected = false;
                }
            }
            ComponentTestHelper.SetProperty(_control, FrequencyMixerPanel.FrequenciesProperty, frequencies);

            // Act
            bool noneSelected = false;
            ComponentTestHelper.InvokeOnDispatcher(_control, () =>
            {
                noneSelected = _control.AreNoFrequenciesSelected();
            });

            // Assert
            noneSelected.Should().BeTrue();
        }

        [Fact]
        public void SelectAllCommand_WhenBound_CanExecute()
        {
            // Arrange
            var mockCommand = new TestDataFactory.MockCommand();

            // Act
            ComponentTestHelper.SetProperty(_control, FrequencyMixerPanel.SelectAllCommandProperty, mockCommand);

            // Assert
            var result = ComponentTestHelper.GetProperty<TestDataFactory.MockCommand>(_control, FrequencyMixerPanel.SelectAllCommandProperty);
            result.Should().NotBeNull();
            result.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void SelectNoneCommand_WhenBound_CanExecute()
        {
            // Arrange
            var mockCommand = new TestDataFactory.MockCommand();

            // Act
            ComponentTestHelper.SetProperty(_control, FrequencyMixerPanel.SelectNoneCommandProperty, mockCommand);

            // Assert
            var result = ComponentTestHelper.GetProperty<TestDataFactory.MockCommand>(_control, FrequencyMixerPanel.SelectNoneCommandProperty);
            result.Should().NotBeNull();
            result.CanExecute(null).Should().BeTrue();
        }

        #region Helper Methods

        private System.Windows.Controls.Button? FindButton(System.Windows.DependencyObject parent, string name)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

                if (child is System.Windows.Controls.Button button && button.Name == name)
                {
                    return button;
                }

                var result = FindButton(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        #endregion

        public void Dispose()
        {
            ComponentTestHelper.CleanupComponent(_control);
        }
    }
}
