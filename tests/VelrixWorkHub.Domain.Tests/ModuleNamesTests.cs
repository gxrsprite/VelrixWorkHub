using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public class ModuleNamesTests
{
    [Fact]
    public void ModuleNames_AreStableLowercaseIdentifiers()
    {
        Assert.Equal("oa", ModuleNames.Oa);
        Assert.Equal("crm", ModuleNames.Crm);
    }
}
