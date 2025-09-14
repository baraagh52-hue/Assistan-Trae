using Microsoft.Extensions.Logging;
using PersonalAiAssistant.Models;
using PersonalAiAssistant.Services;
using System.Globalization;
using System.Text.Json;

namespace PersonalAiAssistant.Services
{
    public class PrayerTimeService : IPrayerTimeService, IDisposable
    {
        private readonly ILogger<PrayerTimeService> _logger;
        private readonly IConfigurationManager _configManager;
        private readonly HttpClient _httpClient;
        
        private bool _isInitialized = false;
        private bool _isNotificationServiceRunning = false;
        private bool _disposed = false;
        
        private Timer? _notificationTimer;
        private Timer? _nextPrayerUpdateTimer;
        private readonly object _lockObject = new object();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        
        // Prayer time data
        private PrayerTimes? _todaysPrayerTimes;
        private PrayerTimes? _tomorrowsPrayerTimes;
        private DateTime _lastPrayerTimesUpdate = DateTime.MinValue;
        private PrayerInfo? _nextPrayer;
        
        // Location and calculation settings
        private double _latitude;
        private double _longitude;
        private string _city = string.Empty;
        private string _country = string.Empty;
        private int _calculationMethod = 2; // Islamic Society of North America (ISNA)
        private int _madhab = 0; // Shafi (for Asr calculation)
        
        // API settings
        private readonly string _prayerTimesApiUrl = "http://api.aladhan.com/v1/timings";
        
        // Notification settings
        private readonly TimeSpan _notificationCheckInterval = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _nextPrayerUpdateInterval = TimeSpan.FromMinutes(5);
        private readonly HashSet<string> _notifiedPrayers = new();

        public event EventHandler<PrayerTimeNotificationEventArgs>? PrayerTimeNotification;
        public event EventHandler<NextPrayerUpdatedEventArgs>? NextPrayerUpdated;

        public bool IsInitialized => _isInitialized;
        public bool IsNotificationServiceRunning => _isNotificationServiceRunning;

        public PrayerTimeService(ILogger<PrayerTimeService> logger, IConfigurationManager configManager)
        {
            _logger = logger;
            _configManager = configManager;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                if (_isInitialized)
                {
                    _logger.LogInformation("Prayer time service already initialized");
                    return true;
                }

                _logger.LogInformation("Initializing prayer time service");
                
                // Load configuration
                var prayerSettings = _configManager.PrayerSettings;
                _latitude = prayerSettings.Latitude;
                _longitude = prayerSettings.Longitude;
                _city = prayerSettings.City;
                _country = prayerSettings.Country;
                _calculationMethod = prayerSettings.CalculationMethod;
                _madhab = prayerSettings.Madhab;
                
                // Validate location
                if (_latitude == 0 && _longitude == 0)
                {
                    _logger.LogWarning("Location not configured. Prayer times will not be available.");
                    _isInitialized = true; // Still mark as initialized but without location
                    return true;
                }
                
                // Test API connection
                if (!await TestApiConnectionAsync())
                {
                    _logger.LogWarning("Cannot connect to prayer times API. Service will work offline with cached data.");
                }
                
                // Load today's prayer times
                await LoadPrayerTimesAsync(DateTime.Today);
                
                // Load tomorrow's prayer times for next prayer calculation
                await LoadPrayerTimesAsync(DateTime.Today.AddDays(1));
                
                // Calculate next prayer
                UpdateNextPrayer();
                
                _isInitialized = true;
                _logger.LogInformation("Prayer time service initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing prayer time service");
                return false;
            }
        }

        public async Task<PrayerTimes?> GetTodaysPrayerTimesAsync()
        {
            try
            {
                if (!_isInitialized)
                {
                    _logger.LogWarning("Service not initialized");
                    return null;
                }

                // Check if we need to refresh today's prayer times
                if (_todaysPrayerTimes == null || _todaysPrayerTimes.Date.Date != DateTime.Today)
                {
                    await LoadPrayerTimesAsync(DateTime.Today);
                }
                
                return _todaysPrayerTimes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting today's prayer times");
                return null;
            }
        }

        public async Task<PrayerTimes?> GetPrayerTimesAsync(DateTime date)
        {
            try
            {
                if (!_isInitialized)
                {
                    _logger.LogWarning("Service not initialized");
                    return null;
                }

                // Check if we already have this date cached
                if (_todaysPrayerTimes?.Date.Date == date.Date)
                {
                    return _todaysPrayerTimes;
                }
                
                if (_tomorrowsPrayerTimes?.Date.Date == date.Date)
                {
                    return _tomorrowsPrayerTimes;
                }
                
                // Load prayer times for the requested date
                return await LoadPrayerTimesAsync(date);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting prayer times for date {Date}", date);
                return null;
            }
        }

