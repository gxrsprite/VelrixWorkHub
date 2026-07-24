using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmpDeliveryRecordServiceTests
{
    [Fact]
    public void DeliveryRecordService_TracksRequirementWbsAndStatusHistory()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-DELIVERY", "交付项目", null, null, today, today.AddDays(30));
        var requirement = new PmpRequirement(project.Id, null, null, "REQ-DELIVERY", false, "产品负责人", PmpRequirementPriority.High, PmpRequirementType.Functional, today, null, null, "支持导出交付报告", null, "项目经理", "{}");
        var wbs = new PmpWbsTask(project.Id, null, "实现导出", "开发负责人", 1, today, today.AddDays(5), false);
        var records = new DeliveryRepository();
        var histories = new HistoryRepository();
        var service = new PmpDeliveryRecordService(records, histories, new ProjectRepository(project), new RequirementRepository(requirement), new WbsRepository(wbs));

        var defect = service.Create(project.Id, requirement.Id, wbs.Id, "BUG-001", PmpDeliveryRecordType.Defect, "导出文件缺少表头", "回归发现", "开发负责人", null, null, null, "{}", "admin");
        service.SetStatus(defect, PmpDeliveryRecordStatus.InProgress, "开始修复", "开发负责人");
        service.SetStatus(defect, PmpDeliveryRecordStatus.Resolved, "已修复", "开发负责人");
        service.SetStatus(defect, PmpDeliveryRecordStatus.Closed, "测试通过", "测试负责人");

        Assert.Equal(PmpDeliveryRecordStatus.Closed, defect.Status);
        Assert.Equal(4, service.ListHistory(defect.Id).Count);
        Assert.Throws<InvalidOperationException>(() => service.Edit(defect, requirement.Id, wbs.Id, "BUG-001", PmpDeliveryRecordType.Defect, defect.Title, null, null, null, null, null, "{}"));
    }

    [Fact]
    public void DeliveryRecordService_RequiresReviewConclusionReleaseResultAndSameProjectSources()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmpProject("PRJ-DELIVERY-A", "交付项目 A", null, null, today, today.AddDays(30));
        var otherProject = new PmpProject("PRJ-DELIVERY-B", "交付项目 B", null, null, today, today.AddDays(30));
        var requirement = new PmpRequirement(project.Id, null, null, "REQ-A", false, "提出人", PmpRequirementPriority.Medium, PmpRequirementType.Functional, today, null, null, "需求", null, null, "{}");
        var otherWbs = new PmpWbsTask(otherProject.Id, null, "其他任务", null, 1, today, today.AddDays(2), false);
        var service = new PmpDeliveryRecordService(new DeliveryRepository(), new HistoryRepository(), new ProjectRepository(project, otherProject), new RequirementRepository(requirement), new WbsRepository(otherWbs));

        Assert.Throws<InvalidOperationException>(() => service.Create(project.Id, requirement.Id, otherWbs.Id, "BUG-CROSS", PmpDeliveryRecordType.Defect, "跨项目引用", null, null, null, null, null, "{}", "admin"));
        Assert.Throws<ArgumentException>(() => service.Create(project.Id, null, null, "REL-EMPTY", PmpDeliveryRecordType.Release, "未填版本", null, null, null, null, null, "{}", "admin"));
        var review = service.Create(project.Id, requirement.Id, null, "REV-001", PmpDeliveryRecordType.Review, "需求评审", null, "项目经理", null, null, null, "{}", "admin");
        Assert.Throws<ArgumentException>(() => service.SetStatus(review, PmpDeliveryRecordStatus.Passed, "缺少结论", "项目经理"));
        service.Edit(review, requirement.Id, null, "REV-001", PmpDeliveryRecordType.Review, "需求评审", null, "项目经理", "通过，进入开发。", null, null, "{}");
        service.SetStatus(review, PmpDeliveryRecordStatus.Passed, "评审通过", "项目经理");
        var release = service.Create(project.Id, null, null, "REL-001", PmpDeliveryRecordType.Release, "首个发布", null, "发布负责人", null, "1.0.0", null, "{}", "admin");
        Assert.Throws<ArgumentException>(() => service.SetStatus(release, PmpDeliveryRecordStatus.Released, "缺少发布结果", "发布负责人"));
    }

    private sealed class ProjectRepository(params PmpProject[] data) : IPmpProjectRepository { public IReadOnlyList<PmpProject> List() => data; public void Add(PmpProject item) { } public void Update(PmpProject item) { } public void Remove(Guid id) { } }
    private sealed class RequirementRepository(params PmpRequirement[] data) : IPmpRequirementRepository { public IReadOnlyList<PmpRequirement> List(Guid? projectId = null) => projectId is Guid id ? data.Where(x => x.ProjectId == id).ToArray() : data; public void Add(PmpRequirement item) { } public void Update(PmpRequirement item) { } public void Remove(Guid id) { } }
    private sealed class WbsRepository(params PmpWbsTask[] data) : IPmpWbsTaskRepository { public IReadOnlyList<PmpWbsTask> List(Guid? projectId = null) => projectId is Guid id ? data.Where(x => x.ProjectId == id).ToArray() : data; public void Add(PmpWbsTask item) { } public void Update(PmpWbsTask item) { } public void Remove(Guid id) { } }
    private sealed class DeliveryRepository : IPmpDeliveryRecordRepository { private readonly List<PmpDeliveryRecord> data = []; public IReadOnlyList<PmpDeliveryRecord> List(Guid? projectId = null) => projectId is Guid id ? data.Where(x => x.ProjectId == id).ToArray() : data; public void Add(PmpDeliveryRecord item) => data.Add(item); public void Update(PmpDeliveryRecord item) { } public void Remove(Guid id) => data.RemoveAll(x => x.Id == id); }
    private sealed class HistoryRepository : IPmpDeliveryRecordStatusHistoryRepository { private readonly List<PmpDeliveryRecordStatusHistory> data = []; public IReadOnlyList<PmpDeliveryRecordStatusHistory> List(Guid deliveryRecordId) => data.Where(x => x.DeliveryRecordId == deliveryRecordId).ToArray(); public void Add(PmpDeliveryRecordStatusHistory item) => data.Add(item); }
}
