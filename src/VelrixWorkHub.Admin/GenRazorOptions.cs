using System.Collections.Generic;

internal class GenRazorOptions
{
	public int curd_PageSize = 20;

	public bool curd_IsRemove = true;

	public bool curd_IsAdd = true;

	public bool curd_IsEdit = true;

	public bool curd_IsMultiSelect = true;

	public bool curd_FormInLine = false;

	public string curd_DialogClassName = "modal-xl";

	public string MenuPath = string.Empty;

	public string MenuLabel = string.Empty;

	public GenTableDesign Design = new GenTableDesign();

	public List<GenTableInfo> AllTables = new List<GenTableInfo>();
}
