using BootstrapBlazor.Components;
using FreeSql;
using VelrixWorkHub.Application.Platform;

namespace VelrixWorkHub.Infrastructure.Platform;

public sealed class FreeSqlAdminPermissionAuditService(IFreeSql fsql) : IAdminPermissionAuditService
{
    public void Record(
        Guid subjectId,
        string subjectType,
        string action,
        string beforeData,
        string afterData,
        Guid? actorUserId,
        string? actorUserName)
    {
        if (subjectId == Guid.Empty)
            throw new ArgumentException("权限审计主体不能为空。", nameof(subjectId));
        if (string.IsNullOrWhiteSpace(subjectType))
            throw new ArgumentException("权限审计主体类型不能为空。", nameof(subjectType));
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("权限审计动作不能为空。", nameof(action));

        fsql.Insert(new SysPermissionAuditLog
        {
            Id = Guid.CreateVersion7(),
            SubjectId = subjectId,
            SubjectType = subjectType.Trim(),
            Action = action.Trim(),
            BeforeData = beforeData ?? string.Empty,
            AfterData = afterData ?? string.Empty,
            CreatedUserId = actorUserId,
            CreatedUserName = actorUserName ?? string.Empty,
            CreatedTime = DateTime.Now
        }).ExecuteAffrows();
    }

    public async Task<IReadOnlyList<AdminPermissionAuditEntry>> ListAsync(
        Guid? subjectId = null,
        string? subjectType = null,
        int take = 100,
        string? action = null)
    {
        return (await fsql.Select<SysPermissionAuditLog>()
                .WhereIf(subjectId.HasValue, item => item.SubjectId == subjectId!.Value)
                .WhereIf(!string.IsNullOrWhiteSpace(subjectType), item => item.SubjectType == subjectType!.Trim())
                .WhereIf(!string.IsNullOrWhiteSpace(action), item => item.Action == action!.Trim())
                .OrderByDescending(item => item.CreatedTime)
                .Take(Math.Clamp(take, 1, 500))
                .ToListAsync())
            .Select(item => new AdminPermissionAuditEntry(
                item.Id,
                item.SubjectId,
                item.SubjectType,
                item.Action,
                item.CreatedUserId,
                item.CreatedUserName,
                item.BeforeData,
                item.AfterData,
                item.CreatedTime))
            .ToArray();
    }
}
