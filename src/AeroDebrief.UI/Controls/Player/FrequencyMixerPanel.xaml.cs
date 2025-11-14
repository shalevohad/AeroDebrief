using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AeroDebrief.UI.ViewModels;
using AeroDebrief.UI.Helpers;

namespace AeroDebrief.UI.Controls.Player
{
    /// <summary>
    /// Independent frequency mixer panel that combines frequency selection and mixer controls.
    /// Wraps the FrequencyTreeView control and provides bulk selection actions.
    /// </summary>
    /// <remarks>
    /// This component can be reused in:
    /// - Main player interface
    /// - Dedicated mixer windows
    /// - Analysis tools
    /// - Custom audio filtering interfaces
    /// 
    /// <para><b>Features:</b></para>
    /// <list type="bullet">
    /// <item><description>Frequency tree visualization with modulation support</description></item>
    /// <item><description>Per-frequency mixer controls (volume, pan, mute, solo)</description></item>
    /// <item><description>Bulk selection actions (Select All/None)</description></item>
    /// <item><description>Event aggregation for all frequency and mixer changes</description></item>
    /// <item><description>Support for pilot-based filtering</description></item>
    /// </list>
    /// 
    /// <para><b>Event Flow:</b></para>
    /// The component aggregates events from the FrequencyTreeView and re-raises them,
    /// allowing parent controls to handle frequency selection and mixer changes without
    /// directly coupling to the tree view implementation.
    /// </remarks>
    public partial class FrequencyMixerPanel : UserControl
    {
        #region Dependency Properties

        /// <summary>
        /// Gets or sets the collection of frequency groups to display.
        /// </summary>
        public static readonly DependencyProperty FrequenciesProperty =
            DependencyProperty.Register(
                nameof(Frequencies),
                typeof(ObservableCollection<FrequencyGroupViewModel>),
                typeof(FrequencyMixerPanel),
                new PropertyMetadata(null, OnFrequenciesChanged));

        /// <summary>
        /// Gets or sets the command to execute when selecting all frequencies.
        /// </summary>
        public static readonly DependencyProperty SelectAllCommandProperty =
            DependencyProperty.Register(
                nameof(SelectAllCommand),
                typeof(ICommand),
                typeof(FrequencyMixerPanel),
                new PropertyMetadata(null));

        /// <summary>
        /// Gets or sets the command to execute when deselecting all frequencies.
        /// </summary>
        public static readonly DependencyProperty SelectNoneCommandProperty =
            DependencyProperty.Register(
                nameof(SelectNoneCommand),
                typeof(ICommand),
                typeof(FrequencyMixerPanel),
                new PropertyMetadata(null));

        /// <summary>
        /// Gets or sets whether to enable search/filter functionality (future feature).
        /// </summary>
        public static readonly DependencyProperty EnableSearchProperty =
            DependencyProperty.Register(
                nameof(EnableSearch),
                typeof(bool),
                typeof(FrequencyMixerPanel),
                new PropertyMetadata(false));

        /// <summary>
        /// Gets or sets the search text for filtering frequencies (future feature).
        /// </summary>
        public static readonly DependencyProperty SearchTextProperty =
            DependencyProperty.Register(
                nameof(SearchText),
                typeof(string),
                typeof(FrequencyMixerPanel),
                new PropertyMetadata(string.Empty, OnSearchTextChanged));

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the frequency groups collection.
        /// </summary>
        public ObservableCollection<FrequencyGroupViewModel>? Frequencies
        {
            get => (ObservableCollection<FrequencyGroupViewModel>?)GetValue(FrequenciesProperty);
            set => SetValue(FrequenciesProperty, value);
        }

        /// <summary>
        /// Gets or sets the select all command.
        /// </summary>
        public ICommand? SelectAllCommand
        {
            get => (ICommand?)GetValue(SelectAllCommandProperty);
            set => SetValue(SelectAllCommandProperty, value);
        }

        /// <summary>
        /// Gets or sets the select none command.
        /// </summary>
        public ICommand? SelectNoneCommand
        {
            get => (ICommand?)GetValue(SelectNoneCommandProperty);
            set => SetValue(SelectNoneCommandProperty, value);
        }

        /// <summary>
        /// Gets or sets whether search is enabled.
        /// </summary>
        public bool EnableSearch
        {
            get => (bool)GetValue(EnableSearchProperty);
            set => SetValue(EnableSearchProperty, value);
        }

        /// <summary>
        /// Gets or sets the search text.
        /// </summary>
        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised when a frequency selection changes (selected or deselected).
        /// </summary>
        public event Events.RoutedEventHandler<Events.FrequencySelectionChangedEventArgs> FrequencySelectionChanged
        {
            add => this.AddFrequencySelectionChangedHandler(value);
            remove => this.RemoveFrequencySelectionChangedHandler(value);
        }

