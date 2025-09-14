using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PersonalAiAssistant.Models;
using System.IO;

namespace PersonalAiAssistant.Services
{
    public class ConfigurationManager : IConfigurationManager
    {
        private readonly ILogger<ConfigurationManager> _logger;
        private readonly string _configFilePath;
        private AppSettings _settings;

        public ConfigurationManager(ILogger<ConfigurationManager> logger)
        {
            _logger = logger;
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            _settings = new AppSettings();
        }

        #region Voice Settings
        public string WakeWord
        {
            get => _settings.VoiceSettings.WakeWord;
            set => _settings.VoiceSettings.WakeWord = value;
        }

        public string VoskModelPath
        {
            get => _settings.VoiceSettings.VoskModelPath;
            set => _settings.VoiceSettings.VoskModelPath = value;
        }

        public string KokoroServerUrl
        {
            get => _settings.VoiceSettings.KokoroServerUrl;
            set => _settings.VoiceSettings.KokoroServerUrl = value;
        }

        public int AudioSampleRate
        {
            get => _settings.VoiceSettings.AudioSampleRate;
            set => _settings.VoiceSettings.AudioSampleRate = value;
        }

        public double VoiceActivationThreshold
        {
            get => _settings.VoiceSettings.VoiceActivationThreshold;
            set => _settings.VoiceSettings.VoiceActivationThreshold = value;
        }

        public int SilenceTimeoutMs
        {
            get => _settings.VoiceSettings.SilenceTimeoutMs;
            set => _settings.VoiceSettings.SilenceTimeoutMs = value;
        }
        #endregion

        #region LLM Settings
        public string LLMProvider
        {
            get => _settings.LLMSettings.Provider;
            set => _settings.LLMSettings.Provider = value;
        }

        public string GeminiApiKey
        {
            get => _settings.LLMSettings.GeminiApiKey;
            set => _settings.LLMSettings.GeminiApiKey = value;
        }

        public string GroqApiKey
        {
            get => _settings.LLMSettings.GroqApiKey;
            set => _settings.LLMSettings.GroqApiKey = value;
        }

        public int MaxTokens
        {
            get => _settings.LLMSettings.MaxTokens;
            set => _settings.LLMSettings.MaxTokens = value;
        }

        public double Temperature
        {
            get => _settings.LLMSettings.Temperature;
            set => _settings.LLMSettings.Temperature = value;
        }

        public string SystemPrompt
        {
            get => _settings.LLMSettings.SystemPrompt;
            set => _settings.LLMSettings.SystemPrompt = value;
        }
        #endregion

        #region Microsoft Settings
        public string ClientId
        {
            get => _settings.MicrosoftSettings.ClientId;
            set => _settings.MicrosoftSettings.ClientId = value;
        }

        public string TenantId
        {
            get => _settings.MicrosoftSettings.TenantId;
            set => _settings.MicrosoftSettings.TenantId = value;
        }

        public string RedirectUri
        {
            get => _settings.MicrosoftSettings.RedirectUri;
            set => _settings.MicrosoftSettings.RedirectUri = value;
        }
        #endregion

        #region Prayer Settings
        public string City
        {
            get => _settings.PrayerSettings.City;
            set => _settings.PrayerSettings.City = value;
        }

        public string Country
        {
            get => _settings.PrayerSettings.Country;
            set => _settings.PrayerSettings.Country = value;
        }

        public double Latitude
        {
            get => _settings.PrayerSettings.Latitude;
            set => _settings.PrayerSettings.Latitude = value;
        }

        public double Longitude
        {
            get => _settings.PrayerSettings.Longitude;
            set => _settings.PrayerSettings.Longitude = value;
        }

        public string CalculationMethod
        {
            get => _settings.PrayerSettings.CalculationMethod;
            set => _settings.PrayerSettings.CalculationMethod = value;
        }

        public bool EnablePrayerNotifications
        {
            get => _settings.PrayerSettings.EnableNotifications;
            set => _settings.PrayerSettings.EnableNotifications = value;
        }

        public int NotificationMinutesBefore
        {
            get => _settings.PrayerSettings.NotificationMinutesBefore;
            set => _settings.PrayerSettings.NotificationMinutesBefore = value;
        }
        #endregion

        #region ActivityWatch Settings
        public string ActivityWatchServerUrl
        {
            get => _settings.ActivityWatchSettings.ServerUrl;
            set => _settings.ActivityWatchSettings.ServerUrl = value;
        }

