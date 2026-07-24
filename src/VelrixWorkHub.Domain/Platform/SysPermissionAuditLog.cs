using FreeSql.DataAnnotations;

namespace BootstrapBlazor.Components;

/// <summary>
/// 权限集合变更审计日志。
/// </summary>
public class SysPermissionAuditLog : EntityCreated<Guid>
{
    [Column(StringLength = 30, IsNullable = false)]
    public string SubjectType { get; set; } = string.Empty;

    public Guid SubjectId { get; set; }

    [Column(StringLength = 20, IsNullable = false)]
    public string Action { get; set; } = string.Empty;

    [Column(StringLength = -1, IsNullable = false)]
    public string BeforeData { get; set; } = string.Empty;

    [Column(StringLength = -1, IsNullable = false)]
    public string AfterData { get; set; } = string.Empty;
}
