using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PersonalAiAssistant.Interfaces;
using PersonalAiAssistant.Services;
using PersonalAiAssistant.Models;

namespace PersonalAiAssistant
{
    public class Program
    {
        [STAThread]
        public static async Task Main(string[] args)
        {
            try
            {
                // Detect platform and run appropriate interface
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    await RunWindowsApplicationAsync(args);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || 
                         RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    await RunConsoleApplicationAsync(args);
                }
                else
                {
                    Console.WriteLine("Unsupported platform. This application supports Windows, Linux, and macOS.");
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }

        private static async Task RunWindowsApplicationAsync(string[] args)
        {
#if NET8_0_WINDOWS
            // Run WPF application on Windows
            var app = new App();
            app.InitializeComponent();
            app.Run();
#else
            // Fallback to console on Windows if WPF is not available
            Console.WriteLine("WPF not available. Running in console mode...");
            await RunConsoleApplicationAsync(args);
#endif
        }

        private static async Task RunConsoleApplicationAsync(string[] args)
        {
            Console.WriteLine($"Starting Personal AI Assistant on {RuntimeInformation.OSDescription}");
            Console.WriteLine($"Architecture: {RuntimeInformation.OSArchitecture}");
            Console.WriteLine();

            // Build host with dependency injection
            var host = CreateHostBuilder(args).Build();

            try
            {
                // Get console interface and run
                using var consoleInterface = host.Services.GetRequiredService<ConsoleInterface>();
                await consoleInterface.RunAsync();
            }
            catch (Exception ex)
            {
                var logger = host.Services.GetService<ILogger<Program>>();
                logger?.LogError(ex, "Application crashed");
                Console.WriteLine($"Application error: {ex.Message}");
                throw;
            }
            finally
            {
                await host.StopAsync();
                host.Dispose();
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    // Register core services
                    services.AddSingleton<ILoggingService, LoggingService>();
                    services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();
                    services.AddSingleton<IConfigurationManager, ConfigurationManager>();
                    
                    // Register AI services
                    services.AddSingleton<IWakeWordService, WakeWordService>();
                    services.AddSingleton<ISpeechToTextService, SpeechToTextService>();
                    services.AddSingleton<ITextToSpeechService, TextToSpeechService>();
                    services.AddSingleton<ILLMService, LLMService>();
                    
                    // Register productivity services
                    services.AddSingleton<IMicrosoftToDoService, MicrosoftToDoService>();
                    services.AddSingleton<IPrayerTimeService, PrayerTimeService>();
                    services.AddSingleton<IActivityWatchService, ActivityWatchService>();
                    
                    // Register orchestrator
                    services.AddSingleton<IApplicationOrchestrator, ApplicationOrchestrator>();
                    
                    // Register console interface
                    services.AddSingleton<ConsoleInterface>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Information);
                    
                    // Add file logging for Linux/Raspberry Pi
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                                                  ".personal-ai-assistant", "logs");
                        Directory.CreateDirectory(logPath);
                        
                        // Note: You might want to add a file logging provider here
                        // For now, we'll rely on console logging
                    }
                });
    }
}