using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PersonalAiAssistant.Interfaces;
using PersonalAiAssistant.Models;
using System.Text;
using System.Runtime.InteropServices;

namespace PersonalAiAssistant
{
    public class ConsoleInterface : IDisposable
    {
        private readonly ILogger<ConsoleInterface> _logger;
        private readonly IApplicationOrchestrator _orchestrator;
        private readonly IConfigurationManager _configManager;
        private readonly IErrorHandlingService _errorHandler;
        private readonly ILoggingService _loggingService;
        private readonly IWakeWordService _wakeWordService;
        private readonly ISpeechToTextService _sttService;
        private readonly ITextToSpeechService _ttsService;
        private readonly ILLMService _llmService;
        private readonly IMicrosoftToDoService _todoService;
        private readonly IPrayerTimeService _prayerService;
        private readonly IActivityWatchService _activityService;
        
        private bool _isRunning;
        private bool _disposed;
        private readonly StringBuilder _statusDisplay;
        private readonly Timer _statusUpdateTimer;

        public ConsoleInterface(
            ILogger<ConsoleInterface> logger,
            IApplicationOrchestrator orchestrator,
            IConfigurationManager configManager,
            IErrorHandlingService errorHandler,
            ILoggingService loggingService,
            IWakeWordService wakeWordService,
            ISpeechToTextService sttService,
            ITextToSpeechService ttsService,
            ILLMService llmService,
            IMicrosoftToDoService todoService,
            IPrayerTimeService prayerService,
            IActivityWatchService activityService)
        {
            _logger = logger;
            _orchestrator = orchestrator;
            _configManager = configManager;
            _errorHandler = errorHandler;
            _loggingService = loggingService;
            _wakeWordService = wakeWordService;
            _sttService = sttService;
            _ttsService = ttsService;
            _llmService = llmService;
            _todoService = todoService;
            _prayerService = prayerService;
            _activityService = activityService;
            
            _statusDisplay = new StringBuilder();
            _statusUpdateTimer = new Timer(UpdateStatusDisplay, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
            
            SubscribeToEvents();
        }

        public async Task RunAsync()
        {
            await _errorHandler.ExecuteWithErrorHandlingAsync(async () =>
            {
                _loggingService.LogInfo("Starting Console Interface...", "ConsoleInterface");
                
                DisplayWelcomeMessage();
                
                // Initialize the orchestrator
                bool initialized = await _orchestrator.InitializeAsync();
                
                if (!initialized)
                {
                    Console.WriteLine("❌ Failed to initialize the application. Check logs for details.");
                    return;
                }
                
                _isRunning = true;
                Console.WriteLine("✅ Personal AI Assistant is ready!");
                Console.WriteLine("Type 'help' for commands or 'quit' to exit.");
                
                await RunCommandLoopAsync();
                
            }, "RunAsync", "ConsoleInterface");
        }

        private void DisplayWelcomeMessage()
        {
            Console.Clear();
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                Personal AI Assistant                         ║");
            Console.WriteLine("║              Cross-Platform Voice Assistant                  ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine($"Platform: {RuntimeInformation.OSDescription}");
            Console.WriteLine($"Architecture: {RuntimeInformation.OSArchitecture}");
            Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
            Console.WriteLine();
        }

        private async Task RunCommandLoopAsync()
        {
            while (_isRunning)
            {
                Console.Write("AI Assistant> ");
                var input = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(input))
                    continue;
                
                await ProcessCommandAsync(input);
            }
        }

        private async Task ProcessCommandAsync(string command)
        {
            await _errorHandler.ExecuteWithErrorHandlingAsync(async () =>
            {
                switch (command.ToLower())
                {
                    case "help":
                        DisplayHelp();
                        break;
                    
                    case "status":
                        await DisplayStatusAsync();
                        break;
                    
                    case "start":
                        await StartServicesAsync();
                        break;
                    
                    case "stop":
                        await StopServicesAsync();
                        break;
                    
                    case "test-mic":
                        await TestMicrophoneAsync();
                        break;
                    
                    case "test-tts":
                        await TestTextToSpeechAsync();
                        break;
                    
                    case "test-llm":
                        await TestLLMAsync();
                        break;
                    
                    case "config":
                        await DisplayConfigurationAsync();
                        break;
                    
                    case "logs":
                        DisplayRecentLogs();
                        break;
                    
                    case "tasks":
                        await DisplayTasksAsync();
                        break;
                    
                    case "prayer":
                        await DisplayPrayerTimesAsync();
                        break;
                    
                    case "clear":
                        Console.Clear();
                        DisplayWelcomeMessage();
                        break;
                    
                    case "quit":
                    case "exit":
                        _isRunning = false;
                        Console.WriteLine("Goodbye!");
                        break;
                    
                    default:
                        if (command.StartsWith("say "))
                        {
                            var text = command.Substring(4);
                            await _ttsService.SpeakAsync(text);
                        }
                        else if (command.StartsWith("ask "))
                        {
                            var question = command.Substring(4);
                            await ProcessLLMQueryAsync(question);
                        }
                        else
                        {
                            Console.WriteLine($"Unknown command: {command}. Type 'help' for available commands.");
                        }
                        break;
                }
            }, "ProcessCommandAsync", "ConsoleInterface");
        }

        private void DisplayHelp()
        {
            Console.WriteLine();
            Console.WriteLine("Available Commands:");
            Console.WriteLine("══════════════════");
            Console.WriteLine("help          - Show this help message");
            Console.WriteLine("status        - Show system status");
            Console.WriteLine("start         - Start voice services");
            Console.WriteLine("stop          - Stop voice services");
            Console.WriteLine("test-mic      - Test microphone input");
            Console.WriteLine("test-tts      - Test text-to-speech");
            Console.WriteLine("test-llm      - Test LLM connection");
            Console.WriteLine("config        - Show configuration");
            Console.WriteLine("logs          - Show recent logs");
            Console.WriteLine("tasks         - Show Microsoft To-Do tasks");
            Console.WriteLine("prayer        - Show prayer times");
            Console.WriteLine("say <text>    - Speak text using TTS");
            Console.WriteLine("ask <question>- Ask the AI assistant");
            Console.WriteLine("clear         - Clear screen");
            Console.WriteLine("quit/exit     - Exit the application");
            Console.WriteLine();
        }

        private async Task DisplayStatusAsync()
        {
            Console.WriteLine();
            Console.WriteLine("System Status:");
            Console.WriteLine("═════════════");
            Console.WriteLine($"Orchestrator: {(_orchestrator.IsRunning ? "✅ Running" : "❌ Stopped")}");
            Console.WriteLine($"Wake Word:    {(_wakeWordService.IsListening ? "✅ Listening" : "❌ Not Listening")}");
            Console.WriteLine($"STT Service:  {(await _sttService.IsAvailableAsync() ? "✅ Available" : "❌ Unavailable")}");
            Console.WriteLine($"TTS Service:  {(await _ttsService.IsAvailableAsync() ? "✅ Available" : "❌ Unavailable")}");
            Console.WriteLine($"LLM Service:  {(await _llmService.TestConnectionAsync() ? "✅ Connected" : "❌ Disconnected")}");
            Console.WriteLine($"Todo Service: {(await _todoService.IsAuthenticatedAsync() ? "✅ Authenticated" : "❌ Not Authenticated")}");
            Console.WriteLine($"Prayer Times: {(_prayerService.IsConfigured ? "✅ Configured" : "❌ Not Configured")}");
            Console.WriteLine($"Activity:     {(_activityService.IsConnected ? "✅ Connected" : "❌ Disconnected")}");
            Console.WriteLine();
        }

        private async Task StartServicesAsync()
        {
            Console.WriteLine("Starting voice services...");
            await _wakeWordService.StartListeningAsync();
            Console.WriteLine("✅ Voice services started");
        }

        private async Task StopServicesAsync()
        {
            Console.WriteLine("Stopping voice services...");
            await _wakeWordService.StopListeningAsync();
            Console.WriteLine("✅ Voice services stopped");
        }

        private async Task TestMicrophoneAsync()
        {
            Console.WriteLine("Testing microphone... (speak for 3 seconds)");
            var result = await _sttService.RecognizeSpeechAsync(TimeSpan.FromSeconds(3));
            Console.WriteLine($"Recognized: {result ?? "(no speech detected)"}");
        }

        private async Task TestTextToSpeechAsync()
        {
            Console.WriteLine("Testing text-to-speech...");
            await _ttsService.SpeakAsync("Hello! Text to speech is working correctly.");
            Console.WriteLine("✅ TTS test completed");
        }

        private async Task TestLLMAsync()
        {
            Console.WriteLine("Testing LLM connection...");
            var connected = await _llmService.TestConnectionAsync();
            Console.WriteLine(connected ? "✅ LLM connection successful" : "❌ LLM connection failed");
        }

        private async Task ProcessLLMQueryAsync(string question)
        {
            Console.WriteLine("Processing query...");
            var response = await _llmService.ProcessMessageAsync(question);
            Console.WriteLine($"AI: {response}");
            
            // Optionally speak the response
            if (await _ttsService.IsAvailableAsync())
            {
                await _ttsService.SpeakAsync(response);
            }
        }

        private async Task DisplayConfigurationAsync()
        {
            var config = await _configManager.GetConfigurationAsync();
            Console.WriteLine();
            Console.WriteLine("Configuration:");
            Console.WriteLine("═════════════");
            Console.WriteLine($"Wake Words: {string.Join(", ", config.WakeWords ?? new[] { "Not configured" })}");
            Console.WriteLine($"LLM Provider: {config.LLMProvider ?? "Not configured"}");
            Console.WriteLine($"LLM Model: {config.LLMModel ?? "Not configured"}");
            Console.WriteLine($"Vosk Model: {config.VoskModelPath ?? "Not configured"}");
            Console.WriteLine($"Kokoro Server: {config.KokoroServerUrl ?? "Not configured"}");
            Console.WriteLine();
        }

        private void DisplayRecentLogs()
        {
            var logs = _loggingService.GetRecentLogs(10);
            Console.WriteLine();
            Console.WriteLine("Recent Logs:");
            Console.WriteLine("═══════════");
            foreach (var log in logs)
            {
                Console.WriteLine($"[{log.Timestamp:HH:mm:ss}] {log.Level}: {log.Message}");
            }
            Console.WriteLine();
        }

        private async Task DisplayTasksAsync()
        {
            if (!await _todoService.IsAuthenticatedAsync())
            {
                Console.WriteLine("❌ Microsoft To-Do not authenticated");
                return;
            }
            
            var tasks = await _todoService.GetTasksAsync();
            Console.WriteLine();
            Console.WriteLine("Microsoft To-Do Tasks:");
            Console.WriteLine("═════════════════════");
            
            if (tasks?.Any() == true)
            {
                foreach (var task in tasks.Take(10))
                {
                    var status = task.IsCompleted ? "✅" : "⏳";
                    Console.WriteLine($"{status} {task.Title}");
                }
            }
            else
            {
                Console.WriteLine("No tasks found");
            }
            Console.WriteLine();
        }

        private async Task DisplayPrayerTimesAsync()
        {
            if (!_prayerService.IsConfigured)
            {
                Console.WriteLine("❌ Prayer times not configured");
                return;
            }
            
            var prayerTimes = await _prayerService.GetTodaysPrayerTimesAsync();
            Console.WriteLine();
            Console.WriteLine("Today's Prayer Times:");
            Console.WriteLine("═══════════════════");
            Console.WriteLine($"Fajr:    {prayerTimes.Fajr:HH:mm}");
            Console.WriteLine($"Dhuhr:   {prayerTimes.Dhuhr:HH:mm}");
            Console.WriteLine($"Asr:     {prayerTimes.Asr:HH:mm}");
            Console.WriteLine($"Maghrib: {prayerTimes.Maghrib:HH:mm}");
            Console.WriteLine($"Isha:    {prayerTimes.Isha:HH:mm}");
            Console.WriteLine();
        }

        private void UpdateStatusDisplay(object? state)
        {
            // Update console title with basic status
            try
            {
                var status = _orchestrator.IsRunning ? "Running" : "Stopped";
                var listening = _wakeWordService.IsListening ? "Listening" : "Idle";
                Console.Title = $"AI Assistant - {status} - {listening}";
            }
            catch
            {
                // Ignore title update errors on some platforms
            }
        }

        private void SubscribeToEvents()
        {
            _wakeWordService.WakeWordDetected += OnWakeWordDetected;
            _orchestrator.StateChanged += OnOrchestratorStateChanged;
            _errorHandler.ErrorOccurred += OnErrorOccurred;
        }

        private void OnWakeWordDetected(object? sender, WakeWordEventArgs e)
        {
            Console.WriteLine($"\n🎤 Wake word detected: {e.WakeWord}");
            Console.Write("AI Assistant> ");
        }

        private void OnOrchestratorStateChanged(object? sender, ApplicationOrchestratorEventArgs e)
        {
            Console.WriteLine($"\n📊 System state: {e.State}");
            Console.Write("AI Assistant> ");
        }

        private void OnErrorOccurred(object? sender, ErrorEventArgs e)
        {
            Console.WriteLine($"\n❌ Error in {e.Component}: {e.Exception.Message}");
            Console.Write("AI Assistant> ");
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _statusUpdateTimer?.Dispose();
            _disposed = true;
        }
    }
}