using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace Symphex.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
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
    }
}