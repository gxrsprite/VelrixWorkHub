using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Notifications;

/// <summary>
/// SMTP 渠道的部署配置。密码只从运行环境的配置提供程序读取，不能写入仓库配置或业务表。
/// </summary>
public sealed class ExternalNotificationEmailOptions
{
    public bool Enabled { get; init; }
    public string? Host { get; init; }
    public int Port { get; init; } = 587;
    public bool UseSsl { get; init; } = true;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? FromAddress { get; init; }
    public string? FromDisplayName { get; init; }

    public void Validate()
    {
        if (!Enabled) return;
        if (string.IsNullOrWhiteSpace(Host) || Host.Trim().Length > 255)
            throw new InvalidOperationException("启用邮件站外通知时必须配置有效的 ExternalNotifications:Email:Host。");
        if (Port is < 1 or > 65535)
            throw new InvalidOperationException("ExternalNotifications:Email:Port 必须在 1 到 65535 之间。");
        if (string.IsNullOrWhiteSpace(FromAddress) || !MailAddress.TryCreate(FromAddress.Trim(), out _))
            throw new InvalidOperationException("启用邮件站外通知时必须配置有效的 ExternalNotifications:Email:FromAddress。");
        if (string.IsNullOrWhiteSpace(Username) != string.IsNullOrWhiteSpace(Password))
            throw new InvalidOperationException("ExternalNotifications:Email:Username 与 Password 必须同时配置，或同时留空。");
    }
}

public sealed record ExternalSmtpMessage(
    string To,
    string FromAddress,
    string? FromDisplayName,
    string Subject,
    string Body,
    string MessageId,
    string Host,
    int Port,
    bool UseSsl,
    string? Username,
    string? Password);

public interface IExternalSmtpSender
{
    Task SendAsync(ExternalSmtpMessage message, CancellationToken cancellationToken = default);
}

public sealed class SystemExternalSmtpSender : IExternalSmtpSender
{
    public async Task SendAsync(ExternalSmtpMessage message, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(message.Host, message.Port)
        {
            EnableSsl = message.UseSsl,
            UseDefaultCredentials = string.IsNullOrWhiteSpace(message.Username)
        };
        if (!string.IsNullOrWhiteSpace(message.Username))
            client.Credentials = new NetworkCredential(message.Username, message.Password);

        using var mail = new MailMessage
        {
            From = new MailAddress(message.FromAddress, message.FromDisplayName ?? string.Empty),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = false,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8
        };
        mail.To.Add(new MailAddress(message.To));
        mail.Headers.Add("Message-Id", message.MessageId);
        await client.SendMailAsync(mail).WaitAsync(cancellationToken);
    }
}

/// <summary>仅在明确启用 SMTP 配置时注册；由持久化 Outbox Worker 调用，绝不在业务交易中直接发信。</summary>
public sealed class SmtpExternalNotificationProvider(
    ExternalNotificationEmailOptions options,
    IExternalSmtpSender sender) : IExternalNotificationChannelProvider
{
    public ExternalNotificationChannel Channel => ExternalNotificationChannel.Email;

    public Task SendAsync(ExternalNotificationMessage message, CancellationToken cancellationToken = default)
    {
        if (message.Channel != Channel) throw new InvalidOperationException("SMTP Provider 只能处理邮件渠道通知。");
        options.Validate();
        if (!MailAddress.TryCreate(message.Address, out _))
            throw new InvalidOperationException("站外邮件收件地址无效。");

        return sender.SendAsync(new ExternalSmtpMessage(
            message.Address,
            options.FromAddress!.Trim(),
            string.IsNullOrWhiteSpace(options.FromDisplayName) ? null : options.FromDisplayName.Trim(),
            message.Title,
            BuildBody(message),
            BuildMessageId(message.DedupeKey, message.Address, options.FromAddress),
            options.Host!.Trim(),
            options.Port,
            options.UseSsl,
            string.IsNullOrWhiteSpace(options.Username) ? null : options.Username.Trim(),
            options.Password), cancellationToken);
    }

    private static string BuildBody(ExternalNotificationMessage message)
        => string.IsNullOrWhiteSpace(message.Href) ? message.Content : $"{message.Content}\n\n办理入口：{message.Href}";

    private static string BuildMessageId(string dedupeKey, string address, string? fromAddress)
    {
        var localPart = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{dedupeKey}\n{address}"))).ToLowerInvariant();
        var domain = fromAddress?.Split('@', 2).LastOrDefault();
        return $"<{localPart}@{(string.IsNullOrWhiteSpace(domain) ? "velrix.local" : domain)}>";
    }
}
