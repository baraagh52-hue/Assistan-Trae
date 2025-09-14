using Microsoft.Extensions.Logging;
using PersonalAiAssistant.Models;
using PersonalAiAssistant.Services;
using System.Text.Json;
using System.Text;

namespace PersonalAiAssistant.Services
{
    public class ActivityWatchClient : IActivityWatchClient, IDisposable
    {
        private readonly ILogger<ActivityWatchClient> _logger;
        private readonly IConfigurationManager _configManager;
        private readonly HttpClient _httpClient;
        
        private bool _isInitialized = false;
        private bool _isConnected = false;
        private bool _disposed = false;
        
        private string _serverUrl = "http://localhost:5600";
        private string _bucketId = "aw-watcher-window_personal-ai-assistant";
        private readonly string _clientName = "personal-ai-assistant";
        private readonly string _hostname;
        
        private Timer? _activityTimer;
        private readonly object _lockObject = new object();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        
        // Activity tracking
        private ActivityEvent? _lastActivity;
        private readonly TimeSpan _activityCheckInterval = TimeSpan.FromSeconds(10);
        private readonly List<ActivityEvent> _recentActivities = new();
        private readonly int _maxRecentActivities = 100;
        
        // Connection settings
        private readonly TimeSpan _connectionTimeout = TimeSpan.FromSeconds(10);
        private readonly int _maxRetries = 3;
        private DateTime _lastConnectionAttempt = DateTime.MinValue;
        private readonly TimeSpan _reconnectCooldown = TimeSpan.FromMinutes(1);

        public event EventHandler<ActivityEventArgs>? ActivityChanged;
        public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

        public bool IsInitialized => _isInitialized;
        public bool IsConnected => _isConnected;
        public string ServerUrl => _serverUrl;
        public string BucketId => _bucketId;

        public ActivityWatchClient(ILogger<ActivityWatchClient> logger, IConfigurationManager configManager)
        {
            _logger = logger;
            _configManager = configManager;
            _hostname = Environment.MachineName.ToLowerInvariant();
            
            _httpClient = new HttpClient
            {
                Timeout = _connectionTimeout
            };
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                if (_isInitialized)
                {
                    _logger.LogInformation("ActivityWatch client already initialized");
                    return true;
                }

                _logger.LogInformation("Initializing ActivityWatch client");
                
                // Load configuration
                var awSettings = _configManager.ActivityWatchSettings;
                _serverUrl = awSettings.ServerUrl;
                
                if (string.IsNullOrEmpty(_serverUrl))
                {
                    _serverUrl = "http://localhost:5600";
                    _logger.LogInformation("Using default ActivityWatch server URL: {ServerUrl}", _serverUrl);
                }
                
                // Set bucket ID with hostname
                _bucketId = $"aw-watcher-window_{_hostname}";
                
                // Test connection
                await TestConnectionAsync();
                
                // Create or ensure bucket exists
                if (_isConnected)
                {
                    await EnsureBucketExistsAsync();
                }
                
                _isInitialized = true;
                _logger.LogInformation("ActivityWatch client initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing ActivityWatch client");
                return false;
            }
        }

        public async Task<bool> StartTrackingAsync()
        {
            try
            {
                if (!_isInitialized)
                {
                    _logger.LogWarning("Client not initialized");
                    return false;
                }

                if (_activityTimer != null)
                {
                    _logger.LogInformation("Activity tracking already started");
                    return true;
                }

                _logger.LogInformation("Starting activity tracking");
                
                // Start activity monitoring timer
                _activityTimer = new Timer(CheckActivityCallback, null, TimeSpan.Zero, _activityCheckInterval);
                
                _logger.LogInformation("Activity tracking started");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting activity tracking");
                return false;
            }
        }