        /// <summary>
        /// Raised when a mixer value changes (volume, pan).
        /// </summary>
        public event Events.RoutedEventHandler<Events.MixerValueChangedEventArgs> MixerValueChanged
        {
            add => this.AddMixerValueChangedHandler(value);
            remove => this.RemoveMixerValueChangedHandler(value);
        }

        /// <summary>
        /// Raised when a mixer boolean value changes (mute, solo).
        /// </summary>
        public event Events.RoutedEventHandler<Events.MixerBooleanChangedEventArgs> MixerBooleanChanged
        {
            add => this.AddMixerBooleanChangedHandler(value);
            remove => this.RemoveMixerBooleanChangedHandler(value);
        }

        /// <summary>
        /// Raised when all frequencies are selected.
        /// </summary>
        public event EventHandler? AllFrequenciesSelected;

        /// <summary>
        /// Raised when all frequencies are deselected.
        /// </summary>
        public event EventHandler? AllFrequenciesDeselected;

        #endregion

        #region Constructor

        public FrequencyMixerPanel()
        {
            InitializeComponent();
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles the Select All button click.
        /// Executes the SelectAllCommand and raises the AllFrequenciesSelected event.
        /// </summary>
        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            // Execute command if available
            if (SelectAllCommand?.CanExecute(null) == true)
            {
                SelectAllCommand.Execute(null);
            }

            // Raise event for listeners
            OnAllFrequenciesSelected();
        }

        /// <summary>
        /// Handles the Select None button click.
        /// Executes the SelectNoneCommand and raises the AllFrequenciesDeselected event.
        /// </summary>
        private void SelectNoneButton_Click(object sender, RoutedEventArgs e)
        {
            // Execute command if available
            if (SelectNoneCommand?.CanExecute(null) == true)
            {
                SelectNoneCommand.Execute(null);
            }

            // Raise event for listeners
            OnAllFrequenciesDeselected();
        }

        /// <summary>
        /// Handles frequency selection changes from the tree view.
        /// Re-raises the event for parent controls using centralized event system.
        /// </summary>
        private void FrequencyTree_SelectionChanged(object? sender, Controls.FrequencySelectionChangedEventArgs e)
        {
            // Convert from TreeView CLR event args to centralized routed event args
            this.RaiseFrequencySelectionChanged(e.Frequency, e.IsSelected);
        }

        /// <summary>
        /// Handles mixer value changes from the tree view.
        /// Re-raises the event for parent controls using centralized event system.
        /// </summary>
        private void FrequencyTree_MixerValueChanged(object? sender, Controls.MixerValueChangedEventArgs e)
        {
            // Convert from TreeView CLR event args to centralized routed event args
            this.RaiseMixerValueChanged(e.Frequency, e.Property, e.Value);
        }

        /// <summary>
        /// Handles mixer boolean changes from the tree view.
        /// Re-raises the event for parent controls using centralized event system.
        /// </summary>
        private void FrequencyTree_MixerBooleanChanged(object? sender, Controls.MixerBooleanChangedEventArgs e)
        {
            // Convert from TreeView CLR event args to centralized routed event args
            this.RaiseMixerBooleanChanged(e.Frequency, e.Property, e.Value, e.Player);
        }

        #endregion

        #region Protected Event Raisers

        /// <summary>
        /// Raises the AllFrequenciesSelected event.
        /// </summary>
        protected virtual void OnAllFrequenciesSelected()
        {
            AllFrequenciesSelected?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the AllFrequenciesDeselected event.
        /// </summary>
        protected virtual void OnAllFrequenciesDeselected()
        {
            AllFrequenciesDeselected?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Property Change Handlers

        /// <summary>
        /// Handles changes to the Frequencies property.
        /// </summary>
        private static void OnFrequenciesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrequencyMixerPanel panel)
            {
                // Unsubscribe from old collection's CollectionChanged event
                if (e.OldValue is ObservableCollection<FrequencyGroupViewModel> oldCollection)
                {
                    oldCollection.CollectionChanged -= panel.OnFrequenciesCollectionChanged;
                }
                
                // Subscribe to new collection's CollectionChanged event
                if (e.NewValue is ObservableCollection<FrequencyGroupViewModel> newCollection)
                {
                    newCollection.CollectionChanged += panel.OnFrequenciesCollectionChanged;
                }
                
                panel.OnFrequenciesUpdated();
            }
        }
        
