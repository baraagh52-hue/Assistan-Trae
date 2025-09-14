using PersonalAiAssistant.ViewModels;
using System.Windows;

namespace PersonalAiAssistant.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _viewModel;

        public SettingsWindow(SettingsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            
            Loaded += SettingsWindow_Loaded;
        }

        private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadSettingsAsync();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _viewModel.SaveSettingsAsync();
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // Voice Settings Events
        private async void TestWakeWordButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.TestWakeWordAsync();
        }

        private void BrowseSTTModelButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.BrowseSTTModel();
        }

        private async void DownloadVoskModelButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.DownloadVoskModelAsync();
        }

        private async void TestSTTButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.TestSTTAsync();
        }

        private async void TestTTSButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.TestTTSAsync();
        }

        private async void SetupKokoroButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.SetupKokoroServerAsync();
        }

        private async void StartKokoroServerButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.StartKokoroServerAsync();
        }

        private async void StopKokoroServerButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.StopKokoroServerAsync();
        }

        // LLM Settings Events
        private async void TestLLMButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.TestLLMConnectionAsync();
        }

        // Microsoft Account Events
        private async void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.SignInToMicrosoftAsync();
        }

        private async void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.SignOutFromMicrosoftAsync();
        }

        // Prayer Times Events
        private async void AutoDetectLocationButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.AutoDetectLocationAsync();
        }

        private async void TestPrayerTimesButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.TestPrayerTimesAsync();
        }

        // ActivityWatch Events
        private async void TestActivityWatchButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.TestActivityWatchConnectionAsync();
        }
    }
}