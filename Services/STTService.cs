using Microsoft.Extensions.Logging;
using NAudio.Wave;
using PersonalAiAssistant.Services;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace PersonalAiAssistant.Services
{
    public class STTService : ISTTService, IDisposable
    {
        private readonly ILogger<STTService> _logger;
        private readonly IConfigurationManager _configManager;
        
        private WaveInEvent? _waveIn;
        private bool _isListening = false;
        private bool _isInitialized = false;
        private bool _disposed = false;
        private STTStatus _status = STTStatus.NotInitialized;
        
        // Vosk integration (placeholder for actual implementation)
        private object? _voskRecognizer;
        private object? _voskModel;
        private readonly object _lockObject = new object();
        
        // Audio processing
        private readonly ConcurrentQueue<byte[]> _audioQueue = new();
        private readonly CancellationTokenSource _processingCancellation = new();
        private Task? _processingTask;
        
        // Voice Activity Detection
        private readonly List<float> _energyHistory = new();
        private const int ENERGY_HISTORY_SIZE = 20;
        private const double VOICE_THRESHOLD = 0.01;
        private const int SILENCE_FRAMES_THRESHOLD = 30; // ~1.5 seconds at 50ms frames
        private int _silenceFrameCount = 0;
        private bool _voiceDetected = false;
        private readonly List<byte> _recordingBuffer = new();
        
        // Audio settings
        private const int SAMPLE_RATE = 16000;
        private const int CHANNELS = 1;
        private const int BITS_PER_SAMPLE = 16;
        
        public event EventHandler<CommandTranscribedEventArgs>? CommandTranscribed;
        public event EventHandler<STTStatusChangedEventArgs>? StatusChanged;

        public bool IsInitialized => _isInitialized;
        public bool IsListening => _isListening;
        public STTStatus Status => _status;

        public STTService(ILogger<STTService> logger, IConfigurationManager configManager)
        {
            _logger = logger;
            _configManager = configManager;
        }

        public async Task<bool> InitializeAsync(string? modelPath = null)
        {
            try
            {
                if (_isInitialized)
                {
                    _logger.LogInformation("STT service already initialized");
                    return true;
                }

                SetStatus(STTStatus.Initializing);
                
                var voskModelPath = modelPath ?? _configManager.VoskModelPath;
                _logger.LogInformation("Initializing STT service with model: {ModelPath}", voskModelPath);

                // Initialize Vosk model
                if (!await InitializeVoskModelAsync(voskModelPath))
                {
                    SetStatus(STTStatus.Error);
                    _logger.LogError("Failed to initialize Vosk model");
                    return false;
                }

                // Initialize audio input
                if (!InitializeAudioInput())
                {
                    SetStatus(STTStatus.Error);
                    _logger.LogError("Failed to initialize audio input");
                    return false;
                }

                // Start audio processing task
                _processingTask = Task.Run(ProcessAudioAsync, _processingCancellation.Token);

                _isInitialized = true;
                SetStatus(STTStatus.Ready);
                _logger.LogInformation("STT service initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                SetStatus(STTStatus.Error);
                _logger.LogError(ex, "Error initializing STT service");
                return false;
            }
        }

        public async Task<bool> StartListeningAsync()
        {
            try
            {
                if (!_isInitialized)
                {
                    _logger.LogWarning("STT service not initialized. Attempting to initialize...");
                    if (!await InitializeAsync())
                    {
                        return false;
                    }
                }

                if (_isListening)
                {
                    _logger.LogInformation("STT service already listening");
                    return true;
                }

                lock (_lockObject)
                {
                    // Reset voice detection state
                    _voiceDetected = false;
                    _silenceFrameCount = 0;
                    _energyHistory.Clear();
                    _recordingBuffer.Clear();
                    
                    _waveIn?.StartRecording();
                    _isListening = true;
                }

                SetStatus(STTStatus.Listening);
                _logger.LogInformation("Started listening for speech");
                return true;
            }
            catch (Exception ex)
            {
                SetStatus(STTStatus.Error);
                _logger.LogError(ex, "Error starting STT listening");
                return false;
            }
        }

        public async Task StopListeningAsync()
        {
            try
            {
                if (!_isListening)
                {
                    _logger.LogInformation("STT service not currently listening");
                    return;
                }

                lock (_lockObject)
                {
                    _waveIn?.StopRecording();
                    _isListening = false;
                }

                // Process any remaining audio in the buffer
                if (_recordingBuffer.Count > 0)
                {
                    await ProcessFinalAudioAsync();
                }

                SetStatus(STTStatus.Ready);
                _logger.LogInformation("Stopped listening for speech");
            }
            catch (Exception ex)
            {
                SetStatus(STTStatus.Error);
                _logger.LogError(ex, "Error stopping STT listening");
            }
        }

        private async Task<bool> InitializeVoskModelAsync(string modelPath)
        {
            try
            {
                // NOTE: This is a placeholder implementation
                // In the actual implementation, you would:
                // 1. Install Vosk .NET package
                // 2. Load the Vosk model from the specified path
                // 3. Create a recognizer with the model
                
                _logger.LogInformation("Loading Vosk model from: {ModelPath}", modelPath);
                
                if (!Directory.Exists(modelPath))
                {
                    _logger.LogError("Vosk model directory not found: {ModelPath}", modelPath);
                    return false;
                }
                
                // Placeholder: In real implementation, this would be:
                // Vosk.Vosk.SetLogLevel(0);
                // _voskModel = new Vosk.Model(modelPath);
                // _voskRecognizer = new Vosk.VoskRecognizer(_voskModel, SAMPLE_RATE);
                
                await Task.Delay(500); // Simulate model loading time
                _voskModel = new object(); // Placeholder
                _voskRecognizer = new object(); // Placeholder
                
                _logger.LogInformation("Vosk model loaded successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Vosk model from {ModelPath}", modelPath);
                return false;
            }
        }

        private bool InitializeAudioInput()
        {
            try
            {
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(SAMPLE_RATE, BITS_PER_SAMPLE, CHANNELS),
                    BufferMilliseconds = 50 // 50ms buffer for responsive voice detection
                };

                _waveIn.DataAvailable += OnAudioDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;

                _logger.LogInformation("Audio input initialized: {SampleRate}Hz, {Channels} channel(s), {BitsPerSample}-bit", 
                    SAMPLE_RATE, CHANNELS, BITS_PER_SAMPLE);
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
                if (!_isListening) return;

                // Copy audio data
                var audioData = new byte[e.BytesRecorded];
                Array.Copy(e.Buffer, audioData, e.BytesRecorded);
                
                // Add to processing queue
                _audioQueue.Enqueue(audioData);
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
                SetStatus(STTStatus.Error);
            }
            else
            {
                _logger.LogInformation("Audio recording stopped");
            }
        }

        private async Task ProcessAudioAsync()
        {
            _logger.LogInformation("Started STT audio processing task");
            
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
                _logger.LogInformation("STT audio processing task cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in STT audio processing task");
                SetStatus(STTStatus.Error);
            }
        }

        private async Task ProcessAudioFrameAsync(byte[] audioData)
        {
            try
            {
                // Calculate audio energy for voice activity detection
                var energy = CalculateAudioEnergy(audioData);
                _energyHistory.Add((float)energy);
                
                // Keep energy history size manageable
                if (_energyHistory.Count > ENERGY_HISTORY_SIZE)
                {
                    _energyHistory.RemoveAt(0);
                }

                // Voice Activity Detection
                var isVoiceFrame = energy > VOICE_THRESHOLD;
                
                if (isVoiceFrame)
                {
                    if (!_voiceDetected)
                    {
                        _voiceDetected = true;
                        SetStatus(STTStatus.Recording);
                        _logger.LogDebug("Voice activity detected, starting recording");
                    }
                    _silenceFrameCount = 0;
                }
                else if (_voiceDetected)
                {
                    _silenceFrameCount++;
                }

                // Add audio to recording buffer if voice is detected
                if (_voiceDetected)
                {
                    _recordingBuffer.AddRange(audioData);
                }

                // Check if we should stop recording due to silence
                if (_voiceDetected && _silenceFrameCount >= SILENCE_FRAMES_THRESHOLD)
                {
                    _logger.LogDebug("Silence detected, processing recorded audio");
                    await ProcessRecordedAudioAsync();
                    
                    // Reset for next recording
                    _voiceDetected = false;
                    _silenceFrameCount = 0;
                    _recordingBuffer.Clear();
                    SetStatus(STTStatus.Listening);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing audio frame");
            }
        }

        private double CalculateAudioEnergy(byte[] audioData)
        {
            double energy = 0;
            
            // Convert bytes to 16-bit samples and calculate RMS energy
            for (int i = 0; i < audioData.Length - 1; i += 2)
            {
                short sample = BitConverter.ToInt16(audioData, i);
                energy += sample * sample;
            }
            
            return Math.Sqrt(energy / (audioData.Length / 2)) / 32768.0; // Normalize
        }

        private async Task ProcessRecordedAudioAsync()
        {
            try
            {
                if (_recordingBuffer.Count == 0)
                {
                    _logger.LogDebug("No audio data to process");
                    return;
                }

                SetStatus(STTStatus.Processing);
                _logger.LogInformation("Processing {AudioSize} bytes of recorded audio", _recordingBuffer.Count);

                var audioBytes = _recordingBuffer.ToArray();
                var transcription = await TranscribeAudioAsync(audioBytes);
                
                if (!string.IsNullOrWhiteSpace(transcription))
                {
                    _logger.LogInformation("Transcription result: {Transcription}", transcription);
                    
                    var eventArgs = new CommandTranscribedEventArgs
                    {
                        TranscribedText = transcription,
                        Confidence = 0.85f, // Placeholder confidence
                        TranscribedAt = DateTime.UtcNow,
                        AudioDurationMs = (int)(_recordingBuffer.Count / (double)(SAMPLE_RATE * CHANNELS * (BITS_PER_SAMPLE / 8)) * 1000)
                    };
                    
                    CommandTranscribed?.Invoke(this, eventArgs);
                }
                else
                {
                    _logger.LogDebug("No transcription result or empty result");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing recorded audio");
                SetStatus(STTStatus.Error);
            }
        }

        private async Task ProcessFinalAudioAsync()
        {
            try
            {
                if (_recordingBuffer.Count > 0)
                {
                    _logger.LogInformation("Processing final audio buffer");
                    await ProcessRecordedAudioAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing final audio");
            }
        }

        private async Task<string> TranscribeAudioAsync(byte[] audioData)
        {
            try
            {
                // NOTE: This is a placeholder implementation
                // In the actual implementation, you would:
                // 1. Feed the audio data to the Vosk recognizer
                // 2. Get the recognition result
                // 3. Parse the JSON result to extract the text
                
                // Placeholder: In real implementation, this would be:
                // var result = _voskRecognizer.AcceptWaveform(audioData);
                // var finalResult = _voskRecognizer.FinalResult();
                // var jsonResult = JsonSerializer.Deserialize<VoskResult>(finalResult);
                // return jsonResult.Text;
                
                await Task.Delay(200); // Simulate processing time
                
                // Placeholder transcription for demo
                var placeholderPhrases = new[]
                {
                    "Hello assistant",
                    "What's the weather like",
                    "Add task to my todo list",
                    "When is my next prayer time",
                    "Show me my recent activity",
                    "Help me with something"
                };
                
                var random = new Random();
                if (random.NextDouble() < 0.7) // 70% chance of successful transcription
                {
                    return placeholderPhrases[random.Next(placeholderPhrases.Length)];
                }
                
                return string.Empty; // No transcription
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transcribing audio");
                return string.Empty;
            }
        }

        private void SetStatus(STTStatus newStatus)
        {
            if (_status != newStatus)
            {
                var oldStatus = _status;
                _status = newStatus;
                
                _logger.LogDebug("STT status changed from {OldStatus} to {NewStatus}", oldStatus, newStatus);
                
                StatusChanged?.Invoke(this, new STTStatusChangedEventArgs
                {
                    OldStatus = oldStatus,
                    NewStatus = newStatus,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                _logger.LogInformation("Disposing STT service");

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

                // Dispose Vosk resources
                if (_voskRecognizer is IDisposable disposableRecognizer)
                {
                    disposableRecognizer.Dispose();
                }
                if (_voskModel is IDisposable disposableModel)
                {
                    disposableModel.Dispose();
                }
                _voskRecognizer = null;
                _voskModel = null;

                _processingCancellation.Dispose();
                _disposed = true;
                
                _logger.LogInformation("STT service disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing STT service");
            }
        }
    }

    // Placeholder for Vosk result JSON structure
    internal class VoskResult
    {
        public string Text { get; set; } = string.Empty;
        public float Confidence { get; set; }
    }
}