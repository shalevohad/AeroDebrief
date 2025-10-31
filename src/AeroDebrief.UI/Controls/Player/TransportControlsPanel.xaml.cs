using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FontAwesome.WPF;
using AeroDebrief.UI.Helpers;

namespace AeroDebrief.UI.Controls.Player
{
    /// <summary>
    /// Independent transport controls panel for audio/video playback.
    /// Provides Play/Pause/Stop buttons with keyboard shortcut support.
    /// </summary>
    /// <remarks>
    /// This component can be reused in:
    /// - Main player interface
    /// - Mini player windows
    /// - Popup player dialogs
    /// - Custom player implementations
    /// 
    /// <para><b>Keyboard Shortcuts:</b></para>
    /// <list type="bullet">
    /// <item><description>Space: Play/Pause toggle</description></item>
    /// <item><description>Escape: Stop playback</description></item>
    /// <item><description>Left Arrow: Skip backward (if enabled)</description></item>
    /// <item><description>Right Arrow: Skip forward (if enabled)</description></item>
    /// </list>
    /// 
    /// <para><b>States:</b></para>
    /// The component automatically manages button states based on playback status:
    /// - Play button becomes Pause when playing
    /// - Stop button is enabled only when playing or paused
    /// - Skip buttons are enabled when seeking is available
    /// </remarks>
    public partial class TransportControlsPanel : UserControl
    {
        #region Dependency Properties

        /// <summary>
        /// Gets or sets whether the player is currently playing.
        /// </summary>
        public static readonly DependencyProperty IsPlayingProperty =
            DependencyProperty.Register(
                nameof(IsPlaying),
                typeof(bool),
                typeof(TransportControlsPanel),
                new PropertyMetadata(false, OnIsPlayingChanged));

        /// <summary>
        /// Gets or sets whether the player is currently paused.
        /// </summary>
        public static readonly DependencyProperty IsPausedProperty =
            DependencyProperty.Register(
                nameof(IsPaused),
                typeof(bool),
                typeof(TransportControlsPanel),
                new PropertyMetadata(false));

        /// <summary>
        /// Gets or sets whether the play command can be executed.
        /// </summary>
        public static readonly DependencyProperty CanPlayProperty =
            DependencyProperty.Register(
                nameof(CanPlay),
                typeof(bool),
                typeof(TransportControlsPanel),
                new PropertyMetadata(true));

        /// <summary>
        /// Gets or sets whether the pause command can be executed.
        /// </summary>
        public static readonly DependencyProperty CanPauseProperty =
            DependencyProperty.Register(
                nameof(CanPause),
                typeof(bool),
                typeof(TransportControlsPanel),
                new PropertyMetadata(false));

        /// <summary>
        /// Gets or sets whether the stop command can be executed.
        /// </summary>
        public static readonly DependencyProperty CanStopProperty =
            DependencyProperty.Register(
                nameof(CanStop),
                typeof(bool),
                typeof(TransportControlsPanel),
                new PropertyMetadata(false, OnCanStopChanged));

        /// <summary>
        /// Gets or sets whether seeking (skip forward/backward) is available.
        /// </summary>
        public static readonly DependencyProperty CanSeekProperty =
            DependencyProperty.Register(
                nameof(CanSeek),
                typeof(bool),
                typeof(TransportControlsPanel),
                new PropertyMetadata(false));

        /// <summary>
        /// Gets or sets the orientation of the transport controls (Vertical or Horizontal).
        /// </summary>
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(
                nameof(Orientation),
                typeof(Orientation),
                typeof(TransportControlsPanel),
                new PropertyMetadata(Orientation.Vertical, OnOrientationChanged));

        /// <summary>
        /// Gets or sets whether to show skip forward/backward buttons.
        /// </summary>
        public static readonly DependencyProperty ShowSkipButtonsProperty =
            DependencyProperty.Register(
                nameof(ShowSkipButtons),
                typeof(bool),
                typeof(TransportControlsPanel),
                new PropertyMetadata(false));

        /// <summary>
        /// Gets or sets the skip interval in seconds (default: 10 seconds).
        /// </summary>
        public static readonly DependencyProperty SkipIntervalSecondsProperty =
            DependencyProperty.Register(
                nameof(SkipIntervalSeconds),
                typeof(double),
                typeof(TransportControlsPanel),
                new PropertyMetadata(10.0));

