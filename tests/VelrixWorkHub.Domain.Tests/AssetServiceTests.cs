using VelrixWorkHub.Application.Assets;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class AssetServiceTests
{
    [Fact]
    public void Create_RequiresPermissionAndUniqueAssetNo()
    {
        var service = CreateService();
        Assert.Throws<UnauthorizedAccessException>(() => service.Create("AST-001", "IT设备", "笔记本", null, null, null, false));
        service.Create("AST-001", "IT设备", "笔记本", "SN-001", "一号工位", null, true);
        Assert.Throws<InvalidOperationException>(() => service.Create("ast-001", "办公用品", "重复", null, null, null, true));
    }

    [Fact]
    public void AssignAndReturn_ChangesAssetStateAndPreservesAssignmentHistory()
    {
        var service = CreateService();
        var userId = Guid.CreateVersion7();
        var asset = service.Create("AST-002", "办公用品", "显示器", null, "会议室", null, true);

        var assignment = service.Assign(asset, userId, true);

        Assert.Equal(OaAssetStatus.InUse, asset.Status);
        Assert.Equal(userId, asset.ResponsibleUserId);
        Assert.Throws<InvalidOperationException>(() => service.Assign(asset, Guid.CreateVersion7(), true));
        service.Return(assignment, true);

        Assert.Equal(OaAssetStatus.Available, asset.Status);
        Assert.Null(asset.ResponsibleUserId);
        Assert.Equal(OaAssetAssignmentStatus.Returned, assignment.Status);
        Assert.Single(service.ListAssignments(asset.Id));
    }

    [Fact]
    public void InUseAsset_CannotBeEditedOrMovedToMaintenance()
    {
        var service = CreateService();
        var asset = service.Create("AST-003", "固定资产", "工控机", null, null, null, true);
        service.Assign(asset, Guid.CreateVersion7(), true);

        Assert.Throws<InvalidOperationException>(() => service.Edit(asset, asset.AssetNo, asset.Category, "修改名称", null, null, null, true));
        Assert.Throws<InvalidOperationException>(() => service.SetStatus(asset, OaAssetStatus.Maintenance, true));
    }

    [Fact]
    public void Return_RequiresManagePermission()
    {
        var service = CreateService();
        var asset = service.Create("AST-004", "办公用品", "键盘", null, null, null, true);
        var assignment = service.Assign(asset, Guid.CreateVersion7(), true);

        Assert.Throws<UnauthorizedAccessException>(() => service.Return(assignment, false));
        Assert.Equal(OaAssetAssignmentStatus.Active, assignment.Status);
    }

    [Fact]
    public void Return_RollsBackAssetAndAssignmentWhenHistoryWriteFails()
    {
        var assets = new AssetRepository();
        var assignments = new AssignmentRepository { ThrowOnUpdate = true };
        var service = new AssetService(assets, assignments, new RollbackTransactionBoundary());
        var asset = service.Create("AST-005", "办公用品", "键盘", null, null, null, true);
        var assignment = service.Assign(asset, Guid.CreateVersion7(), true);
        assignments.ThrowOnUpdate = true;

        Assert.Throws<InvalidOperationException>(() => service.Return(assignment, true));
        Assert.Equal(OaAssetStatus.InUse, asset.Status);
        Assert.Equal(OaAssetAssignmentStatus.Active, assignment.Status);
        Assert.Equal(assignment.UserId, asset.ResponsibleUserId);
    }

    [Fact]
    public void AssetLifecycle_AppendsImmutableOperationTrail()
    {
        var operations = new OperationRepository();
        var service = new AssetService(new AssetRepository(), new AssignmentRepository(), null, operations);
        var asset = service.Create("AST-006", "IT设备", "显示器", null, null, null, true);
        var assignment = service.Assign(asset, Guid.CreateVersion7(), true);
        service.Return(assignment, true);

        Assert.Equal(
            [OaAssetOperationKind.Returned, OaAssetOperationKind.Assigned, OaAssetOperationKind.Created],
            service.ListOperations(asset.Id).Select(item => item.Kind).ToArray());
        Assert.All(service.ListOperations(asset.Id), item => Assert.Equal(asset.Id, item.AssetId));
    }

    [Fact]
    public void Transfer_RecordsLocationAndImmutableHistory()
    {
        var transfers = new TransferRepository();
        var operations = new OperationRepository();
        var service = new AssetService(new AssetRepository(), new AssignmentRepository(), null, operations, transfers);
        var asset = service.Create("AST-007", "IT设备", "显示器", null, "一号工位", null, true);

        service.Transfer(asset, null, "二号工位", "部门调整", "admin", true);

        var transfer = Assert.Single(service.ListTransfers(asset.Id));
        Assert.Equal("一号工位", transfer.FromLocation);
        Assert.Equal("二号工位", transfer.ToLocation);
        Assert.Equal("部门调整", transfer.Reason);
        Assert.Equal("二号工位", asset.Location);
        Assert.Equal(OaAssetOperationKind.Transferred, Assert.Single(service.ListOperations(asset.Id), item => item.Kind == OaAssetOperationKind.Transferred).Kind);
    }

    [Fact]
    public void InUseTransfer_PreservesResponsibleUserAndRejectsOwnerChange()
    {
        var service = new AssetService(new AssetRepository(), new AssignmentRepository());
        var asset = service.Create("AST-008", "固定资产", "笔记本", null, "一号工位", null, true);
        var userId = Guid.CreateVersion7();
        service.Assign(asset, userId, true);

        service.Transfer(asset, null, "三号工位", "搬迁", "admin", true);

        Assert.Equal(userId, asset.ResponsibleUserId);
        Assert.Equal("三号工位", asset.Location);
        Assert.Throws<InvalidOperationException>(() => service.Transfer(asset, Guid.CreateVersion7(), "四号工位", "换人", "admin", true));
    }

    [Fact]
    public void Transfer_RollsBackAssetWhenHistoryWriteFails()
    {
        var transfers = new TransferRepository { ThrowOnAdd = true };
        var service = new AssetService(new AssetRepository(), new AssignmentRepository(), new RollbackTransactionBoundary(), null, transfers);
        var asset = service.Create("AST-009", "办公用品", "键盘", null, "原位置", null, true);

        Assert.Throws<InvalidOperationException>(() => service.Transfer(asset, null, "新位置", "搬迁", "admin", true));
        Assert.Equal("原位置", asset.Location);
        Assert.Empty(service.ListTransfers(asset.Id));
    }

    [Fact]
    public void Transfer_RollsBackTransferRecordWhenOperationWriteFails()
    {
        var transfers = new TransferRepository();
        var operations = new OperationRepository();
        var service = new AssetService(new AssetRepository(), new AssignmentRepository(), new RollbackTransactionBoundary(), operations, transfers);
        var asset = service.Create("AST-010", "办公用品", "鼠标", null, "原位置", null, true);
        operations.ThrowOnAdd = true;

        Assert.Throws<InvalidOperationException>(() => service.Transfer(asset, null, "新位置", "搬迁", "admin", true));
        Assert.Equal("原位置", asset.Location);
        Assert.Empty(service.ListTransfers(asset.Id));
    }

    [Fact]
    public void Stocktake_MatchedRecordsSnapshotWithoutChangingAsset()
    {
        var stocktakes = new StocktakeRepository();
        var operations = new OperationRepository();
        var service = new AssetService(new AssetRepository(), new AssignmentRepository(), null, operations, null, stocktakes);
        var asset = service.Create("AST-011", "固定资产", "显示器", null, "一号工位", null, true);

        var result = service.Stocktake(asset, OaAssetStatus.Available, null, "一号工位", null, "admin", "{}", true);

        Assert.Equal(OaAssetStocktakeResult.Matched, result.Result);
        Assert.Equal(OaAssetStatus.Available, asset.Status);
        Assert.Equal("一号工位", asset.Location);
        Assert.Equal(OaAssetOperationKind.Stocktaken, Assert.Single(service.ListOperations(asset.Id), item => item.Kind == OaAssetOperationKind.Stocktaken).Kind);
    }

    [Fact]
    public void Stocktake_DifferenceRequiresReasonAndPreservesLedger()
    {
        var stocktakes = new StocktakeRepository();
        var service = new AssetService(new AssetRepository(), new AssignmentRepository(), null, null, null, stocktakes);
        var asset = service.Create("AST-012", "IT设备", "笔记本", null, "一号工位", null, true);

        Assert.Throws<ArgumentException>(() => service.Stocktake(asset, OaAssetStatus.Maintenance, null, "维修间", null, "admin", null, true));
        var result = service.Stocktake(asset, OaAssetStatus.Maintenance, null, "维修间", "盘点发现已送修", "admin", null, true);

        Assert.Equal(OaAssetStocktakeResult.Difference, result.Result);
        Assert.Equal(OaAssetStatus.Available, asset.Status);
        Assert.Single(service.ListStocktakes(asset.Id));
    }

    [Fact]
    public void Stocktake_MissingRequiresReasonAndRecordsMissingResult()
    {
        var stocktakes = new StocktakeRepository();
        var service = new AssetService(new AssetRepository(), new AssignmentRepository(), null, null, null, stocktakes);
        var asset = service.Create("AST-013", "办公用品", "键盘", null, "仓库", null, true);

        Assert.Throws<ArgumentException>(() => service.Stocktake(asset, null, null, null, null, "admin", null, true));
        var result = service.Stocktake(asset, null, null, null, "现场未找到", "admin", null, true);

        Assert.Equal(OaAssetStocktakeResult.Missing, result.Result);
        Assert.Equal("现场未找到", result.Reason);
    }

    [Fact]
    public void Stocktake_RollsBackRecordWhenOperationWriteFails()
    {
        var stocktakes = new StocktakeRepository();
        var operations = new OperationRepository();
        var service = new AssetService(new AssetRepository(), new AssignmentRepository(), new RollbackTransactionBoundary(), operations, null, stocktakes);
        var asset = service.Create("AST-014", "办公用品", "鼠标", null, "仓库", null, true);
        operations.ThrowOnAdd = true;

        Assert.Throws<InvalidOperationException>(() => service.Stocktake(asset, OaAssetStatus.Available, null, "仓库", null, "admin", null, true));
        Assert.Empty(service.ListStocktakes(asset.Id));
    }

    [Fact]
    public void ResolveStocktake_ClosesDifferenceOnceAndPreservesOriginalSnapshot()
    {
        var stocktakes = new StocktakeRepository();
        var operations = new OperationRepository();
        var service = new AssetService(new AssetRepository(), new AssignmentRepository(), null, operations, null, stocktakes);
        var asset = service.Create("ASSET-ST-RESOLVE", "IT设备", "测试设备", null, "一号工位", "{}", true);
        var stocktake = service.Stocktake(asset, OaAssetStatus.Maintenance, null, "维修间", "盘点发现已送修", "admin", "{}", true);

        service.ResolveStocktake(stocktake, "已核对维修单，等待归还后更新台账。", "asset-admin", true);

        Assert.Equal(OaAssetStatus.Available, stocktake.ExpectedStatus);
        Assert.Equal(OaAssetStatus.Maintenance, stocktake.ActualStatus);
        Assert.Equal("asset-admin", stocktake.ResolvedBy);
        Assert.NotNull(stocktake.ResolvedAt);
        Assert.Contains(service.ListOperations(asset.Id), item => item.Kind == OaAssetOperationKind.StocktakeResolved);
        Assert.Throws<InvalidOperationException>(() => service.ResolveStocktake(stocktake, "重复处置", "asset-admin", true));
    }

    private static AssetService CreateService() => new(new AssetRepository(), new AssignmentRepository());

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
        public bool ThrowOnUpdate { get; set; }
        public IReadOnlyList<OaAssetAssignment> List(Guid? assetId = null, Guid? userId = null) => items.Where(item => (!assetId.HasValue || item.AssetId == assetId) && (!userId.HasValue || item.UserId == userId)).ToArray();
        public OaAssetAssignment? Get(Guid id) => items.FirstOrDefault(item => item.Id == id);
        public OaAssetAssignment? GetActive(Guid assetId) => items.FirstOrDefault(item => item.AssetId == assetId && item.Status == OaAssetAssignmentStatus.Active);
        public void Add(OaAssetAssignment assignment) => items.Add(assignment);
        public void Update(OaAssetAssignment assignment)
        {
            if (ThrowOnUpdate) throw new InvalidOperationException("模拟领用历史写入失败。");
        }
    }

    private sealed class OperationRepository : IOaAssetOperationRepository
    {
        private readonly List<OaAssetOperation> items = [];
        public bool ThrowOnAdd { get; set; }
        public IReadOnlyList<OaAssetOperation> List(Guid assetId) => items.Where(item => item.AssetId == assetId).ToArray();
        public void Add(OaAssetOperation operation)
        {
            if (ThrowOnAdd) throw new InvalidOperationException("模拟资产操作流水写入失败。");
            items.Add(operation);
        }
    }

    private sealed class TransferRepository : IOaAssetTransferRepository
    {
        private readonly List<OaAssetTransfer> items = [];
        public bool ThrowOnAdd { get; set; }
        public IReadOnlyList<OaAssetTransfer> List(Guid assetId) => items.Where(item => item.AssetId == assetId).ToArray();
        public void Add(OaAssetTransfer transfer)
        {
            if (ThrowOnAdd) throw new InvalidOperationException("模拟资产转移流水写入失败。");
            items.Add(transfer);
        }
        public void Remove(Guid transferId) => items.RemoveAll(item => item.Id == transferId);
    }

    private sealed class StocktakeRepository : IOaAssetStocktakeRepository
    {
        private readonly List<OaAssetStocktake> items = [];
        public IReadOnlyList<OaAssetStocktake> List(Guid assetId) => items.Where(item => item.AssetId == assetId).ToArray();
        public void Add(OaAssetStocktake stocktake) => items.Add(stocktake);
        public void Update(OaAssetStocktake stocktake) { }
        public void Remove(Guid stocktakeId) => items.RemoveAll(item => item.Id == stocktakeId);
    }

    private sealed class RollbackTransactionBoundary : IWorkflowTransactionBoundary
    {
        public void Execute(Action operation, Action<Exception>? afterRollback = null)
        {
            try { operation(); }
            catch (Exception exception) { afterRollback?.Invoke(exception); throw; }
        }
    }
}
