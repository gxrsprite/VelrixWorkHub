namespace VelrixWorkHub.Application.Platform;

public sealed record AdminPermissionAuditEntry(
    Guid Id,
    Guid SubjectId,
    string SubjectType,
    string Action,
    Guid? ActorUserId,
    string? ActorUserName,
    string BeforeData,
    string AfterData,
    DateTime? CreatedTime);

/// <summary>
/// 权限集合变更审计边界。
/// </summary>
public interface IAdminPermissionAuditService
{
    void Record(
        Guid subjectId,
        string subjectType,
        string action,
        string beforeData,
        string afterData,
        Guid? actorUserId,
        string? actorUserName);

    Task<IReadOnlyList<AdminPermissionAuditEntry>> ListAsync(
        Guid? subjectId = null,
        string? subjectType = null,
        int take = 100,
        string? action = null);
}
