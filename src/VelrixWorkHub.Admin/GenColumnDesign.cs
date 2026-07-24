internal class GenColumnDesign
{
	public GenColumnInfo Column = new GenColumnInfo();

	public int Position = 0;

	public string DisplayText = string.Empty;

	public bool IsDisplay = false;

	public bool IsDisplayManyToOne = false;

	public bool CanSearchText = false;

	public string CanSearchTextTips = string.Empty;

	public bool IsSearchText = false;

	public bool IsSearchFilterEnum = false;

	public bool IsSearchFilterManyToOne = false;

	public bool CanEdit = false;

	public int EditCol = 0;

	public int EditStyle = 0;

	public GenNavigateDesign ManyToOne = new GenNavigateDesign();
}
