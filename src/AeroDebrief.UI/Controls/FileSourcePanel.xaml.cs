using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AeroDebrief.UI.ViewModels;

namespace AeroDebrief.UI.Controls
{
    /// <summary>
    /// Panel for file playback source with drop area and recent files list
    /// </summary>
    public partial class FileSourcePanel : UserControl
    {
        private Brush? _originalDropZoneBrush;
        private Brush? _originalDropZoneBorderBrush;

        public FileSourcePanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Store original brushes for drag feedback
            if (FindName("DropZone") is Border dropZone)
            {
                _originalDropZoneBrush = dropZone.Background;
                _originalDropZoneBorderBrush = dropZone.BorderBrush;
            }
        }

        private void OnPreviewDragOver(object sender, DragEventArgs e)
        {
            bool isValidFile = false;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
                if (files.Length > 0)
                {
                    var ext = System.IO.Path.GetExtension(files[0]).ToLowerInvariant();
                    isValidFile = ext == ".adb" || ext == ".raw";
                }
            }

            e.Effects = isValidFile ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;

            // Visual feedback
            if (sender is Border dropZone)
            {
                if (isValidFile)
                {
                    dropZone.Background = new SolidColorBrush(Color.FromRgb(227, 242, 253)); // Light blue
                    dropZone.BorderBrush = Application.Current.FindResource("AccentBrush") as Brush;
                    dropZone.BorderThickness = new Thickness(2);
                }
                else
                {
                    dropZone.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238)); // Light red
                    dropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(211, 47, 47)); // Red
                    dropZone.BorderThickness = new Thickness(2);
                }
            }
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            // Reset visual feedback
            if (sender is Border dropZone && _originalDropZoneBrush != null)
            {
                dropZone.Background = _originalDropZoneBrush;
                dropZone.BorderBrush = _originalDropZoneBorderBrush;
                dropZone.BorderThickness = new Thickness(2);
            }

            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (files.Length == 0) return;

            var file = files[0];
            var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
            if (ext != ".adb" && ext != ".raw") return;

            if (DataContext is ViewModels.FileSourceViewModel vm)
            {
                vm.SelectedFilePath = file;
                // Trigger load via ICommand
                if (vm.LoadFileCommand?.CanExecute(null) == true)
                {
                    vm.LoadFileCommand.Execute(null);
                }
            }
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            // Reset visual feedback when drag leaves
            if (sender is Border dropZone && _originalDropZoneBrush != null)
            {
                dropZone.Background = _originalDropZoneBrush;
                dropZone.BorderBrush = _originalDropZoneBorderBrush;
                dropZone.BorderThickness = new Thickness(2);
            }
        }

        /// <summary>
        /// Handles double-click on recent files list to open the file
        /// </summary>
        private void RecentFiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is string selectedFilePath)
            {
                // Get the ViewModel
                if (DataContext is FileSourceViewModel viewModel)
                {
                    // Execute the OpenRecentFileCommand with the selected file path
                    if (viewModel.OpenRecentFileCommand.CanExecute(selectedFilePath))
                    {
                        viewModel.OpenRecentFileCommand.Execute(selectedFilePath);
                    }
                }

                e.Handled = true;
            }
        }
    }
}
