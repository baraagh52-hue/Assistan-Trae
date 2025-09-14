using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PersonalAiAssistant.Interfaces;
using PersonalAiAssistant.Models;

namespace PersonalAiAssistant.Services
{
    public class LoggingService : ILoggingService, IDisposable
    {
        private readonly ILogger<LoggingService> _logger;
        private readonly IConfigurationManager _configManager;
        private readonly string _logDirectory;
        private readonly string _logFilePath;
        private readonly object _lockObject = new object();
        private bool _disposed = false;

        public event EventHandler<LogEventArgs>? LogEntryAdded;

        public LoggingService(ILogger<LoggingService> logger, IConfigurationManager configManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            _logFilePath = Path.Combine(_logDirectory, "app.log");
            
            InitializeLogging();
        }

        private void InitializeLogging()
        {
            try
            {
                // Ensure log directory exists
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }

                // Rotate log file if it's too large
                RotateLogFileIfNeeded();

                LogInfo("LoggingService", "Logging service initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize logging service");
            }
        }

        private void RotateLogFileIfNeeded()
        {
            try
            {
                if (!File.Exists(_logFilePath))
                    return;

                var fileInfo = new FileInfo(_logFilePath);
                var maxSizeMB = _configManager.GetValue<int>("Logging:MaxSizeMB", 10);
                var maxSizeBytes = maxSizeMB * 1024 * 1024;

                if (fileInfo.Length > maxSizeBytes)
                {
                    var backupPath = Path.Combine(_logDirectory, $"app_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                    File.Move(_logFilePath, backupPath);
                    
                    // Keep only the last 5 backup files
                    CleanupOldLogFiles();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rotate log file");
            }
        }

        private void CleanupOldLogFiles()
        {
            try
            {
                var logFiles = Directory.GetFiles(_logDirectory, "app_*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .Skip(5);

                foreach (var file in logFiles)
                {
                    file.Delete();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup old log files");
            }
        }

        public void LogDebug(string component, string message, object? data = null)
        {
            LogEntry(LogLevel.Debug, component, message, null, data);
        }

        public void LogInfo(string component, string message, object? data = null)
        {
            LogEntry(LogLevel.Information, component, message, null, data);
        }

        public void LogWarning(string component, string message, Exception? exception = null, object? data = null)
        {
            LogEntry(LogLevel.Warning, component, message, exception, data);
        }

        public void LogError(string component, string message, Exception? exception = null, object? data = null)
        {
            LogEntry(LogLevel.Error, component, message, exception, data);
        }

        public void LogCritical(string component, string message, Exception? exception = null, object? data = null)
        {
            LogEntry(LogLevel.Critical, component, message, exception, data);
        }

        private void LogEntry(LogLevel level, string component, string message, Exception? exception = null, object? data = null)
        {
            if (_disposed) return;

            try
            {
                var logEntry = new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = level,
                    Component = component,
                    Message = message,
                    Exception = exception,
                    Data = data,
                    ThreadId = Environment.CurrentManagedThreadId
                };

                // Log to Microsoft.Extensions.Logging
                _logger.Log(level, exception, "[{Component}] {Message}", component, message);

                // Write to file
                WriteToFile(logEntry);

                // Notify subscribers
                LogEntryAdded?.Invoke(this, new LogEventArgs(logEntry));
            }
            catch (Exception ex)
            {
                // Fallback logging to prevent infinite loops
                try
                {
                    _logger.LogError(ex, "Failed to write log entry");
                }
                catch
                {
                    // If even fallback logging fails, write to console
                    Console.WriteLine($"CRITICAL: Logging system failure - {ex.Message}");
                }
            }
        }

        private void WriteToFile(LogEntry entry)
        {
            lock (_lockObject)
            {
                try
                {
                    var logLine = FormatLogEntry(entry);
                    File.AppendAllText(_logFilePath, logLine + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to write to log file");
                }
            }
        }

        private string FormatLogEntry(LogEntry entry)
        {
            var timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var level = entry.Level.ToString().ToUpper().PadRight(5);
            var component = entry.Component.PadRight(20);
            var threadId = $"[{entry.ThreadId:D3}]";
            
            var logLine = $"{timestamp} {level} {threadId} {component} {entry.Message}";
            
            if (entry.Exception != null)
            {
                logLine += $" | Exception: {entry.Exception.GetType().Name}: {entry.Exception.Message}";
                if (!string.IsNullOrEmpty(entry.Exception.StackTrace))
                {
                    logLine += $" | StackTrace: {entry.Exception.StackTrace.Replace(Environment.NewLine, " | ")}";
                }
            }
            
            if (entry.Data != null)
            {
                try
                {
                    var dataJson = System.Text.Json.JsonSerializer.Serialize(entry.Data);
                    logLine += $" | Data: {dataJson}";
                }
                catch
                {
                    logLine += $" | Data: {entry.Data}";
                }
            }
            
            return logLine;
        }

        public async Task<string[]> GetRecentLogsAsync(int count = 100)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(_logFilePath))
                        return Array.Empty<string>();

                    var lines = File.ReadAllLines(_logFilePath);
                    return lines.TakeLast(count).ToArray();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to read recent logs");
                    return new[] { $"Error reading logs: {ex.Message}" };
                }
            });
        }

        public async Task<string[]> SearchLogsAsync(string searchTerm, DateTime? fromDate = null, DateTime? toDate = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(_logFilePath))
                        return Array.Empty<string>();

                    var lines = File.ReadAllLines(_logFilePath);
                    var filteredLines = lines.Where(line =>
                    {
                        if (string.IsNullOrEmpty(searchTerm) || !line.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                            return false;

                        if (fromDate.HasValue || toDate.HasValue)
                        {
                            // Extract timestamp from log line
                            if (line.Length >= 23 && DateTime.TryParse(line.Substring(0, 23), out var logTime))
                            {
                                if (fromDate.HasValue && logTime < fromDate.Value)
                                    return false;
                                if (toDate.HasValue && logTime > toDate.Value)
                                    return false;
                            }
                        }

                        return true;
                    });

                    return filteredLines.ToArray();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to search logs");
                    return new[] { $"Error searching logs: {ex.Message}" };
                }
            });
        }

        public void ClearLogs()
        {
            try
            {
                lock (_lockObject)
                {
                    if (File.Exists(_logFilePath))
                    {
                        File.Delete(_logFilePath);
                    }
                }
                LogInfo("LoggingService", "Log file cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear logs");
            }
        }

        public LogStatistics GetLogStatistics()
        {
            try
            {
                if (!File.Exists(_logFilePath))
                {
                    return new LogStatistics();
                }

                var lines = File.ReadAllLines(_logFilePath);
                var stats = new LogStatistics
                {
                    TotalEntries = lines.Length,
                    ErrorCount = lines.Count(l => l.Contains(" ERROR ")),
                    WarningCount = lines.Count(l => l.Contains(" WARN ")),
                    InfoCount = lines.Count(l => l.Contains(" INFO ")),
                    DebugCount = lines.Count(l => l.Contains(" DEBUG ")),
                    CriticalCount = lines.Count(l => l.Contains(" CRITICAL ")),
                    FileSizeBytes = new FileInfo(_logFilePath).Length
                };

                // Get date range
                if (lines.Length > 0)
                {
                    if (DateTime.TryParse(lines[0].Substring(0, Math.Min(23, lines[0].Length)), out var firstDate))
                        stats.OldestEntry = firstDate;
                    if (DateTime.TryParse(lines[^1].Substring(0, Math.Min(23, lines[^1].Length)), out var lastDate))
                        stats.NewestEntry = lastDate;
                }

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get log statistics");
                return new LogStatistics();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    LogInfo("LoggingService", "Logging service shutting down");
                }
                catch
                {
                    // Ignore errors during disposal
                }
                
                _disposed = true;
            }
        }
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Component { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public object? Data { get; set; }
        public int ThreadId { get; set; }
    }

    public class LogStatistics
    {
        public int TotalEntries { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public int InfoCount { get; set; }
        public int DebugCount { get; set; }
        public int CriticalCount { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime? OldestEntry { get; set; }
        public DateTime? NewestEntry { get; set; }
    }
}