        public async Task<PrayerInfo?> GetNextPrayerAsync()
        {
            try
            {
                if (!_isInitialized)
                {
                    _logger.LogWarning("Service not initialized");
                    return null;
                }

                UpdateNextPrayer();
                return _nextPrayer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting next prayer");
                return null;
            }
        }

        public async Task<bool> StartNotificationServiceAsync()
        {
            try
            {
                if (!_isInitialized)
                {
                    _logger.LogWarning("Service not initialized");
                    return false;
                }

                if (_isNotificationServiceRunning)
                {
                    _logger.LogInformation("Notification service already running");
                    return true;
                }

                _logger.LogInformation("Starting prayer time notification service");
                
                // Start notification timer
                _notificationTimer = new Timer(CheckForPrayerNotifications, null, TimeSpan.Zero, _notificationCheckInterval);
                
                // Start next prayer update timer
                _nextPrayerUpdateTimer = new Timer(UpdateNextPrayerCallback, null, TimeSpan.Zero, _nextPrayerUpdateInterval);
                
                _isNotificationServiceRunning = true;
                _logger.LogInformation("Prayer time notification service started");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting notification service");
                return false;
            }
        }

        public async Task StopNotificationServiceAsync()
        {
            try
            {
                if (!_isNotificationServiceRunning)
                {
                    _logger.LogInformation("Notification service not running");
                    return;
                }

                _logger.LogInformation("Stopping prayer time notification service");
                
                lock (_lockObject)
                {
                    _notificationTimer?.Dispose();
                    _notificationTimer = null;
                    
                    _nextPrayerUpdateTimer?.Dispose();
                    _nextPrayerUpdateTimer = null;
                }
                
                _isNotificationServiceRunning = false;
                _logger.LogInformation("Prayer time notification service stopped");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping notification service");
            }
        }

        public async Task<bool> SetLocationAsync(double latitude, double longitude, string city = "", string country = "")
        {
            try
            {
                _logger.LogInformation("Setting location: {Latitude}, {Longitude}, {City}, {Country}", 
                    latitude, longitude, city, country);
                
                _latitude = latitude;
                _longitude = longitude;
                _city = city;
                _country = country;
                
                // Update configuration
                var prayerSettings = _configManager.PrayerSettings;
                prayerSettings.Latitude = latitude;
                prayerSettings.Longitude = longitude;
                prayerSettings.City = city;
                prayerSettings.Country = country;
                await _configManager.SaveSettingsAsync();
                
                // Reload prayer times with new location
                if (_isInitialized)
                {
                    await LoadPrayerTimesAsync(DateTime.Today);
                    await LoadPrayerTimesAsync(DateTime.Today.AddDays(1));
                    UpdateNextPrayer();
                }
                
                _logger.LogInformation("Location updated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting location");
                return false;
            }
        }

        public async Task<bool> SetCalculationMethodAsync(int method, int madhab = 0)
        {
            try
            {
                _logger.LogInformation("Setting calculation method: {Method}, madhab: {Madhab}", method, madhab);
                
                _calculationMethod = method;
                _madhab = madhab;
                
                // Update configuration
                var prayerSettings = _configManager.PrayerSettings;
                prayerSettings.CalculationMethod = method;
                prayerSettings.Madhab = madhab;
                await _configManager.SaveSettingsAsync();
                
                // Reload prayer times with new calculation method
                if (_isInitialized)
                {
                    await LoadPrayerTimesAsync(DateTime.Today);
                    await LoadPrayerTimesAsync(DateTime.Today.AddDays(1));
                    UpdateNextPrayer();
                }
                
                _logger.LogInformation("Calculation method updated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting calculation method");
                return false;
            }
        }

        public async Task<bool> SetNotificationSettingsAsync(bool enableNotifications, List<string>? enabledPrayers = null)
        {
            try
            {
                _logger.LogInformation("Setting notification settings: enabled={EnableNotifications}, prayers={Prayers}", 
                    enableNotifications, enabledPrayers != null ? string.Join(", ", enabledPrayers) : "all");
                
                // Update configuration
                var prayerSettings = _configManager.PrayerSettings;
                prayerSettings.EnableNotifications = enableNotifications;
                prayerSettings.EnabledPrayers = enabledPrayers ?? new List<string> { "Fajr", "Dhuhr", "Asr", "Maghrib", "Isha" };
                await _configManager.SaveSettingsAsync();
                
                _logger.LogInformation("Notification settings updated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting notification settings");
                return false;
            }
        }

