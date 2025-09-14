using Microsoft.Extensions.DependencyInjection;
using PersonalAiAssistant.Services;
using PersonalAiAssistant.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace PersonalAiAssistant.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly IServiceProvider _serviceProvider;

        public MainWindow(MainWindowViewModel viewModel, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _serviceProvider = serviceProvider;
            DataContext = _viewModel;
            
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private async void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            await _viewModel.ShutdownAsync();
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessage();
        }

        private async void MessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(MessageInput.Text))
            {
                await SendMessage();
            }
        }

        private async Task SendMessage()
        {
            var message = MessageInput.Text.Trim();
            if (!string.IsNullOrEmpty(message))
            {
                MessageInput.Text = string.Empty;
                await _viewModel.SendMessageAsync(message);
                
                // Auto-scroll to bottom
                ChatScrollViewer.ScrollToEnd();
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private async void RefreshTodoButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.RefreshTodoListAsync();
        }
    }
}