using Microsoft.Win32;
using PersonalAiAssistant.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace PersonalAiAssistant.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly IConfigurationManager _configManager;
        private readonly IWakeWordService _wakeWordService;
        private readonly ISTTService _sttService;
        private readonly ITTSService _ttsService;
        private readonly ILLMService _llmService;
        private readonly IMicrosoftTodoService _todoService;
        private readonly IPrayerTimeService _prayerService;
        private readonly IActivityWatchClient _activityWatch;

        // Voice Settings Properties
        private string _wakeWord = "Assistant";
        private string _sttModelPath = "Models/vosk-model-small-en-us-0.15";
        private string _kokoroServerUrl = "http://localhost:8020";
        private int _sampleRate = 16000;
        private string _wakeWordStatus = "Not Initialized";
        private string _sttStatus = "Model Not Found";
        private string _ttsStatus = "Server Not Running";

        // LLM Settings Properties
        private string _llmProvider = "Gemini";
        private string _apiKey = string.Empty;
        private int _maxTokens = 1000;
        private double _temperature = 0.7;

        // Microsoft Account Properties
        private string _microsoftAccountStatus = "Not signed in";
        private bool _isSignedIn = false;

        // Prayer Settings Properties
        private string _city = string.Empty;
        private string _country = string.Empty;
        private double _latitude = 0.0;
        private double _longitude = 0.0;
        private bool _enableNotifications = true;
        private int _notificationMinutes = 5;

        // ActivityWatch Properties
        private string _activityWatchServerUrl = "http://localhost:5600";
        private bool _enableActivityTracking = true;
        private string _activityWatchStatus = "Not Connected";

        public SettingsViewModel(
            IConfigurationManager configManager,
            IWakeWordService wakeWordService,
            ISTTService sttService,
            ITTSService ttsService,
            ILLMService llmService,
            IMicrosoftTodoService todoService,
            IPrayerTimeService prayerService,
            IActivityWatchClient activityWatch)
        {
            _configManager = configManager;
            _wakeWordService = wakeWordService;
            _sttService = sttService;
            _ttsService = ttsService;
            _llmService = llmService;
            _todoService = todoService;
            _prayerService = prayerService;
            _activityWatch = activityWatch;
        }

        #region Voice Settings Properties
        public string WakeWord
        {
            get => _wakeWord;
            set => SetProperty(ref _wakeWord, value);
        }

        public string STTModelPath
        {
            get => _sttModelPath;
            set => SetProperty(ref _sttModelPath, value);
        }

        public string KokoroServerUrl
        {
            get => _kokoroServerUrl;
            set => SetProperty(ref _kokoroServerUrl, value);
        }

        public int SampleRate
        {
            get => _sampleRate;
            set => SetProperty(ref _sampleRate, value);
        }

        public string WakeWordStatus
        {
            get => _wakeWordStatus;
            set => SetProperty(ref _wakeWordStatus, value);
        }

        public string STTStatus
        {
            get => _sttStatus;
            set => SetProperty(ref _sttStatus, value);
        }

        public string TTSStatus
        {
            get => _ttsStatus;
            set => SetProperty(ref _ttsStatus, value);
        }
        #endregion

        #region LLM Settings Properties
        public string LLMProvider
        {
            get => _llmProvider;
            set => SetProperty(ref _llmProvider, value);
        }

        public string APIKey
        {
            get => _apiKey;
            set => SetProperty(ref _apiKey, value);
        }

        public int MaxTokens
        {
            get => _maxTokens;
            set => SetProperty(ref _maxTokens, value);
        }

        public double Temperature
        {
            get => _temperature;
            set => SetProperty(ref _temperature, value);
        }
        #endregion

        #region Microsoft Account Properties
        public string MicrosoftAccountStatus
        {
            get => _microsoftAccountStatus;
            set => SetProperty(ref _microsoftAccountStatus, value);
        }

        public bool IsSignedIn
        {
            get => _isSignedIn;
            set => SetProperty(ref _isSignedIn, value);
        }
        #endregion

        #region Prayer Settings Properties
        public string City
        {
            get => _city;
            set => SetProperty(ref _city, value);
        }

        public string Country
        {
            get => _country;
            set => SetProperty(ref _country, value);
        }

        public double Latitude
        {
            get => _latitude;
            set => SetProperty(ref _latitude, value);
        }

        public double Longitude
        {
            get => _longitude;
            set => SetProperty(ref _longitude, value);
        }

        public bool EnableNotifications
        {
            get => _enableNotifications;
            set => SetProperty(ref _enableNotifications, value);
        }

        public int NotificationMinutes
        {
            get => _notificationMinutes;
            set => SetProperty(ref _notificationMinutes, value);
        }
        #endregion

        #region ActivityWatch Properties
        public string ActivityWatchServerUrl
        {
            get => _activityWatchServerUrl;
            set => SetProperty(ref _activityWatchServerUrl, value);
        }

        public bool EnableActivityTracking
        {
            get => _enableActivityTracking;
            set => SetProperty(ref _enableActivityTracking, value);
        }

        public string ActivityWatchStatus
        {
            get => _activityWatchStatus;
            set => SetProperty(ref _activityWatchStatus, value);
        }
        #endregion

        public async Task LoadSettingsAsync()
        {
            try
            {
                await _configManager.LoadSettingsAsync();

                // Load Voice Settings
                WakeWord = _configManager.WakeWord;
                STTModelPath = _configManager.VoskModelPath;
                KokoroServerUrl = _configManager.KokoroServerUrl;
                SampleRate = _configManager.AudioSampleRate;

                // Load LLM Settings
                LLMProvider = _configManager.LLMProvider;
                APIKey = _configManager.GeminiApiKey; // Will switch based on provider
                MaxTokens = _configManager.MaxTokens;
                Temperature = _configManager.Temperature;

                // Load Prayer Settings
                City = _configManager.City;
                Country = _configManager.Country;
                Latitude = _configManager.Latitude;
                Longitude = _configManager.Longitude;
                EnableNotifications = _configManager.EnablePrayerNotifications;
                NotificationMinutes = _configManager.NotificationMinutesBefore;

                // Load ActivityWatch Settings
                ActivityWatchServerUrl = _configManager.ActivityWatchServerUrl;
                EnableActivityTracking = _configManager.EnableActivityTracking;

                // Update status indicators
                await UpdateServiceStatusesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task SaveSettingsAsync()
        {
            try
            {
                // Save Voice Settings
                _configManager.WakeWord = WakeWord;
                _configManager.VoskModelPath = STTModelPath;
                _configManager.KokoroServerUrl = KokoroServerUrl;
                _configManager.AudioSampleRate = SampleRate;

                // Save LLM Settings
                _configManager.LLMProvider = LLMProvider;
                if (LLMProvider == "Gemini")
                    _configManager.GeminiApiKey = APIKey;
                else
                    _configManager.GroqApiKey = APIKey;
                _configManager.MaxTokens = MaxTokens;
                _configManager.Temperature = Temperature;

                // Save Prayer Settings
                _configManager.City = City;
                _configManager.Country = Country;
                _configManager.Latitude = Latitude;
                _configManager.Longitude = Longitude;
                _configManager.EnablePrayerNotifications = EnableNotifications;
                _configManager.NotificationMinutesBefore = NotificationMinutes;

                // Save ActivityWatch Settings
                _configManager.ActivityWatchServerUrl = ActivityWatchServerUrl;
                _configManager.EnableActivityTracking = EnableActivityTracking;

                await _configManager.SaveSettingsAsync();

                MessageBox.Show("Settings saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save settings: {ex.Message}");
            }
        }

        #region Voice Settings Methods
        public async Task TestWakeWordAsync()
        {
            try
            {
                WakeWordStatus = "Testing...";
                var result = await _wakeWordService.InitializeAsync(WakeWord);
                WakeWordStatus = result ? "Ready" : "Failed to initialize";
            }
            catch (Exception ex)
            {
                WakeWordStatus = $"Error: {ex.Message}";
            }
        }

        public void BrowseSTTModel()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Vosk Model Directory",
                Filter = "All files (*.*)|*.*",
                CheckFileExists = false,
                CheckPathExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                STTModelPath = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            }
        }

        public async Task DownloadVoskModelAsync()
        {
            try
            {
                STTStatus = "Downloading model...";
                // Implementation would download Vosk model
                await Task.Delay(1000); // Placeholder
                STTStatus = "Model downloaded";
                MessageBox.Show("Vosk model download feature will be implemented in Phase 4.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                STTStatus = $"Download failed: {ex.Message}";
            }
        }

        public async Task TestSTTAsync()
        {
            try
            {
                STTStatus = "Testing...";
                var result = await _sttService.InitializeAsync(STTModelPath);
                STTStatus = result ? "Ready" : "Model not found or invalid";
            }
            catch (Exception ex)
            {
                STTStatus = $"Error: {ex.Message}";
            }
        }

        public async Task TestTTSAsync()
        {
            try
            {
                TTSStatus = "Testing connection...";
                var result = await _ttsService.TestConnectionAsync();
                TTSStatus = result ? "Connected" : "Server not responding";
            }
            catch (Exception ex)
            {
                TTSStatus = $"Error: {ex.Message}";
            }
        }

        public async Task SetupKokoroServerAsync()
        {
            MessageBox.Show("Kokoro server setup will be implemented in Phase 4 with automated installation scripts.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            await Task.CompletedTask;
        }

        public async Task StartKokoroServerAsync()
        {
            try
            {
                TTSStatus = "Starting server...";
                // Implementation would start Kokoro server
                await Task.Delay(1000); // Placeholder
                TTSStatus = "Server starting...";
            }
            catch (Exception ex)
            {
                TTSStatus = $"Failed to start: {ex.Message}";
            }
        }

        public async Task StopKokoroServerAsync()
        {
            try
            {
                TTSStatus = "Stopping server...";
                // Implementation would stop Kokoro server
                await Task.Delay(1000); // Placeholder
                TTSStatus = "Server stopped";
            }
            catch (Exception ex)
            {
                TTSStatus = $"Failed to stop: {ex.Message}";
            }
        }
        #endregion

        #region LLM Settings Methods
        public async Task TestLLMConnectionAsync()
        {
            try
            {
                await _llmService.SetProviderAsync(LLMProvider, APIKey);
                var result = await _llmService.TestConnectionAsync();
                var message = result ? "Connection successful!" : "Connection failed. Please check your API key.";
                MessageBox.Show(message, "LLM Test", MessageBoxButton.OK, result ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error testing connection: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Microsoft Account Methods
        public async Task SignInToMicrosoftAsync()
        {
            try
            {
                MicrosoftAccountStatus = "Signing in...";
                var result = await _todoService.AuthenticateAsync();
                
                if (result)
                {
                    IsSignedIn = true;
                    MicrosoftAccountStatus = $"Signed in as {_todoService.UserDisplayName}";
                }
                else
                {
                    MicrosoftAccountStatus = "Sign in failed";
                }
            }
            catch (Exception ex)
            {
                MicrosoftAccountStatus = $"Error: {ex.Message}";
            }
        }

        public async Task SignOutFromMicrosoftAsync()
        {
            try
            {
                await _todoService.SignOutAsync();
                IsSignedIn = false;
                MicrosoftAccountStatus = "Not signed in";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error signing out: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Prayer Settings Methods
        public async Task AutoDetectLocationAsync()
        {
            try
            {
                // Implementation would use geolocation API
                MessageBox.Show("Auto-location detection will be implemented in Phase 2.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error detecting location: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task TestPrayerTimesAsync()
        {
            try
            {
                if (Latitude == 0 && Longitude == 0)
                {
                    MessageBox.Show("Please set your location first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = await _prayerService.InitializeAsync(Latitude, Longitude);
                if (result)
                {
                    var prayerTimes = await _prayerService.GetPrayerTimesAsync(DateTime.Today);
                    var message = $"Prayer times for today:\n" +
                                 $"Fajr: {prayerTimes.Fajr:HH:mm}\n" +
                                 $"Sunrise: {prayerTimes.Sunrise:HH:mm}\n" +
                                 $"Dhuhr: {prayerTimes.Dhuhr:HH:mm}\n" +
                                 $"Asr: {prayerTimes.Asr:HH:mm}\n" +
                                 $"Maghrib: {prayerTimes.Maghrib:HH:mm}\n" +
                                 $"Isha: {prayerTimes.Isha:HH:mm}";
                    MessageBox.Show(message, "Prayer Times", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to calculate prayer times. Please check your location.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error testing prayer times: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region ActivityWatch Methods
        public async Task TestActivityWatchConnectionAsync()
        {
            try
            {
                ActivityWatchStatus = "Testing connection...";
                var result = await _activityWatch.ConnectAsync(ActivityWatchServerUrl);
                ActivityWatchStatus = result ? "Connected" : "Connection failed";
                
                var message = result ? "Successfully connected to ActivityWatch!" : "Failed to connect. Make sure ActivityWatch is running.";
                MessageBox.Show(message, "ActivityWatch Test", MessageBoxButton.OK, result ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                ActivityWatchStatus = $"Error: {ex.Message}";
                MessageBox.Show($"Error testing connection: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        private async Task UpdateServiceStatusesAsync()
        {
            // Update Wake Word Status
            WakeWordStatus = _wakeWordService.IsListening ? "Ready" : "Not Initialized";

            // Update STT Status
            STTStatus = _sttService.IsInitialized ? "Ready" : "Model Not Found";

            // Update TTS Status
            TTSStatus = _ttsService.IsInitialized ? "Connected" : "Server Not Running";

            // Update Microsoft Account Status
            IsSignedIn = _todoService.IsAuthenticated;
            MicrosoftAccountStatus = IsSignedIn ? $"Signed in as {_todoService.UserDisplayName}" : "Not signed in";

            // Update ActivityWatch Status
            ActivityWatchStatus = _activityWatch.IsConnected ? "Connected" : "Not Connected";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}