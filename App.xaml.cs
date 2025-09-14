using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PersonalAiAssistant.Interfaces;
using PersonalAiAssistant.Services;

namespace PersonalAiAssistant
{
    public partial class App : Application
    {
        private IHost? _host;

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Create the host builder
                var hostBuilder = Host.CreateDefaultBuilder()
                    .ConfigureServices((context, services) =>
                    {
                        ConfigureServices(services);
                    })
                    .ConfigureLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddConsole();
                        logging.AddDebug();
                        logging.SetMinimumLevel(LogLevel.Information);
                    });

                _host = hostBuilder.Build();

                // Start the host
                await _host.StartAsync();

                // Create and show the main window
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start application: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Register core services first
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();
            services.AddSingleton<IConfigurationManager, ConfigurationManager>();

            // Register other services
            services.AddSingleton<IWakeWordService, WakeWordService>();
            services.AddSingleton<ISTTService, STTService>();
            services.AddSingleton<ITTSService, TTSService>();
            services.AddSingleton<ILLMService, LLMService>();
            services.AddSingleton<IMicrosoftTodoService, MicrosoftTodoService>();
            services.AddSingleton<IPrayerTimeService, PrayerTimeService>();
            services.AddSingleton<IActivityWatchClient, ActivityWatchClient>();

            // Register application orchestrator
            services.AddSingleton<IApplicationOrchestrator, ApplicationOrchestrator>();

            // Register main window
            services.AddTransient<MainWindow>();

            // Register HttpClient for services that need it
            services.AddHttpClient();

            // Register logging
            services.AddLogging();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            try
            {
                if (_host != null)
                {
                    // Stop the orchestrator gracefully
                    var orchestrator = _host.Services.GetService<IApplicationOrchestrator>();
                    if (orchestrator != null)
                    {
                        await orchestrator.StopAsync();
                        orchestrator.Dispose();
                    }

                    // Stop and dispose the host
                    await _host.StopAsync();
                    _host.Dispose();
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't prevent shutdown
                System.Diagnostics.Debug.WriteLine($"Error during application shutdown: {ex.Message}");
            }
            finally
            {
                base.OnExit(e);
            }
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                var logger = _host?.Services.GetService<ILogger<App>>();
                logger?.LogError(e.Exception, "Unhandled exception occurred");

                MessageBox.Show($"An unexpected error occurred: {e.Exception.Message}\n\nThe application will continue running, but some features may not work correctly.", 
                              "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Warning);

                // Mark the exception as handled to prevent application crash
                e.Handled = true;
            }
            catch
            {
                // If we can't even show the error message, let the application crash
                e.Handled = false;
            }
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Subscribe to unhandled exception events
            DispatcherUnhandledException += Application_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var logger = _host?.Services.GetService<ILogger<App>>();
                logger?.LogCritical(e.ExceptionObject as Exception, "Critical unhandled exception occurred");

                if (e.ExceptionObject is Exception ex)
                {
                    MessageBox.Show($"A critical error occurred: {ex.Message}\n\nThe application will now exit.", 
                                  "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch
            {
                // If we can't handle the error, just let it crash
            }
        }
    }
}