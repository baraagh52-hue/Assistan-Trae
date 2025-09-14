using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PersonalAiAssistant.Interfaces;
using PersonalAiAssistant.Models;

namespace PersonalAiAssistant.Services
{
    public class ErrorHandlingService : IErrorHandlingService
    {
        private readonly ILoggingService _loggingService;
        private readonly ILogger<ErrorHandlingService> _logger;
        private readonly object _lockObject = new object();
        private readonly Dictionary<string, int> _errorCounts = new Dictionary<string, int>();
        private readonly Dictionary<string, DateTime> _lastErrorTimes = new Dictionary<string, DateTime>();

        public event EventHandler<ErrorEventArgs>? ErrorOccurred;
        public event EventHandler<CriticalErrorEventArgs>? CriticalErrorOccurred;

        public ErrorHandlingService(ILoggingService loggingService, ILogger<ErrorHandlingService> logger)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<T> ExecuteWithErrorHandlingAsync<T>(Func<Task<T>> operation, string operationName, string component, T defaultValue = default(T)!)
        {
            try
            {
                _loggingService.LogDebug(component, $"Starting operation: {operationName}");
                var result = await operation();
                _loggingService.LogDebug(component, $"Completed operation: {operationName}");
                return result;
            }
            catch (Exception ex)
            {
                await HandleErrorAsync(ex, operationName, component);
                return defaultValue;
            }
        }

        public T ExecuteWithErrorHandling<T>(Func<T> operation, string operationName, string component, T defaultValue = default(T)!)
        {
            try
            {
                _loggingService.LogDebug(component, $"Starting operation: {operationName}");
                var result = operation();
                _loggingService.LogDebug(component, $"Completed operation: {operationName}");
                return result;
            }
            catch (Exception ex)
            {
                HandleError(ex, operationName, component);
                return defaultValue;
            }
        }

        public async Task ExecuteWithErrorHandlingAsync(Func<Task> operation, string operationName, string component)
        {
            try
            {
                _loggingService.LogDebug(component, $"Starting operation: {operationName}");
                await operation();
                _loggingService.LogDebug(component, $"Completed operation: {operationName}");
            }
            catch (Exception ex)
            {
                await HandleErrorAsync(ex, operationName, component);
            }
        }

        public void ExecuteWithErrorHandling(Action operation, string operationName, string component)
        {
            try
            {
                _loggingService.LogDebug(component, $"Starting operation: {operationName}");
                operation();
                _loggingService.LogDebug(component, $"Completed operation: {operationName}");
            }
            catch (Exception ex)
            {
                HandleError(ex, operationName, component);
            }
        }

        public async Task HandleErrorAsync(Exception exception, string context, string component)
        {
            await Task.Run(() => HandleError(exception, context, component));
        }

        public void HandleError(Exception exception, string context, string component)
        {
            try
            {
                var errorKey = $"{component}:{context}:{exception.GetType().Name}";
                var errorSeverity = DetermineErrorSeverity(exception);
                
                lock (_lockObject)
                {
                    // Track error frequency
                    _errorCounts[errorKey] = _errorCounts.GetValueOrDefault(errorKey, 0) + 1;
                    _lastErrorTimes[errorKey] = DateTime.UtcNow;
                }

                // Log the error
                switch (errorSeverity)
                {
                    case ErrorSeverity.Low:
                        _loggingService.LogWarning(component, $"Minor error in {context}: {exception.Message}", exception);
                        break;
                    case ErrorSeverity.Medium:
                        _loggingService.LogError(component, $"Error in {context}: {exception.Message}", exception);
                        break;
                    case ErrorSeverity.High:
                        _loggingService.LogCritical(component, $"Critical error in {context}: {exception.Message}", exception);
                        break;
                    case ErrorSeverity.Critical:
                        _loggingService.LogCritical(component, $"CRITICAL SYSTEM ERROR in {context}: {exception.Message}", exception);
                        break;
                }

                // Create error event args
                var errorEventArgs = new ErrorEventArgs
                {
                    Exception = exception,
                    Context = context,
                    Component = component,
                    Severity = errorSeverity,
                    Timestamp = DateTime.UtcNow,
                    ErrorCount = _errorCounts[errorKey],
                    SuggestedAction = GetSuggestedAction(exception, errorSeverity)
                };

                // Raise appropriate events
                ErrorOccurred?.Invoke(this, errorEventArgs);

                if (errorSeverity >= ErrorSeverity.High)
                {
                    var criticalEventArgs = new CriticalErrorEventArgs
                    {
                        Exception = exception,
                        Context = context,
                        Component = component,
                        Severity = errorSeverity,
                        Timestamp = DateTime.UtcNow,
                        RequiresImmediateAttention = errorSeverity == ErrorSeverity.Critical,
                        SuggestedRecoveryAction = GetRecoveryAction(exception)
                    };

                    CriticalErrorOccurred?.Invoke(this, criticalEventArgs);
                }

                // Check for error patterns that might indicate systemic issues
                CheckForErrorPatterns(errorKey, component);
            }
            catch (Exception loggingException)
            {
                // Fallback error handling to prevent infinite loops
                try
                {
                    _logger.LogCritical(loggingException, "Failed to handle error in ErrorHandlingService");
                    Console.WriteLine($"CRITICAL: Error handling failure - Original: {exception.Message}, Logging: {loggingException.Message}");
                }
                catch
                {
                    // Last resort - write to console
                    Console.WriteLine($"FATAL: Complete error handling system failure");
                }
            }
        }

