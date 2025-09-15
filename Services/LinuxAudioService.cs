using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace PersonalAiAssistant.Services
{
    public class LinuxAudioService : IDisposable
    {
        private readonly ILogger<LinuxAudioService> _logger;
        private Process? _arecordProcess;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly object _lockObject = new object();
        private bool _isRecording = false;

        public event EventHandler<byte[]>? AudioDataReceived;

        public LinuxAudioService(ILogger<LinuxAudioService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> StartRecordingAsync(int sampleRate = 16000, int channels = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_isRecording)
                {
                    _logger.LogWarning("Already recording audio");
                    return true;
                }

                _cancellationTokenSource = new CancellationTokenSource();
                var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token).Token;

                _arecordProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "arecord",
                        Arguments = $"-f S16_LE -r {sampleRate} -c {channels} -t raw",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                _arecordProcess.Start();
                _isRecording = true;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var buffer = new byte[4096];
                        var memoryStream = new MemoryStream();
                        
                        while (!combinedToken.IsCancellationRequested)
                        {
                            var bytesRead = await _arecordProcess.StandardOutput.BaseStream.ReadAsync(buffer, 0, buffer.Length, combinedToken);
                            if (bytesRead > 0)
                            {
                                var audioData = new byte[bytesRead];
                                Array.Copy(buffer, audioData, bytesRead);
                                AudioDataReceived?.Invoke(this, audioData);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancellation is requested
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error reading audio data");
                    }
                }, combinedToken);

                _logger.LogInformation("Started audio recording on Linux");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start audio recording");
                return false;
            }
        }

        public async Task StopRecordingAsync()
        {
            try
            {
                if (!_isRecording)
                {
                    return;
                }

                _cancellationTokenSource?.Cancel();
                
                if (_arecordProcess != null)
                {
                    try
                    {
                        if (!_arecordProcess.HasExited)
                        {
                            _arecordProcess.Kill();
                            await _arecordProcess.WaitForExitAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error stopping arecord process");
                    }
                }

                _arecordProcess?.Dispose();
                _arecordProcess = null;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _isRecording = false;

                _logger.LogInformation("Stopped audio recording");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping audio recording");
            }
        }

        public void Dispose()
        {
            StopRecordingAsync().GetAwaiter().GetResult();
        }
    }

    public class LinuxSTTService : ISTTService, IDisposable
    {
        private readonly ILogger<LinuxSTTService> _logger;
        private readonly IConfigurationManager _configManager;
        private readonly LinuxAudioService _audioService;
        private bool _isListening = false;
        private bool _isInitialized = false;
        private CancellationTokenSource? _cancellationTokenSource;

        public event EventHandler<CommandTranscribedEventArgs>? CommandTranscribed;
        public event EventHandler<STTStatusChangedEventArgs>? StatusChanged;

        public bool IsInitialized => _isInitialized;
        public bool IsListening => _isListening;
        public STTStatus Status { get; private set; } = STTStatus.NotInitialized;

        public LinuxSTTService(ILogger<LinuxSTTService> logger, IConfigurationManager configManager)
        {
            _logger = logger;
            _configManager = configManager;
            _audioService = new LinuxAudioService(logger);
        }

        public async Task<bool> InitializeAsync(string? modelPath = null)
        {
            try
            {
                if (_isInitialized)
                    return true;

                SetStatus(STTStatus.Initializing);
                
                var voskModelPath = modelPath ?? _configManager.VoskModelPath;
                _logger.LogInformation("Initializing Linux STT service with model: {ModelPath}", voskModelPath);

                // Check if Vosk model exists
                if (!Directory.Exists(voskModelPath))
                {
                    _logger.LogError("Vosk model directory not found: {ModelPath}", voskModelPath);
                    SetStatus(STTStatus.Error);
                    return false;
                }

                _audioService.AudioDataReceived += OnAudioDataReceived;
                _isInitialized = true;
                SetStatus(STTStatus.Ready);
                
                _logger.LogInformation("Linux STT service initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Linux STT service");
                SetStatus(STTStatus.Error);
                return false;
            }
        }

        public async Task<bool> StartListeningAsync()
        {
            try
            {
                if (!_isInitialized)
                {
                    if (!await InitializeAsync())
                        return false;
                }

                if (_isListening)
                    return true;

                _cancellationTokenSource = new CancellationTokenSource();
                _isListening = true;
                SetStatus(STTStatus.Listening);

                _logger.LogInformation("Started listening for speech on Linux");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting Linux STT listening");
                SetStatus(STTStatus.Error);
                return false;
            }
        }

        public async Task StopListeningAsync()
        {
            try
            {
                if (!_isListening)
                    return;

                _cancellationTokenSource?.Cancel();
                _isListening = false;
                SetStatus(STTStatus.Ready);
                
                _logger.LogInformation("Stopped listening for speech on Linux");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Linux STT listening");
                SetStatus(STTStatus.Error);
            }
        }

        private void OnAudioDataReceived(object? sender, byte[] audioData)
        {
            // Placeholder: Implement Vosk processing
            // In real implementation, this would process the audio with Vosk
            _logger.LogDebug("Received audio data: {Bytes} bytes", audioData.Length);
            
            // For now, simulate transcription
            var simulatedText = "Simulated transcription on Linux";
            CommandTranscribed?.Invoke(this, new CommandTranscribedEventArgs(simulatedText, DateTime.Now));
        }

        private void SetStatus(STTStatus status)
        {
            Status = status;
            StatusChanged?.Invoke(this, new STTStatusChangedEventArgs(status));
        }

        public void Dispose()
        {
            _audioService?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }

    public class LinuxWakeWordService : IWakeWordService, IDisposable
    {
        private readonly ILogger<LinuxWakeWordService> _logger;
        private readonly IConfigurationManager _configManager;
        private readonly LinuxAudioService _audioService;
        private bool _isListening = false;
        private bool _isInitialized = false;

        public event EventHandler<WakeWordDetectedEventArgs>? WakeWordDetected;

        public bool IsListening => _isListening;
        public bool IsInitialized => _isInitialized;

        public LinuxWakeWordService(ILogger<LinuxWakeWordService> logger, IConfigurationManager configManager)
        {
            _logger = logger;
            _configManager = configManager;
            _audioService = new LinuxAudioService(logger);
        }

        public async Task<bool> InitializeAsync(string? wakeWord = null)
        {
            try
            {
                if (_isInitialized)
                    return true;

                var currentWakeWord = wakeWord ?? _configManager.WakeWord;
                _logger.LogInformation("Initializing Linux wake word service with wake word: {WakeWord}", currentWakeWord);

                _audioService.AudioDataReceived += OnAudioDataReceived;
                _isInitialized = true;
                
                _logger.LogInformation("Linux wake word service initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Linux wake word service");
                return false;
            }
        }

        public async Task<bool> StartListeningAsync()
        {
            try
            {
                if (!_isInitialized)
                {
                    if (!await InitializeAsync())
                        return false;
                }

                if (_isListening)
                    return true;

                _isListening = true;
                _logger.LogInformation("Started listening for wake word on Linux");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting Linux wake word listening");
                return false;
            }
        }

        public async Task StopListeningAsync()
        {
            try
            {
                if (!_isListening)
                    return;

                _isListening = false;
                _logger.LogInformation("Stopped listening for wake word on Linux");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Linux wake word listening");
            }
        }

        public async Task<bool> SetWakeWordAsync(string wakeWord)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(wakeWord))
                    return false;

                _configManager.WakeWord = wakeWord;
                await _configManager.SaveSettingsAsync();
                
                _logger.LogInformation("Wake word updated to: {WakeWord}", wakeWord);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting wake word");
                return false;
            }
        }

        private void OnAudioDataReceived(object? sender, byte[] audioData)
        {
            // Placeholder: Implement wake word detection
            // For now, simulate wake word detection
            if (new Random().Next(1000) < 1) // 0.1% chance for testing
            {
                WakeWordDetected?.Invoke(this, new WakeWordDetectedEventArgs("Assistant"));
            }
        }

        public void Dispose()
        {
            _audioService?.Dispose();
        }
    }
}