        /// <summary>
        /// Gets or sets whether keyboard shortcuts are enabled.
        /// </summary>
        public static readonly DependencyProperty EnableKeyboardShortcutsProperty =
            DependencyProperty.Register(
                nameof(EnableKeyboardShortcuts),
                typeof(bool),
                typeof(TransportControlsPanel),
                new PropertyMetadata(true));

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets whether the player is playing.
        /// </summary>
        public bool IsPlaying
        {
            get => (bool)GetValue(IsPlayingProperty);
            set => SetValue(IsPlayingProperty, value);
        }

        /// <summary>
        /// Gets or sets whether the player is paused.
        /// </summary>
        public bool IsPaused
        {
            get => (bool)GetValue(IsPausedProperty);
            set => SetValue(IsPausedProperty, value);
        }

        /// <summary>
        /// Gets or sets whether play can be executed.
        /// </summary>
        public bool CanPlay
        {
            get => (bool)GetValue(CanPlayProperty);
            set => SetValue(CanPlayProperty, value);
        }

        /// <summary>
        /// Gets or sets whether pause can be executed.
        /// </summary>
        public bool CanPause
        {
            get => (bool)GetValue(CanPauseProperty);
            set => SetValue(CanPauseProperty, value);
        }

        /// <summary>
        /// Gets or sets whether stop can be executed.
        /// </summary>
        public bool CanStop
        {
            get => (bool)GetValue(CanStopProperty);
            set => SetValue(CanStopProperty, value);
        }

        /// <summary>
        /// Gets or sets whether seeking is available.
        /// </summary>
        public bool CanSeek
        {
            get => (bool)GetValue(CanSeekProperty);
            set => SetValue(CanSeekProperty, value);
        }

        /// <summary>
        /// Gets or sets the orientation of controls.
        /// </summary>
        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show skip buttons.
        /// </summary>
        public bool ShowSkipButtons
        {
            get => (bool)GetValue(ShowSkipButtonsProperty);
            set => SetValue(ShowSkipButtonsProperty, value);
        }

        /// <summary>
        /// Gets or sets the skip interval in seconds.
        /// </summary>
        public double SkipIntervalSeconds
        {
            get => (double)GetValue(SkipIntervalSecondsProperty);
            set => SetValue(SkipIntervalSecondsProperty, value);
        }

        /// <summary>
        /// Gets or sets whether keyboard shortcuts are enabled.
        /// </summary>
        public bool EnableKeyboardShortcuts
        {
            get => (bool)GetValue(EnableKeyboardShortcutsProperty);
            set => SetValue(EnableKeyboardShortcutsProperty, value);
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised when the user requests to play.
        /// </summary>
        public event RoutedEventHandler PlayRequested
        {
            add => this.AddPlayRequestedHandler(value);
            remove => this.RemovePlayRequestedHandler(value);
        }

        /// <summary>
        /// Raised when the user requests to pause.
        /// </summary>
        public event RoutedEventHandler PauseRequested
        {
            add => this.AddPauseRequestedHandler(value);
            remove => this.RemovePauseRequestedHandler(value);
        }

        /// <summary>
        /// Raised when the user requests to stop.
        /// </summary>
        public event RoutedEventHandler StopRequested
        {
            add => this.AddStopRequestedHandler(value);
            remove => this.RemoveStopRequestedHandler(value);
        }

        /// <summary>
        /// Raised when the user requests to skip forward.
        /// </summary>
        public event Events.RoutedEventHandler<Events.SkipEventArgs> SkipForwardRequested
        {
            add => this.AddSkipForwardRequestedHandler(value);
            remove => this.RemoveSkipForwardRequestedHandler(value);
        }

        /// <summary>
        /// Raised when the user requests to skip backward.
        /// </summary>
        public event Events.RoutedEventHandler<Events.SkipEventArgs> SkipBackwardRequested
        {
            add => this.AddSkipBackwardRequestedHandler(value);
            remove => this.RemoveSkipBackwardRequestedHandler(value);
        }

        #endregion

        #region Constructor

        public TransportControlsPanel()
        {
            InitializeComponent();
            
            // Enable keyboard shortcuts
            this.Focusable = true;
            this.PreviewKeyDown += TransportControlsPanel_PreviewKeyDown;
            this.Loaded += TransportControlsPanel_Loaded;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles the panel loaded event to set initial focus.
        /// </summary>
        private void TransportControlsPanel_Loaded(object sender, RoutedEventArgs e)
        {
            // Set focus to enable keyboard shortcuts
            if (EnableKeyboardShortcuts)
            {
                this.Focus();
            }
            
            // Update initial button states
            UpdatePlayPauseButton();
            UpdateStopButton();
        }

        /// <summary>
        /// Handles keyboard shortcuts for transport controls.
        /// </summary>
        private void TransportControlsPanel_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!EnableKeyboardShortcuts)
                return;

            switch (e.Key)
            {
                case Key.Space:
                    // Toggle Play/Pause
                    if (IsPlaying && CanPause)
                    {
                        OnPauseRequested();
                    }
                    else if (!IsPlaying && CanPlay)
                    {
                        OnPlayRequested();
                    }
                    e.Handled = true;
                    break;

                case Key.Escape:
                    // Stop playback
                    if (CanStop)
                    {
                        OnStopRequested();
                    }
                    e.Handled = true;
                    break;

                case Key.Left:
                    // Skip backward
                    if (ShowSkipButtons && CanSeek)
                    {
                        OnSkipBackwardRequested();
                    }
                    e.Handled = true;
                    break;

                case Key.Right:
                    // Skip forward
                    if (ShowSkipButtons && CanSeek)
                    {
                        OnSkipForwardRequested();
                    }
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// Handles the Play/Pause button click.
        /// </summary>
        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsPlaying)
            {
                OnPauseRequested();
            }
            else
            {
                OnPlayRequested();
            }
        }

