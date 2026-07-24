using System.Collections.Generic;

internal class GenTableInfo
{
	public string TypeNameSpace = string.Empty;

	public string TypeFullName = string.Empty;

	public string TypeName = string.Empty;

	public string CsName = string.Empty;

	public string Comment = string.Empty;

	public List<GenColumnInfo> Columns = new List<GenColumnInfo>();
}
