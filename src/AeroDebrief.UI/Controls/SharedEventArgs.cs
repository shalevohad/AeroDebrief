using System;

namespace AeroDebrief.UI.Controls
{
    /// <summary>
    /// Shared event args for mixer channel value changes (gain, pan).
    /// Used by both AudioMixerPanel and FrequencyMixerPanel for backwards compatibility.
    /// </summary>
    public class MixerChannelChangedEventArgs : EventArgs
    {
        public double Frequency { get; set; }
        public float Value { get; set; }
    }

    /// <summary>
    /// Shared event args for mixer channel boolean changes (mute, solo).
    /// Used by both AudioMixerPanel and FrequencyMixerPanel for backwards compatibility.
    /// </summary>
    public class MixerChannelBooleanEventArgs : EventArgs
    {
        public double Frequency { get; set; }
        public bool Value { get; set; }
    }

    /// <summary>
    /// Event args for zoom changes in waveform display.
    /// </summary>
    public class ZoomChangedEventArgs : EventArgs
    {
        public double ZoomLevel { get; set; }
        public double StartTime { get; set; }
        public double EndTime { get; set; }
    }
}
