using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;
using PersonalAiAssistant.Models;
using PersonalAiAssistant.Services;
using System.Collections.ObjectModel;

namespace PersonalAiAssistant.Services
{
    public class MicrosoftTodoService : IMicrosoftTodoService, IDisposable
    {
        private readonly ILogger<MicrosoftTodoService> _logger;
        private readonly IConfigurationManager _configManager;
        
        private IPublicClientApplication? _clientApp;
        private GraphServiceClient? _graphServiceClient;
        private IAccount? _currentAccount;
        
        private bool _isInitialized = false;
        private bool _isAuthenticated = false;
        private bool _disposed = false;
        
        private readonly object _lockObject = new object();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        
        // Cache for todo lists and tasks
        private List<TodoTaskList>? _cachedTodoLists;
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
        
        // Microsoft Graph scopes
        private readonly string[] _scopes = { "Tasks.ReadWrite", "User.Read" };
        
        // MSAL configuration
        private string _clientId = string.Empty;
        private string _tenantId = string.Empty;
        private string _redirectUri = "http://localhost";

        public event EventHandler<AuthenticationStatusChangedEventArgs>? AuthenticationStatusChanged;
        public event EventHandler<TodoListUpdatedEventArgs>? TodoListUpdated;

        public bool IsInitialized => _isInitialized;
        public bool IsAuthenticated => _isAuthenticated;
        public string? CurrentUserEmail { get; private set; }

        public MicrosoftTodoService(ILogger<MicrosoftTodoService> logger, IConfigurationManager configManager)
        {
            _logger = logger;
            _configManager = configManager;
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                if (_isInitialized)
                {
                    _logger.LogInformation("Microsoft Todo service already initialized");
                    return true;
                }

                _logger.LogInformation("Initializing Microsoft Todo service");
                
                // Load configuration
                var microsoftSettings = _configManager.MicrosoftSettings;
                _clientId = microsoftSettings.ClientId;
                _tenantId = microsoftSettings.TenantId;
                _redirectUri = microsoftSettings.RedirectUri;
                
                if (string.IsNullOrWhiteSpace(_clientId))
                {
                    _logger.LogWarning("Microsoft Client ID not configured");
                    _isInitialized = true; // Still mark as initialized but not authenticated
                    return true;
                }

                // Initialize MSAL client
                var builder = PublicClientApplicationBuilder
                    .Create(_clientId)
                    .WithRedirectUri(_redirectUri)
                    .WithLogging(MSALLogCallback, LogLevel.Info, enablePiiLogging: false);
                
                if (!string.IsNullOrWhiteSpace(_tenantId))
                {
                    builder = builder.WithTenantId(_tenantId);
                }
                else
                {
                    builder = builder.WithAuthority(AzureCloudInstance.AzurePublic, AadAuthorityAudience.AzureAdAndPersonalMicrosoftAccount);
                }
                
                _clientApp = builder.Build();
                
                // Try to get cached account
                await TryGetCachedAccountAsync();
                
                _isInitialized = true;
                _logger.LogInformation("Microsoft Todo service initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Microsoft Todo service");
                return false;
            }
        }

