using FreeSql.DataAnnotations;

namespace BootstrapBlazor.Components;

public class SysTenantMenu
{
	[Column(StringLength = 50)]
	public string TenantId { get; set; }

	public Guid MenuId { get; set; }

	public SysTenant Tenant { get; set; }

	public SysMenu Menu { get; set; }
}
