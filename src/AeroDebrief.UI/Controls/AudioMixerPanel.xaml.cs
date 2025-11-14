using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AeroDebrief.UI.ViewModels;

namespace AeroDebrief.UI.Controls
{
    /// <summary>
    /// Audio mixer panel with per-frequency volume, pan, mute, and solo controls
    /// </summary>
    public partial class AudioMixerPanel : UserControl
    {
        public AudioMixerPanel()
        {
            InitializeComponent();
        }

        #region Dependency Properties

        /// <summary>
        /// Collection of mixer channels to display
        /// </summary>
        public static readonly DependencyProperty MixerChannelsProperty =
            DependencyProperty.Register(
                nameof(MixerChannels),
                typeof(ObservableCollection<MixerChannelViewModel>),
                typeof(AudioMixerPanel),
                new PropertyMetadata(null));

        public ObservableCollection<MixerChannelViewModel>? MixerChannels
        {
            get => (ObservableCollection<MixerChannelViewModel>?)GetValue(MixerChannelsProperty);
            set => SetValue(MixerChannelsProperty, value);
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised when a channel's gain (volume) changes
        /// </summary>
        public event EventHandler<MixerChannelChangedEventArgs>? GainChanged;

        /// <summary>
        /// Raised when a channel's pan changes
        /// </summary>
        public event EventHandler<MixerChannelChangedEventArgs>? PanChanged;

        /// <summary>
        /// Raised when a channel's mute state changes
        /// </summary>
        public event EventHandler<MixerChannelBooleanEventArgs>? MuteChanged;

        /// <summary>
        /// Raised when a channel's solo state changes
        /// </summary>
        public event EventHandler<MixerChannelBooleanEventArgs>? SoloChanged;

        #endregion

        #region Event Handlers

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider && slider.Tag is double frequency)
            {
                GainChanged?.Invoke(this, new MixerChannelChangedEventArgs
                {
                    Frequency = frequency,
                    Value = (float)e.NewValue
                });
            }
        }

        private void PanSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider && slider.Tag is double frequency)
            {
                PanChanged?.Invoke(this, new MixerChannelChangedEventArgs
                {
                    Frequency = frequency,
                    Value = (float)e.NewValue
                });
            }
        }

        private void Mute_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button && button.Tag is double frequency)
            {
                MuteChanged?.Invoke(this, new MixerChannelBooleanEventArgs
                {
                    Frequency = frequency,
                    Value = true
                });
            }
        }

        private void Mute_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button && button.Tag is double frequency)
            {
                MuteChanged?.Invoke(this, new MixerChannelBooleanEventArgs
                {
                    Frequency = frequency,
                    Value = false
                });
            }
        }

        private void Solo_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button && button.Tag is double frequency)
            {
                SoloChanged?.Invoke(this, new MixerChannelBooleanEventArgs
                {
                    Frequency = frequency,
                    Value = true
                });
            }
        }

        private void Solo_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button && button.Tag is double frequency)
            {
                SoloChanged?.Invoke(this, new MixerChannelBooleanEventArgs
                {
                    Frequency = frequency,
                    Value = false
                });
            }
        }

        private void RemoveChannel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is double frequency)
            {
                var channel = MixerChannels?.FirstOrDefault(ch => Math.Abs(ch.Frequency - frequency) < 0.1);
                if (channel != null)
                {
                    MixerChannels?.Remove(channel);
                }
            }
        }

        private void ResetAll_Click(object sender, RoutedEventArgs e)
        {
            if (MixerChannels == null) return;

            foreach (var channel in MixerChannels)
            {
                channel.Volume = 1.0f;
                channel.Pan = 0.0f;
                channel.IsMuted = false;
                channel.IsSolo = false;
            }
        }

        #endregion
    }
}
