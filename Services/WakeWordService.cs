using Microsoft.Extensions.Logging;
using NAudio.Wave;
using PersonalAiAssistant.Services;
using PersonalAiAssistant.Interfaces;
using System.Collections.Concurrent;

namespace PersonalAiAssistant.Services
{
    public class WakeWordService : IWakeWordService, IDisposable
    {
        private readonly ILogger<WakeWordService> _logger;
        private readonly IConfigurationManager _configManager;
        private readonly IErrorHandlingService _errorHandler;
        private readonly ILoggingService _loggingService;
        
        private WaveInEvent? _waveIn;
        private bool _isListening = false;
        private bool _isInitialized = false;
        private bool _disposed = false;
        private string _currentWakeWord = "Assistant";
        
        // Audio processing
        private readonly ConcurrentQueue<float[]> _audioQueue = new();
        private readonly CancellationTokenSource _processingCancellation = new();
        private Task? _processingTask;
        
        // Porcupine integration (placeholder for actual implementation)
        private object? _porcupineEngine;
        private readonly object _lockObject = new object();
        
        // Audio buffer settings
        private const int SAMPLE_RATE = 16000;
        private const int FRAME_LENGTH = 512; // Porcupine frame length
        private readonly List<float> _audioBuffer = new();

        public event EventHandler<WakeWordDetectedEventArgs>? WakeWordDetected;

        public bool IsListening => _isListening;
        public bool IsInitialized => _isInitialized;

        public WakeWordService(ILogger<WakeWordService> logger, IConfigurationManager configManager,
            IErrorHandlingService errorHandler, ILoggingService loggingService)
        {
            _logger = logger;
            _configManager = configManager;
            _errorHandler = errorHandler;
            _loggingService = loggingService;
        }

        public async Task<bool> InitializeAsync(string? wakeWord = null)
        {
            return await _errorHandler.ExecuteWithErrorHandlingAsync(async () =>
            {
                if (_isInitialized)
                {
                    _loggingService.LogInfo("Wake word service already initialized", "WakeWordService");
                    return true;
                }

                _currentWakeWord = wakeWord ?? _configManager.WakeWord;
                _loggingService.LogInfo($"Initializing wake word service with wake word: {_currentWakeWord}", "WakeWordService");

                // Initialize Porcupine engine
                if (!await InitializePorcupineAsync())
                {
                    _loggingService.LogError("Failed to initialize Porcupine engine", "WakeWordService");
                    return false;
                }

                // Initialize audio input
                if (!InitializeAudioInput())
                {
                    _loggingService.LogError("Failed to initialize audio input", "WakeWordService");
                    return false;
                }

                // Start audio processing task
                _processingTask = Task.Run(ProcessAudioAsync, _processingCancellation.Token);

                _isInitialized = true;
                _loggingService.LogInfo("Wake word service initialized successfully", "WakeWordService");
                return true;
            }, "InitializeAsync", "WakeWordService", false);
        }

        public async Task<bool> StartListeningAsync()
        {
            return await _errorHandler.ExecuteWithErrorHandlingAsync(async () =>
            {
                if (!_isInitialized)
                {
                    _loggingService.LogWarning("Wake word service not initialized. Attempting to initialize...", "WakeWordService");
                    if (!await InitializeAsync())
                    {
                        return false;
                    }
                }

                if (_isListening)
                {
                    _loggingService.LogInfo("Wake word service already listening", "WakeWordService");
                    return true;
                }

                lock (_lockObject)
                {
                    _waveIn?.StartRecording();
                    _isListening = true;
                }

                _loggingService.LogInfo($"Started listening for wake word: {_currentWakeWord}", "WakeWordService");
                return true;
            }, "StartListeningAsync", "WakeWordService", false);
        }

        public async Task StopListeningAsync()
        {
            await _errorHandler.ExecuteWithErrorHandlingAsync(async () =>
            {
                if (!_isListening)
                {
                    _loggingService.LogInfo("Wake word service not currently listening", "WakeWordService");
                    return;
                }

                lock (_lockObject)
                {
                    _waveIn?.StopRecording();
                    _isListening = false;
                }

                _loggingService.LogInfo("Stopped listening for wake word", "WakeWordService");
                await Task.CompletedTask;
            }, "StopListeningAsync", "WakeWordService");
        }

        public async Task<bool> SetWakeWordAsync(string wakeWord)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(wakeWord))
                {
                    _logger.LogWarning("Invalid wake word provided");
                    return false;
                }

                var wasListening = _isListening;
                
                // Stop listening if currently active
                if (wasListening)
                {
                    await StopListeningAsync();
                }

                _currentWakeWord = wakeWord;
                _configManager.WakeWord = wakeWord;
                await _configManager.SaveSettingsAsync();

                // Reinitialize with new wake word
                _isInitialized = false;
                if (!await InitializeAsync())
                {
                    _logger.LogError("Failed to reinitialize with new wake word");
                    return false;
                }

                // Resume listening if it was active before
                if (wasListening)
                {
                    await StartListeningAsync();
                }

