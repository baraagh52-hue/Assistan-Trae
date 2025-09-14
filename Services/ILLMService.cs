using System;
using System.Threading.Tasks;

namespace PersonalAiAssistant.Services
{
    public interface ILLMService
    {
        event EventHandler<LLMResponseEventArgs> ResponseReceived;
        event EventHandler<LLMErrorEventArgs> ErrorOccurred;
        
        Task<string> ProcessRequestAsync(string userInput, string context = "");
        Task<bool> TestConnectionAsync();
        
        bool IsConfigured { get; }
        string CurrentProvider { get; }
        
        Task SetProviderAsync(string provider, string apiKey);
        Task<LLMUsageInfo> GetUsageInfoAsync();
    }
    
    public class LLMResponseEventArgs : EventArgs
    {
        public string UserInput { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; } = DateTime.Now;
        public TimeSpan ProcessingTime { get; set; }
        public int TokensUsed { get; set; }
    }
    
    public class LLMErrorEventArgs : EventArgs
    {
        public string ErrorMessage { get; set; } = string.Empty;
        public string UserInput { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public DateTime OccurredAt { get; set; } = DateTime.Now;
    }
    
    public class LLMUsageInfo
    {
        public int TotalTokensUsed { get; set; }
        public int RequestsToday { get; set; }
        public DateTime LastRequest { get; set; }
        public string Provider { get; set; } = string.Empty;
        public bool HasQuotaRemaining { get; set; }
    }
}