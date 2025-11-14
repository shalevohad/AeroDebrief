using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AeroDebrief.UI.Controls.Player
{
    /// <summary>
    /// Independent slide-in overlay panel for displaying content (typically file selection).
    /// Provides smooth animations, keyboard shortcuts, and flexible content hosting.
    /// </summary>
    /// <remarks>
    /// This component can be reused for:
    /// - File selection panels
    /// - Settings dialogs
    /// - Filter configuration
    /// - Any slide-in overlay content
    /// 
    /// <para><b>Features:</b></para>
    /// <list type="bullet">
    /// <item><description>Smooth slide-in/out animations from right side</description></item>
    /// <item><description>Dimmed overlay background</description></item>
    /// <item><description>Keyboard shortcut support (ESC to close)</description></item>
    /// <item><description>Click outside to close functionality</description></item>
    /// <item><description>Flexible content hosting via ContentPresenter</description></item>
    /// <item><description>Customizable panel width and title</description></item>
    /// </list>
    /// 
    /// <para><b>Animation Details:</b></para>
    /// - Opening: 300ms slide-in with QuadraticEaseOut
    /// - Closing: 250ms slide-out with QuadraticEaseIn
    /// - Overlay fades in/out with panel animation
    /// 
    /// <para><b>Usage Example:</b></para>
    /// <code>
    /// &lt;player:FileSourcePanelOverlay 
    ///     IsOpen="{Binding IsFileSourcePanelOpen}"
    ///     PanelWidth="400"
    ///     PanelTitle="Select Recording File"
    ///     PanelContent="{Binding FileSourcePanel}"
    ///     PanelClosed="OnFileSourcePanelClosed"/&gt;
    /// </code>
    /// </remarks>
    public partial class FileSourcePanelOverlay : UserControl
    {
        #region Constants

        private const int OpenAnimationDuration = 300;  // milliseconds
        private const int CloseAnimationDuration = 250; // milliseconds

        #endregion

        #region Dependency Properties

        /// <summary>
        /// Gets or sets whether the panel is open.
        /// </summary>
        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(
                nameof(IsOpen),
                typeof(bool),
                typeof(FileSourcePanelOverlay),
                new PropertyMetadata(false, OnIsOpenChanged));

        /// <summary>
        /// Gets or sets the panel width.
        /// </summary>
        public static readonly DependencyProperty PanelWidthProperty =
            DependencyProperty.Register(
                nameof(PanelWidth),
                typeof(double),
                typeof(FileSourcePanelOverlay),
                new PropertyMetadata(400.0));

        /// <summary>
        /// Gets or sets the panel title.
        /// </summary>
        public static readonly DependencyProperty PanelTitleProperty =
            DependencyProperty.Register(
                nameof(PanelTitle),
                typeof(string),
                typeof(FileSourcePanelOverlay),
                new PropertyMetadata("Panel"));

        /// <summary>
        /// Gets or sets the panel content.
        /// </summary>
        public static readonly DependencyProperty PanelContentProperty =
            DependencyProperty.Register(
                nameof(PanelContent),
                typeof(object),
                typeof(FileSourcePanelOverlay),
                new PropertyMetadata(null));

        /// <summary>
        /// Gets or sets whether to enable keyboard shortcuts (ESC to close).
        /// </summary>
        public static readonly DependencyProperty EnableKeyboardShortcutsProperty =
            DependencyProperty.Register(
                nameof(EnableKeyboardShortcuts),
                typeof(bool),
                typeof(FileSourcePanelOverlay),
                new PropertyMetadata(true));

        /// <summary>
        /// Gets or sets whether clicking outside the panel closes it.
        /// </summary>
        public static readonly DependencyProperty CloseOnClickOutsideProperty =
            DependencyProperty.Register(
                nameof(CloseOnClickOutside),
                typeof(bool),
                typeof(FileSourcePanelOverlay),
                new PropertyMetadata(true));

        /// <summary>
        /// Gets or sets the animation duration in milliseconds.
        /// </summary>
        public static readonly DependencyProperty AnimationDurationProperty =
            DependencyProperty.Register(
                nameof(AnimationDuration),
                typeof(int),
                typeof(FileSourcePanelOverlay),
                new PropertyMetadata(OpenAnimationDuration));

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets whether the panel is open.
        /// </summary>
        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        /// <summary>
        /// Gets or sets the panel width.
        /// </summary>
        public double PanelWidth
        {
            get => (double)GetValue(PanelWidthProperty);
            set => SetValue(PanelWidthProperty, value);
        }

        /// <summary>
        /// Gets or sets the panel title.
        /// </summary>
        public string PanelTitle
        {
            get => (string)GetValue(PanelTitleProperty);
            set => SetValue(PanelTitleProperty, value);
        }

        /// <summary>
        /// Gets or sets the panel content.
        /// </summary>
        public object PanelContent
        {
            get => GetValue(PanelContentProperty);
            set => SetValue(PanelContentProperty, value);
        }

        /// <summary>
        /// Gets or sets whether keyboard shortcuts are enabled.
        /// </summary>
        public bool EnableKeyboardShortcuts
        {
            get => (bool)GetValue(EnableKeyboardShortcutsProperty);
            set => SetValue(EnableKeyboardShortcutsProperty, value);
        }

        /// <summary>
        /// Gets or sets whether clicking outside closes the panel.
        /// </summary>
        public bool CloseOnClickOutside
        {
            get => (bool)GetValue(CloseOnClickOutsideProperty);
            set => SetValue(CloseOnClickOutsideProperty, value);
        }

        /// <summary>
        /// Gets or sets the animation duration.
        /// </summary>
        public int AnimationDuration
        {
            get => (int)GetValue(AnimationDurationProperty);
            set => SetValue(AnimationDurationProperty, value);
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised when the panel is opened.
        /// </summary>
        public event EventHandler? PanelOpened;

        /// <summary>
        /// Raised when the panel is closed.
        /// </summary>
        public event EventHandler? PanelClosed;

        /// <summary>
        /// Raised when the panel is opening (before animation starts).
        /// </summary>
        public event EventHandler? PanelOpening;

        /// <summary>
        /// Raised when the panel is closing (before animation starts).
        /// </summary>
        public event EventHandler? PanelClosing;

        #endregion

        #region Constructor

        public FileSourcePanelOverlay()
        {
            InitializeComponent();
            
            // Enable keyboard shortcuts
            this.PreviewKeyDown += FileSourcePanelOverlay_PreviewKeyDown;
            this.Loaded += FileSourcePanelOverlay_Loaded;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles the control loaded event.
        /// </summary>
        private void FileSourcePanelOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            // Set initial transform
            if (PanelTransform != null)
            {
                PanelTransform.X = IsOpen ? 0 : PanelWidth;
            }
        }

        /// <summary>
        /// Handles keyboard shortcuts (ESC to close).
        /// </summary>
        private void FileSourcePanelOverlay_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!EnableKeyboardShortcuts || !IsOpen)
                return;

            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles the close button click.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Handles clicks on the overlay background (outside the panel).
        /// </summary>
        private void OverlayRoot_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Only close if clicking directly on the overlay (not the panel)
            if (CloseOnClickOutside && e.Source == OverlayRoot)
            {
                Close();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Opens the panel with animation.
        /// </summary>
        public void Open()
        {
            if (IsOpen)
                return;

            IsOpen = true;
        }

        /// <summary>
        /// Closes the panel with animation.
        /// </summary>
        public void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
        }

        /// <summary>
        /// Toggles the panel open/closed state.
        /// </summary>
        public void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        #endregion

        #region Property Change Handlers

        /// <summary>
        /// Handles changes to the IsOpen property.
        /// </summary>
        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FileSourcePanelOverlay panel)
            {
                bool isOpen = (bool)e.NewValue;
                
                if (isOpen)
                {
                    panel.AnimateOpen();
                }
                else
                {
                    panel.AnimateClose();
                }
            }
        }

        #endregion

        #region Animation Methods

        /// <summary>
        /// Animates the panel opening.
        /// </summary>
        private void AnimateOpen()
        {
            if (PanelTransform == null)
                return;

            // Raise opening event
            OnPanelOpening();

            // Focus the panel for keyboard input
            this.Focus();

            // Slide-in animation
            var animation = new DoubleAnimation
            {
                From = PanelWidth,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(AnimationDuration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            animation.Completed += (s, e) =>
            {
                OnPanelOpened();
            };

            PanelTransform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        /// <summary>
        /// Animates the panel closing.
        /// </summary>
        private void AnimateClose()
        {
            if (PanelTransform == null)
                return;

            // Raise closing event
            OnPanelClosing();

            // Slide-out animation
            var animation = new DoubleAnimation
            {
                From = 0,
                To = PanelWidth,
                Duration = TimeSpan.FromMilliseconds(CloseAnimationDuration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            animation.Completed += (s, e) =>
            {
                OnPanelClosed();
            };

            PanelTransform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        #endregion

        #region Protected Event Raisers

        /// <summary>
        /// Raises the PanelOpening event.
        /// </summary>
        protected virtual void OnPanelOpening()
        {
            PanelOpening?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the PanelOpened event.
        /// </summary>
        protected virtual void OnPanelOpened()
        {
            PanelOpened?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the PanelClosing event.
        /// </summary>
        protected virtual void OnPanelClosing()
        {
            PanelClosing?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the PanelClosed event.
        /// </summary>
        protected virtual void OnPanelClosed()
        {
            PanelClosed?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}
