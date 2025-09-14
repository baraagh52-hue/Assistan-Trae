using Microsoft.Extensions.Logging;
using PersonalAiAssistant.Models;
using PersonalAiAssistant.Services;
using System.Text;

namespace PersonalAiAssistant.Services
{
    public class ApplicationOrchestrator : IApplicationOrchestrator, IDisposable
    {
        private readonly ILogger<ApplicationOrchestrator> _logger;
        private readonly IWakeWordService _wakeWordService;
        private readonly ISTTService _sttService;
        private readonly ITTSService _ttsService;
        private readonly ILLMService _llmService;
        private readonly IMicrosoftTodoService _todoService;
        private readonly IPrayerTimeService _prayerTimeService;
        private readonly IActivityWatchClient _activityWatchClient;
        private readonly IConfigurationManager _configManager;
        private readonly IErrorHandlingService _errorHandler;
        private readonly ILoggingService _loggingService;
        
        private bool _isInitialized = false;
        private bool _isRunning = false;
        private bool _disposed = false;
        private SystemStatus _systemStatus = SystemStatus.NotInitialized;
        
        private readonly object _lockObject = new object();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        
        // Voice interaction state
        private bool _isListeningForCommand = false;
        private DateTime _lastWakeWordDetected = DateTime.MinValue;
        private readonly TimeSpan _commandTimeoutDuration = TimeSpan.FromSeconds(10);
        
        // Context data
        private string _lastSystemContext = string.Empty;
        private DateTime _lastContextUpdate = DateTime.MinValue;
        private readonly TimeSpan _contextRefreshInterval = TimeSpan.FromMinutes(5);

        public event EventHandler<VoiceInteractionEventArgs>? VoiceInteractionOccurred;
        public event EventHandler<SystemStatusChangedEventArgs>? SystemStatusChanged;

        public bool IsInitialized => _isInitialized;
        public bool IsRunning => _isRunning;
        public SystemStatus Status => _systemStatus;

        public ApplicationOrchestrator(
            ILogger<ApplicationOrchestrator> logger,
            IWakeWordService wakeWordService,
            ISTTService sttService,
            ITTSService ttsService,
            ILLMService llmService,
            IMicrosoftTodoService todoService,
            IPrayerTimeService prayerTimeService,
            IActivityWatchClient activityWatchClient,
            IConfigurationManager configManager,
            IErrorHandlingService errorHandler,
            ILoggingService loggingService)
        {
            _logger = logger;
            _wakeWordService = wakeWordService;
            _sttService = sttService;
            _ttsService = ttsService;
            _llmService = llmService;
            _todoService = todoService;
            _prayerTimeService = prayerTimeService;
            _activityWatchClient = activityWatchClient;
            _configManager = configManager;
            _errorHandler = errorHandler;
            _loggingService = loggingService;
        }

