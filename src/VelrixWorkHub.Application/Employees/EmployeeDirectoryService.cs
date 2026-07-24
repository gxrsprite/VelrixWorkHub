namespace VelrixWorkHub.Application.Employees;

public enum EmployeeDirectoryStatus
{
    All,
    Enabled,
    Disabled
}

public sealed record EmployeeDirectoryEntry(
    Guid UserId,
    string Username,
    string DisplayName,
    Guid? OrganizationId,
    string? OrganizationName,
    bool IsEnabled,
    string? Description,
    DateTime? LastLoginTime,
    IReadOnlyList<EmployeeDirectoryRole>? Roles = null);

public sealed record EmployeeDirectoryOrganization(Guid Id, string Name);
public sealed record EmployeeDirectoryRole(Guid Id, string Name);

public interface IEmployeeDirectoryRepository
{
    IReadOnlyList<EmployeeDirectoryEntry> List();
    IReadOnlyList<EmployeeDirectoryOrganization> ListOrganizations();
    IReadOnlyList<EmployeeDirectoryRole> ListRoles() => [];
}

public sealed class EmployeeDirectoryService(IEmployeeDirectoryRepository repository)
{
    public IReadOnlyList<EmployeeDirectoryEntry> List(
        string? keyword = null,
        Guid? organizationId = null,
        EmployeeDirectoryStatus status = EmployeeDirectoryStatus.Enabled)
    {
        var text = keyword?.Trim();
        var query = repository.List().AsEnumerable();

        if (!string.IsNullOrWhiteSpace(text))
        {
            query = query.Where(item =>
                item.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase)
                || item.Username.Contains(text, StringComparison.OrdinalIgnoreCase)
                || (item.OrganizationName?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.Description?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (organizationId is Guid selectedOrganizationId && selectedOrganizationId != Guid.Empty)
            query = query.Where(item => item.OrganizationId == selectedOrganizationId);

        query = status switch
        {
            EmployeeDirectoryStatus.Enabled => query.Where(item => item.IsEnabled),
            EmployeeDirectoryStatus.Disabled => query.Where(item => !item.IsEnabled),
            _ => query
        };

        return query
            .OrderBy(item => item.OrganizationName ?? "")
            .ThenBy(item => item.DisplayName)
            .ThenBy(item => item.Username)
            .ToArray();
    }

    public IReadOnlyList<EmployeeDirectoryOrganization> ListOrganizations() =>
        repository.ListOrganizations()
            .Where(item => item.Id != Guid.Empty && !string.IsNullOrWhiteSpace(item.Name))
            .OrderBy(item => item.Name)
            .ToArray();

    public IReadOnlyList<EmployeeDirectoryRole> ListRoles() =>
        repository.ListRoles()
            .Where(item => item.Id != Guid.Empty && !string.IsNullOrWhiteSpace(item.Name))
            .OrderBy(item => item.Name)
            .ToArray();

    public int Count(EmployeeDirectoryStatus status = EmployeeDirectoryStatus.Enabled) => List(status: status).Count;
}
