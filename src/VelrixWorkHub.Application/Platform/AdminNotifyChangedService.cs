namespace AdminBlazor.Services;

public sealed class AdminNotifyChangedService
{
    public event Func<AdminNotifyChangedEventArgs, Task>? Changed;

    public async Task NotifyAsync(Type entityType, string action, object? source = null)
    {
        var handlers = Changed;
        if (handlers == null)
            return;

        var args = new AdminNotifyChangedEventArgs(entityType, action, source);
        foreach (Func<AdminNotifyChangedEventArgs, Task> handler in handlers.GetInvocationList())
        {
            await handler(args);
        }
    }
}

public sealed record AdminNotifyChangedEventArgs(Type EntityType, string Action, object? Source);
