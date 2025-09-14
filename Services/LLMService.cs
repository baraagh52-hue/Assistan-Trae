using Microsoft.Extensions.Logging;
using PersonalAiAssistant.Models;
using PersonalAiAssistant.Services;
using System.Text.Json;
using System.Text;

namespace PersonalAiAssistant.Services
{
    public class LLMService : ILLMService, IDisposable
    {
        private readonly ILogger<LLMService> _logger;
        private readonly IConfigurationManager _configManager;
        private readonly HttpClient _httpClient;
        
        private bool _isInitialized = false;
        private bool _isConnected = false;
        private bool _disposed = false;
        
        private string _apiUrl = string.Empty;
        private string _apiKey = string.Empty;
        private string _model = "gpt-3.5-turbo";
        private string _provider = "openai"; // openai, anthropic, local, etc.
        
        private readonly List<ChatMessage> _conversationHistory = new();
        private readonly int _maxHistoryMessages = 50;
        private readonly object _lockObject = new object();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        
        // Request settings
        private readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(60);
        private readonly int _maxRetries = 3;
        private readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(2);
        
        // Context and system prompt
        private string _systemPrompt = "You are a helpful AI assistant integrated into a personal productivity application. You can help with tasks, answer questions, and provide information. Keep responses concise and helpful.";
        private readonly Dictionary<string, object> _contextData = new();

        public event EventHandler<LLMResponseEventArgs>? ResponseReceived;
        public event EventHandler<LLMErrorEventArgs>? ErrorOccurred;
        public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

        public bool IsInitialized => _isInitialized;
        public bool IsConnected => _isConnected;
        public string Provider => _provider;
        public string Model => _model;
        public int ConversationHistoryCount => _conversationHistory.Count;

