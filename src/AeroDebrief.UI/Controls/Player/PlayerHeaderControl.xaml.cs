using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Helpers;

namespace AeroDebrief.UI.Controls.Player
{
    /// <summary>
    /// Independent player header control that displays status, source information, and action buttons.
    /// </summary>
    /// <remarks>
    /// This component can be reused in:
    /// - Main player interface
    /// - Mini player windows
    /// - Popup player dialogs
    /// - Custom player implementations
    /// 
    /// The component is stateless and receives all data via dependency properties,
    /// communicating changes through events. This ensures complete independence
    /// from parent controls.
    /// </remarks>
    public partial class PlayerHeaderControl : UserControl
    {
        #region Dependency Properties

        /// <summary>
        /// Gets or sets the current status message displayed to the user.
        /// </summary>
        public static readonly DependencyProperty StatusMessageProperty =
            DependencyProperty.Register(
                nameof(StatusMessage),
                typeof(string),
                typeof(PlayerHeaderControl),
                new PropertyMetadata("Ready"));

        /// <summary>
        /// Gets or sets the name of the current source (file or server).
        /// </summary>
        public static readonly DependencyProperty SourceNameProperty =
            DependencyProperty.Register(
                nameof(SourceName),
                typeof(string),
                typeof(PlayerHeaderControl),
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// Gets or sets the current player mode (Idle/Recording/Playback).
        /// </summary>
        public static readonly DependencyProperty CurrentModeProperty =
            DependencyProperty.Register(
                nameof(CurrentMode),
                typeof(PlayerMode),
                typeof(PlayerHeaderControl),
                new PropertyMetadata(PlayerMode.Idle));

        /// <summary>
        /// Gets or sets whether the player is in idle mode.
        /// </summary>
        public static readonly DependencyProperty IsIdleProperty =
            DependencyProperty.Register(
                nameof(IsIdle),
                typeof(bool),
                typeof(PlayerHeaderControl),
                new PropertyMetadata(true));

        /// <summary>
        /// Gets or sets the command executed when the user wants to change the source.
        /// </summary>
        public static readonly DependencyProperty ChangeSourceCommandProperty =
            DependencyProperty.Register(
                nameof(ChangeSourceCommand),
                typeof(ICommand),
                typeof(PlayerHeaderControl),
                new PropertyMetadata(null));

        /// <summary>
        /// Gets or sets the command executed when the user opens settings.
        /// </summary>
        public static readonly DependencyProperty OpenSettingsCommandProperty =
            DependencyProperty.Register(
                nameof(OpenSettingsCommand),
                typeof(ICommand),
                typeof(PlayerHeaderControl),
                new PropertyMetadata(null));

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the current status message.
        /// </summary>
        public string StatusMessage
        {
            get => (string)GetValue(StatusMessageProperty);
            set => SetValue(StatusMessageProperty, value);
        }

        /// <summary>
        /// Gets or sets the source name.
        /// </summary>
        public string SourceName
        {
            get => (string)GetValue(SourceNameProperty);
            set => SetValue(SourceNameProperty, value);
        }

        /// <summary>
        /// Gets or sets the current player mode.
        /// </summary>
        public PlayerMode CurrentMode
        {
            get => (PlayerMode)GetValue(CurrentModeProperty);
            set => SetValue(CurrentModeProperty, value);
        }

        /// <summary>
        /// Gets or sets whether the player is idle.
        /// </summary>
        public bool IsIdle
        {
            get => (bool)GetValue(IsIdleProperty);
            set => SetValue(IsIdleProperty, value);
        }

        /// <summary>
        /// Gets or sets the change source command.
        /// </summary>
        public ICommand ChangeSourceCommand
        {
            get => (ICommand)GetValue(ChangeSourceCommandProperty);
            set => SetValue(ChangeSourceCommandProperty, value);
        }

        /// <summary>
        /// Gets or sets the open settings command.
        /// </summary>
        public ICommand OpenSettingsCommand
        {
            get => (ICommand)GetValue(OpenSettingsCommandProperty);
            set => SetValue(OpenSettingsCommandProperty, value);
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised when the user selects a source type (Server or File).
        /// </summary>
        public event Events.RoutedEventHandler<Events.SourceTypeSelectedEventArgs> SourceTypeSelected
        {
            add => this.AddSourceTypeSelectedHandler(value);
            remove => this.RemoveSourceTypeSelectedHandler(value);
        }

        /// <summary>
        /// Raised when the user requests to open the file panel.
        /// </summary>
        public event RoutedEventHandler FilePanelRequested
        {
            add => this.AddFilePanelRequestedHandler(value);
            remove => this.RemoveFilePanelRequestedHandler(value);
        }

        /// <summary>
        /// Raised when the user requests to open the server panel.
        /// </summary>
        public event RoutedEventHandler ServerPanelRequested
        {
            add => this.AddServerPanelRequestedHandler(value);
            remove => this.RemoveServerPanelRequestedHandler(value);
        }

        #endregion

        #region Constructor

        public PlayerHeaderControl()
        {
            InitializeComponent();
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles the Server Source button click.
        /// Raises the ServerPanelRequested event to open the server panel.
        /// </summary>
        private void ServerSourceButton_Click(object sender, RoutedEventArgs e)
        {
            this.RaiseServerPanelRequested();
        }

        /// <summary>
        /// Handles the File Source button click.
        /// Raises the SourceTypeSelected event with SourceType.File.
        /// </summary>
        private void FileSourceButton_Click(object sender, RoutedEventArgs e)
        {
            this.RaiseSourceTypeSelected(SourceType.File);
        }

        /// <summary>
        /// Handles the Open File Panel button click.
        /// Raises the FilePanelRequested event.
        /// </summary>
        private void OpenFilePanelButton_Click(object sender, RoutedEventArgs e)
        {
            this.RaiseFilePanelRequested();
        }
        
        /// <summary>
        /// Handles the Open Server Panel button click.
        /// Raises the ServerPanelRequested event.
        /// </summary>
        private void OpenServerPanelButton_Click(object sender, RoutedEventArgs e)
        {
            this.RaiseServerPanelRequested();
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Defines the type of source selected by the user.
    /// </summary>
    public enum SourceType
    {
        /// <summary>
        /// SRS Server source
        /// </summary>
        Server,

        /// <summary>
        /// Recording file source
        /// </summary>
        File
    }

    #endregion
}
