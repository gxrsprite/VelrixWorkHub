using FreeSql;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.PmpProjects;
public static class PmpProjectSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<PmpProjectRecord>(); fsql.CodeFirst.SyncStructure<PmpProjectStatusHistoryRecord>(); if (fsql.Select<PmpProjectRecord>().Any()) return;
        var customer = fsql.Select<VelrixWorkHub.Infrastructure.Customers.CustomerRecord>().First();
        var item = new PmpProject("PRJ-001", "客户交付一期项目", customer?.Id, "项目经理", DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(60))); item.SetStatus(PmpProjectStatus.Active); var now = DateTime.Now;
        fsql.Insert(new PmpProjectRecord { Id = item.Id, Code = item.Code, Name = item.Name, CustomerId = item.CustomerId, ManagerName = item.ManagerName, PlannedStart = item.PlannedStart.ToDateTime(TimeOnly.MinValue), PlannedEnd = item.PlannedEnd.ToDateTime(TimeOnly.MinValue), PercentComplete = item.PercentComplete, Status = item.Status, CreatedTime = now, ModifiedTime = now }).ExecuteAffrows();
    }
}
