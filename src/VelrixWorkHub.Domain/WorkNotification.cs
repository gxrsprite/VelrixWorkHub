namespace VelrixWorkHub.Domain;

public enum WorkNotificationKind
{
    Approval,
    Reminder,
    Assignment,
    System
}

public sealed class WorkNotification
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string Recipient { get; }
    public WorkNotificationKind Kind { get; }
    public string Title { get; }
    public string Content { get; }
    public string? Href { get; }
    public string DedupeKey { get; }
    public DateTime CreatedAt { get; }
    public DateTime? ReadAt { get; private set; }
    public bool IsRead => ReadAt is not null;

    public WorkNotification(string recipient, WorkNotificationKind kind, string title, string content, string? href, string dedupeKey, DateTime? createdAt = null)
    {
        Validate(recipient, title, content, href, dedupeKey, kind);
        Recipient = NormalizeRecipient(recipient);
        Kind = kind;
        Title = title.Trim();
        Content = content.Trim();
        Href = string.IsNullOrWhiteSpace(href) ? null : href.Trim();
        DedupeKey = dedupeKey.Trim();
        CreatedAt = createdAt ?? DateTime.Now;
    }

    private WorkNotification(Guid id, string recipient, WorkNotificationKind kind, string title, string content, string? href, string dedupeKey, DateTime createdAt, DateTime? readAt)
    {
        Id = id;
        Recipient = recipient;
        Kind = kind;
        Title = title;
        Content = content;
        Href = href;
        DedupeKey = dedupeKey;
        CreatedAt = createdAt;
        ReadAt = readAt;
    }

    public static WorkNotification Rehydrate(Guid id, string recipient, WorkNotificationKind kind, string title, string content, string? href, string dedupeKey, DateTime createdAt, DateTime? readAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("通知标识不能为空。", nameof(id));
        Validate(recipient, title, content, href, dedupeKey, kind);
        return new WorkNotification(id, NormalizeRecipient(recipient), kind, title.Trim(), content.Trim(), string.IsNullOrWhiteSpace(href) ? null : href.Trim(), dedupeKey.Trim(), createdAt, readAt);
    }

    public void MarkRead(DateTime? readAt = null) => ReadAt ??= readAt ?? DateTime.Now;

    private static string NormalizeRecipient(string recipient) => recipient.Trim().ToLowerInvariant();

    private static void Validate(string recipient, string title, string content, string? href, string dedupeKey, WorkNotificationKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (string.IsNullOrWhiteSpace(recipient) || recipient.Trim().Length > 100) throw new ArgumentException("通知接收人不能为空且不能超过 100 个字符。", nameof(recipient));
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200) throw new ArgumentException("通知标题不能为空且不能超过 200 个字符。", nameof(title));
        if (string.IsNullOrWhiteSpace(content) || content.Trim().Length > 2000) throw new ArgumentException("通知内容不能为空且不能超过 2000 个字符。", nameof(content));
        if (href?.Trim().Length > 500) throw new ArgumentException("通知链接不能超过 500 个字符。", nameof(href));
        if (string.IsNullOrWhiteSpace(dedupeKey) || dedupeKey.Trim().Length > 200) throw new ArgumentException("通知去重键不能为空且不能超过 200 个字符。", nameof(dedupeKey));
    }
}
