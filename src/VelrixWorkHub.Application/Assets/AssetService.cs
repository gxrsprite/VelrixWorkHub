using VelrixWorkHub.Domain;
using VelrixWorkHub.Application.Workflow;

namespace VelrixWorkHub.Application.Assets;

public interface IOaAssetRepository
{
    IReadOnlyList<OaAsset> List();
    OaAsset? Get(Guid id);
    void Add(OaAsset asset);
    void Update(OaAsset asset);
}

public interface IOaAssetAssignmentRepository
{
    IReadOnlyList<OaAssetAssignment> List(Guid? assetId = null, Guid? userId = null);
    OaAssetAssignment? Get(Guid id);
    OaAssetAssignment? GetActive(Guid assetId);
    void Add(OaAssetAssignment assignment);
    void Update(OaAssetAssignment assignment);
}

public interface IOaAssetOperationRepository
{
    IReadOnlyList<OaAssetOperation> List(Guid assetId);
    void Add(OaAssetOperation operation);
}

public interface IOaAssetTransferRepository
{
    IReadOnlyList<OaAssetTransfer> List(Guid assetId);
    void Add(OaAssetTransfer transfer);
    void Remove(Guid transferId);
}

public interface IOaAssetStocktakeRepository
{
    IReadOnlyList<OaAssetStocktake> List(Guid assetId);
    void Add(OaAssetStocktake stocktake);
    void Update(OaAssetStocktake stocktake);
    void Remove(Guid stocktakeId);
}

