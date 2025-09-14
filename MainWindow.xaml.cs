using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersonalAiAssistant.Interfaces;
using PersonalAiAssistant.Models;
using PersonalAiAssistant.Services;

namespace PersonalAiAssistant
{
    public partial class MainWindow : Window
    {
        private readonly ILogger<MainWindow> _logger;
        private readonly IApplicationOrchestrator _orchestrator;
        private readonly IConfigurationManager _configManager;
        private readonly IErrorHandlingService _errorHandler;
        private readonly ILoggingService _loggingService;
        private readonly IWakeWordService _wakeWordService;
        private readonly ISpeechToTextService _sttService;
        private readonly ITextToSpeechService _ttsService;
        private readonly ILLMService _llmService;
        private readonly IMicrosoftToDoService _todoService;
        private readonly IPrayerTimeService _prayerService;
        private readonly IActivityWatchService _activityService;
        private readonly DispatcherTimer _statusUpdateTimer;
        private readonly StringBuilder _activityLog;
        private bool _isSystemRunning = false;
        private bool _isInitialized;

        public MainWindow(
            ILogger<MainWindow> logger,
            IApplicationOrchestrator orchestrator,
            IConfigurationManager configManager,
            IErrorHandlingService errorHandler,
            ILoggingService loggingService,
            IWakeWordService wakeWordService,
            ISpeechToTextService sttService,
            ITextToSpeechService ttsService,
            ILLMService llmService,
            IMicrosoftToDoService todoService,
            IPrayerTimeService prayerService,
            IActivityWatchService activityService)
        {
            InitializeComponent();
            
            _logger = logger;
            _orchestrator = orchestrator;
            _configManager = configManager;
            _errorHandler = errorHandler;
            _loggingService = loggingService;
            _wakeWordService = wakeWordService;
            _sttService = sttService;
            _ttsService = ttsService;
            _llmService = llmService;
            _todoService = todoService;
            _prayerService = prayerService;
            _activityService = activityService;
            _activityLog = new StringBuilder();
            
            // Initialize status update timer
            _statusUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
            _statusUpdateTimer.Start();
            
            // Subscribe to orchestrator events
            SubscribeToEvents();
            
            // Initialize UI
            InitializeUI();
            
            _logger.LogInformation("MainWindow initialized");
        }

        private void SubscribeToEvents()
        {
            _orchestrator.SystemStatusChanged += OnSystemStatusChanged;
            _orchestrator.CommandProcessed += OnCommandProcessed;
            _orchestrator.EmergencyShutdown += OnEmergencyShutdown;
        }

        private void InitializeUI()
        {
            // Load settings and populate UI
            LoadSettings();
            
            // Initialize combo boxes
            InitializeComboBoxes();
            
            // Update initial status
            UpdateSystemStatus("Initialized", false);
            
            AddActivityLog("System initialized and ready");
            
            // Initialize async components
            _ = Task.Run(InitializeAsync);
        }

