using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Notifications;

public static class ExternalNotificationServiceCollectionExtensions
{
    /// <summary>
    /// 只有显式 Enabled 的邮件渠道才进入 Provider 集合。缺失或禁用配置保持 Outbox Pending，避免环境误发。
    /// </summary>
    public static IServiceCollection AddConfiguredExternalNotificationEmail(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection("ExternalNotifications:Email").Get<ExternalNotificationEmailOptions>() ?? new ExternalNotificationEmailOptions();
        options.Validate();
        services.AddSingleton(options);
        services.AddSingleton<IExternalNotificationChannelConfigurationProvider, ConfiguredExternalNotificationChannelConfigurationProvider>();
        if (!options.Enabled) return services;

        services.AddSingleton<IExternalSmtpSender, SystemExternalSmtpSender>();
        services.AddScoped<IExternalNotificationChannelProvider, SmtpExternalNotificationProvider>();
        return services;
    }
}

public sealed class ConfiguredExternalNotificationChannelConfigurationProvider(ExternalNotificationEmailOptions emailOptions) : IExternalNotificationChannelConfigurationProvider
{
    public IReadOnlyList<ExternalNotificationChannelConfiguration> List()
        =>
        [
            new(ExternalNotificationChannel.Email,
                emailOptions.Enabled ? ExternalNotificationChannelConfigurationState.Enabled : ExternalNotificationChannelConfigurationState.Disabled,
                emailOptions.Enabled ? "SMTP 已启用" : "SMTP 未启用"),
            new(ExternalNotificationChannel.Sms, ExternalNotificationChannelConfigurationState.AwaitingProvider, "待接入短信 Provider"),
            new(ExternalNotificationChannel.WeCom, ExternalNotificationChannelConfigurationState.AwaitingProvider, "待接入企业微信 Provider"),
            new(ExternalNotificationChannel.DingTalk, ExternalNotificationChannelConfigurationState.AwaitingProvider, "待接入钉钉 Provider")
        ];
}
