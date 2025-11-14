using System;
using System.Windows;
using AeroDebrief.UI.Controls.Player;
using AeroDebrief.Core.Models;

namespace AeroDebrief.UI.Events
{
    /// <summary>
    /// Centralized registry for all player-related routed events in the application
    /// </summary>
    public static class PlayerEvents
    {
        #region Seek Events

        /// <summary>
        /// Raised when the user requests to seek to a specific position
        /// </summary>
        public static readonly RoutedEvent SeekRequestedEvent =
            EventManager.RegisterRoutedEvent(
                "SeekRequested",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler<SeekRequestedEventArgs>),
                typeof(PlayerEvents));

        #endregion

        #region Zoom Events

        /// <summary>
        /// Raised when the zoom level changes
        /// </summary>
        public static readonly RoutedEvent ZoomChangedEvent =
            EventManager.RegisterRoutedEvent(
                "ZoomChanged",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler<ZoomChangedEventArgs>),
                typeof(PlayerEvents));

        /// <summary>
        /// Raised when a zoom region is selected
        /// </summary>
        public static readonly RoutedEvent ZoomRegionSelectedEvent =
            EventManager.RegisterRoutedEvent(
                "ZoomRegionSelected",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler<ZoomRegionSelectedEventArgs>),
                typeof(PlayerEvents));

        #endregion

        #region Transport Control Events

        /// <summary>
        /// Raised when play is requested
        /// </summary>
        public static readonly RoutedEvent PlayRequestedEvent =
            EventManager.RegisterRoutedEvent(
                "PlayRequested",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(PlayerEvents));

        /// <summary>
        /// Raised when pause is requested
        /// </summary>
        public static readonly RoutedEvent PauseRequestedEvent =
            EventManager.RegisterRoutedEvent(
                "PauseRequested",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(PlayerEvents));

        /// <summary>
        /// Raised when stop is requested
        /// </summary>
        public static readonly RoutedEvent StopRequestedEvent =
            EventManager.RegisterRoutedEvent(
                "StopRequested",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(PlayerEvents));

        /// <summary>
        /// Raised when skip forward is requested
        /// </summary>
        public static readonly RoutedEvent SkipForwardRequestedEvent =
            EventManager.RegisterRoutedEvent(
                "SkipForwardRequested",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler<SkipEventArgs>),
                typeof(PlayerEvents));

        /// <summary>
        /// Raised when skip backward is requested
        /// </summary>
        public static readonly RoutedEvent SkipBackwardRequestedEvent =
            EventManager.RegisterRoutedEvent(
                "SkipBackwardRequested",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler<SkipEventArgs>),
                typeof(PlayerEvents));

        #endregion

        #region Waveform Events

        /// <summary>
        /// Raised when the waveform control size changes (for GPU compositor)
        /// </summary>
        public static readonly RoutedEvent WaveformSizeChangedEvent =
            EventManager.RegisterRoutedEvent(
                "WaveformSizeChanged",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler<WaveformSizeChangedEventArgs>),
                typeof(PlayerEvents));

        #endregion

        #region Minimap Events

        /// <summary>
        /// Raised when the minimap is clicked
        /// </summary>
        public static readonly RoutedEvent MinimapClickedEvent =
            EventManager.RegisterRoutedEvent(
                "MinimapClicked",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler<MinimapClickedEventArgs>),
                typeof(PlayerEvents));

        /// <summary>
        /// Raised when the minimap viewport is dragged
        /// </summary>
        public static readonly RoutedEvent MinimapDraggedEvent =
            EventManager.RegisterRoutedEvent(
                "MinimapDragged",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler<MinimapDraggedEventArgs>),
                typeof(PlayerEvents));

        #endregion

        #region Frequency Events

        /// <summary>
        /// Raised when a frequency's selection state changes
        /// </summary>
        public static readonly RoutedEvent FrequencySelectionChangedEvent =
            EventManager.RegisterRoutedEvent(
                "FrequencySelectionChanged",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler<FrequencySelectionChangedEventArgs>),
                typeof(PlayerEvents));

        /// <summary>
        /// Raised when a mixer value (volume/pan) changes
        /// </summary>
        public static readonly RoutedEvent MixerValueChangedEvent =
            EventManager.RegisterRoutedEvent(
                "MixerValueChanged",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler<MixerValueChangedEventArgs>),
                typeof(PlayerEvents));

        /// <summary>
        /// Raised when a mixer boolean (mute/solo) changes
        /// </summary>
        public static readonly RoutedEvent MixerBooleanChangedEvent =
            EventManager.RegisterRoutedEvent(
                "MixerBooleanChanged",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler<MixerBooleanChangedEventArgs>),
                typeof(PlayerEvents));

        #endregion

        #region Source Selection Events

        /// <summary>
        /// Raised when a source type is selected
        /// </summary>
        public static readonly RoutedEvent SourceTypeSelectedEvent =
            EventManager.RegisterRoutedEvent(
                "SourceTypeSelected",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler<SourceTypeSelectedEventArgs>),
                typeof(PlayerEvents));

        /// <summary>
        /// Raised when the file panel is requested
        /// </summary>
        public static readonly RoutedEvent FilePanelRequestedEvent =
            EventManager.RegisterRoutedEvent(
                "FilePanelRequested",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(PlayerEvents));

        /// <summary>
        /// Raised when the server panel is requested
        /// </summary>
        public static readonly RoutedEvent ServerPanelRequestedEvent =
            EventManager.RegisterRoutedEvent(
                "ServerPanelRequested",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(PlayerEvents));

        #endregion
    }

    #region Event Arguments

    /// <summary>
    /// Generic routed event handler delegate for events with data
    /// </summary>
    public delegate void RoutedEventHandler<TEventArgs>(object sender, TEventArgs e) where TEventArgs : RoutedEventArgs;

    /// <summary>
    /// Event arguments for seek requests
    /// </summary>
    public class SeekRequestedEventArgs : RoutedEventArgs
    {
        public double NormalizedPosition { get; }

        public SeekRequestedEventArgs(RoutedEvent routedEvent, object source, double normalizedPosition)
            : base(routedEvent, source)
        {
            NormalizedPosition = normalizedPosition;
        }
    }

    /// <summary>
    /// Event arguments for zoom changes
    /// </summary>
    public class ZoomChangedEventArgs : RoutedEventArgs
    {
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public double ZoomLevel { get; set; }

        public ZoomChangedEventArgs(RoutedEvent routedEvent, object source)
            : base(routedEvent, source)
        {
        }
    }

    /// <summary>
    /// Event arguments for zoom region selection
    /// </summary>
    public class ZoomRegionSelectedEventArgs : RoutedEventArgs
    {
        public double StartTime { get; }
        public double EndTime { get; }

        public ZoomRegionSelectedEventArgs(RoutedEvent routedEvent, object source, double startTime, double endTime)
            : base(routedEvent, source)
        {
            StartTime = startTime;
            EndTime = endTime;
        }
    }

    /// <summary>
    /// Event arguments for waveform size changes
    /// </summary>
    public class WaveformSizeChangedEventArgs : RoutedEventArgs
    {
        public double NewWidth { get; set; }
        public double NewHeight { get; set; }

        public WaveformSizeChangedEventArgs(RoutedEvent routedEvent, object source)
            : base(routedEvent, source)
        {
        }
    }

    /// <summary>
    /// Event arguments for minimap clicks
    /// </summary>
    public class MinimapClickedEventArgs : RoutedEventArgs
    {
        public double StartTime { get; }
        public double EndTime { get; }

        public MinimapClickedEventArgs(RoutedEvent routedEvent, object source, double startTime, double endTime)
            : base(routedEvent, source)
        {
            StartTime = startTime;
            EndTime = endTime;
        }
    }

    /// <summary>
    /// Event arguments for minimap drag
    /// </summary>
    public class MinimapDraggedEventArgs : RoutedEventArgs
    {
        public double StartTime { get; }
        public double EndTime { get; }

        public MinimapDraggedEventArgs(RoutedEvent routedEvent, object source, double startTime, double endTime)
            : base(routedEvent, source)
        {
            StartTime = startTime;
            EndTime = endTime;
        }
    }

    /// <summary>
    /// Event arguments for frequency selection changes
    /// </summary>
    public class FrequencySelectionChangedEventArgs : RoutedEventArgs
    {
        public ViewModels.FrequencyViewModel Frequency { get; }
        public bool IsSelected { get; }

        public FrequencySelectionChangedEventArgs(RoutedEvent routedEvent, object source, 
            ViewModels.FrequencyViewModel frequency, bool isSelected)
            : base(routedEvent, source)
        {
            Frequency = frequency;
            IsSelected = isSelected;
        }
    }

    /// <summary>
    /// Event arguments for mixer value changes
    /// </summary>
    public class MixerValueChangedEventArgs : RoutedEventArgs
    {
        public ViewModels.FrequencyViewModel Frequency { get; }
        public string Property { get; }
        public float Value { get; }

        public MixerValueChangedEventArgs(RoutedEvent routedEvent, object source,
            ViewModels.FrequencyViewModel frequency, string property, float value)
            : base(routedEvent, source)
        {
            Frequency = frequency;
            Property = property;
            Value = value;
        }
    }

    /// <summary>
    /// Event arguments for mixer boolean changes
    /// </summary>
    public class MixerBooleanChangedEventArgs : RoutedEventArgs
    {
        public ViewModels.FrequencyViewModel Frequency { get; }
        public string Property { get; }
        public bool Value { get; }
        public PlayerFrequencyInfo? Player { get; }

        public MixerBooleanChangedEventArgs(RoutedEvent routedEvent, object source,
            ViewModels.FrequencyViewModel frequency, string property, bool value, PlayerFrequencyInfo? player = null)
            : base(routedEvent, source)
        {
            Frequency = frequency;
            Property = property;
            Value = value;
            Player = player;
        }
    }

    /// <summary>
    /// Event arguments for source type selection
    /// </summary>
    public class SourceTypeSelectedEventArgs : RoutedEventArgs
    {
        public SourceType SourceType { get; }

        public SourceTypeSelectedEventArgs(RoutedEvent routedEvent, object source, SourceType sourceType)
            : base(routedEvent, source)
        {
            SourceType = sourceType;
        }
    }

    /// <summary>
    /// Event arguments for skip forward/backward events
    /// </summary>
    public class SkipEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Gets the skip interval in seconds (positive for forward, negative for backward)
        /// </summary>
        public double IntervalSeconds { get; }

        /// <summary>
        /// Initializes a new instance of the SkipEventArgs class
        /// </summary>
        /// <param name="routedEvent">The routed event identifier</param>
        /// <param name="source">The source of the event</param>
        /// <param name="intervalSeconds">The skip interval in seconds</param>
        public SkipEventArgs(RoutedEvent routedEvent, object source, double intervalSeconds)
            : base(routedEvent, source)
        {
            IntervalSeconds = intervalSeconds;
        }
    }

    #endregion
}
