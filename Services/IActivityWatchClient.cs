using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PersonalAiAssistant.Services
{
    public interface IActivityWatchClient
    {
        event EventHandler<ActivityUpdatedEventArgs> ActivityUpdated;
        
        Task<bool> ConnectAsync(string serverUrl);
        Task<List<ActivityEvent>> GetRecentActivityAsync(int hours = 1);
        Task<ActivitySummary> GetActivitySummaryAsync(DateTime startDate, DateTime endDate);
        
        bool IsConnected { get; }
        string ServerUrl { get; }
        
        Task<bool> TestConnectionAsync();
        void Disconnect();
    }
    
    public class ActivityEvent
    {
        public DateTime Timestamp { get; set; }
        public TimeSpan Duration { get; set; }
        public string Application { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }
    
    public class ActivitySummary
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public TimeSpan TotalActiveTime { get; set; }
        public Dictionary<string, TimeSpan> ApplicationUsage { get; set; } = new();
        public Dictionary<string, TimeSpan> CategoryUsage { get; set; } = new();
        public string MostUsedApplication { get; set; } = string.Empty;
        public string CurrentActivity { get; set; } = string.Empty;
    }
    
    public class ActivityUpdatedEventArgs : EventArgs
    {
        public ActivityEvent CurrentActivity { get; set; } = new();
        public ActivitySummary TodaySummary { get; set; } = new();
    }
}