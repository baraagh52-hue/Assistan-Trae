using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PersonalAiAssistant.Services
{
    public interface IMicrosoftTodoService
    {
        event EventHandler<TodoListUpdatedEventArgs> TodoListUpdated;
        
        Task<bool> AuthenticateAsync();
        Task<List<TodoItem>> GetTodoItemsAsync();
        Task<TodoItem> CreateTodoItemAsync(string title, string? description = null, DateTime? dueDate = null);
        Task<TodoItem> UpdateTodoItemAsync(string id, string? title = null, string? description = null, DateTime? dueDate = null, bool? isCompleted = null);
        Task<bool> DeleteTodoItemAsync(string id);
        
        bool IsAuthenticated { get; }
        string? UserDisplayName { get; }
        
        Task SignOutAsync();
    }
    
    public class TodoItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Priority { get; set; } = "Normal";
    }
    
    public class TodoListUpdatedEventArgs : EventArgs
    {
        public List<TodoItem> TodoItems { get; set; } = new();
        public string UpdateType { get; set; } = string.Empty; // "Added", "Updated", "Deleted", "Refreshed"
        public TodoItem? AffectedItem { get; set; }
    }
}