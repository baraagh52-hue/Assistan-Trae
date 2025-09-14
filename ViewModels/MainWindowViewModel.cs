using PersonalAiAssistant.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace PersonalAiAssistant.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly IApplicationOrchestrator _orchestrator;
        private readonly IMicrosoftTodoService _todoService;
        private readonly IPrayerTimeService _prayerService;
        private readonly IActivityWatchClient _activityWatch;
        
        private string _statusText = "Initializing...";
        private string _voiceStatusText = "Not Ready";
        private string _connectionStatus = "Offline";
        private string _nextPrayerName = "--";
        private string _nextPrayerTime = "--:--";
        private string _timeRemaining = "--";
        
        public ObservableCollection<ChatMessage> ChatMessages { get; } = new();
        public ObservableCollection<TodoItem> TodoItems { get; } = new();
        
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }
        
        public string VoiceStatusText
        {
            get => _voiceStatusText;
            set => SetProperty(ref _voiceStatusText, value);
        }
        
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }
        
        public string NextPrayerName
        {
            get => _nextPrayerName;
            set => SetProperty(ref _nextPrayerName, value);
        }
        
        public string NextPrayerTime
        {
            get => _nextPrayerTime;
            set => SetProperty(ref _nextPrayerTime, value);
        }
        
        public string TimeRemaining
        {
            get => _timeRemaining;
            set => SetProperty(ref _timeRemaining, value);
        }

        public MainWindowViewModel(
            IApplicationOrchestrator orchestrator,
            IMicrosoftTodoService todoService,
            IPrayerTimeService prayerService,
            IActivityWatchClient activityWatch)
        {
            _orchestrator = orchestrator;
            _todoService = todoService;
            _prayerService = prayerService;
            _activityWatch = activityWatch;
            
            SubscribeToEvents();
        }

        public async Task InitializeAsync()
        {
            try
            {
                StatusText = "Initializing services...";
                
                // Initialize the main orchestrator
                await _orchestrator.InitializeAsync();
                await _orchestrator.StartServicesAsync();
                
                // Load initial data
                await RefreshTodoListAsync();
                await UpdatePrayerTimesAsync();
                
                StatusText = "Ready";
                VoiceStatusText = "Ready";
                ConnectionStatus = "Online";
                
                // Add welcome message
                AddChatMessage("Assistant", "Hello! I'm your personal AI assistant. I'm ready to help you with voice commands, managing your tasks, and keeping track of prayer times.", false);
            }
            catch (Exception ex)
            {
                StatusText = "Error during initialization";
                AddChatMessage("System", $"Error initializing: {ex.Message}", false);
            }
        }

        public async Task SendMessageAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            
            // Add user message to chat
            AddChatMessage("You", message, true);
            
            try
            {
                // Process the command
                await _orchestrator.ProcessTextCommandAsync(message);
            }
            catch (Exception ex)
            {
                AddChatMessage("Assistant", $"I encountered an error: {ex.Message}", false);
            }
        }

        public async Task RefreshTodoListAsync()
        {
            try
            {
                if (_todoService.IsAuthenticated)
                {
                    var todos = await _todoService.GetTodoItemsAsync();
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TodoItems.Clear();
                        foreach (var todo in todos.Take(10)) // Show only first 10 items
                        {
                            TodoItems.Add(todo);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                AddChatMessage("System", $"Error refreshing todo list: {ex.Message}", false);
            }
        }

        public async Task UpdatePrayerTimesAsync()
        {
            try
            {
                if (_prayerService.IsInitialized)
                {
                    var nextPrayer = await _prayerService.GetNextPrayerAsync();
                    
                    NextPrayerName = nextPrayer.Name;
                    NextPrayerTime = nextPrayer.Time.ToString("HH:mm");
                    
                    var remaining = nextPrayer.TimeRemaining;
                    if (remaining.TotalDays >= 1)
                        TimeRemaining = $"in {remaining.Days}d {remaining.Hours}h";
                    else if (remaining.TotalHours >= 1)
                        TimeRemaining = $"in {remaining.Hours}h {remaining.Minutes}m";
                    else
                        TimeRemaining = $"in {remaining.Minutes}m";
                }
            }
            catch (Exception ex)
            {
                NextPrayerName = "Error";
                NextPrayerTime = "--:--";
                TimeRemaining = "--";
            }
        }

        public async Task ShutdownAsync()
        {
            try
            {
                await _orchestrator.StopServicesAsync();
            }
            catch (Exception ex)
            {
                // Log error but don't prevent shutdown
                System.Diagnostics.Debug.WriteLine($"Error during shutdown: {ex.Message}");
            }
        }

        private void SubscribeToEvents()
        {
            _orchestrator.VoiceInteractionStarted += OnVoiceInteractionStarted;
            _orchestrator.VoiceInteractionCompleted += OnVoiceInteractionCompleted;
            _orchestrator.SystemStatusChanged += OnSystemStatusChanged;
            
            _todoService.TodoListUpdated += OnTodoListUpdated;
            _prayerService.NextPrayerUpdated += OnNextPrayerUpdated;
            _prayerService.PrayerTimeNotification += OnPrayerTimeNotification;
        }

        private void OnVoiceInteractionStarted(object? sender, VoiceInteractionEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                VoiceStatusText = e.Type switch
                {
                    VoiceInteractionType.WakeWordDetected => "Wake word detected",
                    VoiceInteractionType.CommandTranscribed => "Listening...",
                    VoiceInteractionType.LLMProcessing => "Processing...",
                    VoiceInteractionType.SpeechSynthesis => "Speaking...",
                    _ => "Active"
                };
            });
        }

        private void OnVoiceInteractionCompleted(object? sender, VoiceInteractionEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (e.WasSuccessful)
                {
                    if (!string.IsNullOrEmpty(e.Command))
                        AddChatMessage("You (Voice)", e.Command, true);
                    
                    if (!string.IsNullOrEmpty(e.Response))
                        AddChatMessage("Assistant", e.Response, false);
                }
                else if (!string.IsNullOrEmpty(e.ErrorMessage))
                {
                    AddChatMessage("System", $"Voice error: {e.ErrorMessage}", false);
                }
                
                VoiceStatusText = "Ready";
            });
        }

        private void OnSystemStatusChanged(object? sender, SystemStatusChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusText = e.NewStatus switch
                {
                    SystemStatus.Ready => "Ready",
                    SystemStatus.Listening => "Listening...",
                    SystemStatus.Processing => "Processing...",
                    SystemStatus.Speaking => "Speaking...",
                    SystemStatus.Error => "Error",
                    SystemStatus.Offline => "Offline",
                    _ => "Unknown"
                };
                
                ConnectionStatus = e.NewStatus == SystemStatus.Offline ? "Offline" : "Online";
            });
        }

        private void OnTodoListUpdated(object? sender, TodoListUpdatedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                await RefreshTodoListAsync();
            });
        }

        private void OnNextPrayerUpdated(object? sender, NextPrayerUpdatedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                NextPrayerName = e.NextPrayer.Name;
                NextPrayerTime = e.NextPrayer.Time.ToString("HH:mm");
                
                var remaining = e.NextPrayer.TimeRemaining;
                if (remaining.TotalDays >= 1)
                    TimeRemaining = $"in {remaining.Days}d {remaining.Hours}h";
                else if (remaining.TotalHours >= 1)
                    TimeRemaining = $"in {remaining.Hours}h {remaining.Minutes}m";
                else
                    TimeRemaining = $"in {remaining.Minutes}m";
            });
        }

        private void OnPrayerTimeNotification(object? sender, PrayerTimeNotificationEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var message = e.NotificationType == "Reminder" 
                    ? $"Reminder: {e.PrayerName} prayer is in {e.MinutesBeforePrayer} minutes"
                    : $"It's time for {e.PrayerName} prayer";
                    
                AddChatMessage("Prayer Reminder", message, false);
            });
        }

        private void AddChatMessage(string sender, string message, bool isUser)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ChatMessages.Add(new ChatMessage
                {
                    Sender = sender,
                    Message = message,
                    IsUser = isUser,
                    Timestamp = DateTime.Now
                });
                
                // Keep only last 100 messages to prevent memory issues
                while (ChatMessages.Count > 100)
                {
                    ChatMessages.RemoveAt(0);
                }
            });
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

    public class ChatMessage
    {
        public string Sender { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsUser { get; set; }
        public DateTime Timestamp { get; set; }
    }
}