public sealed class AssetService(
    IOaAssetRepository assets,
    IOaAssetAssignmentRepository assignments,
    IWorkflowTransactionBoundary? transactions = null,
    IOaAssetOperationRepository? operations = null,
    IOaAssetTransferRepository? transfers = null,
    IOaAssetStocktakeRepository? stocktakes = null)
{
    public IReadOnlyList<OaAsset> List() => assets.List().OrderBy(item => item.Status).ThenBy(item => item.AssetNo).ToArray();
    public OaAsset? Get(Guid id) => id == Guid.Empty ? null : assets.Get(id);
    public IReadOnlyList<OaAsset> ListByUser(Guid userId) => userId == Guid.Empty ? [] : assets.List().Where(item => item.ResponsibleUserId == userId).OrderBy(item => item.AssetNo).ToArray();
    public IReadOnlyList<OaAssetAssignment> ListAssignments(Guid? assetId = null, Guid? userId = null) => assignments.List(assetId, userId).OrderByDescending(item => item.AssignedAt).ToArray();
    public IReadOnlyList<OaAssetOperation> ListOperations(Guid assetId) => assetId == Guid.Empty || operations is null ? [] : operations.List(assetId).OrderByDescending(item => item.OccurredAt).ThenByDescending(item => item.Id).ToArray();
    public IReadOnlyList<OaAssetTransfer> ListTransfers(Guid assetId) => assetId == Guid.Empty || transfers is null ? [] : transfers.List(assetId).OrderByDescending(item => item.TransferredAt).ThenByDescending(item => item.Id).ToArray();
    public IReadOnlyList<OaAssetStocktake> ListStocktakes(Guid assetId) => assetId == Guid.Empty || stocktakes is null ? [] : stocktakes.List(assetId).OrderByDescending(item => item.StocktakenAt).ThenByDescending(item => item.Id).ToArray();

    public OaAsset Create(string assetNo, string category, string name, string? serialNo, string? location, string? otherInfo, bool canManage)
    {
        EnsureManage(canManage);
        if (assets.List().Any(item => item.AssetNo.Equals(assetNo.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("资产编号已存在。");
        var asset = new OaAsset(assetNo, category, name, serialNo, location, otherInfo, DateTime.Now);
        var operation = new OaAssetOperation(asset.Id, OaAssetOperationKind.Created, null, null, asset.Status, null, "system", "登记资产台账", asset.CreatedAt);
        void Core() { assets.Add(asset); operations?.Add(operation); }
        if (transactions is null) Core(); else transactions.Execute(Core);
        return asset;
    }

    public void Edit(OaAsset asset, string assetNo, string category, string name, string? serialNo, string? location, string? otherInfo, bool canManage)
    {
        EnsureManage(canManage);
        if (asset.Status == OaAssetStatus.InUse) throw new InvalidOperationException("在用资产不能编辑台账。");
        if (assets.List().Any(item => item.Id != asset.Id && item.AssetNo.Equals(assetNo.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("资产编号已存在。");
        var old = (asset.AssetNo, asset.Category, asset.Name, asset.SerialNo, asset.Location, asset.OtherInfo, asset.Status, asset.UpdatedAt);
        asset.Edit(assetNo, category, name, serialNo, location, otherInfo, DateTime.Now);
        var operation = new OaAssetOperation(asset.Id, OaAssetOperationKind.Edited, null, old.Status, asset.Status, asset.ResponsibleUserId, "system", "编辑资产台账", asset.UpdatedAt);
        void Core() { assets.Update(asset); operations?.Add(operation); }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ =>
        {
            asset.Edit(old.AssetNo, old.Category, old.Name, old.SerialNo, old.Location, old.OtherInfo, DateTime.Now);
            asset.SetStatus(old.Status, DateTime.Now);
        });
    }

    public OaAssetAssignment Assign(OaAsset asset, Guid userId, bool canManage)
    {
        EnsureManage(canManage);
        if (assignments.GetActive(asset.Id) is not null) throw new InvalidOperationException("资产已有未归还领用记录。");
        var assignedAt = DateTime.Now;
        var previousStatus = asset.Status;
        var previousResponsibleUserId = asset.ResponsibleUserId;
        var assignment = new OaAssetAssignment(asset.Id, userId, assignedAt);
        var operation = new OaAssetOperation(asset.Id, OaAssetOperationKind.Assigned, assignment.Id, previousStatus, OaAssetStatus.InUse, userId, userId.ToString(), "领用资产", assignedAt);
        void Core()
        {
            asset.Assign(userId, assignedAt);
            assets.Update(asset);
            assignments.Add(assignment);
            operations?.Add(operation);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => asset.RestoreAssignmentForRecovery(previousResponsibleUserId, previousStatus, DateTime.Now));
        return assignment;
    }

    public void Return(OaAssetAssignment assignment, bool canManage)
    {
        EnsureManage(canManage);
        if (assignment.Status != OaAssetAssignmentStatus.Active) throw new InvalidOperationException("当前资产领用记录不能归还。");
        var asset = assets.Get(assignment.AssetId) ?? throw new InvalidOperationException("资产不存在，不能归还。");
        var returnedAt = DateTime.Now;
        var previousStatus = asset.Status;
        var previousResponsibleUserId = asset.ResponsibleUserId;
        var previousAssignmentStatus = assignment.Status;
        var previousReturnedAt = assignment.ReturnedAt;
        var operation = new OaAssetOperation(asset.Id, OaAssetOperationKind.Returned, assignment.Id, previousStatus, OaAssetStatus.Available, assignment.UserId, assignment.UserId.ToString(), "归还资产", returnedAt);
        void Core()
        {
            asset.Return(returnedAt);
            assignment.Return(returnedAt);
            assets.Update(asset);
            assignments.Update(assignment);
            operations?.Add(operation);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ =>
        {
            asset.RestoreAssignmentForRecovery(previousResponsibleUserId, previousStatus, DateTime.Now);
            assignment.RestoreForRecovery(previousAssignmentStatus, previousReturnedAt);
        });
    }

    public void SetStatus(OaAsset asset, OaAssetStatus status, bool canManage)
    {
        EnsureManage(canManage);
        if (asset.Status == OaAssetStatus.InUse) throw new InvalidOperationException("在用资产必须通过归还操作改变状态。");
        if (asset.Status == status) return;
        var previousStatus = asset.Status;
        asset.SetStatus(status, DateTime.Now);
        var operation = new OaAssetOperation(asset.Id, OaAssetOperationKind.StatusChanged, null, previousStatus, status, asset.ResponsibleUserId, "system", $"资产状态变更为 {status}", asset.UpdatedAt);
        void Core() { assets.Update(asset); operations?.Add(operation); }
        if (transactions is null) Core(); else transactions.Execute(Core, _ => asset.SetStatus(previousStatus, DateTime.Now));
    }

    public void Transfer(OaAsset asset, Guid? toUserId, string? toLocation, string reason, string actorName, bool canManage)
    {
        EnsureManage(canManage);
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("转移原因不能为空。", nameof(reason));
        if (asset.Status is OaAssetStatus.Maintenance or OaAssetStatus.Retired)
            throw new InvalidOperationException("维修中或已报废资产不能转移。");
        var targetUserId = asset.Status == OaAssetStatus.InUse ? toUserId ?? asset.ResponsibleUserId : toUserId;
        if (asset.Status == OaAssetStatus.Available && targetUserId.HasValue)
            throw new InvalidOperationException("可用资产不能直接指定责任人，请通过领用申请或管理员领用操作分配。");
        if (asset.Status == OaAssetStatus.InUse && targetUserId != asset.ResponsibleUserId)
            throw new InvalidOperationException("在用资产暂不直接变更责任人，请先归还再重新领用；本操作可转移存放位置。");
        if (asset.ResponsibleUserId == targetUserId && string.Equals(asset.Location, toLocation?.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException("资产责任人和位置没有变化，无需转移。");

        var transferredAt = DateTime.Now;
        var fromUserId = asset.ResponsibleUserId;
        var fromLocation = asset.Location;
        asset.Transfer(targetUserId, toLocation, transferredAt);
        var transfer = new OaAssetTransfer(asset.Id, fromUserId, targetUserId, fromLocation, asset.Location, reason, actorName, transferredAt);
        var operation = new OaAssetOperation(asset.Id, OaAssetOperationKind.Transferred, null, asset.Status, asset.Status, targetUserId,
            actorName, $"转移资产：{reason.Trim()}；位置 {fromLocation ?? "未登记"} → {asset.Location ?? "未登记"}", transferredAt);
        void Core()
        {
            assets.Update(asset);
            transfers?.Add(transfer);
            operations?.Add(operation);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ =>
        {
            transfers?.Remove(transfer.Id);
            asset.Transfer(fromUserId, fromLocation, DateTime.Now);
        });
    }

    public OaAssetStocktake Stocktake(OaAsset asset, OaAssetStatus? actualStatus, Guid? actualResponsibleUserId,
        string? actualLocation, string? reason, string actorName, string? otherInfo, bool canManage)
    {
        EnsureManage(canManage);
        var stocktakenAt = DateTime.Now;
        var stocktake = new OaAssetStocktake(asset.Id, asset.Status, actualStatus, asset.ResponsibleUserId, actualResponsibleUserId,
            asset.Location, actualLocation, reason, actorName, otherInfo, stocktakenAt);
        var operation = new OaAssetOperation(asset.Id, OaAssetOperationKind.Stocktaken, null, asset.Status, actualStatus,
            actualResponsibleUserId, actorName, stocktake.Result switch
            {
                OaAssetStocktakeResult.Matched => "资产盘点一致",
                OaAssetStocktakeResult.Missing => $"资产盘点未找到：{stocktake.Reason}",
                _ => $"资产盘点存在差异：{stocktake.Reason}"
            }, stocktakenAt);
        void Core()
        {
            stocktakes?.Add(stocktake);
            operations?.Add(operation);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => stocktakes?.Remove(stocktake.Id));
        return stocktake;
    }

    public void ResolveStocktake(OaAssetStocktake stocktake, string resolution, string actorName, bool canManage)
    {
        EnsureManage(canManage);
        if (stocktakes is null) throw new InvalidOperationException("资产盘点处置存储未配置。");
        var old = (stocktake.Resolution, stocktake.ResolvedBy, stocktake.ResolvedAt);
        var resolvedAt = DateTime.Now;
        stocktake.Resolve(resolution, actorName, resolvedAt);
        var operation = new OaAssetOperation(stocktake.AssetId, OaAssetOperationKind.StocktakeResolved, null, null, null, null,
            actorName, $"盘点差异处置：{stocktake.Resolution}", resolvedAt);
        void Core() { stocktakes.Update(stocktake); operations?.Add(operation); }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ => stocktake.RestoreResolutionForRecovery(old.Resolution, old.ResolvedBy, old.ResolvedAt));
    }

    private static void EnsureManage(bool canManage)
    {
        if (!canManage) throw new UnauthorizedAccessException("当前用户没有维护资产的权限。");
    }
}

internal static class OaAssetRecoveryExtensions
{
    public static void RestoreAssignmentForRecovery(this OaAsset asset, Guid? responsibleUserId, OaAssetStatus status, DateTime updatedAt)
    {
        if (status == OaAssetStatus.InUse && responsibleUserId.HasValue)
            asset.Assign(responsibleUserId.Value, updatedAt);
        else if (asset.Status == OaAssetStatus.InUse)
            asset.Return(updatedAt);
        else
            asset.SetStatus(status, updatedAt);
    }

    public static void RestoreForRecovery(this OaAssetAssignment assignment, OaAssetAssignmentStatus status, DateTime? returnedAt)
        => assignment.RestoreForRecoveryState(status, returnedAt);
}