        public string BucketName
        {
            get => _settings.ActivityWatchSettings.BucketName;
            set => _settings.ActivityWatchSettings.BucketName = value;
        }

        public bool EnableActivityTracking
        {
            get => _settings.ActivityWatchSettings.EnableTracking;
            set => _settings.ActivityWatchSettings.EnableTracking = value;
        }
        #endregion

        #region UI Settings
        public string Theme
        {
            get => _settings.UISettings.Theme;
            set => _settings.UISettings.Theme = value;
        }

        public int WindowWidth
        {
            get => _settings.UISettings.WindowWidth;
            set => _settings.UISettings.WindowWidth = value;
        }

        public int WindowHeight
        {
            get => _settings.UISettings.WindowHeight;
            set => _settings.UISettings.WindowHeight = value;
        }

        public bool MinimizeToTray
        {
            get => _settings.UISettings.MinimizeToTray;
            set => _settings.UISettings.MinimizeToTray = value;
        }

        public bool StartMinimized
        {
            get => _settings.UISettings.StartMinimized;
            set => _settings.UISettings.StartMinimized = value;
        }
        #endregion

        public async Task<bool> LoadSettingsAsync()
        {
            try
            {
                _logger.LogInformation("Loading configuration from {ConfigPath}", _configFilePath);

                if (!File.Exists(_configFilePath))
                {
                    _logger.LogWarning("Configuration file not found at {ConfigPath}. Creating default settings.", _configFilePath);
                    _settings = CreateDefaultSettings();
                    await SaveSettingsAsync();
                    return true;
                }

                var json = await File.ReadAllTextAsync(_configFilePath);
                var loadedSettings = JsonConvert.DeserializeObject<AppSettings>(json);
                
                if (loadedSettings != null)
                {
                    _settings = loadedSettings;
                    _logger.LogInformation("Configuration loaded successfully");
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to deserialize configuration file");
                    _settings = CreateDefaultSettings();
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configuration from {ConfigPath}", _configFilePath);
                _settings = CreateDefaultSettings();
                return false;
            }
        }

        public async Task<bool> SaveSettingsAsync()
        {
            try
            {
                _logger.LogInformation("Saving configuration to {ConfigPath}", _configFilePath);

                var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                await File.WriteAllTextAsync(_configFilePath, json);
                
                _logger.LogInformation("Configuration saved successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving configuration to {ConfigPath}", _configFilePath);
                return false;
            }
        }

        public T GetSetting<T>(string key, T defaultValue = default)
        {
            try
            {
                // Use reflection to get nested properties
                var properties = key.Split(':');
                object current = _settings;

                foreach (var property in properties)
                {
                    var prop = current.GetType().GetProperty(property);
                    if (prop == null)
                    {
                        _logger.LogWarning("Property {Property} not found in settings", property);
                        return defaultValue;
                    }
                    current = prop.GetValue(current);
                    if (current == null)
                    {
                        return defaultValue;
                    }
                }

                if (current is T result)
                {
                    return result;
                }
                
                // Try to convert if types don't match
                return (T)Convert.ChangeType(current, typeof(T));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting setting {Key}", key);
                return defaultValue;
            }
        }

        public void SetSetting<T>(string key, T value)
        {
            try
            {
                var properties = key.Split(':');
                object current = _settings;

                // Navigate to the parent object
                for (int i = 0; i < properties.Length - 1; i++)
                {
                    var prop = current.GetType().GetProperty(properties[i]);
                    if (prop == null)
                    {
                        _logger.LogWarning("Property {Property} not found in settings", properties[i]);
                        return;
                    }
                    current = prop.GetValue(current);
                    if (current == null)
                    {
                        _logger.LogWarning("Property {Property} is null", properties[i]);
                        return;
                    }
                }

                // Set the final property
                var finalProp = current.GetType().GetProperty(properties.Last());
                if (finalProp != null && finalProp.CanWrite)
                {
                    finalProp.SetValue(current, value);
                    _logger.LogDebug("Setting {Key} updated to {Value}", key, value);
                }
                else
                {
                    _logger.LogWarning("Property {Property} not found or not writable", properties.Last());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting {Key} to {Value}", key, value);
            }
        }

        public async Task ResetToDefaultsAsync()
        {
            try
            {
                _logger.LogInformation("Resetting configuration to defaults");
                _settings = CreateDefaultSettings();
                await SaveSettingsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting configuration to defaults");
                throw;
            }
        }

        public bool ValidateSettings()
        {
            try
            {
                var isValid = true;
                var errors = new List<string>();

                // Validate Voice Settings
                if (string.IsNullOrWhiteSpace(_settings.VoiceSettings.WakeWord))
                {
                    errors.Add("Wake word cannot be empty");
                    isValid = false;
                }

                if (string.IsNullOrWhiteSpace(_settings.VoiceSettings.VoskModelPath))
                {
                    errors.Add("Vosk model path cannot be empty");
                    isValid = false;
                }

                if (string.IsNullOrWhiteSpace(_settings.VoiceSettings.KokoroServerUrl))
                {
                    errors.Add("Kokoro server URL cannot be empty");
                    isValid = false;
                }

                // Validate LLM Settings
                if (string.IsNullOrWhiteSpace(_settings.LLMSettings.Provider))
                {
                    errors.Add("LLM provider must be specified");
                    isValid = false;
                }

                if (_settings.LLMSettings.Provider == "Gemini" && string.IsNullOrWhiteSpace(_settings.LLMSettings.GeminiApiKey))
                {
                    errors.Add("Gemini API key is required when using Gemini provider");
                    isValid = false;
                }

                if (_settings.LLMSettings.Provider == "Groq" && string.IsNullOrWhiteSpace(_settings.LLMSettings.GroqApiKey))
                {
                    errors.Add("Groq API key is required when using Groq provider");
                    isValid = false;
                }

                // Validate Microsoft Settings
                if (string.IsNullOrWhiteSpace(_settings.MicrosoftSettings.ClientId))
                {
                    errors.Add("Microsoft Client ID cannot be empty");
                    isValid = false;
                }

                // Validate Prayer Settings (if notifications are enabled)
                if (_settings.PrayerSettings.EnableNotifications)
                {
                    if (_settings.PrayerSettings.Latitude == 0 && _settings.PrayerSettings.Longitude == 0)
                    {
                        errors.Add("Location coordinates are required for prayer notifications");
                        isValid = false;
                    }
                }

                // Validate ActivityWatch Settings (if tracking is enabled)
                if (_settings.ActivityWatchSettings.EnableTracking)
                {
                    if (string.IsNullOrWhiteSpace(_settings.ActivityWatchSettings.ServerUrl))
                    {
                        errors.Add("ActivityWatch server URL is required when tracking is enabled");
                        isValid = false;
                    }
                }

                if (!isValid)
                {
                    _logger.LogWarning("Configuration validation failed: {Errors}", string.Join(", ", errors));
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating settings");
                return false;
            }
        }

        private AppSettings CreateDefaultSettings()
        {
            return new AppSettings
            {
                VoiceSettings = new VoiceSettings
                {
                    WakeWord = "Assistant",
                    VoskModelPath = "Models/vosk-model-small-en-us-0.15",
                    KokoroServerUrl = "http://localhost:8020",
                    AudioSampleRate = 16000,
                    VoiceActivationThreshold = 0.5,
                    SilenceTimeoutMs = 2000
                },
                LLMSettings = new LLMSettings
                {
                    Provider = "Gemini",
                    GeminiApiKey = "",
                    GroqApiKey = "",
                    MaxTokens = 1000,
                    Temperature = 0.7,
                    SystemPrompt = "You are a helpful personal AI assistant. Be concise and friendly."
                },
                MicrosoftSettings = new MicrosoftSettings
                {
                    ClientId = "your-client-id",
                    TenantId = "common",
                    RedirectUri = "http://localhost:8080/auth/callback"
                },
                PrayerSettings = new PrayerSettings
                {
                    City = "",
                    Country = "",
                    Latitude = 0.0,
                    Longitude = 0.0,
                    CalculationMethod = "MuslimWorldLeague",
                    EnableNotifications = true,
                    NotificationMinutesBefore = 5
                },
                ActivityWatchSettings = new ActivityWatchSettings
                {
                    ServerUrl = "http://localhost:5600",
                    BucketName = "aw-watcher-window",
                    EnableTracking = true
                },
                UISettings = new UISettings
                {
                    Theme = "Light",
                    WindowWidth = 1000,
                    WindowHeight = 700,
                    MinimizeToTray = true,
                    StartMinimized = false
                }
            };
        }
    }
}