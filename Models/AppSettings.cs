namespace PersonalAiAssistant.Models
{
    public class AppSettings
    {
        public VoiceSettings VoiceSettings { get; set; } = new VoiceSettings();
        public LLMSettings LLMSettings { get; set; } = new LLMSettings();
        public MicrosoftSettings MicrosoftSettings { get; set; } = new MicrosoftSettings();
        public PrayerSettings PrayerSettings { get; set; } = new PrayerSettings();
        public ActivityWatchSettings ActivityWatchSettings { get; set; } = new ActivityWatchSettings();
        public UISettings UISettings { get; set; } = new UISettings();
    }

    public class VoiceSettings
    {
        public string WakeWord { get; set; } = "Assistant";
        public string VoskModelPath { get; set; } = "Models/vosk-model-small-en-us-0.15";
        public string KokoroServerUrl { get; set; } = "http://localhost:8020";
        public int AudioSampleRate { get; set; } = 16000;
        public double VoiceActivationThreshold { get; set; } = 0.5;
        public int SilenceTimeoutMs { get; set; } = 2000;
    }

    public class LLMSettings
    {
        public string Provider { get; set; } = "Gemini";
        public string GeminiApiKey { get; set; } = string.Empty;
        public string GroqApiKey { get; set; } = string.Empty;
        public int MaxTokens { get; set; } = 1000;
        public double Temperature { get; set; } = 0.7;
        public string SystemPrompt { get; set; } = "You are a helpful personal AI assistant. Be concise and friendly.";
    }

    public class MicrosoftSettings
    {
        public string ClientId { get; set; } = "your-client-id";
        public string TenantId { get; set; } = "common";
        public string RedirectUri { get; set; } = "http://localhost:8080/auth/callback";
    }

    public class PrayerSettings
    {
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public double Latitude { get; set; } = 0.0;
        public double Longitude { get; set; } = 0.0;
        public string CalculationMethod { get; set; } = "MuslimWorldLeague";
        public bool EnableNotifications { get; set; } = true;
        public int NotificationMinutesBefore { get; set; } = 5;
    }

    public class ActivityWatchSettings
    {
        public string ServerUrl { get; set; } = "http://localhost:5600";
        public string BucketName { get; set; } = "aw-watcher-window";
        public bool EnableTracking { get; set; } = true;
    }

    public class UISettings
    {
        public string Theme { get; set; } = "Light";
        public int WindowWidth { get; set; } = 1000;
        public int WindowHeight { get; set; } = 700;
        public bool MinimizeToTray { get; set; } = true;
        public bool StartMinimized { get; set; } = false;
    }
}