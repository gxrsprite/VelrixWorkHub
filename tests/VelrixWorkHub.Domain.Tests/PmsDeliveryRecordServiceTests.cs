using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmsDeliveryRecordServiceTests
{
    [Fact]
    public void DeliveryRecordService_TracksRequirementWbsAndStatusHistory()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmsProject("PRJ-DELIVERY", "交付项目", null, null, today, today.AddDays(30));
        var requirement = new PmsRequirement(project.Id, null, null, "REQ-DELIVERY", false, "产品负责人", PmsRequirementPriority.High, PmsRequirementType.Functional, today, null, null, "支持导出交付报告", null, "项目经理", "{}");
        var wbs = new PmsWbsTask(project.Id, null, "实现导出", "开发负责人", 1, today, today.AddDays(5), false);
        var records = new DeliveryRepository();
        var histories = new HistoryRepository();
        var service = new PmsDeliveryRecordService(records, histories, new ProjectRepository(project), new RequirementRepository(requirement), new WbsRepository(wbs));

        var defect = service.Create(project.Id, requirement.Id, wbs.Id, "BUG-001", PmsDeliveryRecordType.Defect, "导出文件缺少表头", "回归发现", "开发负责人", null, null, null, "{}", "admin");
        service.SetStatus(defect, PmsDeliveryRecordStatus.InProgress, "开始修复", "开发负责人");
        service.SetStatus(defect, PmsDeliveryRecordStatus.Resolved, "已修复", "开发负责人");
        service.SetStatus(defect, PmsDeliveryRecordStatus.Closed, "测试通过", "测试负责人");

        Assert.Equal(PmsDeliveryRecordStatus.Closed, defect.Status);
        Assert.Equal(4, service.ListHistory(defect.Id).Count);
        Assert.Throws<InvalidOperationException>(() => service.Edit(defect, requirement.Id, wbs.Id, "BUG-001", PmsDeliveryRecordType.Defect, defect.Title, null, null, null, null, null, "{}"));
    }

    [Fact]
    public void DeliveryRecordService_RequiresReviewConclusionReleaseResultAndSameProjectSources()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var project = new PmsProject("PRJ-DELIVERY-A", "交付项目 A", null, null, today, today.AddDays(30));
        var otherProject = new PmsProject("PRJ-DELIVERY-B", "交付项目 B", null, null, today, today.AddDays(30));
        var requirement = new PmsRequirement(project.Id, null, null, "REQ-A", false, "提出人", PmsRequirementPriority.Medium, PmsRequirementType.Functional, today, null, null, "需求", null, null, "{}");
        var otherWbs = new PmsWbsTask(otherProject.Id, null, "其他任务", null, 1, today, today.AddDays(2), false);
        var service = new PmsDeliveryRecordService(new DeliveryRepository(), new HistoryRepository(), new ProjectRepository(project, otherProject), new RequirementRepository(requirement), new WbsRepository(otherWbs));

        Assert.Throws<InvalidOperationException>(() => service.Create(project.Id, requirement.Id, otherWbs.Id, "BUG-CROSS", PmsDeliveryRecordType.Defect, "跨项目引用", null, null, null, null, null, "{}", "admin"));
        Assert.Throws<ArgumentException>(() => service.Create(project.Id, null, null, "REL-EMPTY", PmsDeliveryRecordType.Release, "未填版本", null, null, null, null, null, "{}", "admin"));
        var review = service.Create(project.Id, requirement.Id, null, "REV-001", PmsDeliveryRecordType.Review, "需求评审", null, "项目经理", null, null, null, "{}", "admin");
        Assert.Throws<ArgumentException>(() => service.SetStatus(review, PmsDeliveryRecordStatus.Passed, "缺少结论", "项目经理"));
        service.Edit(review, requirement.Id, null, "REV-001", PmsDeliveryRecordType.Review, "需求评审", null, "项目经理", "通过，进入开发。", null, null, "{}");
        service.SetStatus(review, PmsDeliveryRecordStatus.Passed, "评审通过", "项目经理");
        var release = service.Create(project.Id, null, null, "REL-001", PmsDeliveryRecordType.Release, "首个发布", null, "发布负责人", null, "1.0.0", null, "{}", "admin");
        Assert.Throws<ArgumentException>(() => service.SetStatus(release, PmsDeliveryRecordStatus.Released, "缺少发布结果", "发布负责人"));
    }

    private sealed class ProjectRepository(params PmsProject[] data) : IPmsProjectRepository { public IReadOnlyList<PmsProject> List() => data; public void Add(PmsProject item) { } public void Update(PmsProject item) { } public void Remove(Guid id) { } }
    private sealed class RequirementRepository(params PmsRequirement[] data) : IPmsRequirementRepository { public IReadOnlyList<PmsRequirement> List(Guid? projectId = null) => projectId is Guid id ? data.Where(x => x.ProjectId == id).ToArray() : data; public void Add(PmsRequirement item) { } public void Update(PmsRequirement item) { } public void Remove(Guid id) { } }
    private sealed class WbsRepository(params PmsWbsTask[] data) : IPmsWbsTaskRepository { public IReadOnlyList<PmsWbsTask> List(Guid? projectId = null) => projectId is Guid id ? data.Where(x => x.ProjectId == id).ToArray() : data; public void Add(PmsWbsTask item) { } public void Update(PmsWbsTask item) { } public void Remove(Guid id) { } }
    private sealed class DeliveryRepository : IPmsDeliveryRecordRepository { private readonly List<PmsDeliveryRecord> data = []; public IReadOnlyList<PmsDeliveryRecord> List(Guid? projectId = null) => projectId is Guid id ? data.Where(x => x.ProjectId == id).ToArray() : data; public void Add(PmsDeliveryRecord item) => data.Add(item); public void Update(PmsDeliveryRecord item) { } public void Remove(Guid id) => data.RemoveAll(x => x.Id == id); }
    private sealed class HistoryRepository : IPmsDeliveryRecordStatusHistoryRepository { private readonly List<PmsDeliveryRecordStatusHistory> data = []; public IReadOnlyList<PmsDeliveryRecordStatusHistory> List(Guid deliveryRecordId) => data.Where(x => x.DeliveryRecordId == deliveryRecordId).ToArray(); public void Add(PmsDeliveryRecordStatusHistory item) => data.Add(item); }
}