        public async Task StopTrackingAsync()
        {
            try
            {
                if (_activityTimer == null)
                {
                    _logger.LogInformation("Activity tracking not running");
                    return;
                }

                _logger.LogInformation("Stopping activity tracking");
                
                lock (_lockObject)
                {
                    _activityTimer?.Dispose();
                    _activityTimer = null;
                }
                
                _logger.LogInformation("Activity tracking stopped");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping activity tracking");
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                _logger.LogDebug("Testing connection to ActivityWatch server: {ServerUrl}", _serverUrl);
                
                var infoUrl = $"{_serverUrl}/api/0/info";
                var response = await _httpClient.GetAsync(infoUrl, _cancellationTokenSource.Token);
                
                var wasConnected = _isConnected;
                _isConnected = response.IsSuccessStatusCode;
                _lastConnectionAttempt = DateTime.UtcNow;
                
                if (_isConnected)
                {
                    var content = await response.Content.ReadAsStringAsync(_cancellationTokenSource.Token);
                    _logger.LogDebug("Successfully connected to ActivityWatch server. Info: {Info}", content);
                }
                else
                {
                    _logger.LogWarning("Failed to connect to ActivityWatch server. Status: {StatusCode}", response.StatusCode);
                }
                
                // Notify if connection status changed
                if (wasConnected != _isConnected)
                {
                    ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs
                    {
                        IsConnected = _isConnected,
                        ServerUrl = _serverUrl,
                        Timestamp = DateTime.UtcNow
                    });
                }
                
                return _isConnected;
            }
            catch (Exception ex)
            {
                var wasConnected = _isConnected;
                _isConnected = false;
                _lastConnectionAttempt = DateTime.UtcNow;
                
                _logger.LogError(ex, "Error testing connection to ActivityWatch server");
                
                // Notify if connection status changed
                if (wasConnected != _isConnected)
                {
                    ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs
                    {
                        IsConnected = _isConnected,
                        ServerUrl = _serverUrl,
                        Timestamp = DateTime.UtcNow
                    });
                }
                
