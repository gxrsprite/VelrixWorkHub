using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmpWeeklyWorkLogSubmissionServiceTests
{
    [Fact]
    public void ListForMember_OnlyReturnsTheNamedMembersWeeklySnapshots()
    {
        var projectId = Guid.CreateVersion7();
        var monday = new DateOnly(2026, 7, 20);
        var mine = new PmpWeeklyWorkLogSubmission(projectId, "项目经理", monday, "[{\"hours\":8}]", 8);
        var other = new PmpWeeklyWorkLogSubmission(projectId, "实施顾问", monday, "[{\"hours\":6}]", 6);
        var repository = new SubmissionRepository(mine, other);
        var service = new PmpWeeklyWorkLogSubmissionService(repository, new PmpWorkLogService(new WorkLogRepository(), new ProjectRepository(), new TaskRepository(), new MemberRepository()), new MemberRepository());

        var result = service.ListForMember(projectId, " 项目经理 ");

        Assert.Single(result);
        Assert.Same(mine, result[0]);
    }

    [Fact]
    public void Submit_FreezesWbsTitleStartsWorkflowAndRejectsDuplicateWeek()
    {
        var projectId = Guid.CreateVersion7();
        var monday = new DateOnly(2026, 7, 20);
        var member = new PmpProjectMember(projectId, "项目经理", "项目经理");
        var task = new PmpWbsTask(projectId, null, "完成接口联调", "项目经理", 1, monday, monday.AddDays(6), false);
        var logs = new WorkLogRepository(new PmpWorkLog(projectId, task.Id, monday, member.MemberName, 8m, "联调"));
        var submissions = new SubmissionRepository();
        var binding = CreatePublishedBinding();
        var service = new PmpWeeklyWorkLogSubmissionService(
            submissions,
            new PmpWorkLogService(logs, new ProjectRepository(CreateProject(projectId, monday)), new TaskRepository(task), new MemberRepository(member)),
            new MemberRepository(member),
            new TaskRepository(task),
            binding);

        var submitted = service.Submit(projectId, " 项目经理 ", monday, "project.manager");

        var snapshot = Assert.Single(service.GetSnapshot(submitted));
        Assert.Equal("完成接口联调", snapshot.WbsTaskTitle);
        Assert.Equal(8m, submitted.TotalHours);
        Assert.Equal(PmpWeeklyWorkLogSubmissionStatus.Submitted, submitted.Status);
        Assert.Single(binding.List(nameof(PmpWeeklyWorkLogSubmission), submitted.Id));
        Assert.Throws<InvalidOperationException>(() => service.Submit(projectId, member.MemberName, monday, "project.manager"));
    }

    [Fact]
    public void Submit_RejectsNonMemberAndMissingWorkflowWithoutPersistingSubmission()
    {
        var projectId = Guid.CreateVersion7();
        var monday = new DateOnly(2026, 7, 20);
        var member = new PmpProjectMember(projectId, "项目经理", "项目经理");
        var submissions = new SubmissionRepository();
        var workLogs = new PmpWorkLogService(new WorkLogRepository(), new ProjectRepository(CreateProject(projectId, monday)), new TaskRepository(), new MemberRepository(member));
        var service = new PmpWeeklyWorkLogSubmissionService(submissions, workLogs, new MemberRepository(member));

        Assert.Throws<InvalidOperationException>(() => service.Submit(projectId, "非项目成员", monday, "outsider"));
        Assert.Throws<InvalidOperationException>(() => service.Submit(projectId, member.MemberName, monday, "project.manager"));
        Assert.Empty(submissions.List(projectId));
    }

    [Fact]
    public void Submit_WhenNoPublishedWorkflowExists_CompensatesPersistedSubmission()
    {
        var projectId = Guid.CreateVersion7();
        var monday = new DateOnly(2026, 7, 20);
        var member = new PmpProjectMember(projectId, "项目经理", "项目经理");
        var task = new PmpWbsTask(projectId, null, "填写周报", "项目经理", 1, monday, monday.AddDays(6), false);
        var submissions = new SubmissionRepository();
        var service = new PmpWeeklyWorkLogSubmissionService(
            submissions,
            new PmpWorkLogService(new WorkLogRepository(new PmpWorkLog(projectId, task.Id, monday, member.MemberName, 8m, null)), new ProjectRepository(CreateProject(projectId, monday)), new TaskRepository(task), new MemberRepository(member)),
            new MemberRepository(member), new TaskRepository(task),
            new WorkflowBindingService(new WorkflowDefinitionService(new DefinitionRepository()), new WorkflowInstanceService(new InstanceRepository())));

        Assert.Throws<InvalidOperationException>(() => service.Submit(projectId, member.MemberName, monday, "project.manager"));
        Assert.Empty(submissions.List(projectId));
    }

    [Fact]
    public void Submit_AfterRejectedWeekPreservesHistoryAndCreatesFreshApprovalSnapshot()
    {
        var projectId = Guid.CreateVersion7();
        var monday = new DateOnly(2026, 7, 20);
        var member = new PmpProjectMember(projectId, "项目经理", "项目经理");
        var task = new PmpWbsTask(projectId, null, "补充交付说明", "项目经理", 1, monday, monday.AddDays(6), false);
        var rejected = new PmpWeeklyWorkLogSubmission(projectId, member.MemberName, monday, "[{\"hours\":6}]", 6m);
        rejected.Submit("project.manager", new DateTime(2026, 7, 21, 9, 0, 0));
        rejected.Reject("请补充交付说明。");
        var submissions = new SubmissionRepository(rejected);
        var service = new PmpWeeklyWorkLogSubmissionService(
            submissions,
            new PmpWorkLogService(new WorkLogRepository(new PmpWorkLog(projectId, task.Id, monday, member.MemberName, 8m, "已补充说明")), new ProjectRepository(CreateProject(projectId, monday)), new TaskRepository(task), new MemberRepository(member)),
            new MemberRepository(member), new TaskRepository(task), CreatePublishedBinding());

        var resubmitted = service.Submit(projectId, member.MemberName, monday, "project.manager");

        Assert.Equal(PmpWeeklyWorkLogSubmissionStatus.Rejected, rejected.Status);
        Assert.Equal("请补充交付说明。", rejected.RejectionReason);
        Assert.Equal(PmpWeeklyWorkLogSubmissionStatus.Submitted, resubmitted.Status);
        Assert.Equal(8m, resubmitted.TotalHours);
        Assert.Equal(2, submissions.List(projectId).Count);
    }

    [Fact]
    public void SubmitForProjectMember_UsesStableMemberUserIdAndRejectsOtherUsers()
    {
        var projectId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var monday = new DateOnly(2026, 7, 20);
        var member = new PmpProjectMember(projectId, "项目经理", "项目经理", userId: userId);
        var task = new PmpWbsTask(projectId, null, "填写周报", "项目经理", 1, monday, monday.AddDays(6), false);
        var submissions = new SubmissionRepository();
        var service = new PmpWeeklyWorkLogSubmissionService(
            submissions,
            new PmpWorkLogService(new WorkLogRepository(new PmpWorkLog(projectId, task.Id, monday, member.MemberName, 8m, null)), new ProjectRepository(CreateProject(projectId, monday)), new TaskRepository(task), new MemberRepository(member)),
            new MemberRepository(member), new TaskRepository(task), CreatePublishedBinding());

        Assert.Throws<UnauthorizedAccessException>(() => service.SubmitForProjectMember(projectId, Guid.CreateVersion7(), monday, "other.user"));
        var submitted = service.SubmitForProjectMember(projectId, userId, monday, "project.manager");

        Assert.Equal(member.MemberName, submitted.MemberName);
        Assert.Single(submissions.List(projectId));
    }

    [Fact]
    public void ListForProjectMember_UsesStableUserIdInsteadOfCallerSuppliedName()
    {
        var projectId = Guid.CreateVersion7();
        var memberUserId = Guid.CreateVersion7();
        var mine = new PmpWeeklyWorkLogSubmission(projectId, "项目经理", new DateOnly(2026, 7, 20), "[{\"hours\":8}]", 8m);
        var repository = new SubmissionRepository(mine);
        var member = new PmpProjectMember(projectId, "项目经理", "项目经理", userId: memberUserId);
        var service = new PmpWeeklyWorkLogSubmissionService(repository, new PmpWorkLogService(new WorkLogRepository(), new ProjectRepository(), new TaskRepository(), new MemberRepository(member)), new MemberRepository(member));

        Assert.Single(service.ListForProjectMember(projectId, memberUserId));
        Assert.Empty(service.ListForProjectMember(projectId, Guid.CreateVersion7()));
    }

    [Fact]
    public void WorkflowHistory_ReturnsOnlyTheLatestMeaningfulDecisionForSubmission()
    {
        var submission = new PmpWeeklyWorkLogSubmission(Guid.CreateVersion7(), "项目经理", new DateOnly(2026, 7, 20), "[{\"hours\":8}]", 8m);
        var instanceId = Guid.CreateVersion7();
        var operations = new OperationRepository(
            new WorkflowOperation(instanceId, null, null, nameof(PmpWeeklyWorkLogSubmission), submission.Id, WorkflowOperationKind.Started, "project.manager", null, "提交", "started", new DateTime(2026, 7, 21, 9, 0, 0)),
            new WorkflowOperation(instanceId, null, null, nameof(PmpWeeklyWorkLogSubmission), submission.Id, WorkflowOperationKind.Rejected, "admin", null, "请补充说明", "rejected", new DateTime(2026, 7, 21, 10, 0, 0)));
        var history = new PmpWeeklyWorkLogSubmissionWorkflowHistoryService(new WorkflowOperationService(operations));

        var decision = history.GetLatestDecision(submission);

        Assert.NotNull(decision);
        Assert.Equal(WorkflowOperationKind.Rejected, decision.Kind);
        Assert.Equal("请补充说明", decision.Comment);
    }

    private static PmpProject CreateProject(Guid projectId, DateOnly monday)
    {
        var project = new PmpProject("PMP-WORKLOG", "周工时项目", null, null, monday, monday.AddDays(6)) { Id = projectId };
        project.SetStatus(PmpProjectStatus.Active);
        return project;
    }

    private static WorkflowBindingService CreatePublishedBinding()
    {
        var definitions = new WorkflowDefinitionService(new DefinitionRepository());
        var definition = definitions.CreateDraft(WorkflowBindingCodes.PmpWeeklyWorkLogApproval, "周工时审批");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id); definition.Connect(approval.Id, end.Id);
        definitions.Publish(definition);
        return new WorkflowBindingService(definitions, new WorkflowInstanceService(new InstanceRepository()));
    }

    private sealed class SubmissionRepository(params PmpWeeklyWorkLogSubmission[] items) : IPmpWeeklyWorkLogSubmissionRepository
    { private readonly List<PmpWeeklyWorkLogSubmission> data = [.. items]; public IReadOnlyList<PmpWeeklyWorkLogSubmission> List(Guid? projectId = null) => projectId is Guid id ? data.Where(x => x.ProjectId == id).ToArray() : data; public void Add(PmpWeeklyWorkLogSubmission item) => data.Add(item); public void Update(PmpWeeklyWorkLogSubmission item) { } public void Remove(Guid id) => data.RemoveAll(x => x.Id == id); }
    private sealed class WorkLogRepository(params PmpWorkLog[] items) : IPmpWorkLogRepository { private readonly List<PmpWorkLog> data = [.. items]; public IReadOnlyList<PmpWorkLog> List(Guid? projectId = null) => projectId is Guid id ? data.Where(x => x.ProjectId == id).ToArray() : data; public void Add(PmpWorkLog item) => data.Add(item); public void Update(PmpWorkLog item) { } public void Remove(Guid id) => data.RemoveAll(x => x.Id == id); }
    private sealed class ProjectRepository(params PmpProject[] items) : IPmpProjectRepository { private readonly List<PmpProject> data = [.. items]; public IReadOnlyList<PmpProject> List() => data; public void Add(PmpProject item) => data.Add(item); public void Update(PmpProject item) { } public void Remove(Guid id) => data.RemoveAll(x => x.Id == id); }
    private sealed class TaskRepository(params PmpWbsTask[] items) : IPmpWbsTaskRepository { private readonly List<PmpWbsTask> data = [.. items]; public IReadOnlyList<PmpWbsTask> List(Guid? projectId = null) => projectId is Guid id ? data.Where(x => x.ProjectId == id).ToArray() : data; public void Add(PmpWbsTask item) => data.Add(item); public void Update(PmpWbsTask item) { } public void Remove(Guid id) => data.RemoveAll(x => x.Id == id); }
    private sealed class MemberRepository(params PmpProjectMember[] items) : IPmpProjectMemberRepository { private readonly List<PmpProjectMember> data = [.. items]; public IReadOnlyList<PmpProjectMember> List(Guid? projectId = null) => projectId is Guid id ? data.Where(x => x.ProjectId == id).ToArray() : data; public void Add(PmpProjectMember item) => data.Add(item); public void Update(PmpProjectMember item) { } public void Remove(Guid id) => data.RemoveAll(x => x.Id == id); }
    private sealed class DefinitionRepository : IWorkflowDefinitionRepository { private readonly List<WorkflowDefinition> data = []; public IReadOnlyList<WorkflowDefinition> List(string? code = null, WorkflowDefinitionStatus? status = null) => data.Where(x => code is null || x.Code.Equals(code, StringComparison.OrdinalIgnoreCase)).Where(x => status is null || x.Status == status).ToArray(); public void Add(WorkflowDefinition item) => data.Add(item); public bool TryAdd(WorkflowDefinition item) { if (data.Any(x => x.Id == item.Id)) return false; Add(item); return true; } public void Update(WorkflowDefinition item) { } public void Remove(Guid id) => data.RemoveAll(x => x.Id == id); }
    private sealed class InstanceRepository : IWorkflowInstanceRepository { private readonly List<WorkflowInstance> data = []; public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null) => data.Where(x => businessType is null || x.BusinessType == businessType).Where(x => businessId is null || x.BusinessId == businessId).Where(x => status is null || x.Status == status).ToArray(); public void Add(WorkflowInstance item) => data.Add(item); public bool TryAdd(WorkflowInstance item) { if (data.Any(x => x.Id == item.Id)) return false; Add(item); return true; } public void Update(WorkflowInstance item) { } public bool TryUpdate(WorkflowInstance item) { item.MarkPersistedRevision(item.Revision + 1); return true; } }
    private sealed class OperationRepository(params WorkflowOperation[] items) : IWorkflowOperationRepository { private readonly List<WorkflowOperation> data = [.. items]; public IReadOnlyList<WorkflowOperation> List(Guid? instanceId = null, string? businessType = null, Guid? businessId = null, WorkflowOperationKind? kind = null) => data.Where(x => instanceId is null || x.InstanceId == instanceId).Where(x => businessType is null || x.BusinessType == businessType).Where(x => businessId is null || x.BusinessId == businessId).Where(x => kind is null || x.Kind == kind).ToArray(); public WorkflowOperation? FindByDedupeKey(string dedupeKey) => data.FirstOrDefault(x => x.DedupeKey == dedupeKey); public void Add(WorkflowOperation item) => data.Add(item); public bool TryAdd(WorkflowOperation item) { if (FindByDedupeKey(item.DedupeKey) is not null) return false; Add(item); return true; } }
}