        public async Task<bool> InitializeAsync()
        {
            return await _errorHandler.ExecuteWithErrorHandlingAsync(async () =>
            {
                if (_isInitialized)
                {
                    _loggingService.LogInfo("Application orchestrator already initialized", "ApplicationOrchestrator");
                    return true;
                }

                _loggingService.LogInfo("Initializing application orchestrator", "ApplicationOrchestrator");
                SetSystemStatus(SystemStatus.Initializing);

                // Initialize all services
                var initializationTasks = new List<Task<bool>>();
                
                // Core voice services (critical)
                initializationTasks.Add(InitializeServiceAsync("Wake Word Service", () => _wakeWordService.InitializeAsync()));
                initializationTasks.Add(InitializeServiceAsync("STT Service", () => _sttService.InitializeAsync()));
                initializationTasks.Add(InitializeServiceAsync("TTS Service", () => _ttsService.InitializeAsync()));
                
                // LLM service (critical for AI functionality)
                initializationTasks.Add(InitializeServiceAsync("LLM Service", () => _llmService.InitializeAsync()));
                
                // Context services (non-critical)
                initializationTasks.Add(InitializeServiceAsync("Microsoft Todo Service", () => _todoService.InitializeAsync()));
                initializationTasks.Add(InitializeServiceAsync("Prayer Time Service", () => _prayerTimeService.InitializeAsync()));
                initializationTasks.Add(InitializeServiceAsync("ActivityWatch Client", () => _activityWatchClient.ConnectAsync()));

                var results = await Task.WhenAll(initializationTasks);
                
                // Check critical services
                var criticalServicesInitialized = results.Take(4).All(r => r);
                if (!criticalServicesInitialized)
                {
                    _loggingService.LogError("Failed to initialize critical services", "ApplicationOrchestrator");
                    SetSystemStatus(SystemStatus.Error);
                    return false;
                }

                // Subscribe to service events
                SubscribeToServiceEvents();

                _isInitialized = true;
                SetSystemStatus(SystemStatus.Ready);
                _loggingService.LogInfo("Application orchestrator initialized successfully", "ApplicationOrchestrator");
                
                return true;
            }, "InitializeAsync", "ApplicationOrchestrator", false);
        }

        public async Task<bool> StartAsync()
        {
            try
            {
                if (!_isInitialized)
                {
                    _logger.LogWarning("Cannot start orchestrator - not initialized");
                    return false;
                }

                if (_isRunning)
                {
                    _logger.LogInformation("Application orchestrator already running");
                    return true;
                }

                _logger.LogInformation("Starting application orchestrator");
                SetSystemStatus(SystemStatus.Starting);

                // Start wake word detection
                if (!await _wakeWordService.StartListeningAsync())
                {
                    _logger.LogError("Failed to start wake word service");
                    SetSystemStatus(SystemStatus.Error);
                    return false;
                }

                // Start prayer time notifications if configured
                if (_prayerTimeService.IsInitialized)
                {
                    await _prayerTimeService.StartNotificationServiceAsync();
                }

                _isRunning = true;
                SetSystemStatus(SystemStatus.Running);
                _logger.LogInformation("Application orchestrator started successfully");
                
                // Announce readiness
                await _ttsService.SpeakAsync("Personal AI Assistant is ready. Say the wake word to begin.", _cancellationTokenSource.Token);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting application orchestrator");
                SetSystemStatus(SystemStatus.Error);
                return false;
            }
        }

        public async Task StopAsync()
        {
            try
            {
                if (!_isRunning)
                {
                    _logger.LogInformation("Application orchestrator not running");
                    return;
                }

                _logger.LogInformation("Stopping application orchestrator");
                SetSystemStatus(SystemStatus.Stopping);

                // Stop all services
                var stopTasks = new List<Task>
                {
                    _wakeWordService.StopListeningAsync(),
                    _sttService.StopListeningAsync(),
                    _ttsService.StopSpeakingAsync()
                };

                if (_prayerTimeService.IsNotificationServiceRunning)
                {
                    stopTasks.Add(_prayerTimeService.StopNotificationServiceAsync());
                }

                await Task.WhenAll(stopTasks);

                _isRunning = false;
                SetSystemStatus(SystemStatus.Stopped);
                _logger.LogInformation("Application orchestrator stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping application orchestrator");
                SetSystemStatus(SystemStatus.Error);
            }
        }

        public async Task<string> ProcessVoiceCommandAsync(string command)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(command))
                {
                    return "I didn't hear anything. Please try again.";
                }

                _logger.LogInformation("Processing voice command: {Command}", command);
                
                // Notify about voice interaction
                VoiceInteractionOccurred?.Invoke(this, new VoiceInteractionEventArgs
                {
                    InteractionType = VoiceInteractionType.CommandReceived,
                    Command = command,
                    Timestamp = DateTime.UtcNow
                });

                // Get current system context
                var context = await GetSystemContextAsync();
                
