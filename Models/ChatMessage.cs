using System;

namespace PersonalAiAssistant.Models
{
    public class ChatMessage
    {
        public string Role { get; set; } = string.Empty; // "system", "user", "assistant"
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? MessageId { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
        
        public ChatMessage()
        {
            Timestamp = DateTime.UtcNow;
            MessageId = Guid.NewGuid().ToString();
        }
        
        public ChatMessage(string role, string content) : this()
        {
            Role = role;
            Content = content;
        }
        
        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss}] {Role}: {Content}";
        }
    }
    
    public class TodoItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Body { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public DateTime? DueDateTime { get; set; }
        public DateTime? CompletedDateTime { get; set; }
        public string? ListId { get; set; }
        public string? ListName { get; set; }
        public string Priority { get; set; } = "normal"; // low, normal, high
        public List<string>? Categories { get; set; }
        
        public TodoItem()
        {
            Id = Guid.NewGuid().ToString();
            CreatedDateTime = DateTime.UtcNow;
            Categories = new List<string>();
        }
        
        public override string ToString()
        {
            var status = IsCompleted ? "✓" : "○";
            var dueText = DueDateTime.HasValue ? $" (Due: {DueDateTime.Value:MM/dd})" : "";
            return $"{status} {Title}{dueText}";
        }
    }
    
    public class PrayerTimes
    {
        public DateTime Date { get; set; }
        public DateTime Fajr { get; set; }
        public DateTime Sunrise { get; set; }
        public DateTime Dhuhr { get; set; }
        public DateTime Asr { get; set; }
        public DateTime Sunset { get; set; }
        public DateTime Maghrib { get; set; }
        public DateTime Isha { get; set; }
        public DateTime Midnight { get; set; }
        public string Location { get; set; } = string.Empty;
        public int CalculationMethod { get; set; }
        public int Madhab { get; set; }
        
        public List<(string Name, DateTime Time)> GetAllPrayerTimes()
        {
            return new List<(string, DateTime)>
            {
                ("Fajr", Fajr),
                ("Sunrise", Sunrise),
                ("Dhuhr", Dhuhr),
                ("Asr", Asr),
                ("Sunset", Sunset),
                ("Maghrib", Maghrib),
                ("Isha", Isha),
                ("Midnight", Midnight)
            };
        }
        
        public DateTime? GetPrayerTime(string prayerName)
        {
            return prayerName.ToLowerInvariant() switch
            {
                "fajr" => Fajr,
                "sunrise" => Sunrise,
                "dhuhr" => Dhuhr,
                "asr" => Asr,
                "sunset" => Sunset,
                "maghrib" => Maghrib,
                "isha" => Isha,
                "midnight" => Midnight,
                _ => null
            };
        }
    }
    
    public class PrayerInfo
    {
        public string Name { get; set; } = string.Empty;
        public DateTime Time { get; set; }
        public TimeSpan TimeUntil { get; set; }
        public bool IsToday { get; set; } = true;
        
        public string GetFormattedTimeUntil()
        {
            if (TimeUntil.TotalDays >= 1)
            {
                return $"{TimeUntil.Days}d {TimeUntil.Hours}h {TimeUntil.Minutes}m";
            }
            else if (TimeUntil.TotalHours >= 1)
            {
                return $"{TimeUntil.Hours}h {TimeUntil.Minutes}m";
            }
            else if (TimeUntil.TotalMinutes >= 1)
            {
                return $"{TimeUntil.Minutes}m";
            }
            else
            {
                return "Now";
            }
        }
        
        public override string ToString()
        {
            return $"{Name} at {Time:HH:mm} ({GetFormattedTimeUntil()})";
        }
    }
    
    public class ActivityEvent
    {
        public DateTime Timestamp { get; set; }
        public TimeSpan Duration { get; set; }
        public string ApplicationName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public Dictionary<string, object>? Metadata { get; set; }
        
        public ActivityEvent()
        {
            Timestamp = DateTime.UtcNow;
            Metadata = new Dictionary<string, object>();
        }
        
        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss}] {ApplicationName} - {WindowTitle} ({Duration.TotalMinutes:F1}m)";
        }
    }
    
    public class ActivitySummary
    {
        public DateTime Date { get; set; }
        public TimeSpan TotalActiveTime { get; set; }
        public Dictionary<string, TimeSpan> ApplicationUsage { get; set; } = new();
        public Dictionary<string, TimeSpan> WindowTitleUsage { get; set; } = new();
        public string MostUsedApplication { get; set; } = string.Empty;
        public string MostUsedWindowTitle { get; set; } = string.Empty;
        public int TotalApplications => ApplicationUsage.Count;
        public int TotalWindows => WindowTitleUsage.Count;
        
        public List<(string Name, TimeSpan Duration)> GetTopApplications(int count = 5)
        {
            return ApplicationUsage
                .OrderByDescending(kvp => kvp.Value)
                .Take(count)
                .Select(kvp => (kvp.Key, kvp.Value))
                .ToList();
        }
        
        public List<(string Name, TimeSpan Duration)> GetTopWindows(int count = 5)
        {
            return WindowTitleUsage
                .OrderByDescending(kvp => kvp.Value)
                .Take(count)
                .Select(kvp => (kvp.Key, kvp.Value))
                .ToList();
        }
        
        public override string ToString()
        {
            return $"Activity Summary for {Date:yyyy-MM-dd}: {TotalActiveTime.TotalHours:F1}h active, {TotalApplications} apps, {TotalWindows} windows";
        }
    }
}