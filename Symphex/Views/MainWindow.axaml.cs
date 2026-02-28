using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Input;
using System;
using System.Linq;

namespace Symphex.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            // Set background immediately before anything else
            Background = Avalonia.Media.Brushes.Black;
            
            InitializeComponent();

            // Center the window when it starts
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Size window based on screen dimensions
            SetWindowSizeBasedOnScreen();

            this.Loaded += MainWindow_Loaded;
            
            // Set up drag-and-drop for the URL input area
            AddHandler(DragDrop.DragOverEvent, OnDragOver);
            AddHandler(DragDrop.DropEvent, OnDrop);
        }

        private void SetWindowSizeBasedOnScreen()
        {
            try
            {
                // Get the primary screen
                var screen = Screens.Primary;

                if (screen != null)
                {
                    // Get screen working area (excludes taskbar)
                    var workingArea = screen.WorkingArea;

                    // Calculate window size as percentage of screen
                    // Use smaller percentages: 50% width and 60% height
                    double targetWidth = workingArea.Width * 0.50;
                    double targetHeight = workingArea.Height * 0.60;

                    // Set constraints to match your current XAML values
                    const double MIN_WIDTH = 900;
                    const double MIN_HEIGHT = 650;
                    const double MAX_WIDTH = 1100;  // Slightly larger than minimum
                    const double MAX_HEIGHT = 800;   // Slightly larger than minimum

                    // Apply constraints
                    Width = Math.Max(MIN_WIDTH, Math.Min(MAX_WIDTH, targetWidth));
                    Height = Math.Max(MIN_HEIGHT, Math.Min(MAX_HEIGHT, targetHeight));

                    // Set minimum window size to match your XAML
                    MinWidth = MIN_WIDTH;
                    MinHeight = MIN_HEIGHT;
                }
                else
                {
                    // Fallback to your current XAML dimensions
                    Width = 900;
                    Height = 650;
                    MinWidth = 900;
                    MinHeight = 650;
                }
            }
            catch (Exception)
            {
                // Fallback to your current XAML dimensions
                Width = 900;
                Height = 650;
                MinWidth = 900;
                MinHeight = 650;
            }
        }

        private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            var scrollViewer = this.FindControl<ScrollViewer>("CliOutput");
            if (DataContext is ViewModels.MainWindowViewModel viewModel)
            {
                viewModel.CliScrollViewer = scrollViewer;

                // Subscribe to property changes to auto-scroll when new text is added
                viewModel.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(viewModel.CliOutput))
                    {
                        // Auto-scroll to bottom when new content is added
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            scrollViewer?.ScrollToEnd();
                        }, Avalonia.Threading.DispatcherPriority.Background);
                    }
                };
            }
        }

        private void CliOutput_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer != null && DataContext is ViewModels.MainWindowViewModel viewModel)
            {
                // Optional: You can add logic here to detect if user manually scrolled up
                // and temporarily disable auto-scroll until they scroll back to bottom
            }
        }

        private void OnArtworkOption1Tapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.MainWindowViewModel viewModel)
            {
                viewModel.SelectArtworkCommand.Execute(0);
            }
        }

        private void OnArtworkOption2Tapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.MainWindowViewModel viewModel)
            {
                viewModel.SelectArtworkCommand.Execute(1);
            }
        }

        private void OnArtworkOption3Tapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.MainWindowViewModel viewModel)
            {
                viewModel.SelectArtworkCommand.Execute(2);
            }
        }

        private void OnArtworkOption4Tapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.MainWindowViewModel viewModel)
            {
                viewModel.SelectArtworkCommand.Execute(3);
            }
        }

        private void OnSkipArtworkSelection(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.MainWindowViewModel viewModel)
            {
                viewModel.SelectArtworkCommand.Execute(-1);
            }
        }

        private void OnNoPictureSelected(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.MainWindowViewModel viewModel)
            {
                viewModel.SelectArtworkCommand.Execute(-2); // -2 = no picture
            }
        }

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            if (DataContext is ViewModels.MainWindowViewModel viewModel)
            {
                if (viewModel.IsResizeMode || viewModel.IsMetadataMode)
                {
                    // Check if the drag data contains files
                    #pragma warning disable CS0618
                    var files = e.Data?.GetFiles();
                    #pragma warning restore CS0618
                    if (files?.Any() == true)
                    {
                        e.DragEffects = DragDropEffects.Copy;
                        e.Handled = true;
                    }
                }
            }
        }

        private async void OnDrop(object? sender, DragEventArgs e)
        {
            if (DataContext is ViewModels.MainWindowViewModel viewModel)
            {
                #pragma warning disable CS0618
                var files = e.Data?.GetFiles();
                #pragma warning restore CS0618
                
                if (files != null && files.Any())
                {
                    var paths = files.Select(f => f.Path.LocalPath).ToList();
                    
                    if (viewModel.IsResizeMode)
                    {
                        await viewModel.ProcessDroppedFilesAsync(paths);
                    }
                    else if (viewModel.IsMetadataMode)
                    {
                        await viewModel.ProcessMetadataSourceFilesAsync(paths);
                    }
                }
                e.Handled = true;
            }
        }

        private bool IsWithinSourceDropZone(Control? control)
        {
            // No longer needed - keeping for compatibility
            return false;
        }
    }
}