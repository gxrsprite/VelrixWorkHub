using FreeSql;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.PmsProjects;
public static class PmsProjectIssueSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<PmsProjectIssueRecord>(); if (fsql.Select<PmsProjectIssueRecord>().Any()) return; var project = fsql.Select<PmsProjectRecord>().OrderBy(x => x.CreatedTime).First(); if (project is null) return; var today = DateOnly.FromDateTime(DateTime.Today); var risk = new PmsProjectIssue(project.Id, PmsProjectIssueKind.Risk, "客户需求确认延期风险", "若需求确认延期，将影响方案评审节点。", "项目经理", PmsProjectIssuePriority.High, today.AddDays(7)); var issue = new PmsProjectIssue(project.Id, PmsProjectIssueKind.Issue, "待确认接口负责人", "需要客户指定接口联调负责人。", "业务负责人", PmsProjectIssuePriority.Medium, today.AddDays(10)); var now = DateTime.Now; fsql.Insert(new[] { risk, issue }.Select(x => new PmsProjectIssueRecord { Id = x.Id, ProjectId = x.ProjectId, Kind = x.Kind, Title = x.Title, Description = x.Description, OwnerName = x.OwnerName, Priority = x.Priority, Status = x.Status, DueDate = x.DueDate?.ToDateTime(TimeOnly.MinValue), CreatedTime = now, ModifiedTime = now })).ExecuteAffrows();
    }
}
