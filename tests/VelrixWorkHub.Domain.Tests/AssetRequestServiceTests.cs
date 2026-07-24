using VelrixWorkHub.Application.Assets;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class AssetRequestServiceTests
{
    [Fact]
    public void ApprovalCreatesAssignmentAndLocksAsset()
    {
        var fixture = CreateFixture();
        var userId = Guid.CreateVersion7();
        var request = fixture.Service.Create(userId, "alice", fixture.Asset.Id, "入职办公需要", "{}");
        request.Submit(DateTime.Now);
        fixture.Requests.Update(request);

        fixture.Service.ApplyApproval(request, "admin");

        Assert.Equal(OaAssetRequestStatus.Approved, request.Status);
        Assert.Equal(OaAssetStatus.InUse, fixture.Asset.Status);
        Assert.Equal(userId, fixture.Asset.ResponsibleUserId);
        Assert.Equal(request.AssignmentId, fixture.Assignments.List(fixture.Asset.Id).Single().Id);
    }

    [Fact]
    public void ApprovalRejectsDuplicatePendingRequestForSameAsset()
    {
        var fixture = CreateFixture();
        var first = fixture.Service.Create(Guid.CreateVersion7(), "alice", fixture.Asset.Id, "第一申请", "{}");
        first.Submit(DateTime.Now);
        fixture.Requests.Update(first);
        var second = fixture.Service.Create(Guid.CreateVersion7(), "bob", fixture.Asset.Id, "第二申请", "{}");
        second.Submit(DateTime.Now);
        fixture.Requests.Update(second);

        Assert.Throws<InvalidOperationException>(() => fixture.Service.ApplyApproval(second, "admin"));
        Assert.Equal(OaAssetRequestStatus.Submitted, second.Status);
        Assert.Equal(OaAssetStatus.Available, fixture.Asset.Status);
    }

    [Fact]
    public void RejectedRequestCanBeEditedAndResubmittedForApproval()
    {
        var fixture = CreateFixture();
        var userId = Guid.CreateVersion7();
        var request = fixture.Service.Create(userId, "alice", fixture.Asset.Id, "原申请", "{}");
        request.Submit(DateTime.Now);
        fixture.Requests.Update(request);
        fixture.Service.ApplyRejection(request, "请补充使用场景");

        fixture.Service.Edit(request, userId, "alice", "补充后的申请", "{\"scene\":\"onboarding\"}");
        request.Submit(DateTime.Now);
        fixture.Requests.Update(request);
        fixture.Service.ApplyApproval(request, "admin");

        Assert.Equal(OaAssetRequestStatus.Approved, request.Status);
        Assert.Equal("补充后的申请", request.Reason);
        Assert.Equal("{\"scene\":\"onboarding\"}", request.OtherInfo);
    }

    [Fact]
    public void RequestOperationsRequireApplicantOwnership()
    {
        var fixture = CreateFixture();
        var owner = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        var request = fixture.Service.Create(owner, "alice", fixture.Asset.Id, "申请", "{}");

        Assert.Throws<UnauthorizedAccessException>(() => fixture.Service.Edit(request, other, "other", "越权", "{}"));
        Assert.Throws<UnauthorizedAccessException>(() => fixture.Service.SubmitAndStartWorkflow(request, other, "other"));
    }

    [Fact]
    public void WorkflowApprovalHandlerLocksAssetOnlyAfterApprovedAction()
    {
        var fixture = CreateFixture();
        var request = fixture.Service.Create(Guid.CreateVersion7(), "alice", fixture.Asset.Id, "审批领用", "{}");
        request.Submit(DateTime.Now);
        fixture.Requests.Update(request);
        var definition = new WorkflowDefinition("ASSET_TEST", "资产测试", 1, "测试");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始", 0, 0);
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", 100, 0, "{\"approver\":\"admin\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束", 200, 0);
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        var instance = WorkflowInstance.Start(definition, nameof(OaAssetRequest), request.Id, startedBy: "alice");
        var handler = new AssetRequestWorkflowActionHandler(fixture.Requests, fixture.Service);

        handler.Execute(new WorkflowActionContext(instance, WorkflowActionTrigger.Approved, null, "admin"),
            new WorkflowActionDefinition(WorkflowActionType.SetField, nameof(OaAssetRequest.Status), nameof(OaAssetRequestStatus.Approved)));

        Assert.Equal(OaAssetRequestStatus.Approved, request.Status);
        Assert.Equal(OaAssetStatus.InUse, fixture.Asset.Status);
    }

    private static Fixture CreateFixture()
    {
        var assetRepository = new AssetRepository();
        var assignments = new AssignmentRepository();
        var assetService = new AssetService(assetRepository, assignments);
        var asset = assetService.Create("AST-REQ-001", "IT设备", "笔记本", null, "一号工位", "{}", true);
        var requests = new RequestRepository();
        return new Fixture(new AssetRequestService(requests, assetService), asset, assignments, requests);
    }

    private sealed record Fixture(AssetRequestService Service, OaAsset Asset, AssignmentRepository Assignments, RequestRepository Requests);

    private sealed class AssetRepository : IOaAssetRepository
    {
        private readonly List<OaAsset> items = [];
        public IReadOnlyList<OaAsset> List() => items;
        public OaAsset? Get(Guid id) => items.FirstOrDefault(item => item.Id == id);
        public void Add(OaAsset asset) => items.Add(asset);
        public void Update(OaAsset asset) { }
    }

    private sealed class AssignmentRepository : IOaAssetAssignmentRepository
    {
        private readonly List<OaAssetAssignment> items = [];
        public IReadOnlyList<OaAssetAssignment> List(Guid? assetId = null, Guid? userId = null) => items.Where(item => (!assetId.HasValue || item.AssetId == assetId) && (!userId.HasValue || item.UserId == userId)).ToArray();
        public OaAssetAssignment? Get(Guid id) => items.FirstOrDefault(item => item.Id == id);
        public OaAssetAssignment? GetActive(Guid assetId) => items.FirstOrDefault(item => item.AssetId == assetId && item.Status == OaAssetAssignmentStatus.Active);
        public void Add(OaAssetAssignment assignment) => items.Add(assignment);
        public void Update(OaAssetAssignment assignment) { }
    }

    private sealed class RequestRepository : IOaAssetRequestRepository
    {
        private readonly List<OaAssetRequest> items = [];
        public IReadOnlyList<OaAssetRequest> List(Guid? applicantUserId = null, Guid? assetId = null) => items.Where(item => (!applicantUserId.HasValue || item.ApplicantUserId == applicantUserId) && (!assetId.HasValue || item.AssetId == assetId)).ToArray();
        public OaAssetRequest? Get(Guid id) => items.FirstOrDefault(item => item.Id == id);
        public void Add(OaAssetRequest request) => items.Add(request);
        public void Update(OaAssetRequest request) { }
    }
}