        public LLMService(ILogger<LLMService> logger, IConfigurationManager configManager)
        {
            _logger = logger;
            _configManager = configManager;
            
            _httpClient = new HttpClient
            {
                Timeout = _requestTimeout
            };
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                if (_isInitialized)
                {
                    _logger.LogInformation("LLM service already initialized");
                    return true;
                }

                _logger.LogInformation("Initializing LLM service");
                
                // Load configuration
                var llmSettings = _configManager.LLMSettings;
                _provider = llmSettings.Provider;
                _apiUrl = llmSettings.ApiUrl;
                _apiKey = llmSettings.ApiKey;
                _model = llmSettings.Model;
                _systemPrompt = llmSettings.SystemPrompt;
                
                // Set default API URL based on provider if not specified
                if (string.IsNullOrEmpty(_apiUrl))
                {
                    _apiUrl = GetDefaultApiUrl(_provider);
                }
                
                // Configure HTTP client headers
                ConfigureHttpClient();
                
                // Test connection
                await TestConnectionAsync();
                
                // Initialize conversation with system prompt
                if (!string.IsNullOrEmpty(_systemPrompt))
                {
                    lock (_lockObject)
                    {
                        _conversationHistory.Clear();
                        _conversationHistory.Add(new ChatMessage
                        {
                            Role = "system",
                            Content = _systemPrompt,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
                
                _isInitialized = true;
                _logger.LogInformation("LLM service initialized successfully with provider: {Provider}, model: {Model}", _provider, _model);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing LLM service");
                return false;
            }
        }

        public async Task<string> SendMessageAsync(string message, Dictionary<string, object>? context = null)
        {
            try
            {
                if (!_isInitialized)
                {
                    throw new InvalidOperationException("LLM service not initialized");
                }

                if (string.IsNullOrWhiteSpace(message))
                {
                    throw new ArgumentException("Message cannot be empty", nameof(message));
                }

                _logger.LogDebug("Sending message to LLM: {Message}", message.Substring(0, Math.Min(message.Length, 100)));
                
                // Add user message to conversation history
                var userMessage = new ChatMessage
                {
                    Role = "user",
                    Content = message,
                    Timestamp = DateTime.UtcNow
                };
                
                lock (_lockObject)
                {
                    _conversationHistory.Add(userMessage);
                    TrimConversationHistory();
                }
                
                // Update context if provided
                if (context != null)
                {
                    foreach (var kvp in context)
                    {
                        _contextData[kvp.Key] = kvp.Value;
                    }
                }
                
                // Send request to LLM API
                var response = await SendLLMRequestAsync();
                
                if (!string.IsNullOrEmpty(response))
                {
                    // Add assistant response to conversation history
                    var assistantMessage = new ChatMessage
                    {
                        Role = "assistant",
                        Content = response,
                        Timestamp = DateTime.UtcNow
                    };
                    
                    lock (_lockObject)
                    {
                        _conversationHistory.Add(assistantMessage);
                        TrimConversationHistory();
                    }
                    
                    // Notify listeners
                    ResponseReceived?.Invoke(this, new LLMResponseEventArgs
                    {
                        UserMessage = message,
                        AssistantResponse = response,
                        Timestamp = DateTime.UtcNow,
                        Context = new Dictionary<string, object>(_contextData)
                    });
                    
                    _logger.LogDebug("Received LLM response: {Response}", response.Substring(0, Math.Min(response.Length, 100)));
                }
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to LLM");
                
                ErrorOccurred?.Invoke(this, new LLMErrorEventArgs
                {
                    Message = message,
                    Error = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
                
                throw;
            }
        }

        public async Task<string> ProcessCommandAsync(string command, Dictionary<string, object>? context = null)
        {
            try
            {
                _logger.LogDebug("Processing command: {Command}", command);
                
                // Enhance the command with context information
                var enhancedPrompt = BuildCommandPrompt(command, context);
                
                // Send to LLM for processing
                var response = await SendMessageAsync(enhancedPrompt, context);
                
                _logger.LogDebug("Command processed successfully");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing command: {Command}", command);
                throw;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                _logger.LogDebug("Testing connection to LLM provider: {Provider}", _provider);
                
                var wasConnected = _isConnected;
                
                // Send a simple test message
                var testResponse = await SendTestRequestAsync();
                _isConnected = !string.IsNullOrEmpty(testResponse);
                
                if (_isConnected)
                {
                    _logger.LogDebug("Successfully connected to LLM provider");
                }
                else
                {
                    _logger.LogWarning("Failed to connect to LLM provider");
                }
                
                // Notify if connection status changed
                if (wasConnected != _isConnected)
                {
                    ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs
                    {
                        IsConnected = _isConnected,
                        ServerUrl = _apiUrl,
                        Timestamp = DateTime.UtcNow
                    });
                }
                
                return _isConnected;
            }
            catch (Exception ex)
            {
                var wasConnected = _isConnected;
                _isConnected = false;
                
                _logger.LogError(ex, "Error testing connection to LLM provider");
                
                // Notify if connection status changed
                if (wasConnected != _isConnected)
                {
                    ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs
                    {
                        IsConnected = _isConnected,
                        ServerUrl = _apiUrl,
                        Timestamp = DateTime.UtcNow
                    });
                }
                
                return false;
            }
        }

        public async Task<bool> SetProviderAsync(string provider, string apiUrl, string apiKey, string model)
        {
            try
            {
                _logger.LogInformation("Setting LLM provider: {Provider}, model: {Model}", provider, model);
                
                _provider = provider;
                _apiUrl = apiUrl;
                _apiKey = apiKey;
                _model = model;
                
                // Update configuration
                var llmSettings = _configManager.LLMSettings;
                llmSettings.Provider = provider;
                llmSettings.ApiUrl = apiUrl;
                llmSettings.ApiKey = apiKey;
                llmSettings.Model = model;
                await _configManager.SaveSettingsAsync();
                
                // Reconfigure HTTP client
                ConfigureHttpClient();
                
                // Test new connection
                await TestConnectionAsync();
                
                _logger.LogInformation("LLM provider updated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting LLM provider");
                return false;
            }
        }

        public async Task<bool> SetSystemPromptAsync(string systemPrompt)
        {
            try
            {
                _logger.LogInformation("Setting system prompt");
                
                _systemPrompt = systemPrompt;
                
                // Update configuration
                var llmSettings = _configManager.LLMSettings;
                llmSettings.SystemPrompt = systemPrompt;
                await _configManager.SaveSettingsAsync();
                
                // Update conversation history
                lock (_lockObject)
                {
                    // Remove old system message if exists
                    var oldSystemMessage = _conversationHistory.FirstOrDefault(m => m.Role == "system");
                    if (oldSystemMessage != null)
                    {
                        _conversationHistory.Remove(oldSystemMessage);
                    }
                    
                    // Add new system message at the beginning
                    if (!string.IsNullOrEmpty(systemPrompt))
                    {
                        _conversationHistory.Insert(0, new ChatMessage
                        {
                            Role = "system",
                            Content = systemPrompt,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
                
                _logger.LogInformation("System prompt updated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting system prompt");
                return false;
            }
        }

        public void ClearConversationHistory()
        {
            try
            {
                _logger.LogInformation("Clearing conversation history");
                
                lock (_lockObject)
                {
                    var systemMessage = _conversationHistory.FirstOrDefault(m => m.Role == "system");
                    _conversationHistory.Clear();
                    
                    // Keep system message if it exists
                    if (systemMessage != null)
                    {
                        _conversationHistory.Add(systemMessage);
                    }
                }
                
                _logger.LogInformation("Conversation history cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing conversation history");
            }
        }

        public List<ChatMessage> GetConversationHistory()
        {
            lock (_lockObject)
            {
                return new List<ChatMessage>(_conversationHistory);
            }
        }

        public void AddContext(string key, object value)
        {
            try
            {
                _contextData[key] = value;
                _logger.LogDebug("Added context: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding context: {Key}", key);
            }
        }

        public void RemoveContext(string key)
        {
            try
            {
                _contextData.Remove(key);
                _logger.LogDebug("Removed context: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing context: {Key}", key);
            }
        }

        public Dictionary<string, object> GetContext()
        {
            return new Dictionary<string, object>(_contextData);
        }

        private async Task<string> SendLLMRequestAsync()
        {
            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    var requestBody = BuildRequestBody();
                    var jsonContent = JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                    
                    var response = await _httpClient.PostAsync(_apiUrl, content, _cancellationTokenSource.Token);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync(_cancellationTokenSource.Token);
                        return ExtractResponseContent(responseContent);
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync(_cancellationTokenSource.Token);
                        _logger.LogWarning("LLM API request failed. Status: {StatusCode}, Content: {Content}", 
                            response.StatusCode, errorContent);
                        
                        if (attempt < _maxRetries)
                        {
                            await Task.Delay(_retryDelay, _cancellationTokenSource.Token);
                            continue;
                        }
                        
                        throw new HttpRequestException($"LLM API request failed with status {response.StatusCode}: {errorContent}");
                    }
                }
                catch (Exception ex) when (attempt < _maxRetries)
                {
                    _logger.LogWarning(ex, "LLM request attempt {Attempt} failed, retrying...", attempt);
                    await Task.Delay(_retryDelay, _cancellationTokenSource.Token);
                }
            }
            
            throw new InvalidOperationException("All LLM request attempts failed");
        }

        private async Task<string> SendTestRequestAsync()
        {
            try
            {
                var testMessages = new List<object>
                {
                    new { role = "user", content = "Hello, this is a test message. Please respond with 'Test successful'." }
                };
                
                var requestBody = _provider.ToLowerInvariant() switch
                {
                    "openai" => new
                    {
                        model = _model,
                        messages = testMessages,
                        max_tokens = 50,
                        temperature = 0.1
                    },
                    "anthropic" => new
                    {
                        model = _model,
                        messages = testMessages,
                        max_tokens = 50,
                        temperature = 0.1
                    },
                    _ => new
                    {
                        model = _model,
                        messages = testMessages,
                        max_tokens = 50,
                        temperature = 0.1
                    }
                };
                
                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(_apiUrl, content, _cancellationTokenSource.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(_cancellationTokenSource.Token);
                    return ExtractResponseContent(responseContent);
                }
                
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private object BuildRequestBody()
        {
            var messages = new List<object>();
            
            lock (_lockObject)
            {
                foreach (var msg in _conversationHistory)
                {
                    messages.Add(new { role = msg.Role, content = msg.Content });
                }
            }
            
            return _provider.ToLowerInvariant() switch
            {
                "openai" => new
                {
                    model = _model,
                    messages = messages,
                    max_tokens = 1000,
                    temperature = 0.7,
                    top_p = 1.0,
                    frequency_penalty = 0.0,
                    presence_penalty = 0.0
                },
                "anthropic" => new
                {
                    model = _model,
                    messages = messages,
                    max_tokens = 1000,
                    temperature = 0.7
                },
                _ => new
                {
                    model = _model,
                    messages = messages,
                    max_tokens = 1000,
                    temperature = 0.7
                }
            };
        }

        private string ExtractResponseContent(string responseJson)
        {
            try
            {
                using var document = JsonDocument.Parse(responseJson);
                var root = document.RootElement;
                
                return _provider.ToLowerInvariant() switch
                {
                    "openai" => root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty,
                    "anthropic" => root.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty,
                    _ => root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting response content from: {Response}", responseJson);
                return string.Empty;
            }
        }

        private string BuildCommandPrompt(string command, Dictionary<string, object>? context)
        {
            var promptBuilder = new StringBuilder();
            
            promptBuilder.AppendLine("Please process the following command:");
            promptBuilder.AppendLine($"Command: {command}");
            
            if (context != null && context.Any())
            {
                promptBuilder.AppendLine("\nContext:");
                foreach (var kvp in context)
                {
                    promptBuilder.AppendLine($"- {kvp.Key}: {kvp.Value}");
                }
            }
            
            if (_contextData.Any())
            {
                promptBuilder.AppendLine("\nAdditional Context:");
                foreach (var kvp in _contextData)
                {
                    promptBuilder.AppendLine($"- {kvp.Key}: {kvp.Value}");
                }
            }
            
            promptBuilder.AppendLine("\nPlease provide a helpful response or execute the requested action.");
            
            return promptBuilder.ToString();
        }

        private void ConfigureHttpClient()
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                
                switch (_provider.ToLowerInvariant())
                {
                    case "openai":
                        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
                        break;
                    case "anthropic":
                        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
                        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                        break;
                    default:
                        if (!string.IsNullOrEmpty(_apiKey))
                        {
                            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
                        }
                        break;
                }
                
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "PersonalAiAssistant/1.0");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error configuring HTTP client");
            }
        }

        private string GetDefaultApiUrl(string provider)
        {
            return provider.ToLowerInvariant() switch
            {
                "openai" => "https://api.openai.com/v1/chat/completions",
                "anthropic" => "https://api.anthropic.com/v1/messages",
                "local" => "http://localhost:8080/v1/chat/completions",
                _ => "https://api.openai.com/v1/chat/completions"
            };
        }

        private void TrimConversationHistory()
        {
            if (_conversationHistory.Count > _maxHistoryMessages)
            {
                // Keep system message and remove oldest user/assistant messages
                var systemMessage = _conversationHistory.FirstOrDefault(m => m.Role == "system");
                var otherMessages = _conversationHistory.Where(m => m.Role != "system").ToList();
                
                var messagesToRemove = otherMessages.Count - (_maxHistoryMessages - (systemMessage != null ? 1 : 0));
                
                if (messagesToRemove > 0)
                {
                    for (int i = 0; i < messagesToRemove; i++)
                    {
                        _conversationHistory.Remove(otherMessages[i]);
                    }
                    
                    _logger.LogDebug("Trimmed {Count} messages from conversation history", messagesToRemove);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                _logger.LogInformation("Disposing LLM service");

                // Cancel operations
                _cancellationTokenSource.Cancel();

                // Dispose HTTP client
                _httpClient.Dispose();
                _cancellationTokenSource.Dispose();
                
                _disposed = true;
                _logger.LogInformation("LLM service disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing LLM service");
            }
        }
    }
}