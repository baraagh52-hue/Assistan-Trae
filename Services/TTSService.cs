using Microsoft.Extensions.Logging;
using NAudio.Wave;
using PersonalAiAssistant.Services;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace PersonalAiAssistant.Services
{
    public class TTSService : ITTSService, IDisposable
    {
        private readonly ILogger<TTSService> _logger;
        private readonly IConfigurationManager _configManager;
        private readonly HttpClient _httpClient;
        
        private bool _isInitialized = false;
        private bool _isSpeaking = false;
        private bool _disposed = false;
        private TTSStatus _status = TTSStatus.NotInitialized;
        
        private WaveOutEvent? _waveOut;
        private readonly object _lockObject = new object();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        
        // Kokoro server settings
        private string _kokoroServerUrl = "http://localhost:8020";
        private readonly List<string> _availableVoices = new();
        private string _currentVoice = "default";
        
        // Audio playback queue
        private readonly Queue<byte[]> _audioQueue = new();
        private bool _isProcessingQueue = false;

        public event EventHandler<TTSStatusChangedEventArgs>? StatusChanged;
        public event EventHandler<SpeechStartedEventArgs>? SpeechStarted;
        public event EventHandler<SpeechCompletedEventArgs>? SpeechCompleted;

        public bool IsInitialized => _isInitialized;
        public bool IsSpeaking => _isSpeaking;
        public TTSStatus Status => _status;

        public TTSService(ILogger<TTSService> logger, IConfigurationManager configManager)
        {
            _logger = logger;
            _configManager = configManager;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                if (_isInitialized)
                {
                    _logger.LogInformation("TTS service already initialized");
                    return true;
                }

                SetStatus(TTSStatus.Initializing);
                
                _kokoroServerUrl = _configManager.KokoroServerUrl;
                _logger.LogInformation("Initializing TTS service with Kokoro server: {ServerUrl}", _kokoroServerUrl);

                // Test connection to Kokoro server
                if (!await TestConnectionAsync())
                {
                    SetStatus(TTSStatus.ServerNotAvailable);
                    _logger.LogError("Failed to connect to Kokoro server");
                    return false;
                }

                // Get available voices
                await LoadAvailableVoicesAsync();

                // Initialize audio output
                if (!InitializeAudioOutput())
                {
                    SetStatus(TTSStatus.Error);
                    _logger.LogError("Failed to initialize audio output");
                    return false;
                }

                _isInitialized = true;
                SetStatus(TTSStatus.Ready);
                _logger.LogInformation("TTS service initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                SetStatus(TTSStatus.Error);
                _logger.LogError(ex, "Error initializing TTS service");
                return false;
            }
        }

        public async Task<bool> SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_isInitialized)
                {
                    _logger.LogWarning("TTS service not initialized. Attempting to initialize...");
                    if (!await InitializeAsync())
                    {
                        return false;
                    }
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogWarning("Cannot speak empty or null text");
                    return false;
                }

                _logger.LogInformation("Speaking text: {Text}", text);
                
                // Generate audio from Kokoro server
                var audioData = await GenerateAudioAsync(text, cancellationToken);
                if (audioData == null || audioData.Length == 0)
                {
                    _logger.LogError("Failed to generate audio for text: {Text}", text);
                    return false;
                }

                // Play the audio
                await PlayAudioAsync(audioData, cancellationToken);
                
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Speech operation was cancelled");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error speaking text: {Text}", text);
                SetStatus(TTSStatus.Error);
                return false;
            }
        }

        public async Task StopSpeakingAsync()
        {
            try
            {
                if (!_isSpeaking)
                {
                    _logger.LogInformation("TTS service not currently speaking");
                    return;
                }

                lock (_lockObject)
                {
                    _waveOut?.Stop();
                    _audioQueue.Clear();
                    _isSpeaking = false;
                }

                SetStatus(TTSStatus.Ready);
                _logger.LogInformation("Stopped speaking");
                
                SpeechCompleted?.Invoke(this, new SpeechCompletedEventArgs
                {
                    CompletedAt = DateTime.UtcNow,
                    WasCancelled = true
                });
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping speech");
            }
        }

        public async Task<List<string>> GetAvailableVoicesAsync()
        {
            try
            {
                if (_availableVoices.Count == 0)
                {
                    await LoadAvailableVoicesAsync();
                }
                return new List<string>(_availableVoices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available voices");
                return new List<string>();
            }
        }

        public async Task<bool> SetVoiceAsync(string voiceName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(voiceName))
                {
                    _logger.LogWarning("Invalid voice name provided");
                    return false;
                }

                if (_availableVoices.Count == 0)
                {
                    await LoadAvailableVoicesAsync();
                }

                if (!_availableVoices.Contains(voiceName))
                {
                    _logger.LogWarning("Voice {VoiceName} not available. Available voices: {Voices}", 
                        voiceName, string.Join(", ", _availableVoices));
                    return false;
                }

                _currentVoice = voiceName;
                _logger.LogInformation("Voice set to: {VoiceName}", voiceName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting voice to {VoiceName}", voiceName);
                return false;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                _logger.LogInformation("Testing connection to Kokoro server: {ServerUrl}", _kokoroServerUrl);
                
                var healthCheckUrl = $"{_kokoroServerUrl.TrimEnd('/')}/health";
                var response = await _httpClient.GetAsync(healthCheckUrl, _cancellationTokenSource.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully connected to Kokoro server");
                    return true;
                }
                else
                {
                    _logger.LogWarning("Kokoro server responded with status: {StatusCode}", response.StatusCode);
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Failed to connect to Kokoro server at {ServerUrl}", _kokoroServerUrl);
                return false;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Connection to Kokoro server timed out");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection to Kokoro server");
                return false;
            }
        }

        private async Task LoadAvailableVoicesAsync()
        {
            try
            {
                _logger.LogInformation("Loading available voices from Kokoro server");
                
                var voicesUrl = $"{_kokoroServerUrl.TrimEnd('/')}/voices";
                var response = await _httpClient.GetAsync(voicesUrl, _cancellationTokenSource.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var voicesResponse = JsonSerializer.Deserialize<VoicesResponse>(jsonContent);
                    
                    _availableVoices.Clear();
                    if (voicesResponse?.Voices != null)
                    {
                        _availableVoices.AddRange(voicesResponse.Voices);
                    }
                    
                    if (_availableVoices.Count == 0)
                    {
                        _availableVoices.Add("default");
                    }
                    
                    _logger.LogInformation("Loaded {VoiceCount} voices: {Voices}", 
                        _availableVoices.Count, string.Join(", ", _availableVoices));
                }
                else
                {
                    _logger.LogWarning("Failed to load voices, using default voice");
                    _availableVoices.Clear();
                    _availableVoices.Add("default");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading available voices, using default");
                _availableVoices.Clear();
                _availableVoices.Add("default");
            }
        }

        private bool InitializeAudioOutput()
        {
            try
            {
                // Audio output will be initialized when needed
                _logger.LogInformation("Audio output ready for initialization");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing audio output");
                return false;
            }
        }

        private async Task<byte[]?> GenerateAudioAsync(string text, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Generating audio for text: {Text}", text);
                
                var ttsRequest = new TTSRequest
                {
                    Text = text,
                    Voice = _currentVoice,
                    Format = "wav",
                    SampleRate = 22050
                };
                
                var jsonContent = JsonSerializer.Serialize(ttsRequest);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                var ttsUrl = $"{_kokoroServerUrl.TrimEnd('/')}/tts";
                var response = await _httpClient.PostAsync(ttsUrl, httpContent, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var audioData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    _logger.LogDebug("Generated {AudioSize} bytes of audio data", audioData.Length);
                    return audioData;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Kokoro server returned error {StatusCode}: {Error}", 
                        response.StatusCode, errorContent);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating audio for text: {Text}", text);
                return null;
            }
        }

        private async Task PlayAudioAsync(byte[] audioData, CancellationToken cancellationToken)
        {
            try
            {
                SetStatus(TTSStatus.Speaking);
                _isSpeaking = true;
                
                SpeechStarted?.Invoke(this, new SpeechStartedEventArgs
                {
                    StartedAt = DateTime.UtcNow,
                    Text = "[Audio Content]",
                    Voice = _currentVoice
                });
                
                using var audioStream = new MemoryStream(audioData);
                using var waveFileReader = new WaveFileReader(audioStream);
                
                lock (_lockObject)
                {
                    _waveOut = new WaveOutEvent();
                    _waveOut.PlaybackStopped += OnPlaybackStopped;
                    _waveOut.Init(waveFileReader);
                    _waveOut.Play();
                }
                
                // Wait for playback to complete or cancellation
                while (_waveOut.PlaybackState == PlaybackState.Playing && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(100, cancellationToken);
                }
                
                if (cancellationToken.IsCancellationRequested)
                {
                    lock (_lockObject)
                    {
                        _waveOut?.Stop();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Audio playback was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error playing audio");
                SetStatus(TTSStatus.Error);
                throw;
            }
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            try
            {
                lock (_lockObject)
                {
                    _waveOut?.Dispose();
                    _waveOut = null;
                    _isSpeaking = false;
                }
                
                SetStatus(TTSStatus.Ready);
                
                if (e.Exception != null)
                {
                    _logger.LogError(e.Exception, "Audio playback stopped due to error");
                    SetStatus(TTSStatus.Error);
                }
                else
                {
                    _logger.LogDebug("Audio playback completed successfully");
                }
                
                SpeechCompleted?.Invoke(this, new SpeechCompletedEventArgs
                {
                    CompletedAt = DateTime.UtcNow,
                    WasCancelled = e.Exception != null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling playback stopped event");
            }
        }

        private void SetStatus(TTSStatus newStatus)
        {
            if (_status != newStatus)
            {
                var oldStatus = _status;
                _status = newStatus;
                
                _logger.LogDebug("TTS status changed from {OldStatus} to {NewStatus}", oldStatus, newStatus);
                
                StatusChanged?.Invoke(this, new TTSStatusChangedEventArgs
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
                _logger.LogInformation("Disposing TTS service");

                // Stop any ongoing speech
                StopSpeakingAsync().Wait(1000);

                // Cancel any ongoing operations
                _cancellationTokenSource.Cancel();

                // Dispose audio resources
                lock (_lockObject)
                {
                    _waveOut?.Dispose();
                    _waveOut = null;
                }

                // Dispose HTTP client
                _httpClient.Dispose();
                _cancellationTokenSource.Dispose();
                
                _disposed = true;
                _logger.LogInformation("TTS service disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing TTS service");
            }
        }
    }

    // Data models for Kokoro API
    internal class TTSRequest
    {
        public string Text { get; set; } = string.Empty;
        public string Voice { get; set; } = "default";
        public string Format { get; set; } = "wav";
        public int SampleRate { get; set; } = 22050;
    }

    internal class VoicesResponse
    {
        public List<string>? Voices { get; set; }
    }
}