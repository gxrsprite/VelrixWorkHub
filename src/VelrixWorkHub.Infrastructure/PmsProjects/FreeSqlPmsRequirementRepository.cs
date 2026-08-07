using FreeSql;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

public sealed class FreeSqlPmsRequirementRepository(IFreeSql fsql) : IPmsRequirementRepository
{
    public IReadOnlyList<PmsRequirement> List(Guid? projectId = null)
    {
        var query = fsql.Select<PmsRequirementRecord>();
        if (projectId is not null) query = query.Where(x => x.ProjectId == projectId);
        return query.OrderBy(x => x.Status).OrderByDescending(x => x.IsHighlighted).OrderByDescending(x => x.Priority).OrderByDescending(x => x.ProposedDate).ToList().Select(ToDomain).ToArray();
    }

    public void Add(PmsRequirement item) { var now = DateTime.Now; fsql.Insert(ToRecord(item, now, now)).ExecuteAffrows(); }
    public void Update(PmsRequirement item) { var rows = fsql.Update<PmsRequirementRecord>().SetSource(ToRecord(item, null, DateTime.Now)).IgnoreColumns(x => x.CreatedTime).Where(x => x.Id == item.Id).ExecuteAffrows(); if (rows == 0) throw new InvalidOperationException("需求不存在或已被删除。"); }
    public void Remove(Guid id) => fsql.Delete<PmsRequirementRecord>().Where(x => x.Id == id).ExecuteAffrows();

    private static PmsRequirement ToDomain(PmsRequirementRecord x) => PmsRequirement.Restore(x.Id, x.ProjectId, x.ProductId, x.BaselineId, x.RequirementNo, x.IsHighlighted, x.Proposer, x.Priority, x.Status, x.RequirementType, DateOnly.FromDateTime(x.ProposedDate), x.DesiredCompletionDate is DateTime desired ? DateOnly.FromDateTime(desired) : null, x.PlannedCompletionDate is DateTime planned ? DateOnly.FromDateTime(planned) : null, x.Description, x.BackgroundValue, x.OwnerName, x.OtherInfo);
    private static PmsRequirementRecord ToRecord(PmsRequirement x, DateTime? created, DateTime modified) => new() { Id = x.Id, ProjectId = x.ProjectId, ProductId = x.ProductId, BaselineId = x.BaselineId, RequirementNo = x.RequirementNo, IsHighlighted = x.IsHighlighted, Proposer = x.Proposer, OwnerName = x.OwnerName, Priority = x.Priority, Status = x.Status, RequirementType = x.RequirementType, ProposedDate = x.ProposedDate.ToDateTime(TimeOnly.MinValue), DesiredCompletionDate = x.DesiredCompletionDate?.ToDateTime(TimeOnly.MinValue), PlannedCompletionDate = x.PlannedCompletionDate?.ToDateTime(TimeOnly.MinValue), Description = x.Description, BackgroundValue = x.BackgroundValue, OtherInfo = x.OtherInfo, CreatedTime = created ?? modified, ModifiedTime = modified };
}
