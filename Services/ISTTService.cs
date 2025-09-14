using System;
using System.Threading.Tasks;

namespace PersonalAiAssistant.Services
{
    public interface ISTTService
    {
        event EventHandler<CommandTranscribedEventArgs> CommandTranscribed;
        event EventHandler<STTStatusChangedEventArgs> StatusChanged;
        
        Task<bool> InitializeAsync(string modelPath);
        Task StartListeningAsync();
        Task StopListeningAsync();
        
        bool IsInitialized { get; }
        bool IsListening { get; }
        STTStatus Status { get; }
        
        void Dispose();
    }
    
    public class CommandTranscribedEventArgs : EventArgs
    {
        public string TranscribedText { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public DateTime TranscribedAt { get; set; } = DateTime.Now;
        public bool IsFinal { get; set; }
    }
    
    public class STTStatusChangedEventArgs : EventArgs
    {
        public STTStatus OldStatus { get; set; }
        public STTStatus NewStatus { get; set; }
        public string Message { get; set; } = string.Empty;
    }
    
    public enum STTStatus
    {
        NotInitialized,
        Initializing,
        Ready,
        Listening,
        Processing,
        Error
    }
}