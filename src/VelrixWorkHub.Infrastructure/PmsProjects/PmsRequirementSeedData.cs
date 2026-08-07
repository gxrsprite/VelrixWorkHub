using FreeSql;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

public static class PmsRequirementSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<PmsRequirementRecord>();
        if (fsql.Select<PmsRequirementRecord>().Any()) return;
        var project = fsql.Select<PmsProjectRecord>().OrderBy(x => x.CreatedTime).First();
        if (project is null) return;
        var product = fsql.Select<VelrixWorkHub.Infrastructure.Products.ProductRecord>().OrderBy(x => x.CreatedTime).First();
        var item = new PmsRequirement(project.Id, product?.Id, null, "REQ-001", true, "业务负责人", PmsRequirementPriority.High, PmsRequirementType.Functional, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(14)), DateOnly.FromDateTime(DateTime.Today.AddDays(21)), "支持客户按项目查看订单履约状态。", "减少项目经理跨模块核对成本。", "项目经理", "{}");
        var now = DateTime.Now;
        fsql.Insert(new PmsRequirementRecord { Id = item.Id, ProjectId = item.ProjectId, ProductId = item.ProductId, RequirementNo = item.RequirementNo, IsHighlighted = item.IsHighlighted, Proposer = item.Proposer, OwnerName = item.OwnerName, Priority = item.Priority, Status = item.Status, RequirementType = item.RequirementType, ProposedDate = item.ProposedDate.ToDateTime(TimeOnly.MinValue), DesiredCompletionDate = item.DesiredCompletionDate?.ToDateTime(TimeOnly.MinValue), PlannedCompletionDate = item.PlannedCompletionDate?.ToDateTime(TimeOnly.MinValue), Description = item.Description, BackgroundValue = item.BackgroundValue, OtherInfo = item.OtherInfo, CreatedTime = now, ModifiedTime = now }).ExecuteAffrows();
    }
}