        /// <summary>
        /// Handles collection changed events (items added/removed) to update button states
        /// </summary>
        private void OnFrequenciesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnFrequenciesUpdated();
        }

        /// <summary>
        /// Handles changes to the SearchText property (future feature).
        /// </summary>
        private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrequencyMixerPanel panel)
            {
                panel.FilterFrequencies(e.NewValue as string);
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Called when the Frequencies collection is updated.
        /// Can be used to update UI state or perform validation.
        /// </summary>
        private void OnFrequenciesUpdated()
        {
            var logger = NLog.LogManager.GetCurrentClassLogger();
            
            // Log frequency count
            int frequencyCount = Frequencies?.Count ?? 0;
            int totalFrequencies = 0;
            
            if (Frequencies != null)
            {
                foreach (var group in Frequencies)
                {
                    totalFrequencies += group.Frequencies?.Count ?? 0;
                }
            }
            
            logger.Info($"?? FrequencyMixerPanel.OnFrequenciesUpdated: {frequencyCount} groups, {totalFrequencies} total frequencies");
            
            // Resolve named UI elements safely using FindName to avoid generated-field issues
            var selectAllBtn = this.FindName("SelectAllButton") as Button;
            var selectNoneBtn = this.FindName("SelectNoneButton") as Button;
            var emptyState = this.FindName("EmptyStatePlaceholder") as FrameworkElement;
            var freqTree = this.FindName("FrequencyTree") as FrameworkElement;

            // Update button states and visibility based on frequency count
            if (Frequencies != null && Frequencies.Count > 0)
            {
                if (selectAllBtn != null) selectAllBtn.IsEnabled = true;
                if (selectNoneBtn != null) selectNoneBtn.IsEnabled = true;

                // Hide empty state, show tree
                if (emptyState != null) emptyState.Visibility = System.Windows.Visibility.Collapsed;
                if (freqTree != null) freqTree.Visibility = System.Windows.Visibility.Visible;

                logger.Debug($"   ? Buttons enabled, tree visible ({frequencyCount} groups, {totalFrequencies} frequencies)");
            }
            else
            {
                if (selectAllBtn != null) selectAllBtn.IsEnabled = false;
                if (selectNoneBtn != null) selectNoneBtn.IsEnabled = false;

                // Show empty state, hide tree
                if (emptyState != null) emptyState.Visibility = System.Windows.Visibility.Visible;
                if (freqTree != null) freqTree.Visibility = System.Windows.Visibility.Collapsed;

                logger.Debug($"   ? Buttons disabled, empty state visible (no groups)");
            }
        }

        /// <summary>
        /// Filters frequencies based on search text (future feature).
        /// </summary>
        /// <param name="searchText">The search text to filter by.</param>
        private void FilterFrequencies(string? searchText)
        {
            if (!EnableSearch || string.IsNullOrWhiteSpace(searchText))
            {
                // Show all frequencies
                // TODO: Implement filtering logic
                return;
            }

            // TODO: Filter frequency tree based on search text
            // This could filter by:
            // - Frequency value
            // - Modulation type
            // - Pilot name
            // - Coalition
        }

        /// <summary>
        /// Gets the total number of frequencies across all groups.
        /// </summary>
        /// <returns>The total frequency count.</returns>
        public int GetTotalFrequencyCount()
        {
            if (Frequencies == null)
                return 0;

            int count = 0;
            foreach (var group in Frequencies)
            {
                count += group.Frequencies?.Count ?? 0;
            }
            return count;
        }

        /// <summary>
        /// Gets the number of currently selected frequencies.
        /// </summary>
        /// <returns>The selected frequency count.</returns>
        public int GetSelectedFrequencyCount()
        {
            if (Frequencies == null)
                return 0;

            int count = 0;
            foreach (var group in Frequencies)
            {
                if (group.Frequencies != null)
                {
                    foreach (var freq in group.Frequencies)
                    {
                        if (freq.IsSelected)
                            count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Checks if all frequencies are currently selected.
        /// </summary>
        /// <returns>True if all frequencies are selected, false otherwise.</returns>
        public bool AreAllFrequenciesSelected()
        {
            if (Frequencies == null || Frequencies.Count == 0)
                return false;

            foreach (var group in Frequencies)
            {
                if (group.Frequencies != null)
                {
                    foreach (var freq in group.Frequencies)
                    {
                        if (!freq.IsSelected)
                            return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Checks if no frequencies are currently selected.
        /// </summary>
        /// <returns>True if no frequencies are selected, false otherwise.</returns>
        public bool AreNoFrequenciesSelected()
        {
            return GetSelectedFrequencyCount() == 0;
        }

        #endregion
    }
}
