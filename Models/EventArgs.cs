using System;
using System.Collections.Generic;

namespace PersonalAiAssistant.Models
{
    // Wake Word Service Events
    public class WakeWordDetectedEventArgs : EventArgs
    {
        public string WakeWord { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public DateTime Timestamp { get; set; }
        public TimeSpan AudioDuration { get; set; }
    }

    public class WakeWordStatusChangedEventArgs : EventArgs
    {
        public bool IsListening { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? ErrorMessage { get; set; }
    }

    // STT Service Events
    public class CommandTranscribedEventArgs : EventArgs
    {
        public string TranscribedText { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public DateTime Timestamp { get; set; }
        public TimeSpan AudioDuration { get; set; }
        public bool IsFinal { get; set; }
    }

    public class STTStatusChangedEventArgs : EventArgs
    {
        public bool IsListening { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class VoiceActivityDetectedEventArgs : EventArgs
    {
        public bool IsVoiceDetected { get; set; }
        public float AudioLevel { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // TTS Service Events
    public class TTSStatusChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public bool IsSpeaking { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class SpeechStartedEventArgs : EventArgs
    {
        public string Text { get; set; } = string.Empty;
        public string Voice { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
    }

    public class SpeechCompletedEventArgs : EventArgs
    {
        public string Text { get; set; } = string.Empty;
        public string Voice { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public TimeSpan ActualDuration { get; set; }
        public bool WasInterrupted { get; set; }
    }

    // LLM Service Events
    public class LLMResponseEventArgs : EventArgs
    {
        public string UserMessage { get; set; } = string.Empty;
        public string AssistantResponse { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();
        public TimeSpan ResponseTime { get; set; }
        public int TokensUsed { get; set; }
    }

    public class LLMErrorEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? ErrorCode { get; set; }
        public Exception? Exception { get; set; }
    }

    // Microsoft Todo Service Events
    public class TodoUpdatedEventArgs : EventArgs
    {
        public List<TodoItem> TodoItems { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public string UpdateType { get; set; } = string.Empty; // "refresh", "add", "complete", "delete"
        public TodoItem? UpdatedItem { get; set; }
    }

    public class TodoErrorEventArgs : EventArgs
    {
        public string Operation { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Exception? Exception { get; set; }
    }

    public class AuthenticationStatusChangedEventArgs : EventArgs
    {
        public bool IsAuthenticated { get; set; }
        public string? UserEmail { get; set; }
        public string? UserName { get; set; }
        public DateTime Timestamp { get; set; }
        public string? ErrorMessage { get; set; }
    }

    // Prayer Time Service Events
    public class PrayerTimeNotificationEventArgs : EventArgs
    {
        public PrayerInfo PrayerInfo { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public string NotificationType { get; set; } = "time"; // "time", "reminder", "call"
    }

    public class NextPrayerUpdatedEventArgs : EventArgs
    {
        public PrayerInfo? NextPrayer { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // ActivityWatch Client Events
    public class ActivityEventArgs : EventArgs
    {
        public ActivityEvent Activity { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class ConnectionStatusChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public string ServerUrl { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ServiceName { get; set; }
    }

    // Application Orchestrator Events
    public class CommandProcessedEventArgs : EventArgs
    {
        public string Command { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public bool WasSuccessful { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();
    }

    public class SystemStatusChangedEventArgs : EventArgs
    {
        public string Status { get; set; } = string.Empty; // "initializing", "ready", "listening", "processing", "speaking", "error", "shutdown"
        public DateTime Timestamp { get; set; }
        public string? Details { get; set; }
        public Dictionary<string, bool> ServiceStatuses { get; set; } = new();
    }

    public class EmergencyShutdownEventArgs : EventArgs
    {
        public string Reason { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Exception? Exception { get; set; }
        public bool IsRecoverable { get; set; }
    }

    public class ContextUpdatedEventArgs : EventArgs
    {
        public Dictionary<string, object> Context { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public string UpdateType { get; set; } = string.Empty; // "add", "update", "remove", "clear"
        public string? Key { get; set; }
        public object? Value { get; set; }
    }

    // Configuration Events
    public class ConfigurationChangedEventArgs : EventArgs
    {
        public string Section { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public object? OldValue { get; set; }
        public object? NewValue { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // UI Events
    public class NotificationEventArgs : EventArgs
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "info"; // "info", "warning", "error", "success"
        public DateTime Timestamp { get; set; }
        public TimeSpan? Duration { get; set; }
        public Dictionary<string, object>? Data { get; set; }
    }

    public class WindowStateChangedEventArgs : EventArgs
    {
        public string WindowName { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty; // "opened", "closed", "minimized", "maximized", "focused", "unfocused"
        public DateTime Timestamp { get; set; }
    }

    // Voice Processing Events
    public class AudioLevelChangedEventArgs : EventArgs
    {
        public float Level { get; set; }
        public float Peak { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsClipping { get; set; }
    }

    public class ProcessingStateChangedEventArgs : EventArgs
    {
        public string State { get; set; } = string.Empty; // "idle", "listening", "processing", "responding"
        public DateTime Timestamp { get; set; }
        public string? CurrentOperation { get; set; }
        public float? Progress { get; set; }
    }

    public class VoiceProcessingEventArgs : EventArgs
    {
        public string ProcessingStage { get; set; } = string.Empty;
        public bool IsProcessing { get; set; }
        public string? AudioData { get; set; }
        public string? ProcessedText { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    // Error Handling Event Args
    public class ErrorEventArgs : EventArgs
    {
        public Exception Exception { get; set; }
        public string Context { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        public ErrorSeverity Severity { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string? SuggestedAction { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();

        public ErrorEventArgs(Exception exception, string context, string component)
        {
            Exception = exception;
            Context = context;
            Component = component;
        }
    }

    public class CriticalErrorEventArgs : EventArgs
    {
        public Exception Exception { get; set; }
        public string Context { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool RequiresRestart { get; set; }
        public string? RecoveryAction { get; set; }

        public CriticalErrorEventArgs(Exception exception, string context, string component)
        {
            Exception = exception;
            Context = context;
            Component = component;
        }
    }

    // Error Severity Enum
    public enum ErrorSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    // Error Statistics Model
    public class ErrorStatistics
    {
        public int TotalErrors { get; set; }
        public int CriticalErrors { get; set; }
        public int HighSeverityErrors { get; set; }
        public int MediumSeverityErrors { get; set; }
        public int LowSeverityErrors { get; set; }
        public DateTime LastErrorTime { get; set; }
        public string? LastErrorComponent { get; set; }
        public Dictionary<string, int> ErrorsByComponent { get; set; } = new();
        public Dictionary<string, int> ErrorsByType { get; set; } = new();
        public TimeSpan UpTime { get; set; }
        public double ErrorRate { get; set; } // Errors per hour
    }
}