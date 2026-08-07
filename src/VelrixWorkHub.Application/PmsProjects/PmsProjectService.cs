using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.PmsProjects;
public sealed class PmsProjectService(IPmsProjectRepository repository, IPmsProjectStatusHistoryRepository? statusHistory = null)
{
    public IReadOnlyList<PmsProject> List(string? keyword = null, PmsProjectStatus? status = null)
    {
        var query = repository.List().AsEnumerable(); var text = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(x => x.Code.Contains(text, StringComparison.OrdinalIgnoreCase) || x.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || (x.ManagerName?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        if (status is not null) query = query.Where(x => x.Status == status);
        return query.ToArray();
    }
    public PmsProject Create(string code, string name, Guid? customerId, string? managerName, DateOnly start, DateOnly end) { var item = new PmsProject(code, name, customerId, managerName, start, end); EnsureUnique(item); repository.Add(item); return item; }
    public PmsProject CreateDetailed(string code, string name, Guid? customerId, string? managerName, DateOnly start, DateOnly end, PmsProjectInitiationMode initiationMode, string? projectAlias, string? projectChineseName, string? projectEnglishName, string? productName, string? projectStage, string? productLine, string? projectCategory, string? projectSubcategory, string? projectSubcategoryCode, string? versionType, string? projectVersion, DateOnly? expectedInitiationDate, DateOnly? actualInitiationDate, string? developmentMode, string? departmentName, string? domainManagerName, string? businessInitiatorName, string? overview, string? objective, string? otherInfo)
    { var item = new PmsProject(code, name, customerId, managerName, start, end, initiationMode, projectAlias, projectChineseName, projectEnglishName, productName, projectStage, productLine, projectCategory, projectSubcategory, projectSubcategoryCode, versionType, projectVersion, expectedInitiationDate, actualInitiationDate, developmentMode, departmentName, domainManagerName, businessInitiatorName, overview, objective, otherInfo); EnsureUnique(item); repository.Add(item); return item; }
    public void Edit(PmsProject item, string code, string name, Guid? customerId, string? managerName, DateOnly start, DateOnly end) { item.Edit(code, name, customerId, managerName, start, end); EnsureUnique(item); repository.Update(item); }
    public void EditDetailed(PmsProject item, string code, string name, Guid? customerId, string? managerName, DateOnly start, DateOnly end, PmsProjectInitiationMode initiationMode, string? projectAlias, string? projectChineseName, string? projectEnglishName, string? productName, string? projectStage, string? productLine, string? projectCategory, string? projectSubcategory, string? projectSubcategoryCode, string? versionType, string? projectVersion, DateOnly? expectedInitiationDate, DateOnly? actualInitiationDate, string? developmentMode, string? departmentName, string? domainManagerName, string? businessInitiatorName, string? overview, string? objective, string? otherInfo)
    { item.Edit(code, name, customerId, managerName, start, end); item.EditDetails(initiationMode, projectAlias, projectChineseName, projectEnglishName, productName, projectStage, productLine, projectCategory, projectSubcategory, projectSubcategoryCode, versionType, projectVersion, expectedInitiationDate, actualInitiationDate, developmentMode, departmentName, domainManagerName, businessInitiatorName, overview, objective, otherInfo); EnsureUnique(item); repository.Update(item); }
    public void SetStatus(PmsProject item, PmsProjectStatus status) { item.SetStatus(status); repository.Update(item); }
    public void ChangeStatus(PmsProject item, PmsProjectStatus status, string reason, string actorName)
    { var history = new PmsProjectStatusHistory(item.Id, item.Status, status, reason, actorName, DateTime.Now); item.SetStatus(status); repository.Update(item); statusHistory?.Add(history); }
    public IReadOnlyList<PmsProjectStatusHistory> ListStatusHistory(Guid projectId) => statusHistory?.List(projectId) ?? Array.Empty<PmsProjectStatusHistory>();
    public void SetPercentComplete(PmsProject item, int percent) { item.SetPercentComplete(percent); repository.Update(item); }
    public void Remove(PmsProject item) => repository.Remove(item.Id);
    private void EnsureUnique(PmsProject item) { if (repository.List().Any(x => x.Id != item.Id && x.Code.Equals(item.Code, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("项目编号已存在。"); }
}
