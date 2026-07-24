using BootstrapBlazor.Components;

namespace VelrixWorkHub.Application.Platform;

public interface IAdminPermissionService
{
    Task<IReadOnlyList<SysRole>> LoadUserRolesAsync(Guid userId);

    Task<IReadOnlyList<SysMenu>> LoadAuthorizedMenusAsync(Guid userId, IReadOnlyList<SysRole>? roles = null);

    Task<IReadOnlyList<string>> LoadAuthorizedButtonPathsAsync(IReadOnlyList<SysRole> roles);

    Task<bool> CanAccessMenuAsync(Guid userId, string path);
}