        private async void InitializeAsync()
        {
            await _errorHandler.ExecuteWithErrorHandlingAsync(async () =>
            {
                _loggingService.LogInfo("Initializing Main Window...", "MainWindow");
                
                // Initialize the orchestrator
                bool initialized = await _orchestrator.InitializeAsync();
                
                if (initialized)
                {
                    _isInitialized = true;
                    _loggingService.LogInfo("Main Window initialized successfully", "MainWindow");
                    
                    // Subscribe to events
                    SubscribeToEvents();
                    
                    // Load initial settings
                    await LoadSettingsAsync();
                    
                    // Update UI
                    UpdateSystemStatus();
                    
                    // Start status update timer
                    _statusUpdateTimer.Start();
                }
                else
                {
                    _loggingService.LogError("Failed to initialize Main Window", "MainWindow");
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Failed to initialize the application. Please check the logs for more details.", 
                            "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            }, "InitializeAsync", "MainWindow");
        }

        private async Task LoadSettingsAsync()
        {
            await Task.Run(() => LoadSettings());
        }

        private void UpdateSystemStatus()
        {
            UpdateSystemStatus("Ready", _isSystemRunning);
        }

        private void InitializeComboBoxes()
        {
            // Voice combo box (will be populated when TTS service is available)
            VoiceComboBox.Items.Add("Default");
            VoiceComboBox.SelectedIndex = 0;
            
            // LLM Provider combo box
            LLMProviderComboBox.Items.Add("OpenAI");
            LLMProviderComboBox.Items.Add("Anthropic");
            LLMProviderComboBox.Items.Add("Local");
            LLMProviderComboBox.SelectedIndex = 0;
            
            // LLM Model combo box
            LLMModelComboBox.Items.Add("gpt-4");
            LLMModelComboBox.Items.Add("gpt-3.5-turbo");
            LLMModelComboBox.Items.Add("claude-3-sonnet");
            LLMModelComboBox.Items.Add("claude-3-haiku");
            LLMModelComboBox.SelectedIndex = 0;
            
            // Calculation Method combo box
            CalculationMethodComboBox.Items.Add("Muslim World League");
            CalculationMethodComboBox.Items.Add("Islamic Society of North America");
            CalculationMethodComboBox.Items.Add("Egyptian General Authority of Survey");
            CalculationMethodComboBox.Items.Add("Umm Al-Qura University, Makkah");
            CalculationMethodComboBox.Items.Add("University of Islamic Sciences, Karachi");
            CalculationMethodComboBox.SelectedIndex = 0;
        }

        private void LoadSettings()
        {
            try
            {
                var settings = _configManager.LoadSettings();
                
                // Voice settings
                WakeWordTextBox.Text = settings.Voice.WakeWord;
                ConfidenceSlider.Value = settings.Voice.ConfidenceThreshold;
                VolumeSlider.Value = settings.Voice.Volume;
                
                // LLM settings
                LLMProviderComboBox.SelectedItem = settings.LLM.Provider;
                LLMModelComboBox.SelectedItem = settings.LLM.Model;
                BaseURLTextBox.Text = settings.LLM.BaseUrl;
                
                // Microsoft settings
                ClientIDTextBox.Text = settings.Microsoft.ClientId;
                EnableTodoCheckBox.IsChecked = settings.Microsoft.EnableTodoIntegration;
                
                // Prayer settings
                CityTextBox.Text = settings.Prayer.City;
                CountryTextBox.Text = settings.Prayer.Country;
                CalculationMethodComboBox.SelectedItem = settings.Prayer.CalculationMethod;
                EnablePrayerNotificationsCheckBox.IsChecked = settings.Prayer.EnableNotifications;
                
                _logger.LogInformation("Settings loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load settings");
                AddActivityLog($"Error loading settings: {ex.Message}");
            }
        }

        private void SaveSettings()
        {
            try
            {
                var settings = _configManager.LoadSettings();
                
                // Voice settings
                settings.Voice.WakeWord = WakeWordTextBox.Text;
                settings.Voice.ConfidenceThreshold = (float)ConfidenceSlider.Value;
                settings.Voice.Volume = (int)VolumeSlider.Value;
                
                // LLM settings
                settings.LLM.Provider = LLMProviderComboBox.SelectedItem?.ToString() ?? "OpenAI";
                settings.LLM.Model = LLMModelComboBox.SelectedItem?.ToString() ?? "gpt-4";
                settings.LLM.ApiKey = APIKeyPasswordBox.Password;
                settings.LLM.BaseUrl = BaseURLTextBox.Text;
                
                // Microsoft settings
                settings.Microsoft.ClientId = ClientIDTextBox.Text;
                settings.Microsoft.EnableTodoIntegration = EnableTodoCheckBox.IsChecked ?? false;
                
                // Prayer settings
                settings.Prayer.City = CityTextBox.Text;
                settings.Prayer.Country = CountryTextBox.Text;
                settings.Prayer.CalculationMethod = CalculationMethodComboBox.SelectedItem?.ToString() ?? "Muslim World League";
                settings.Prayer.EnableNotifications = EnablePrayerNotificationsCheckBox.IsChecked ?? false;
                
                _configManager.SaveSettings(settings);
                
                AddActivityLog("Settings saved successfully");
                MessageBox.Show("Settings saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                
                _logger.LogInformation("Settings saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings");
                AddActivityLog($"Error saving settings: {ex.Message}");
                MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddActivityLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                _activityLog.AppendLine($"[{timestamp}] {message}");
                
                // Keep only last 50 lines
                var lines = _activityLog.ToString().Split('\n');
                if (lines.Length > 50)
                {
                    _activityLog.Clear();
                    _activityLog.AppendLine(string.Join("\n", lines.Skip(lines.Length - 50)));
                }
                
                ActivityLogText.Text = _activityLog.ToString();
            });
        }

        private void UpdateSystemStatus(string status, bool isRunning)
        {
            Dispatcher.Invoke(() =>
            {
                SystemStatusText.Text = status;
                SystemStatusIndicator.Fill = isRunning ? new SolidColorBrush(Color.FromRgb(40, 167, 69)) : new SolidColorBrush(Color.FromRgb(108, 117, 125));
                _isSystemRunning = isRunning;
                
                StartSystemBtn.IsEnabled = !isRunning;
                StopSystemBtn.IsEnabled = isRunning;
            });
        }

        private void UpdateServiceStatus(string serviceName, bool isConnected, string statusText = "")
        {
            Dispatcher.Invoke(() =>
            {
                var color = isConnected ? new SolidColorBrush(Color.FromRgb(40, 167, 69)) : new SolidColorBrush(Color.FromRgb(220, 53, 69));
                var text = isConnected ? "Connected" : "Disconnected";
                
                if (!string.IsNullOrEmpty(statusText))
                    text = statusText;
                
                switch (serviceName.ToLower())
                {
                    case "todo":
                        TodoStatusText.Text = text;
                        TodoStatusText.Foreground = color;
                        break;
                    case "prayer":
                        PrayerStatusText.Text = text;
                        PrayerStatusText.Foreground = color;
                        break;
                    case "activitywatch":
                        ActivityWatchStatusText.Text = text;
                        ActivityWatchStatusText.Foreground = color;
                        break;
                    case "llm":
                        LLMStatusText.Text = text;
                        LLMStatusText.Foreground = color;
                        break;
                    case "wakeword":
                        WakeWordStatusText.Text = text;
                        WakeWordStatusIndicator.Fill = color;
                        break;
                    case "stt":
                        STTStatusText.Text = text;
                        STTStatusIndicator.Fill = color;
                        break;
                    case "tts":
                        TTSStatusText.Text = text;
                        TTSStatusIndicator.Fill = color;
                        break;
                }
            });
        }

        #region Event Handlers

        private void OnSystemStatusChanged(object? sender, SystemStatusChangedEventArgs e)
        {
            AddActivityLog($"System status: {e.Status}");
            UpdateSystemStatus(e.Status, e.Status == "ready" || e.Status == "listening" || e.Status == "processing" || e.Status == "speaking");
            
            // Update individual service statuses
            foreach (var serviceStatus in e.ServiceStatuses)
            {
                UpdateServiceStatus(serviceStatus.Key, serviceStatus.Value);
            }
        }

        private void OnCommandProcessed(object? sender, CommandProcessedEventArgs e)
        {
            AddActivityLog($"Command processed: {e.Command} -> {e.Response}");
        }

        private void OnEmergencyShutdown(object? sender, EmergencyShutdownEventArgs e)
        {
            AddActivityLog($"Emergency shutdown: {e.Reason}");
            UpdateSystemStatus("Emergency Shutdown", false);
            MessageBox.Show($"Emergency shutdown occurred: {e.Reason}", "Emergency Shutdown", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void StatusUpdateTimer_Tick(object? sender, EventArgs e)
        {
            // Update UI elements that need periodic refresh
            // This could include checking service health, updating next prayer time, etc.
        }

        #endregion

        #region UI Event Handlers

        private void DashboardTab_Click(object sender, RoutedEventArgs e)
        {
            DashboardPage.Visibility = Visibility.Visible;
            SettingsPage.Visibility = Visibility.Collapsed;
            
            DashboardTab.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212));
            SettingsTab.Foreground = new SolidColorBrush(Color.FromRgb(176, 176, 176));
        }

        private void SettingsTab_Click(object sender, RoutedEventArgs e)
        {
            DashboardPage.Visibility = Visibility.Collapsed;
            SettingsPage.Visibility = Visibility.Visible;
            
            SettingsTab.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212));
            DashboardTab.Foreground = new SolidColorBrush(Color.FromRgb(176, 176, 176));
        }

        private async void StartSystem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddActivityLog("Starting system...");
                await _orchestrator.StartAsync();
                AddActivityLog("System started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start system");
                AddActivityLog($"Failed to start system: {ex.Message}");
                MessageBox.Show($"Failed to start system: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StopSystem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddActivityLog("Stopping system...");
                await _orchestrator.StopAsync();
                AddActivityLog("System stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop system");
                AddActivityLog($"Failed to stop system: {ex.Message}");
                MessageBox.Show($"Failed to stop system: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ToggleWakeWord_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // This would need to be implemented based on the wake word service status
                AddActivityLog("Toggling wake word detection...");
                // Implementation depends on orchestrator providing wake word control
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to toggle wake word");
                AddActivityLog($"Failed to toggle wake word: {ex.Message}");
            }
        }

        private async void TestTTS_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddActivityLog("Testing TTS...");
                // This would need to be implemented to test TTS service
                await Task.Delay(100); // Placeholder
                AddActivityLog("TTS test completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to test TTS");
                AddActivityLog($"Failed to test TTS: {ex.Message}");
            }
        }

