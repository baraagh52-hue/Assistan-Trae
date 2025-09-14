using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PersonalAiAssistant.Models;

namespace PersonalAiAssistant.Interfaces
{
    public interface ILoggingService : IDisposable
    {
        /// <summary>
        /// Event raised when a new log entry is added
        /// </summary>
        event EventHandler<LogEventArgs>? LogEntryAdded;

        /// <summary>
        /// Log a debug message
        /// </summary>
        /// <param name="component">The component generating the log</param>
        /// <param name="message">The log message</param>
        /// <param name="data">Optional additional data</param>
        void LogDebug(string component, string message, object? data = null);

        /// <summary>
        /// Log an informational message
        /// </summary>
        /// <param name="component">The component generating the log</param>
        /// <param name="message">The log message</param>
        /// <param name="data">Optional additional data</param>
        void LogInfo(string component, string message, object? data = null);

        /// <summary>
        /// Log a warning message
        /// </summary>
        /// <param name="component">The component generating the log</param>
        /// <param name="message">The log message</param>
        /// <param name="exception">Optional exception</param>
        /// <param name="data">Optional additional data</param>
        void LogWarning(string component, string message, Exception? exception = null, object? data = null);

        /// <summary>
        /// Log an error message
        /// </summary>
        /// <param name="component">The component generating the log</param>
        /// <param name="message">The log message</param>
        /// <param name="exception">Optional exception</param>
        /// <param name="data">Optional additional data</param>
        void LogError(string component, string message, Exception? exception = null, object? data = null);

        /// <summary>
        /// Log a critical error message
        /// </summary>
        /// <param name="component">The component generating the log</param>
        /// <param name="message">The log message</param>
        /// <param name="exception">Optional exception</param>
        /// <param name="data">Optional additional data</param>
        void LogCritical(string component, string message, Exception? exception = null, object? data = null);

        /// <summary>
        /// Get recent log entries
        /// </summary>
        /// <param name="count">Number of recent entries to retrieve</param>
        /// <returns>Array of log lines</returns>
        Task<string[]> GetRecentLogsAsync(int count = 100);

        /// <summary>
        /// Search log entries
        /// </summary>
        /// <param name="searchTerm">Term to search for</param>
        /// <param name="fromDate">Optional start date filter</param>
        /// <param name="toDate">Optional end date filter</param>
        /// <returns>Array of matching log lines</returns>
        Task<string[]> SearchLogsAsync(string searchTerm, DateTime? fromDate = null, DateTime? toDate = null);

        /// <summary>
        /// Clear all log entries
        /// </summary>
        void ClearLogs();

        /// <summary>
        /// Get log file statistics
        /// </summary>
        /// <returns>Log statistics</returns>
        LogStatistics GetLogStatistics();
    }
}