                return false;
            }
        }

        public async Task<List<ActivityEvent>> GetRecentActivityAsync(TimeSpan timespan)
        {
            try
            {
                if (!_isConnected)
                {
                    _logger.LogWarning("Not connected to ActivityWatch server");
                    return new List<ActivityEvent>();
                }

                var endTime = DateTime.UtcNow;
                var startTime = endTime.Subtract(timespan);
                
                var eventsUrl = $"{_serverUrl}/api/0/buckets/{_bucketId}/events" +
                               $"?start={startTime:yyyy-MM-ddTHH:mm:ss.fffZ}" +
                               $"&end={endTime:yyyy-MM-ddTHH:mm:ss.fffZ}" +
                               $"&limit=100";
                
                var response = await _httpClient.GetAsync(eventsUrl, _cancellationTokenSource.Token);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get recent activity. Status: {StatusCode}", response.StatusCode);
                    return new List<ActivityEvent>();
                }
                
                var jsonContent = await response.Content.ReadAsStringAsync(_cancellationTokenSource.Token);
                var events = JsonSerializer.Deserialize<List<ActivityWatchEvent>>(jsonContent) ?? new List<ActivityWatchEvent>();
                
                var activityEvents = events.Select(ConvertToActivityEvent).Where(e => e != null).Cast<ActivityEvent>().ToList();
                
                _logger.LogDebug("Retrieved {Count} recent activity events", activityEvents.Count);
                return activityEvents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent activity");
                return new List<ActivityEvent>();
            }
        }

        public async Task<ActivitySummary?> GetActivitySummaryAsync(DateTime date)
        {
            try
            {
                if (!_isConnected)
                {
                    _logger.LogWarning("Not connected to ActivityWatch server");
                    return null;
                }

                var startTime = date.Date;
                var endTime = date.Date.AddDays(1);
                
                var events = await GetActivityEventsAsync(startTime, endTime);
                
                if (!events.Any())
                {
                    return new ActivitySummary
                    {
                        Date = date,
                        TotalActiveTime = TimeSpan.Zero,
                        ApplicationUsage = new Dictionary<string, TimeSpan>(),
                        WindowTitleUsage = new Dictionary<string, TimeSpan>(),
                        MostUsedApplication = string.Empty,
                        MostUsedWindowTitle = string.Empty
                    };
                }
                
                var appUsage = new Dictionary<string, TimeSpan>();
                var windowUsage = new Dictionary<string, TimeSpan>();
                var totalActiveTime = TimeSpan.Zero;
                
                foreach (var evt in events)
                {
                    var duration = evt.Duration;
                    totalActiveTime = totalActiveTime.Add(duration);
                    
                    // Application usage
                    if (!string.IsNullOrEmpty(evt.ApplicationName))
                    {
                        if (appUsage.ContainsKey(evt.ApplicationName))
                        {
                            appUsage[evt.ApplicationName] = appUsage[evt.ApplicationName].Add(duration);
                        }
                        else
                        {
                            appUsage[evt.ApplicationName] = duration;
                        }
                    }
                    
                    // Window title usage
                    if (!string.IsNullOrEmpty(evt.WindowTitle))
                    {
                        if (windowUsage.ContainsKey(evt.WindowTitle))
                        {
                            windowUsage[evt.WindowTitle] = windowUsage[evt.WindowTitle].Add(duration);
                        }
                        else
                        {
                            windowUsage[evt.WindowTitle] = duration;
                        }
                    }
                }
                
                var mostUsedApp = appUsage.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key ?? string.Empty;
                var mostUsedWindow = windowUsage.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key ?? string.Empty;
                
                var summary = new ActivitySummary
                {
                    Date = date,
                    TotalActiveTime = totalActiveTime,
                    ApplicationUsage = appUsage,
                    WindowTitleUsage = windowUsage,
                    MostUsedApplication = mostUsedApp,
                    MostUsedWindowTitle = mostUsedWindow
                };
                
                _logger.LogDebug("Generated activity summary for {Date}: {TotalTime} total active time", 
                    date.ToString("yyyy-MM-dd"), totalActiveTime);
                
                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting activity summary for {Date}", date);
                return null;
            }
        }

        public async Task<ActivityEvent?> GetCurrentActivityAsync()
        {
            try
            {
                // Return the last tracked activity
                return _lastActivity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current activity");
                return null;
            }
        }

        public async Task<bool> SetServerUrlAsync(string serverUrl)
        {
            try
            {
                _logger.LogInformation("Setting ActivityWatch server URL: {ServerUrl}", serverUrl);
                
                _serverUrl = serverUrl;
                
                // Update configuration
                var awSettings = _configManager.ActivityWatchSettings;
                awSettings.ServerUrl = serverUrl;
                await _configManager.SaveSettingsAsync();
                
                // Test new connection
                await TestConnectionAsync();
                
                // Recreate bucket if connected
                if (_isConnected)
                {
                    await EnsureBucketExistsAsync();
                }
                
                _logger.LogInformation("ActivityWatch server URL updated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting server URL");
                return false;
            }
        }

        private async Task<List<ActivityEvent>> GetActivityEventsAsync(DateTime startTime, DateTime endTime)
        {
            try
            {
                var eventsUrl = $"{_serverUrl}/api/0/buckets/{_bucketId}/events" +
                               $"?start={startTime:yyyy-MM-ddTHH:mm:ss.fffZ}" +
                               $"&end={endTime:yyyy-MM-ddTHH:mm:ss.fffZ}" +
                               $"&limit=1000";
                
                var response = await _httpClient.GetAsync(eventsUrl, _cancellationTokenSource.Token);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get activity events. Status: {StatusCode}", response.StatusCode);
                    return new List<ActivityEvent>();
                }
                
                var jsonContent = await response.Content.ReadAsStringAsync(_cancellationTokenSource.Token);
                var events = JsonSerializer.Deserialize<List<ActivityWatchEvent>>(jsonContent) ?? new List<ActivityWatchEvent>();
                
                return events.Select(ConvertToActivityEvent).Where(e => e != null).Cast<ActivityEvent>().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting activity events from {StartTime} to {EndTime}", startTime, endTime);
                return new List<ActivityEvent>();
            }
        }

        private async Task<bool> EnsureBucketExistsAsync()
        {
            try
            {
                _logger.LogDebug("Ensuring bucket exists: {BucketId}", _bucketId);
                
                // Check if bucket exists
                var bucketsUrl = $"{_serverUrl}/api/0/buckets";
                var response = await _httpClient.GetAsync(bucketsUrl, _cancellationTokenSource.Token);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get buckets list. Status: {StatusCode}", response.StatusCode);
                    return false;
                }
                
                var jsonContent = await response.Content.ReadAsStringAsync(_cancellationTokenSource.Token);
                var buckets = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent) ?? new Dictionary<string, object>();
                
                if (buckets.ContainsKey(_bucketId))
                {
                    _logger.LogDebug("Bucket already exists: {BucketId}", _bucketId);
                    return true;
                }
                
                // Create bucket
                var createBucketUrl = $"{_serverUrl}/api/0/buckets/{_bucketId}";
                var bucketData = new
                {
                    client = _clientName,
                    type = "currentwindow",
                    hostname = _hostname
                };
                
                var jsonData = JsonSerializer.Serialize(bucketData);
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                
                var createResponse = await _httpClient.PostAsync(createBucketUrl, content, _cancellationTokenSource.Token);
                
                if (createResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully created bucket: {BucketId}", _bucketId);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Failed to create bucket. Status: {StatusCode}", createResponse.StatusCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring bucket exists");
                return false;
            }
        }

        private void CheckActivityCallback(object? state)
        {
            try
            {
                // For now, we'll simulate activity detection
                // In a real implementation, this would use Windows API to get current window info
                var currentActivity = GetCurrentWindowActivity();
                
                if (currentActivity != null && !IsSameActivity(currentActivity, _lastActivity))
                {
                    _lastActivity = currentActivity;
                    
                    // Add to recent activities
                    lock (_lockObject)
                    {
                        _recentActivities.Add(currentActivity);
                        if (_recentActivities.Count > _maxRecentActivities)
                        {
                            _recentActivities.RemoveAt(0);
                        }
                    }
                    
                    // Send to ActivityWatch if connected
                    if (_isConnected)
                    {
                        _ = Task.Run(async () => await SendActivityEventAsync(currentActivity));
                    }
                    
                    // Notify listeners
                    ActivityChanged?.Invoke(this, new ActivityEventArgs
                    {
                        Activity = currentActivity,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in activity check callback");
            }
        }

        private ActivityEvent? GetCurrentWindowActivity()
        {
            try
            {
                // This is a simplified implementation
                // In a real implementation, you would use Windows API calls like:
                // - GetForegroundWindow() to get the active window handle
                // - GetWindowText() to get the window title
                // - GetWindowThreadProcessId() and OpenProcess() to get process info
                
                // For now, return a placeholder activity
                return new ActivityEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Duration = _activityCheckInterval,
                    ApplicationName = "Unknown Application",
                    WindowTitle = "Unknown Window",
                    Url = string.Empty,
                    Category = "unknown"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current window activity");
                return null;
            }
        }

        private bool IsSameActivity(ActivityEvent current, ActivityEvent? previous)
        {
            if (previous == null)
                return false;
                
            return current.ApplicationName == previous.ApplicationName &&
                   current.WindowTitle == previous.WindowTitle &&
                   current.Url == previous.Url;
        }

        private async Task<bool> SendActivityEventAsync(ActivityEvent activity)
        {
            try
            {
                if (!_isConnected)
                    return false;
                
                var awEvent = new ActivityWatchEvent
                {
                    Timestamp = activity.Timestamp,
                    Duration = activity.Duration.TotalSeconds,
                    Data = new Dictionary<string, object>
                    {
                        ["app"] = activity.ApplicationName,
                        ["title"] = activity.WindowTitle
                    }
                };
                
                if (!string.IsNullOrEmpty(activity.Url))
                {
                    awEvent.Data["url"] = activity.Url;
                }
                
                var eventsUrl = $"{_serverUrl}/api/0/buckets/{_bucketId}/events";
                var jsonData = JsonSerializer.Serialize(new[] { awEvent });
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(eventsUrl, content, _cancellationTokenSource.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Successfully sent activity event to ActivityWatch");
                    return true;
                }
                else
                {
                    _logger.LogWarning("Failed to send activity event. Status: {StatusCode}", response.StatusCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending activity event");
                return false;
            }
        }

        private ActivityEvent? ConvertToActivityEvent(ActivityWatchEvent awEvent)
        {
            try
            {
                var activity = new ActivityEvent
                {
                    Timestamp = awEvent.Timestamp,
                    Duration = TimeSpan.FromSeconds(awEvent.Duration),
                    ApplicationName = awEvent.Data.ContainsKey("app") ? awEvent.Data["app"].ToString() ?? string.Empty : string.Empty,
                    WindowTitle = awEvent.Data.ContainsKey("title") ? awEvent.Data["title"].ToString() ?? string.Empty : string.Empty,
                    Url = awEvent.Data.ContainsKey("url") ? awEvent.Data["url"].ToString() ?? string.Empty : string.Empty,
                    Category = "window"
                };
                
                return activity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting ActivityWatch event");
                return null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                _logger.LogInformation("Disposing ActivityWatch client");

                // Stop tracking
                if (_activityTimer != null)
                {
                    StopTrackingAsync().Wait(1000);
                }

                // Cancel operations
                _cancellationTokenSource.Cancel();

                // Dispose timer
                lock (_lockObject)
                {
                    _activityTimer?.Dispose();
                }

                // Dispose HTTP client
                _httpClient.Dispose();
                _cancellationTokenSource.Dispose();
                
                _disposed = true;
                _logger.LogInformation("ActivityWatch client disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing ActivityWatch client");
            }
        }
    }

    // ActivityWatch API models
    internal class ActivityWatchEvent
    {
        public DateTime Timestamp { get; set; }
        public double Duration { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
    }
}