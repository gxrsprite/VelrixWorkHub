using FreeSql;
using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.PmpProjects;
public sealed class FreeSqlPmpProjectRepository(IFreeSql fsql) : IPmpProjectRepository
{
    public IReadOnlyList<PmpProject> List() => fsql.Select<PmpProjectRecord>().OrderByDescending(x => x.CreatedTime).ToList().Select(ToDomain).ToArray();
    public void Add(PmpProject item) { var now = DateTime.Now; fsql.Insert(ToRecord(item, now, now)).ExecuteAffrows(); }
    public void Update(PmpProject item) { var rows = fsql.Update<PmpProjectRecord>().SetSource(ToRecord(item, null, DateTime.Now)).IgnoreColumns(x => x.CreatedTime).Where(x => x.Id == item.Id).ExecuteAffrows(); if (rows == 0) throw new InvalidOperationException("项目不存在或已被删除。"); }
    public void Remove(Guid id) => fsql.Delete<PmpProjectRecord>().Where(x => x.Id == id).ExecuteAffrows();
    private static PmpProject ToDomain(PmpProjectRecord x)
    {
        var item = new PmpProject(x.Code, x.Name, x.CustomerId, x.ManagerName, DateOnly.FromDateTime(x.PlannedStart), DateOnly.FromDateTime(x.PlannedEnd), x.InitiationMode ?? PmpProjectInitiationMode.PreInitiation, x.ProjectAlias, x.ProjectChineseName, x.ProjectEnglishName, x.ProductName, x.ProjectStage, x.ProductLine, x.ProjectCategory, x.ProjectSubcategory, x.ProjectSubcategoryCode, x.VersionType, x.ProjectVersion, x.ExpectedInitiationDate is DateTime expected ? DateOnly.FromDateTime(expected) : null, x.ActualInitiationDate is DateTime actual ? DateOnly.FromDateTime(actual) : null, x.DevelopmentMode, x.DepartmentName, x.DomainManagerName, x.BusinessInitiatorName, x.Overview, x.Objective, x.OtherInfo) { Id = x.Id };
        item.SetStatus(x.Status); item.SetPercentComplete(x.PercentComplete); return item;
    }
    private static PmpProjectRecord ToRecord(PmpProject x, DateTime? created, DateTime modified) => new() { Id = x.Id, Code = x.Code, Name = x.Name, CustomerId = x.CustomerId, ManagerName = x.ManagerName, PlannedStart = x.PlannedStart.ToDateTime(TimeOnly.MinValue), PlannedEnd = x.PlannedEnd.ToDateTime(TimeOnly.MinValue), PercentComplete = x.PercentComplete, Status = x.Status, InitiationMode = x.InitiationMode, ProjectAlias = x.ProjectAlias, ProjectChineseName = x.ProjectChineseName, ProjectEnglishName = x.ProjectEnglishName, ProductName = x.ProductName, ProjectStage = x.ProjectStage, ProductLine = x.ProductLine, ProjectCategory = x.ProjectCategory, ProjectSubcategory = x.ProjectSubcategory, ProjectSubcategoryCode = x.ProjectSubcategoryCode, VersionType = x.VersionType, ProjectVersion = x.ProjectVersion, ExpectedInitiationDate = x.ExpectedInitiationDate?.ToDateTime(TimeOnly.MinValue), ActualInitiationDate = x.ActualInitiationDate?.ToDateTime(TimeOnly.MinValue), DevelopmentMode = x.DevelopmentMode, DepartmentName = x.DepartmentName, DomainManagerName = x.DomainManagerName, BusinessInitiatorName = x.BusinessInitiatorName, Overview = x.Overview, Objective = x.Objective, OtherInfo = x.OtherInfo, CreatedTime = created ?? modified, ModifiedTime = modified };
}