        private async void RefreshTodos_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddActivityLog("Refreshing todos...");
                // This would need to be implemented to refresh Microsoft Todo
                await Task.Delay(100); // Placeholder
                AddActivityLog("Todos refreshed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh todos");
                AddActivityLog($"Failed to refresh todos: {ex.Message}");
            }
        }

        private async void CheckPrayerTimes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddActivityLog("Checking prayer times...");
                // This would need to be implemented to check prayer times
                await Task.Delay(100); // Placeholder
                AddActivityLog("Prayer times checked");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check prayer times");
                AddActivityLog($"Failed to check prayer times: {ex.Message}");
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to reset all settings to defaults? This action cannot be undone.", 
                                       "Reset Settings", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _configManager.ResetToDefaults();
                    LoadSettings();
                    AddActivityLog("Settings reset to defaults");
                    MessageBox.Show("Settings have been reset to defaults.", "Reset Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reset settings");
                    AddActivityLog($"Failed to reset settings: {ex.Message}");
                    MessageBox.Show($"Failed to reset settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void Authenticate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddActivityLog("Starting Microsoft authentication...");
                // This would need to be implemented to authenticate with Microsoft
                await Task.Delay(100); // Placeholder
                AddActivityLog("Microsoft authentication completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to authenticate with Microsoft");
                AddActivityLog($"Failed to authenticate: {ex.Message}");
                MessageBox.Show($"Failed to authenticate with Microsoft: {ex.Message}", "Authentication Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _statusUpdateTimer?.Stop();
            
            // Unsubscribe from events
            _orchestrator.SystemStatusChanged -= OnSystemStatusChanged;
            _orchestrator.CommandProcessed -= OnCommandProcessed;
            _orchestrator.EmergencyShutdown -= OnEmergencyShutdown;
            
            base.OnClosed(e);
        }
    }
}