        public async Task<bool> AuthenticateAsync()
        {
            try
            {
                if (!_isInitialized)
                {
                    _logger.LogWarning("Service not initialized. Attempting to initialize...");
                    if (!await InitializeAsync())
                    {
                        return false;
                    }
                }

                if (_clientApp == null)
                {
                    _logger.LogError("MSAL client not initialized");
                    return false;
                }

                _logger.LogInformation("Attempting to authenticate with Microsoft");
                
                AuthenticationResult? result = null;
                
                // Try silent authentication first
                if (_currentAccount != null)
                {
                    try
                    {
                        result = await _clientApp.AcquireTokenSilent(_scopes, _currentAccount)
                            .ExecuteAsync(_cancellationTokenSource.Token);
                        _logger.LogInformation("Silent authentication successful");
                    }
                    catch (MsalUiRequiredException)
                    {
                        _logger.LogInformation("Silent authentication failed, interactive authentication required");
                    }
                }
                
                // If silent authentication failed, try interactive
                if (result == null)
                {
                    try
                    {
                        result = await _clientApp.AcquireTokenInteractive(_scopes)
                            .WithPrompt(Prompt.SelectAccount)
                            .ExecuteAsync(_cancellationTokenSource.Token);
                        _logger.LogInformation("Interactive authentication successful");
                    }
                    catch (MsalException ex)
                    {
                        _logger.LogError(ex, "Interactive authentication failed");
                        return false;
                    }
                }
                
                if (result != null)
                {
                    _currentAccount = result.Account;
                    CurrentUserEmail = result.Account.Username;
                    
                    // Initialize Graph client
                    var authProvider = new DelegateAuthenticationProvider((requestMessage) =>
                    {
                        requestMessage.Headers.Authorization = 
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.AccessToken);
                        return Task.FromResult(0);
                    });
                    
                    _graphServiceClient = new GraphServiceClient(authProvider);
                    
                    _isAuthenticated = true;
                    _logger.LogInformation("Successfully authenticated as {UserEmail}", CurrentUserEmail);
                    
                    AuthenticationStatusChanged?.Invoke(this, new AuthenticationStatusChangedEventArgs
                    {
                        IsAuthenticated = true,
                        UserEmail = CurrentUserEmail,
                        Timestamp = DateTime.UtcNow
                    });
                    
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authentication");
                return false;
            }
        }

        public async Task SignOutAsync()
        {
            try
            {
                if (!_isAuthenticated)
                {
                    _logger.LogInformation("User not authenticated");
                    return;
                }

                _logger.LogInformation("Signing out user: {UserEmail}", CurrentUserEmail);
                
                if (_clientApp != null && _currentAccount != null)
                {
                    await _clientApp.RemoveAsync(_currentAccount);
                }
                
                _currentAccount = null;
                CurrentUserEmail = null;
                _graphServiceClient = null;
                _isAuthenticated = false;
                
                // Clear cache
                _cachedTodoLists = null;
                _lastCacheUpdate = DateTime.MinValue;
                
                AuthenticationStatusChanged?.Invoke(this, new AuthenticationStatusChangedEventArgs
                {
                    IsAuthenticated = false,
                    UserEmail = null,
                    Timestamp = DateTime.UtcNow
                });
                
                _logger.LogInformation("User signed out successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sign out");
            }
        }

