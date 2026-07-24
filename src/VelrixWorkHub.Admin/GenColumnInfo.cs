using System.Collections.Generic;

internal class GenColumnInfo
{
	public string TypeNameSpace = string.Empty;

	public string TypeName = string.Empty;

	public string MapTypeName = string.Empty;

	public Dictionary<string, long> EnumValues = new Dictionary<string, long>();

	public string CsName = string.Empty;

	public string Comment = string.Empty;

	public bool IsPrimary = false;

	public bool IsIdentity = false;

	public bool IsVersion = false;

	public string DbType = string.Empty;

	public int StringLength = 0;

	public int Precision = 0;

	public int Scale = 0;
}