                // Process command with LLM
                var response = await _llmService.ProcessRequestAsync(command, context, _cancellationTokenSource.Token);
                
                if (string.IsNullOrWhiteSpace(response))
                {
                    response = "I'm sorry, I couldn't process your request right now. Please try again.";
                }

                _logger.LogInformation("Generated response: {Response}", response);
                
                // Speak the response
                await _ttsService.SpeakAsync(response, _cancellationTokenSource.Token);
                
                // Notify about response
                VoiceInteractionOccurred?.Invoke(this, new VoiceInteractionEventArgs
                {
                    InteractionType = VoiceInteractionType.ResponseGenerated,
                    Command = command,
                    Response = response,
                    Timestamp = DateTime.UtcNow
                });
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing voice command: {Command}", command);
                var errorResponse = "I encountered an error processing your request. Please try again.";
                await _ttsService.SpeakAsync(errorResponse, _cancellationTokenSource.Token);
                return errorResponse;
            }
        }

        public async Task<string> ProcessTextCommandAsync(string command)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(command))
                {
                    return "Please provide a command.";
                }

                _logger.LogInformation("Processing text command: {Command}", command);
                
                // Get current system context
                var context = await GetSystemContextAsync();
                
                // Process command with LLM
                var response = await _llmService.ProcessRequestAsync(command, context, _cancellationTokenSource.Token);
                
                if (string.IsNullOrWhiteSpace(response))
                {
                    response = "I'm sorry, I couldn't process your request right now. Please try again.";
                }

                _logger.LogInformation("Generated response: {Response}", response);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing text command: {Command}", command);
                return "I encountered an error processing your request. Please try again.";
            }
        }

        public async Task EmergencyShutdownAsync()
        {
            try
            {
                _logger.LogWarning("Performing emergency shutdown");
                SetSystemStatus(SystemStatus.EmergencyShutdown);

                // Cancel all operations
                _cancellationTokenSource.Cancel();

                // Stop all services immediately
                var emergencyTasks = new List<Task>();
                
                try { emergencyTasks.Add(_wakeWordService.StopListeningAsync()); } catch { }
                try { emergencyTasks.Add(_sttService.StopListeningAsync()); } catch { }
                try { emergencyTasks.Add(_ttsService.StopSpeakingAsync()); } catch { }
                
                // Wait for shutdown with timeout
                await Task.WhenAny(Task.WhenAll(emergencyTasks), Task.Delay(5000));
                
                _isRunning = false;
                SetSystemStatus(SystemStatus.Stopped);
                _logger.LogWarning("Emergency shutdown completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during emergency shutdown");
            }
        }

        public async Task<string> GetSystemContextAsync()
        {
            try
            {
                // Check if context needs refresh
                if (DateTime.UtcNow - _lastContextUpdate < _contextRefreshInterval && !string.IsNullOrEmpty(_lastSystemContext))
                {
                    return _lastSystemContext;
                }

                _logger.LogDebug("Refreshing system context");
                var contextBuilder = new StringBuilder();
                
                // Current time and date
                contextBuilder.AppendLine($"Current time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                
                // Prayer times context
                if (_prayerTimeService.IsInitialized)
                {
                    try
                    {
                        var prayerTimes = await _prayerTimeService.GetTodaysPrayerTimesAsync();
                        if (prayerTimes != null)
                        {
                            contextBuilder.AppendLine("Today's Prayer Times:");
                            contextBuilder.AppendLine($"- Fajr: {prayerTimes.Fajr:HH:mm}");
                            contextBuilder.AppendLine($"- Dhuhr: {prayerTimes.Dhuhr:HH:mm}");
                            contextBuilder.AppendLine($"- Asr: {prayerTimes.Asr:HH:mm}");
                            contextBuilder.AppendLine($"- Maghrib: {prayerTimes.Maghrib:HH:mm}");
                            contextBuilder.AppendLine($"- Isha: {prayerTimes.Isha:HH:mm}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get prayer times for context");
                    }
                }
                
                // Microsoft To-Do context
                if (_todoService.IsAuthenticated)
                {
                    try
                    {
                        var todoLists = await _todoService.GetTodoListsAsync();
                        if (todoLists?.Any() == true)
                        {
                            contextBuilder.AppendLine("\nMicrosoft To-Do Lists:");
                            foreach (var list in todoLists.Take(3)) // Limit to avoid too much context
                            {
                                contextBuilder.AppendLine($"- {list.DisplayName} ({list.Tasks?.Count ?? 0} tasks)");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get todo lists for context");
                    }
                }
                
                // ActivityWatch context
                if (_activityWatchClient.IsConnected)
                {
                    try
                    {
                        var recentActivity = await _activityWatchClient.GetRecentActivityAsync(TimeSpan.FromHours(1));
                        if (recentActivity?.Any() == true)
                        {
                            contextBuilder.AppendLine("\nRecent Activity (last hour):");
                            foreach (var activity in recentActivity.Take(3))
                            {
                                contextBuilder.AppendLine($"- {activity.App}: {activity.Title}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get recent activity for context");
                    }
                }
                
                _lastSystemContext = contextBuilder.ToString();
                _lastContextUpdate = DateTime.UtcNow;
                
                _logger.LogDebug("System context refreshed: {ContextLength} characters", _lastSystemContext.Length);
                return _lastSystemContext;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system context");
                return $"Current time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            }
        }

        private async Task<bool> InitializeServiceAsync(string serviceName, Func<Task<bool>> initializeFunc)
        {
            try
            {
                _logger.LogInformation("Initializing {ServiceName}", serviceName);
                var result = await initializeFunc();
                
                if (result)
                {
                    _logger.LogInformation("{ServiceName} initialized successfully", serviceName);
                }
                else
                {
                    _logger.LogWarning("{ServiceName} failed to initialize", serviceName);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing {ServiceName}", serviceName);
                return false;
            }
        }

        private void SubscribeToServiceEvents()
        {
            try
            {
                _logger.LogDebug("Subscribing to service events");
                
                // Wake word service events
                _wakeWordService.WakeWordDetected += OnWakeWordDetected;
                
                // STT service events
                _sttService.CommandTranscribed += OnCommandTranscribed;
                _sttService.StatusChanged += OnSTTStatusChanged;
                
                // TTS service events
                _ttsService.SpeechStarted += OnSpeechStarted;
                _ttsService.SpeechCompleted += OnSpeechCompleted;
                
                // LLM service events
                _llmService.ResponseReceived += OnLLMResponseReceived;
                _llmService.ErrorOccurred += OnLLMErrorOccurred;
                
                // Prayer time service events
                _prayerTimeService.PrayerTimeNotification += OnPrayerTimeNotification;
                
                _logger.LogDebug("Successfully subscribed to service events");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to service events");
            }
        }

        private async void OnWakeWordDetected(object? sender, WakeWordDetectedEventArgs e)
        {
            try
            {
                _logger.LogInformation("Wake word detected: {WakeWord}", e.WakeWord);
                _lastWakeWordDetected = DateTime.UtcNow;
                _isListeningForCommand = true;
                
                VoiceInteractionOccurred?.Invoke(this, new VoiceInteractionEventArgs
                {
                    InteractionType = VoiceInteractionType.WakeWordDetected,
                    Timestamp = DateTime.UtcNow
                });
                
                // Start STT service to listen for command
                if (!await _sttService.StartListeningAsync())
                {
                    _logger.LogError("Failed to start STT service after wake word detection");
                    _isListeningForCommand = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling wake word detection");
                _isListeningForCommand = false;
            }
        }

        private async void OnCommandTranscribed(object? sender, CommandTranscribedEventArgs e)
        {
            try
            {
                if (!_isListeningForCommand)
                {
                    _logger.LogWarning("Received transcribed command but not listening for commands");
                    return;
                }
                
                _logger.LogInformation("Command transcribed: {Command}", e.Command);
                _isListeningForCommand = false;
                
                // Stop STT service
                await _sttService.StopListeningAsync();
                
                // Process the command
                await ProcessVoiceCommandAsync(e.Command);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling transcribed command");
                _isListeningForCommand = false;
            }
        }

        private void OnSTTStatusChanged(object? sender, STTStatusChangedEventArgs e)
        {
            _logger.LogDebug("STT status changed from {OldStatus} to {NewStatus}", e.OldStatus, e.NewStatus);
            
            // Handle timeout for command listening
            if (_isListeningForCommand && e.NewStatus == STTStatus.Listening)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(_commandTimeoutDuration);
                    if (_isListeningForCommand && DateTime.UtcNow - _lastWakeWordDetected > _commandTimeoutDuration)
                    {
                        _logger.LogInformation("Command listening timeout reached");
                        _isListeningForCommand = false;
                        await _sttService.StopListeningAsync();
                    }
                });
            }
        }

        private void OnSpeechStarted(object? sender, SpeechStartedEventArgs e)
        {
            _logger.LogDebug("TTS speech started: {Voice}", e.Voice);
        }

        private void OnSpeechCompleted(object? sender, SpeechCompletedEventArgs e)
        {
            _logger.LogDebug("TTS speech completed. Cancelled: {WasCancelled}", e.WasCancelled);
        }

        private void OnLLMResponseReceived(object? sender, LLMResponseEventArgs e)
        {
            _logger.LogDebug("LLM response received: {ResponseLength} characters", e.Response?.Length ?? 0);
        }

        private void OnLLMErrorOccurred(object? sender, LLMErrorEventArgs e)
        {
            _logger.LogWarning("LLM error occurred: {ErrorMessage}", e.ErrorMessage);
        }

        private async void OnPrayerTimeNotification(object? sender, PrayerTimeNotificationEventArgs e)
        {
            try
            {
                _logger.LogInformation("Prayer time notification: {PrayerName} at {Time}", e.PrayerInfo.Name, e.PrayerInfo.Time);
                
                var message = $"It's time for {e.PrayerInfo.Name} prayer.";
                await _ttsService.SpeakAsync(message, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling prayer time notification");
            }
        }

        private void SetSystemStatus(SystemStatus newStatus)
        {
            if (_systemStatus != newStatus)
            {
                var oldStatus = _systemStatus;
                _systemStatus = newStatus;
                
                _logger.LogInformation("System status changed from {OldStatus} to {NewStatus}", oldStatus, newStatus);
                
                SystemStatusChanged?.Invoke(this, new SystemStatusChangedEventArgs
                {
                    OldStatus = oldStatus,
                    NewStatus = newStatus,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                _logger.LogInformation("Disposing application orchestrator");

                // Stop if running
                if (_isRunning)
                {
                    StopAsync().Wait(5000);
                }

                // Cancel operations
                _cancellationTokenSource.Cancel();

                // Unsubscribe from events
                try
                {
                    _wakeWordService.WakeWordDetected -= OnWakeWordDetected;
                    _sttService.CommandTranscribed -= OnCommandTranscribed;
                    _sttService.StatusChanged -= OnSTTStatusChanged;
                    _ttsService.SpeechStarted -= OnSpeechStarted;
                    _ttsService.SpeechCompleted -= OnSpeechCompleted;
                    _llmService.ResponseReceived -= OnLLMResponseReceived;
                    _llmService.ErrorOccurred -= OnLLMErrorOccurred;
                    _prayerTimeService.PrayerTimeNotification -= OnPrayerTimeNotification;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error unsubscribing from service events");
                }

                _cancellationTokenSource.Dispose();
                _disposed = true;
                
                _logger.LogInformation("Application orchestrator disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing application orchestrator");
            }
        }
    }
}