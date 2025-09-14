using System;
using System.Threading.Tasks;
using PersonalAiAssistant.Models;
using PersonalAiAssistant.Services;

namespace PersonalAiAssistant.Interfaces
{
    public interface IErrorHandlingService
    {
        /// <summary>
        /// Event raised when any error occurs
        /// </summary>
        event EventHandler<ErrorEventArgs>? ErrorOccurred;

        /// <summary>
        /// Event raised when a critical error occurs
        /// </summary>
        event EventHandler<CriticalErrorEventArgs>? CriticalErrorOccurred;

        /// <summary>
        /// Execute an async operation with error handling
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="operation">The operation to execute</param>
        /// <param name="operationName">Name of the operation for logging</param>
        /// <param name="component">Component name for logging</param>
        /// <param name="defaultValue">Default value to return on error</param>
        /// <returns>Operation result or default value</returns>
        Task<T> ExecuteWithErrorHandlingAsync<T>(Func<Task<T>> operation, string operationName, string component, T defaultValue = default(T)!);

        /// <summary>
        /// Execute a synchronous operation with error handling
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="operation">The operation to execute</param>
        /// <param name="operationName">Name of the operation for logging</param>
        /// <param name="component">Component name for logging</param>
        /// <param name="defaultValue">Default value to return on error</param>
        /// <returns>Operation result or default value</returns>
        T ExecuteWithErrorHandling<T>(Func<T> operation, string operationName, string component, T defaultValue = default(T)!);

        /// <summary>
        /// Execute an async action with error handling
        /// </summary>
        /// <param name="operation">The operation to execute</param>
        /// <param name="operationName">Name of the operation for logging</param>
        /// <param name="component">Component name for logging</param>
        Task ExecuteWithErrorHandlingAsync(Func<Task> operation, string operationName, string component);

        /// <summary>
        /// Execute a synchronous action with error handling
        /// </summary>
        /// <param name="operation">The operation to execute</param>
        /// <param name="operationName">Name of the operation for logging</param>
        /// <param name="component">Component name for logging</param>
        void ExecuteWithErrorHandling(Action operation, string operationName, string component);

        /// <summary>
        /// Handle an error asynchronously
        /// </summary>
        /// <param name="exception">The exception to handle</param>
        /// <param name="context">Context where the error occurred</param>
        /// <param name="component">Component that generated the error</param>
        Task HandleErrorAsync(Exception exception, string context, string component);

        /// <summary>
        /// Handle an error synchronously
        /// </summary>
        /// <param name="exception">The exception to handle</param>
        /// <param name="context">Context where the error occurred</param>
        /// <param name="component">Component that generated the error</param>
        void HandleError(Exception exception, string context, string component);

        /// <summary>
        /// Get error statistics
        /// </summary>
        /// <returns>Error statistics</returns>
        ErrorStatistics GetErrorStatistics();

        /// <summary>
        /// Clear error statistics
        /// </summary>
        void ClearErrorStatistics();

        /// <summary>
        /// Check if the system is healthy based on recent errors
        /// </summary>
        /// <returns>True if system is healthy</returns>
        bool IsHealthy();
    }
}