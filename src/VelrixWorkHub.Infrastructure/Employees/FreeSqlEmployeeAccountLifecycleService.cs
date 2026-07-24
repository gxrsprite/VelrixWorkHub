using BootstrapBlazor.Components;
using FreeSql;
using VelrixWorkHub.Application.Employees;

namespace VelrixWorkHub.Infrastructure.Employees;

public sealed class FreeSqlEmployeeAccountLifecycleService(IFreeSql fsql) : IEmployeeAccountLifecycleService
{
    public void Disable(Guid userId, string actor, string reason)
    {
        if (userId == Guid.Empty) throw new ArgumentException("平台用户不能为空。", nameof(userId));
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("操作者不能为空。", nameof(actor));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("停用原因不能为空。", nameof(reason));

        var user = fsql.Select<SysUser>().Where(item => item.Id == userId).First();
        if (user is null) throw new InvalidOperationException("平台用户不存在，不能停用账号。");
        if (!user.IsEnabled) return;

        var rows = fsql.Update<SysUser>()
            .Set(item => item.IsEnabled, false)
            .Set(item => item.AuthVersion, user.AuthVersion + 1)
            .Where(item => item.Id == userId && item.IsEnabled)
            .ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("平台账号停用失败，账号状态可能已被其他操作改变。");
    }
}
