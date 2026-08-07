using FreeSql;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.PmsProjects;
public static class PmsProjectSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<PmsProjectRecord>(); fsql.CodeFirst.SyncStructure<PmsProjectStatusHistoryRecord>(); if (fsql.Select<PmsProjectRecord>().Any()) return;
        var customer = fsql.Select<VelrixWorkHub.Infrastructure.Customers.CustomerRecord>().First();
        var item = new PmsProject("PRJ-001", "客户交付一期项目", customer?.Id, "项目经理", DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(60))); item.SetStatus(PmsProjectStatus.Active); var now = DateTime.Now;
        fsql.Insert(new PmsProjectRecord { Id = item.Id, Code = item.Code, Name = item.Name, CustomerId = item.CustomerId, ManagerName = item.ManagerName, PlannedStart = item.PlannedStart.ToDateTime(TimeOnly.MinValue), PlannedEnd = item.PlannedEnd.ToDateTime(TimeOnly.MinValue), PercentComplete = item.PercentComplete, Status = item.Status, CreatedTime = now, ModifiedTime = now }).ExecuteAffrows();
    }
}
