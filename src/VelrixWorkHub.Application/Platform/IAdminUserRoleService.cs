using BootstrapBlazor.Components;

namespace VelrixWorkHub.Application.Platform;

public sealed record AdminUserRoleSaveResult(
    bool Success,
    string? Error,
    int AssignedCount);

/// <summary>
/// 平台用户角色分配用例。
/// 角色关联的读取、校验和替换由 Infrastructure 统一实现，页面不直接写 SysRoleUser。
/// </summary>
public interface IAdminUserRoleService
{
    Task<IReadOnlyList<SysRole>> LoadAssignableRolesAsync();

    Task<IReadOnlyList<Guid>> LoadUserRoleIdsAsync(Guid userId);

    Task<AdminUserRoleSaveResult> SaveUserRolesAsync(
        Guid userId,
        IReadOnlyCollection<Guid> roleIds,
        Guid? actorUserId = null,
        string? actorUserName = null);
}
