using System;
using System.Threading.Tasks;

namespace PersonalAiAssistant.Services
{
    public interface ITTSService
    {
        event EventHandler<TTSStatusChangedEventArgs> StatusChanged;
        event EventHandler<SpeechStartedEventArgs> SpeechStarted;
        event EventHandler<SpeechCompletedEventArgs> SpeechCompleted;
        
        Task<bool> InitializeAsync();
        Task<bool> SpeakAsync(string text, string? voice = null);
        Task StopSpeakingAsync();
        
        bool IsInitialized { get; }
        bool IsSpeaking { get; }
        TTSStatus Status { get; }
        
        Task<string[]> GetAvailableVoicesAsync();
        Task SetVoiceAsync(string voiceName);
        Task<bool> TestConnectionAsync();
        
        void Dispose();
    }
    
    public class TTSStatusChangedEventArgs : EventArgs
    {
        public TTSStatus OldStatus { get; set; }
        public TTSStatus NewStatus { get; set; }
        public string Message { get; set; } = string.Empty;
    }
    
    public class SpeechStartedEventArgs : EventArgs
    {
        public string Text { get; set; } = string.Empty;
        public string Voice { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; } = DateTime.Now;
    }
    
    public class SpeechCompletedEventArgs : EventArgs
    {
        public string Text { get; set; } = string.Empty;
        public bool WasInterrupted { get; set; }
        public DateTime CompletedAt { get; set; } = DateTime.Now;
        public TimeSpan Duration { get; set; }
    }
    
    public enum TTSStatus
    {
        NotInitialized,
        Initializing,
        Ready,
        Speaking,
        Error,
        ServerUnavailable
    }
}