        private async Task<PrayerTimes?> LoadPrayerTimesAsync(DateTime date)
        {
            try
            {
                _logger.LogDebug("Loading prayer times for {Date}", date.ToString("yyyy-MM-dd"));
                
                if (_latitude == 0 && _longitude == 0)
                {
                    _logger.LogWarning("Location not set, cannot load prayer times");
                    return null;
                }
                
                // Build API URL
                var dateString = date.ToString("dd-MM-yyyy");
                var url = $"{_prayerTimesApiUrl}/{dateString}?latitude={_latitude}&longitude={_longitude}&method={_calculationMethod}&school={_madhab}";
                
                var response = await _httpClient.GetAsync(url, _cancellationTokenSource.Token);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("API request failed with status {StatusCode}", response.StatusCode);
                    return null;
                }
                
                var jsonContent = await response.Content.ReadAsStringAsync(_cancellationTokenSource.Token);
                var apiResponse = JsonSerializer.Deserialize<PrayerTimesApiResponse>(jsonContent);
                
                if (apiResponse?.Data?.Timings == null)
                {
                    _logger.LogWarning("Invalid API response format");
                    return null;
                }
                
                var timings = apiResponse.Data.Timings;
                var prayerTimes = new PrayerTimes
                {
                    Date = date,
                    Fajr = ParsePrayerTime(date, timings.Fajr),
                    Sunrise = ParsePrayerTime(date, timings.Sunrise),
                    Dhuhr = ParsePrayerTime(date, timings.Dhuhr),
                    Asr = ParsePrayerTime(date, timings.Asr),
                    Sunset = ParsePrayerTime(date, timings.Sunset),
                    Maghrib = ParsePrayerTime(date, timings.Maghrib),
                    Isha = ParsePrayerTime(date, timings.Isha),
                    Midnight = ParsePrayerTime(date, timings.Midnight),
                    Location = $"{_city}, {_country}".Trim(' ', ','),
                    CalculationMethod = _calculationMethod,
                    Madhab = _madhab
                };
                
                // Cache the prayer times
                if (date.Date == DateTime.Today)
                {
                    _todaysPrayerTimes = prayerTimes;
                }
                else if (date.Date == DateTime.Today.AddDays(1))
                {
                    _tomorrowsPrayerTimes = prayerTimes;
                }
                
                _lastPrayerTimesUpdate = DateTime.UtcNow;
                _logger.LogDebug("Successfully loaded prayer times for {Date}", date.ToString("yyyy-MM-dd"));
                
                return prayerTimes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading prayer times for {Date}", date);
                return null;
            }
        }

        private DateTime ParsePrayerTime(DateTime date, string timeString)
        {
            try
            {
                // Remove timezone info if present (e.g., "05:30 (GMT+3)")
                var cleanTimeString = timeString.Split(' ')[0];
                
                if (DateTime.TryParseExact(cleanTimeString, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                {
                    return date.Date.Add(time.TimeOfDay);
                }
                
                _logger.LogWarning("Failed to parse prayer time: {TimeString}", timeString);
                return date.Date;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing prayer time: {TimeString}", timeString);
                return date.Date;
            }
        }

        private void UpdateNextPrayer()
        {
            try
            {
                var now = DateTime.Now;
                var today = _todaysPrayerTimes;
                var tomorrow = _tomorrowsPrayerTimes;
                
                if (today == null)
                {
                    _nextPrayer = null;
                    return;
                }
                
                var prayerTimes = new List<(string Name, DateTime Time)>
                {
                    ("Fajr", today.Fajr),
                    ("Dhuhr", today.Dhuhr),
                    ("Asr", today.Asr),
                    ("Maghrib", today.Maghrib),
                    ("Isha", today.Isha)
                };
                
                // Find next prayer today
                var nextPrayerToday = prayerTimes.FirstOrDefault(p => p.Time > now);
                
                PrayerInfo? nextPrayer = null;
                
                if (nextPrayerToday.Name != null)
                {
                    // Next prayer is today
                    nextPrayer = new PrayerInfo
                    {
                        Name = nextPrayerToday.Name,
                        Time = nextPrayerToday.Time,
                        TimeUntil = nextPrayerToday.Time - now
                    };
                }
                else if (tomorrow != null)
                {
                    // Next prayer is tomorrow's Fajr
                    nextPrayer = new PrayerInfo
                    {
                        Name = "Fajr",
                        Time = tomorrow.Fajr,
                        TimeUntil = tomorrow.Fajr - now
                    };
                }
                
                if (nextPrayer != null && (_nextPrayer == null || _nextPrayer.Name != nextPrayer.Name || _nextPrayer.Time != nextPrayer.Time))
                {
                    _nextPrayer = nextPrayer;
                    _logger.LogDebug("Next prayer updated: {PrayerName} at {Time}", nextPrayer.Name, nextPrayer.Time);
                    
                    NextPrayerUpdated?.Invoke(this, new NextPrayerUpdatedEventArgs
                    {
                        NextPrayer = nextPrayer,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating next prayer");
            }
        }

        private void UpdateNextPrayerCallback(object? state)
        {
            try
            {
                UpdateNextPrayer();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in next prayer update callback");
            }
        }

        private void CheckForPrayerNotifications(object? state)
        {
            try
            {
                var prayerSettings = _configManager.PrayerSettings;
                if (!prayerSettings.EnableNotifications)
                {
                    return;
                }
                
                var now = DateTime.Now;
                var today = _todaysPrayerTimes;
                
                if (today == null)
                {
                    return;
                }
                
                var prayerTimes = new List<(string Name, DateTime Time)>
                {
                    ("Fajr", today.Fajr),
                    ("Dhuhr", today.Dhuhr),
                    ("Asr", today.Asr),
                    ("Maghrib", today.Maghrib),
                    ("Isha", today.Isha)
                };
                
                foreach (var (name, time) in prayerTimes)
                {
                    if (!prayerSettings.EnabledPrayers.Contains(name))
                    {
                        continue;
                    }
                    
                    var timeDiff = Math.Abs((now - time).TotalMinutes);
                    var notificationKey = $"{today.Date:yyyy-MM-dd}_{name}";
                    
                    // Trigger notification if within 1 minute of prayer time and not already notified
                    if (timeDiff <= 1 && !_notifiedPrayers.Contains(notificationKey))
                    {
                        _notifiedPrayers.Add(notificationKey);
                        
                        var prayerInfo = new PrayerInfo
                        {
                            Name = name,
                            Time = time,
                            TimeUntil = time - now
                        };
                        
                        _logger.LogInformation("Triggering prayer notification: {PrayerName} at {Time}", name, time);
                        
                        PrayerTimeNotification?.Invoke(this, new PrayerTimeNotificationEventArgs
                        {
                            PrayerInfo = prayerInfo,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
                
                // Clean up old notifications (older than today)
                var keysToRemove = _notifiedPrayers.Where(k => !k.StartsWith(today.Date.ToString("yyyy-MM-dd"))).ToList();
                foreach (var key in keysToRemove)
                {
                    _notifiedPrayers.Remove(key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for prayer notifications");
            }
        }

        private async Task<bool> TestApiConnectionAsync()
        {
            try
            {
                var testUrl = $"{_prayerTimesApiUrl}/{DateTime.Today:dd-MM-yyyy}?latitude=21.3891&longitude=39.8579&method=2";
                var response = await _httpClient.GetAsync(testUrl, _cancellationTokenSource.Token);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                _logger.LogInformation("Disposing prayer time service");

                // Stop notification service
                if (_isNotificationServiceRunning)
                {
                    StopNotificationServiceAsync().Wait(1000);
                }

                // Cancel operations
                _cancellationTokenSource.Cancel();

                // Dispose timers
                lock (_lockObject)
                {
                    _notificationTimer?.Dispose();
                    _nextPrayerUpdateTimer?.Dispose();
                }

                // Dispose HTTP client
                _httpClient.Dispose();
                _cancellationTokenSource.Dispose();
                
                _disposed = true;
                _logger.LogInformation("Prayer time service disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing prayer time service");
            }
        }
    }

    // API response models
    internal class PrayerTimesApiResponse
    {
        public int Code { get; set; }
        public string Status { get; set; } = string.Empty;
        public PrayerTimesData? Data { get; set; }
    }

    internal class PrayerTimesData
    {
        public PrayerTimings? Timings { get; set; }
        public PrayerDate? Date { get; set; }
        public PrayerMeta? Meta { get; set; }
    }

    internal class PrayerTimings
    {
        public string Fajr { get; set; } = string.Empty;
        public string Sunrise { get; set; } = string.Empty;
        public string Dhuhr { get; set; } = string.Empty;
        public string Asr { get; set; } = string.Empty;
        public string Sunset { get; set; } = string.Empty;
        public string Maghrib { get; set; } = string.Empty;
        public string Isha { get; set; } = string.Empty;
        public string Imsak { get; set; } = string.Empty;
        public string Midnight { get; set; } = string.Empty;
    }

    internal class PrayerDate
    {
        public string Readable { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
    }

    internal class PrayerMeta
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Timezone { get; set; } = string.Empty;
        public PrayerMethod? Method { get; set; }
    }

    internal class PrayerMethod
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}