using System;
using System.Threading.Tasks;

namespace PersonalAiAssistant.Services
{
    public interface IApplicationOrchestrator
    {
        event EventHandler<VoiceInteractionEventArgs> VoiceInteractionStarted;
        event EventHandler<VoiceInteractionEventArgs> VoiceInteractionCompleted;
        event EventHandler<SystemStatusChangedEventArgs> SystemStatusChanged;
        
        Task InitializeAsync();
        Task StartServicesAsync();
        Task StopServicesAsync();
        
        Task ProcessVoiceCommandAsync(string command);
        Task ProcessTextCommandAsync(string command);
        
        bool IsInitialized { get; }
        SystemStatus Status { get; }
        
        Task<string> GetSystemContextAsync();
        Task HandleEmergencyShutdownAsync();
    }
    
    public class VoiceInteractionEventArgs : EventArgs
    {
        public string Command { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool WasSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
        public VoiceInteractionType Type { get; set; }
    }
    
    public class SystemStatusChangedEventArgs : EventArgs
    {
        public SystemStatus OldStatus { get; set; }
        public SystemStatus NewStatus { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; } = DateTime.Now;
    }
    
    public enum VoiceInteractionType
    {
        WakeWordDetected,
        CommandTranscribed,
        LLMProcessing,
        ResponseGenerated,
        SpeechSynthesis
    }
    
    public enum SystemStatus
    {
        NotInitialized,
        Initializing,
        Ready,
        Listening,
        Processing,
        Speaking,
        Error,
        Offline
    }
}