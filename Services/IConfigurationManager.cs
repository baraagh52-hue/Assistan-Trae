using System.Threading.Tasks;

namespace PersonalAiAssistant.Services
{
    public interface IConfigurationManager
    {
        Task<T> GetSettingAsync<T>(string key);
        Task SetSettingAsync<T>(string key, T value);
        Task SaveSettingsAsync();
        Task LoadSettingsAsync();
        
        // Voice Settings
        string WakeWord { get; set; }
        string VoskModelPath { get; set; }
        string KokoroServerUrl { get; set; }
        string MicrophoneDeviceId { get; set; }
        int AudioSampleRate { get; set; }
        int VoiceActivityDetectionTimeout { get; set; }
        
        // LLM Settings
        string LLMProvider { get; set; }
        string GeminiApiKey { get; set; }
        string GroqApiKey { get; set; }
        int MaxTokens { get; set; }
        double Temperature { get; set; }
        
        // Microsoft Settings
        string MicrosoftClientId { get; set; }
        string MicrosoftTenantId { get; set; }
        string MicrosoftRedirectUri { get; set; }
        
        // Prayer Settings
        double Latitude { get; set; }
        double Longitude { get; set; }
        string City { get; set; }
        string Country { get; set; }
        string CalculationMethod { get; set; }
        bool EnablePrayerNotifications { get; set; }
        int NotificationMinutesBefore { get; set; }
        
        // ActivityWatch Settings
        string ActivityWatchServerUrl { get; set; }
        string ActivityWatchBucketName { get; set; }
        bool EnableActivityTracking { get; set; }
        
        // UI Settings
        string Theme { get; set; }
        int WindowWidth { get; set; }
        int WindowHeight { get; set; }
        bool StartMinimized { get; set; }
        bool MinimizeToTray { get; set; }
    }
}