        public async Task<List<TodoTaskList>?> GetTodoListsAsync()
        {
            try
            {
                if (!_isAuthenticated || _graphServiceClient == null)
                {
                    _logger.LogWarning("Not authenticated or Graph client not available");
                    return null;
                }

                // Check cache
                if (_cachedTodoLists != null && DateTime.UtcNow - _lastCacheUpdate < _cacheExpiration)
                {
                    _logger.LogDebug("Returning cached todo lists");
                    return _cachedTodoLists;
                }

                _logger.LogInformation("Fetching todo lists from Microsoft Graph");
                
                var todoLists = await _graphServiceClient.Me.Todo.Lists
                    .Request()
                    .GetAsync(_cancellationTokenSource.Token);
                
                var result = new List<TodoTaskList>();
                
                if (todoLists?.CurrentPage != null)
                {
                    foreach (var list in todoLists.CurrentPage)
                    {
                        var todoList = new TodoTaskList
                        {
                            Id = list.Id,
                            DisplayName = list.DisplayName ?? "Unnamed List",
                            IsOwner = list.IsOwner ?? false,
                            IsShared = list.IsShared ?? false,
                            WellknownListName = list.WellknownListName?.ToString(),
                            Tasks = new ObservableCollection<TodoTask>()
                        };
                        
                        // Get tasks for this list
                        try
                        {
                            var tasks = await _graphServiceClient.Me.Todo.Lists[list.Id].Tasks
                                .Request()
                                .Top(50) // Limit to avoid too much data
                                .GetAsync(_cancellationTokenSource.Token);
                            
                            if (tasks?.CurrentPage != null)
                            {
                                foreach (var task in tasks.CurrentPage)
                                {
                                    var todoTask = new TodoTask
                                    {
                                        Id = task.Id,
                                        Title = task.Title ?? "Untitled Task",
                                        Status = task.Status?.ToString() ?? "notStarted",
                                        Importance = task.Importance?.ToString() ?? "normal",
                                        IsReminderOn = task.IsReminderOn ?? false,
                                        CreatedDateTime = task.CreatedDateTime?.DateTime ?? DateTime.MinValue,
                                        LastModifiedDateTime = task.LastModifiedDateTime?.DateTime ?? DateTime.MinValue,
                                        CompletedDateTime = task.CompletedDateTime?.DateTime,
                                        DueDateTime = task.DueDateTime?.DateTime,
                                        ReminderDateTime = task.ReminderDateTime?.DateTime,
                                        Body = task.Body?.Content ?? string.Empty
                                    };
                                    
                                    todoList.Tasks.Add(todoTask);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error fetching tasks for list {ListId}", list.Id);
                        }
                        
                        result.Add(todoList);
                    }
                }
                
                // Update cache
                _cachedTodoLists = result;
                _lastCacheUpdate = DateTime.UtcNow;
                
                _logger.LogInformation("Fetched {ListCount} todo lists with {TaskCount} total tasks", 
                    result.Count, result.Sum(l => l.Tasks.Count));
                
                TodoListUpdated?.Invoke(this, new TodoListUpdatedEventArgs
                {
                    TodoLists = result,
                    Timestamp = DateTime.UtcNow
                });
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching todo lists");
                return null;
            }
        }

        public async Task<TodoTask?> CreateTaskAsync(string listId, string title, string? description = null, DateTime? dueDate = null)
        {
            try
            {
                if (!_isAuthenticated || _graphServiceClient == null)
                {
                    _logger.LogWarning("Not authenticated or Graph client not available");
                    return null;
                }

                if (string.IsNullOrWhiteSpace(listId) || string.IsNullOrWhiteSpace(title))
                {
                    _logger.LogWarning("Invalid parameters for creating task");
                    return null;
                }

                _logger.LogInformation("Creating task '{Title}' in list {ListId}", title, listId);
                
                var newTask = new Microsoft.Graph.TodoTask
                {
                    Title = title,
                    Body = !string.IsNullOrWhiteSpace(description) ? new ItemBody
                    {
                        Content = description,
                        ContentType = BodyType.Text
                    } : null,
                    DueDateTime = dueDate.HasValue ? new DateTimeTimeZone
                    {
                        DateTime = dueDate.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                        TimeZone = TimeZoneInfo.Local.Id
                    } : null
                };
                
                var createdTask = await _graphServiceClient.Me.Todo.Lists[listId].Tasks
                    .Request()
                    .AddAsync(newTask, _cancellationTokenSource.Token);
                
                if (createdTask != null)
                {
                    var todoTask = new TodoTask
                    {
                        Id = createdTask.Id,
                        Title = createdTask.Title ?? title,
                        Status = createdTask.Status?.ToString() ?? "notStarted",
                        Importance = createdTask.Importance?.ToString() ?? "normal",
                        IsReminderOn = createdTask.IsReminderOn ?? false,
                        CreatedDateTime = createdTask.CreatedDateTime?.DateTime ?? DateTime.UtcNow,
                        LastModifiedDateTime = createdTask.LastModifiedDateTime?.DateTime ?? DateTime.UtcNow,
                        DueDateTime = createdTask.DueDateTime?.DateTime,
                        Body = createdTask.Body?.Content ?? description ?? string.Empty
                    };
                    
                    // Invalidate cache
                    _cachedTodoLists = null;
                    _lastCacheUpdate = DateTime.MinValue;
                    
                    _logger.LogInformation("Successfully created task '{Title}' with ID {TaskId}", title, createdTask.Id);
                    return todoTask;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating task '{Title}' in list {ListId}", title, listId);
                return null;
            }
        }

        public async Task<bool> CompleteTaskAsync(string listId, string taskId)
        {
            try
            {
                if (!_isAuthenticated || _graphServiceClient == null)
                {
                    _logger.LogWarning("Not authenticated or Graph client not available");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(listId) || string.IsNullOrWhiteSpace(taskId))
                {
                    _logger.LogWarning("Invalid parameters for completing task");
                    return false;
                }

                _logger.LogInformation("Completing task {TaskId} in list {ListId}", taskId, listId);
                
                var updateTask = new Microsoft.Graph.TodoTask
                {
                    Status = Microsoft.Graph.TaskStatus.Completed,
                    CompletedDateTime = new DateTimeTimeZone
                    {
                        DateTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                        TimeZone = TimeZoneInfo.Local.Id
                    }
                };
                
                await _graphServiceClient.Me.Todo.Lists[listId].Tasks[taskId]
                    .Request()
                    .UpdateAsync(updateTask, _cancellationTokenSource.Token);
                
                // Invalidate cache
                _cachedTodoLists = null;
                _lastCacheUpdate = DateTime.MinValue;
                
                _logger.LogInformation("Successfully completed task {TaskId}", taskId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing task {TaskId} in list {ListId}", taskId, listId);
                return false;
            }
        }

        public async Task RefreshAsync()
        {
            try
            {
                if (!_isAuthenticated)
                {
                    _logger.LogInformation("Not authenticated, cannot refresh");
                    return;
                }

                _logger.LogInformation("Refreshing todo lists");
                
                // Clear cache to force refresh
                _cachedTodoLists = null;
                _lastCacheUpdate = DateTime.MinValue;
                
                // Fetch fresh data
                await GetTodoListsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing todo lists");
            }
        }

        private async Task TryGetCachedAccountAsync()
        {
            try
            {
                if (_clientApp == null)
                    return;
                
                var accounts = await _clientApp.GetAccountsAsync();
                _currentAccount = accounts.FirstOrDefault();
                
                if (_currentAccount != null)
                {
                    _logger.LogInformation("Found cached account: {Username}", _currentAccount.Username);
                    CurrentUserEmail = _currentAccount.Username;
                    
                    // Try to authenticate silently
                    try
                    {
                        var result = await _clientApp.AcquireTokenSilent(_scopes, _currentAccount)
                            .ExecuteAsync(_cancellationTokenSource.Token);
                        
                        if (result != null)
                        {
                            var authProvider = new DelegateAuthenticationProvider((requestMessage) =>
                            {
                                requestMessage.Headers.Authorization = 
                                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.AccessToken);
                                return Task.FromResult(0);
                            });
                            
                            _graphServiceClient = new GraphServiceClient(authProvider);
                            _isAuthenticated = true;
                            
                            _logger.LogInformation("Successfully authenticated with cached account");
                            
                            AuthenticationStatusChanged?.Invoke(this, new AuthenticationStatusChangedEventArgs
                            {
                                IsAuthenticated = true,
                                UserEmail = CurrentUserEmail,
                                Timestamp = DateTime.UtcNow
                            });
                        }
                    }
                    catch (MsalUiRequiredException)
                    {
                        _logger.LogInformation("Cached account requires interactive authentication");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error trying to get cached account");
            }
        }

        private void MSALLogCallback(LogLevel level, string message, bool containsPii)
        {
            if (containsPii)
                return;
                
            var logLevel = level switch
            {
                LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
                LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
                LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
                LogLevel.Verbose => Microsoft.Extensions.Logging.LogLevel.Debug,
                _ => Microsoft.Extensions.Logging.LogLevel.Trace
            };
            
            _logger.Log(logLevel, "MSAL: {Message}", message);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                _logger.LogInformation("Disposing Microsoft Todo service");

                // Cancel operations
                _cancellationTokenSource.Cancel();

                // Clear sensitive data
                _currentAccount = null;
                CurrentUserEmail = null;
                _graphServiceClient = null;
                _cachedTodoLists = null;
                
                _cancellationTokenSource.Dispose();
                _disposed = true;
                
                _logger.LogInformation("Microsoft Todo service disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing Microsoft Todo service");
            }
        }
    }
}