using System.Collections.Generic;

internal class GenTableDesign
{
	public GenTableInfo Table = new GenTableInfo();

	public bool IsTreeNav = false;

	public List<GenColumnDesign> ColumnDesigns = new List<GenColumnDesign>();

	public List<GenNavigateDesign> OneToOnes = new List<GenNavigateDesign>();

	public List<GenNavigateDesign> ManyToManys = new List<GenNavigateDesign>();

	public List<GenNavigateDesign> OneToManys = new List<GenNavigateDesign>();
}
