using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;
using VelrixWorkHub.Infrastructure.Notifications;

namespace VelrixWorkHub.Domain.Tests;

public sealed class ExternalNotificationChannelConfigurationProviderTests
{
    [Fact]
    public void List_DefaultEmailConfigurationDoesNotClaimDeliveryIsEnabled()
    {
        var provider = new ConfiguredExternalNotificationChannelConfigurationProvider(new ExternalNotificationEmailOptions());

        var channels = provider.List();

        Assert.Equal(4, channels.Count);
        Assert.Equal(ExternalNotificationChannelConfigurationState.Disabled, channels.Single(x => x.Channel == ExternalNotificationChannel.Email).State);
        Assert.All(channels.Where(x => x.Channel != ExternalNotificationChannel.Email), item => Assert.Equal(ExternalNotificationChannelConfigurationState.AwaitingProvider, item.State));
        Assert.DoesNotContain(channels, item => item.Description.Contains("smtp.example", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void List_EnabledEmailConfigurationExposesOnlyStateNotSensitiveValues()
    {
        var provider = new ConfiguredExternalNotificationChannelConfigurationProvider(new ExternalNotificationEmailOptions
        {
            Enabled = true,
            Host = "smtp.example.test",
            FromAddress = "workflow@example.test",
            Username = "workflow",
            Password = "test-secret"
        });

        var email = provider.List().Single(x => x.Channel == ExternalNotificationChannel.Email);

        Assert.Equal(ExternalNotificationChannelConfigurationState.Enabled, email.State);
        Assert.Equal("SMTP 已启用", email.Description);
        Assert.DoesNotContain("smtp.example.test", email.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("test-secret", email.Description, StringComparison.Ordinal);
    }
}
