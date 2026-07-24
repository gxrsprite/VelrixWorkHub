using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Notifications;

/// <summary>
/// 统一分发已经由未来 Outbox 安全交付的消息。单个渠道失败只写入结果，
/// 不会取消其他渠道，更不会影响站内通知或业务状态。
/// </summary>
public sealed class ExternalNotificationDispatcher(
    IExternalNotificationRecipientResolver recipients,
    IEnumerable<IExternalNotificationChannelProvider> providers) : IExternalNotificationDispatcher
{
    private readonly IReadOnlyDictionary<ExternalNotificationChannel, IExternalNotificationChannelProvider> providersByChannel = providers
        .GroupBy(provider => provider.Channel)
        .ToDictionary(group => group.Key, group => group.Single());

    public async Task<ExternalNotificationDispatchResult> DispatchAsync(WorkNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var resolved = recipients.Resolve(notification)
            .GroupBy(item => (item.Channel, Address: AddressKey(item)))
            .Select(group => group.First())
            .ToArray();
        var sent = 0;
        var skipped = 0;
        var failures = new List<ExternalNotificationDispatchFailure>();

        foreach (var recipient in resolved)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!providersByChannel.TryGetValue(recipient.Channel, out var provider))
            {
                skipped++;
                continue;
            }

            var message = ExternalNotificationMessage.From(notification, recipient);
            try
            {
                await provider.SendAsync(message, cancellationToken);
                sent++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failures.Add(new ExternalNotificationDispatchFailure(message, TrimError(exception.Message)));
            }
        }

        return new ExternalNotificationDispatchResult(resolved.Length, sent, skipped, failures);
    }

    private static string TrimError(string error) => string.IsNullOrWhiteSpace(error) ? "外部通知渠道未提供错误信息。" : error.Length <= 2000 ? error : error[..2000];

    private static string AddressKey(ExternalNotificationRecipient recipient)
        => recipient.Channel == ExternalNotificationChannel.Email
            ? recipient.Address.ToUpperInvariant()
            : recipient.Address;
}
