using System.Collections.Generic;

internal class GenNavigateDesign
{
	public string Name = string.Empty;

	public GenNavigateType RefType = default;

	public string RefTypeName = string.Empty;

	public string RefMiddleTypeName = string.Empty;

	public List<GenColumnInfo> Columns = new List<GenColumnInfo>();

	public List<GenColumnInfo> MiddleColumns = new List<GenColumnInfo>();

	public List<GenColumnInfo> RefColumns = new List<GenColumnInfo>();

	public string DisplayText = string.Empty;

	public bool IsDisplay = false;

	public bool IsSearchFilter = false;

	public bool IsSearchText = false;

	public int EditStyle = 0;

	public List<GenColumnDesign> RefColumnDesigns = new List<GenColumnDesign>();
}
