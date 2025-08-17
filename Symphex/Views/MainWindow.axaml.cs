using Avalonia.Controls;
using Avalonia.Interactivity;

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
            }
        }

        private void CliOutput_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer != null && DataContext is ViewModels.MainWindowViewModel viewModel)
            {
              
            }
        }
    }
}