                _logger.LogInformation("Wake word updated to: {WakeWord}", wakeWord);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting wake word to {WakeWord}", wakeWord);
                return false;
            }
        }

        private async Task<bool> InitializePorcupineAsync()
        {
            try
            {
                // NOTE: This is a placeholder implementation
                // In the actual implementation, you would:
                // 1. Install Porcupine .NET package
                // 2. Initialize Porcupine with access key and keyword file
                // 3. Handle the actual wake word detection
                
                _logger.LogInformation("Initializing Porcupine engine (placeholder implementation)");
                
                // Placeholder: In real implementation, this would be:
                // _porcupineEngine = new Porcupine(accessKey, keywordPath);
                
                await Task.Delay(100); // Simulate initialization time
                _porcupineEngine = new object(); // Placeholder
                
                _logger.LogInformation("Porcupine engine initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Porcupine engine");
                return false;
            }
        }

        private bool InitializeAudioInput()
        {
            try
            {
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(SAMPLE_RATE, 16, 1), // 16kHz, 16-bit, mono
                    BufferMilliseconds = 50 // Small buffer for low latency
                };

                _waveIn.DataAvailable += OnAudioDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;

                _logger.LogInformation("Audio input initialized: {SampleRate}Hz, {Channels} channel(s)", 
                    SAMPLE_RATE, _waveIn.WaveFormat.Channels);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing audio input");
                return false;
            }
        }

        private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
        {
            try
            {
                // Convert byte array to float array
                var floatData = new float[e.BytesRecorded / 2]; // 16-bit = 2 bytes per sample
                for (int i = 0; i < floatData.Length; i++)
                {
                    short sample = BitConverter.ToInt16(e.Buffer, i * 2);
                    floatData[i] = sample / 32768f; // Normalize to [-1, 1]
                }

                // Add to processing queue
                _audioQueue.Enqueue(floatData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing audio data");
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                _logger.LogError(e.Exception, "Audio recording stopped due to error");
            }
            else
            {
                _logger.LogInformation("Audio recording stopped");
            }
        }

        private async Task ProcessAudioAsync()
        {
            _logger.LogInformation("Started audio processing task");
            
            try
            {
                while (!_processingCancellation.Token.IsCancellationRequested)
                {
                    if (_audioQueue.TryDequeue(out var audioData))
                    {
                        await ProcessAudioFrameAsync(audioData);
                    }
                    else
                    {
                        // No audio data available, wait a bit
                        await Task.Delay(10, _processingCancellation.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Audio processing task cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in audio processing task");
            }
        }

        private async Task ProcessAudioFrameAsync(float[] audioData)
        {
            try
            {
                // Add audio data to buffer
                _audioBuffer.AddRange(audioData);

                // Process complete frames
                while (_audioBuffer.Count >= FRAME_LENGTH)
                {
                    var frame = _audioBuffer.Take(FRAME_LENGTH).ToArray();
                    _audioBuffer.RemoveRange(0, FRAME_LENGTH);

                    // Process frame with Porcupine
                    if (await ProcessPorcupineFrameAsync(frame))
                    {
                        // Wake word detected!
                        OnWakeWordDetected();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing audio frame");
            }
        }

        private async Task<bool> ProcessPorcupineFrameAsync(float[] frame)
        {
            try
            {
                // NOTE: This is a placeholder implementation
                // In the actual implementation, you would:
                // return _porcupineEngine.Process(frame) >= 0;
                
                // Placeholder: Simple energy-based detection for demo
                var energy = frame.Sum(x => x * x) / frame.Length;
                
                // Very basic placeholder logic - in reality, Porcupine does sophisticated keyword detection
                if (energy > 0.01) // Arbitrary threshold
                {
                    // Simulate random wake word detection for demo (very low probability)
                    var random = new Random();
                    if (random.NextDouble() < 0.001) // 0.1% chance per frame when there's audio
                    {
                        _logger.LogInformation("Wake word detected (placeholder implementation)");
                        return true;
                    }
                }
                
                await Task.CompletedTask;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Porcupine frame");
                return false;
            }
        }

        private void OnWakeWordDetected()
        {
            try
            {
                var eventArgs = new WakeWordDetectedEventArgs
                {
                    WakeWord = _currentWakeWord,
                    DetectedAt = DateTime.UtcNow,
                    Confidence = 0.95f // Placeholder confidence
                };

                _logger.LogInformation("Wake word '{WakeWord}' detected at {Time} with confidence {Confidence}", 
                    eventArgs.WakeWord, eventArgs.DetectedAt, eventArgs.Confidence);

                WakeWordDetected?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling wake word detection");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                _logger.LogInformation("Disposing wake word service");

                // Stop listening
                StopListeningAsync().Wait(1000);

                // Cancel processing task
                _processingCancellation.Cancel();
                _processingTask?.Wait(1000);

                // Dispose audio resources
                lock (_lockObject)
                {
                    _waveIn?.Dispose();
                    _waveIn = null;
                }

                // Dispose Porcupine engine
                if (_porcupineEngine is IDisposable disposablePorcupine)
                {
                    disposablePorcupine.Dispose();
                }
                _porcupineEngine = null;

                _processingCancellation.Dispose();
                _disposed = true;
                
                _logger.LogInformation("Wake word service disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing wake word service");
            }
        }
    }
}