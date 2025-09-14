using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PersonalAiAssistant.Services
{
    public interface IPrayerTimeService
    {
        event EventHandler<PrayerTimeNotificationEventArgs> PrayerTimeNotification;
        event EventHandler<NextPrayerUpdatedEventArgs> NextPrayerUpdated;
        
        Task<bool> InitializeAsync(double latitude, double longitude, string calculationMethod = "MWL");
        Task<PrayerTimes> GetPrayerTimesAsync(DateTime date);
        Task<PrayerInfo> GetNextPrayerAsync();
        
        Task StartNotificationServiceAsync();
        Task StopNotificationServiceAsync();
        
        bool IsInitialized { get; }
        bool IsNotificationServiceRunning { get; }
        
        Task SetLocationAsync(double latitude, double longitude);
        Task SetCalculationMethodAsync(string method);
        Task SetNotificationSettingsAsync(bool enabled, int minutesBefore);
    }
    
    public class PrayerTimes
    {
        public DateTime Date { get; set; }
        public DateTime Fajr { get; set; }
        public DateTime Sunrise { get; set; }
        public DateTime Dhuhr { get; set; }
        public DateTime Asr { get; set; }
        public DateTime Maghrib { get; set; }
        public DateTime Isha { get; set; }
        
        public Dictionary<string, DateTime> GetAllPrayers()
        {
            return new Dictionary<string, DateTime>
            {
                { "Fajr", Fajr },
                { "Sunrise", Sunrise },
                { "Dhuhr", Dhuhr },
                { "Asr", Asr },
                { "Maghrib", Maghrib },
                { "Isha", Isha }
            };
        }
    }
    
    public class PrayerInfo
    {
        public string Name { get; set; } = string.Empty;
        public DateTime Time { get; set; }
        public TimeSpan TimeRemaining { get; set; }
        public bool IsToday { get; set; }
    }
    
    public class PrayerTimeNotificationEventArgs : EventArgs
    {
        public string PrayerName { get; set; } = string.Empty;
        public DateTime PrayerTime { get; set; }
        public int MinutesBeforePrayer { get; set; }
        public string NotificationType { get; set; } = string.Empty; // "Reminder", "Call"
    }
    
    public class NextPrayerUpdatedEventArgs : EventArgs
    {
        public PrayerInfo NextPrayer { get; set; } = new();
    }
}