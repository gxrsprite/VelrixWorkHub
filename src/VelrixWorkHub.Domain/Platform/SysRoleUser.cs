namespace BootstrapBlazor.Components;

public class SysRoleUser
{
	public Guid RoleId { get; set; }

	public Guid UserId { get; set; }

	public SysRole Role { get; set; }

	public SysUser User { get; set; }
}
