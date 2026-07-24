using VelrixWorkHub.Application.Employees;

namespace VelrixWorkHub.Domain.Tests;

public sealed class EmployeeDirectoryServiceTests
{
    [Fact]
    public void List_DefaultsToEnabledAndSupportsOrganizationAndKeywordFilters()
    {
        var engineering = Guid.NewGuid();
        var repository = new TestRepository(
        [
            new(Guid.NewGuid(), "alice", "Alice", engineering, "研发部", true, "后端", DateTime.Now),
            new(Guid.NewGuid(), "bob", "Bob", engineering, "研发部", false, "已离职", DateTime.Now),
            new(Guid.NewGuid(), "carol", "Carol", Guid.NewGuid(), "销售部", true, "客户成功", null)
        ]);
        var service = new EmployeeDirectoryService(repository);

        var result = service.List("后端", engineering);

        var employee = Assert.Single(result);
        Assert.Equal("alice", employee.Username);
        Assert.Equal(2, service.Count());
    }

    [Fact]
    public void List_CanIncludeDisabledEmployeesAndSearchOrganization()
    {
        var repository = new TestRepository(
        [
            new(Guid.NewGuid(), "alice", "Alice", Guid.NewGuid(), "研发部", true, null, null),
            new(Guid.NewGuid(), "bob", "Bob", Guid.NewGuid(), "财务部", false, null, null)
        ]);
        var service = new EmployeeDirectoryService(repository);

        var result = service.List("财务", status: EmployeeDirectoryStatus.All);

        var employee = Assert.Single(result);
        Assert.False(employee.IsEnabled);
        Assert.Equal(2, service.List(status: EmployeeDirectoryStatus.All).Count);
        Assert.Single(service.List(status: EmployeeDirectoryStatus.Disabled));
    }

    private sealed class TestRepository(IReadOnlyList<EmployeeDirectoryEntry> employees) : IEmployeeDirectoryRepository
    {
        public IReadOnlyList<EmployeeDirectoryEntry> List() => employees;

        public IReadOnlyList<EmployeeDirectoryOrganization> ListOrganizations() => employees
            .Where(item => item.OrganizationId is not null && !string.IsNullOrWhiteSpace(item.OrganizationName))
            .GroupBy(item => item.OrganizationId!.Value)
            .Select(group => new EmployeeDirectoryOrganization(group.Key, group.First().OrganizationName!))
            .ToArray();
    }
}