        /// <summary>
        /// Handles the Stop button click.
        /// </summary>
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            OnStopRequested();
        }

        /// <summary>
        /// Handles the Skip Backward button click.
        /// </summary>
        private void SkipBackwardButton_Click(object sender, RoutedEventArgs e)
        {
            OnSkipBackwardRequested();
        }

        /// <summary>
        /// Handles the Skip Forward button click.
        /// </summary>
        private void SkipForwardButton_Click(object sender, RoutedEventArgs e)
        {
            OnSkipForwardRequested();
        }

        #endregion

        #region Protected Event Raisers

        /// <summary>
        /// Raises the PlayRequested event.
        /// </summary>
        protected virtual void OnPlayRequested()
        {
            this.RaisePlayRequested();
        }

        /// <summary>
        /// Raises the PauseRequested event.
        /// </summary>
        protected virtual void OnPauseRequested()
        {
            this.RaisePauseRequested();
        }

        /// <summary>
        /// Raises the StopRequested event.
        /// </summary>
        protected virtual void OnStopRequested()
        {
            this.RaiseStopRequested();
        }

        /// <summary>
        /// Raises the SkipForwardRequested event.
        /// </summary>
        protected virtual void OnSkipForwardRequested()
        {
            this.RaiseSkipForwardRequested(SkipIntervalSeconds);
        }

        /// <summary>
        /// Raises the SkipBackwardRequested event.
        /// </summary>
        protected virtual void OnSkipBackwardRequested()
        {
            this.RaiseSkipBackwardRequested(-SkipIntervalSeconds);
        }

        #endregion

        #region Property Change Handlers

        /// <summary>
        /// Handles changes to the IsPlaying property.
        /// </summary>
        private static void OnIsPlayingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TransportControlsPanel panel)
            {
                panel.UpdatePlayPauseButton();
            }
        }

        /// <summary>
        /// Handles changes to the CanStop property.
        /// </summary>
        private static void OnCanStopChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TransportControlsPanel panel)
            {
                panel.UpdateStopButton();
            }
        }

        /// <summary>
        /// Handles changes to the Orientation property.
        /// </summary>
        private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TransportControlsPanel panel && panel.ControlsContainer != null)
            {
                panel.ControlsContainer.Orientation = (Orientation)e.NewValue;
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Updates the Play/Pause button icon and state.
        /// </summary>
        private void UpdatePlayPauseButton()
        {
            if (PlayPauseIcon != null)
            {
                Dispatcher.Invoke(() =>
                {
                    // Change icon based on playing state
                    PlayPauseIcon.Icon = IsPlaying 
                        ? FontAwesomeIcon.Pause 
                        : FontAwesomeIcon.Play;
                    
                    // Update tooltip
                    PlayPauseButton.ToolTip = IsPlaying 
                        ? "Pause (Space)" 
                        : "Play (Space)";
                });
            }
        }

        /// <summary>
        /// Updates the Stop button enabled state and color.
        /// </summary>
        private void UpdateStopButton()
        {
            if (StopButton != null && StopIcon != null)
            {
                Dispatcher.Invoke(() =>
                {
                    StopButton.IsEnabled = CanStop;
                    
                    // Update stop button color based on enabled state
                    StopIcon.Foreground = CanStop 
                        ? new SolidColorBrush(Color.FromRgb(211, 47, 47))  // Red
                        : new SolidColorBrush(Color.FromRgb(189, 189, 189)); // Gray
                });
            }
        }

        #endregion
    }
}
