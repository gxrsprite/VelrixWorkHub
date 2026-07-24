using System.Net.Mail;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Notifications;

/// <summary>
/// 已解析的站外收件地址。地址语义由渠道 Provider 负责，例如邮箱、手机号或企业通讯录用户标识。
/// </summary>
public sealed record ExternalNotificationRecipient(ExternalNotificationChannel Channel, string Address)
{
    public string Address { get; } = Validate(Address);

    /// <summary>按渠道校验并规范化受控地址；无效档案值只会跳过该渠道，不能影响其他渠道或站内通知。</summary>
    public static bool TryCreate(ExternalNotificationChannel channel, string? address, out ExternalNotificationRecipient recipient)
    {
        recipient = null!;
        if (string.IsNullOrWhiteSpace(address)) return false;
        var normalized = address.Trim();
        if (channel == ExternalNotificationChannel.Email)
        {
            if (!MailAddress.TryCreate(normalized, out var parsed) || !parsed.Address.Equals(normalized, StringComparison.OrdinalIgnoreCase)) return false;
        }
        else if (channel == ExternalNotificationChannel.Sms)
        {
            if (!TryNormalizePhone(normalized, out normalized)) return false;
        }

        try
        {
            recipient = new ExternalNotificationRecipient(channel, normalized);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 500)
            throw new ArgumentException("外部通知地址不能为空且不能超过 500 个字符。", nameof(value));
        return value.Trim();
    }

    private static bool TryNormalizePhone(string value, out string normalized)
    {
        normalized = string.Empty;
        if (!value.All(character => char.IsDigit(character) || character is '+' or ' ' or '-' or '(' or ')')) return false;
        var compact = string.Concat(value.Where(character => char.IsDigit(character) || character == '+'));
        if (compact.Count(character => character == '+') > 1 || (compact.Contains('+') && compact[0] != '+')) return false;
        var digits = compact.TrimStart('+');
        if (digits.Length is < 6 or > 32 || !digits.All(char.IsDigit)) return false;
        normalized = compact;
        return true;
    }
}

/// <summary>传给渠道 Provider 的冻结消息，不携带第三方密钥、签名或模板实现细节。</summary>
public sealed record ExternalNotificationMessage(
    Guid NotificationId,
    ExternalNotificationChannel Channel,
    string Address,
    WorkNotificationKind Kind,
    string Title,
    string Content,
    string? Href,
    string DedupeKey,
    DateTime CreatedAt)
{
    public static ExternalNotificationMessage From(WorkNotification notification, ExternalNotificationRecipient recipient)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(recipient);
        return new(
            notification.Id,
            recipient.Channel,
            recipient.Address,
            notification.Kind,
            notification.Title,
            notification.Content,
            notification.Href,
            $"external:{recipient.Channel}:{notification.Recipient}:{notification.DedupeKey}",
            notification.CreatedAt);
    }
}

/// <summary>
/// 将平台用户名解析为各渠道可用的收件地址。实现必须在 Infrastructure 中读取受控主数据，
/// 不得从业务字段或自由文本猜测手机号、邮箱或企业通讯录标识。
/// </summary>
public interface IExternalNotificationRecipientResolver
{
    IReadOnlyList<ExternalNotificationRecipient> Resolve(WorkNotification notification);
}

/// <summary>单一第三方渠道适配器。真实实现应在持久化 Outbox 消费端调用，而非阻断业务交易。</summary>
public interface IExternalNotificationChannelProvider
{
    ExternalNotificationChannel Channel { get; }

    Task SendAsync(ExternalNotificationMessage message, CancellationToken cancellationToken = default);
}

/// <summary>仅用于运维页面的渠道配置状态；不得包含地址、主机、用户名、密钥或模板正文。</summary>
public enum ExternalNotificationChannelConfigurationState
{
    Disabled,
    Enabled,
    AwaitingProvider
}

public sealed record ExternalNotificationChannelConfiguration(
    ExternalNotificationChannel Channel,
    ExternalNotificationChannelConfigurationState State,
    string Description);

public interface IExternalNotificationChannelConfigurationProvider
{
    IReadOnlyList<ExternalNotificationChannelConfiguration> List();
}

public sealed record ExternalNotificationDispatchFailure(
    ExternalNotificationMessage Message,
    string Error);

public sealed record ExternalNotificationDispatchResult(
    int ResolvedRecipientCount,
    int SentCount,
    int SkippedCount,
    IReadOnlyList<ExternalNotificationDispatchFailure> Failures);

/// <summary>供未来 Outbox Worker 调用的站外通知调度边界。</summary>
public interface IExternalNotificationDispatcher
{
    Task<ExternalNotificationDispatchResult> DispatchAsync(WorkNotification notification, CancellationToken cancellationToken = default);
}

/// <summary>默认不解析任何站外地址，确保未完成第三方配置时不会误投递。</summary>
public sealed class EmptyExternalNotificationRecipientResolver : IExternalNotificationRecipientResolver
{
    public IReadOnlyList<ExternalNotificationRecipient> Resolve(WorkNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return [];
    }
}