        private ErrorSeverity DetermineErrorSeverity(Exception exception)
        {
            return exception switch
            {
                OutOfMemoryException => ErrorSeverity.Critical,
                StackOverflowException => ErrorSeverity.Critical,
                AccessViolationException => ErrorSeverity.Critical,
                InvalidOperationException when exception.Message.Contains("disposed") => ErrorSeverity.High,
                UnauthorizedAccessException => ErrorSeverity.High,
                System.Net.NetworkInformation.NetworkInformationException => ErrorSeverity.Medium,
                System.Net.Http.HttpRequestException => ErrorSeverity.Medium,
                TimeoutException => ErrorSeverity.Medium,
                ArgumentNullException => ErrorSeverity.Medium,
                ArgumentException => ErrorSeverity.Low,
                InvalidOperationException => ErrorSeverity.Low,
                NotSupportedException => ErrorSeverity.Low,
                _ => ErrorSeverity.Medium
            };
        }

        private string GetSuggestedAction(Exception exception, ErrorSeverity severity)
        {
            return exception switch
            {
                OutOfMemoryException => "Restart application immediately. Check for memory leaks.",
                StackOverflowException => "Restart application. Review recursive operations.",
                UnauthorizedAccessException => "Check file/API permissions and authentication.",
                System.Net.Http.HttpRequestException => "Check network connectivity and API endpoints.",
                TimeoutException => "Increase timeout values or check service availability.",
                ArgumentNullException => "Validate input parameters before method calls.",
                InvalidOperationException when exception.Message.Contains("disposed") => "Check object lifecycle management.",
                _ => severity switch
                {
                    ErrorSeverity.Critical => "Immediate intervention required. Consider application restart.",
                    ErrorSeverity.High => "Review and fix the underlying issue promptly.",
                    ErrorSeverity.Medium => "Monitor and address if pattern emerges.",
                    ErrorSeverity.Low => "Log for analysis and fix when convenient.",
                    _ => "Review error details and take appropriate action."
                }
            };
        }

        private string GetRecoveryAction(Exception exception)
        {
            return exception switch
            {
                OutOfMemoryException => "Force garbage collection and restart application",
                StackOverflowException => "Restart application",
                UnauthorizedAccessException => "Re-authenticate or check permissions",
                System.Net.Http.HttpRequestException => "Retry with exponential backoff",
                TimeoutException => "Retry operation with increased timeout",
                _ => "Retry operation or restart affected service"
            };
        }

        private void CheckForErrorPatterns(string errorKey, string component)
        {
            lock (_lockObject)
            {
                var errorCount = _errorCounts[errorKey];
                var lastErrorTime = _lastErrorTimes[errorKey];
                var timeSinceLastError = DateTime.UtcNow - lastErrorTime;

                // Check for rapid error repetition (more than 5 errors in 1 minute)
                if (errorCount >= 5 && timeSinceLastError < TimeSpan.FromMinutes(1))
                {
                    _loggingService.LogCritical(component, 
                        $"Error pattern detected: {errorKey} occurred {errorCount} times in rapid succession",
                        data: new { ErrorKey = errorKey, Count = errorCount, TimeSpan = timeSinceLastError });
                }

                // Check for persistent errors (more than 10 occurrences)
                if (errorCount >= 10)
                {
                    _loggingService.LogCritical(component,
                        $"Persistent error detected: {errorKey} has occurred {errorCount} times",
                        data: new { ErrorKey = errorKey, Count = errorCount });
                }
            }
        }

        public ErrorStatistics GetErrorStatistics()
        {
            lock (_lockObject)
            {
                var stats = new ErrorStatistics
                {
                    TotalErrors = _errorCounts.Values.Sum(),
                    UniqueErrorTypes = _errorCounts.Count,
                    MostFrequentErrors = _errorCounts
                        .OrderByDescending(kvp => kvp.Value)
                        .Take(10)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    RecentErrors = _lastErrorTimes
                        .Where(kvp => DateTime.UtcNow - kvp.Value < TimeSpan.FromHours(24))
                        .Count(),
                    OldestErrorTime = _lastErrorTimes.Values.Any() ? _lastErrorTimes.Values.Min() : (DateTime?)null,
                    NewestErrorTime = _lastErrorTimes.Values.Any() ? _lastErrorTimes.Values.Max() : (DateTime?)null
                };

                return stats;
            }
        }

        public void ClearErrorStatistics()
        {
            lock (_lockObject)
            {
                _errorCounts.Clear();
                _lastErrorTimes.Clear();
            }
            _loggingService.LogInfo("ErrorHandlingService", "Error statistics cleared");
        }

        public bool IsHealthy()
        {
            lock (_lockObject)
            {
                // Consider the system unhealthy if there are too many recent errors
                var recentErrors = _lastErrorTimes
                    .Where(kvp => DateTime.UtcNow - kvp.Value < TimeSpan.FromMinutes(5))
                    .Sum(kvp => _errorCounts[kvp.Key]);

                return recentErrors < 10; // Threshold for "healthy" system
            }
        }
    }

    public enum ErrorSeverity
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    public class ErrorStatistics
    {
        public int TotalErrors { get; set; }
        public int UniqueErrorTypes { get; set; }
        public Dictionary<string, int> MostFrequentErrors { get; set; } = new Dictionary<string, int>();
        public int RecentErrors { get; set; }
        public DateTime? OldestErrorTime { get; set; }
        public DateTime? NewestErrorTime { get; set; }
    }
}