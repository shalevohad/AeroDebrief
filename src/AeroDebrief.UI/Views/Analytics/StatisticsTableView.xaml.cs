using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace AeroDebrief.UI.Views.Analytics
{
    /// <summary>
    /// Data grid summarizing frequency usage statistics
    /// </summary>
    public partial class StatisticsTableView : UserControl
    {
        public static readonly DependencyProperty StatisticsDataProperty =
            DependencyProperty.Register(nameof(StatisticsData), typeof(ObservableCollection<FrequencyStatistics>),
                typeof(StatisticsTableView), new PropertyMetadata(null, OnStatisticsDataChanged));

        public static readonly DependencyProperty HasDataProperty =
            DependencyProperty.Register(nameof(HasData), typeof(bool),
                typeof(StatisticsTableView), new PropertyMetadata(false));

        public ObservableCollection<FrequencyStatistics>? StatisticsData
        {
            get => (ObservableCollection<FrequencyStatistics>?)GetValue(StatisticsDataProperty);
            set => SetValue(StatisticsDataProperty, value);
        }

        public bool HasData
        {
            get => (bool)GetValue(HasDataProperty);
            set => SetValue(HasDataProperty, value);
        }

        public StatisticsTableView()
        {
            InitializeComponent();
        }

        private static void OnStatisticsDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StatisticsTableView view)
            {
                view.StatisticsDataGrid.ItemsSource = e.NewValue as ObservableCollection<FrequencyStatistics>;
                view.HasData = e.NewValue != null && ((ObservableCollection<FrequencyStatistics>)e.NewValue).Count > 0;
            }
        }
    }

    /// <summary>
    /// Statistics data model for a single frequency
    /// </summary>
    public class FrequencyStatistics
    {
        public string Frequency { get; set; } = string.Empty;
        public int UserCount { get; set; }
        public int MessageCount { get; set; }
        public string TotalDuration { get; set; } = string.Empty;
        public string AveragePower { get; set; } = string.Empty;
    }
}
