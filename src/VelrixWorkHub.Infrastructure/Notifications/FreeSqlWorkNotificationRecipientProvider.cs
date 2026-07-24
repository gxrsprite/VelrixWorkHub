using BootstrapBlazor.Components;
using FreeSql;
using VelrixWorkHub.Application.Notifications;

namespace VelrixWorkHub.Infrastructure.Notifications;

public sealed class FreeSqlWorkNotificationRecipientProvider(IFreeSql fsql) : IWorkNotificationRecipientProvider
{
    public IReadOnlyList<string> ListRecipients()
        => fsql.Select<SysUser>()
            .Where(x => x.IsEnabled)
            .ToList()
            .Select(x => x.Username?.Trim() ?? string.Empty)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
