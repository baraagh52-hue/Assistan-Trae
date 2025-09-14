using System;
using System.Threading.Tasks;

namespace PersonalAiAssistant.Services
{
    public interface IWakeWordService
    {
        event EventHandler<WakeWordDetectedEventArgs> WakeWordDetected;
        
        Task StartListeningAsync();
        Task StopListeningAsync();
        bool IsListening { get; }
        
        Task<bool> InitializeAsync(string wakeWord);
        Task SetWakeWordAsync(string wakeWord);
        void Dispose();
    }
    
    public class WakeWordDetectedEventArgs : EventArgs
    {
        public string WakeWord { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; } = DateTime.Now;
        public double Confidence { get; set; }
    }
}