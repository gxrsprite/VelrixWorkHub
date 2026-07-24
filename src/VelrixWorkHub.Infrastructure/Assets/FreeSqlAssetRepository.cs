using FreeSql;
using VelrixWorkHub.Application.Assets;
using VelrixWorkHub.Domain;
using AssetDomain = VelrixWorkHub.Domain.OaAsset;
using AssignmentDomain = VelrixWorkHub.Domain.OaAssetAssignment;

namespace VelrixWorkHub.Infrastructure.Assets;

public sealed class FreeSqlAssetRepository(IFreeSql fsql) : IOaAssetRepository, IOaAssetAssignmentRepository, IOaAssetOperationRepository
{
    public IReadOnlyList<AssetDomain> List() => fsql.Select<OaAssetRecord>().OrderBy(item => item.AssetNo).ToList().Select(ToDomain).ToArray();
    public AssetDomain? Get(Guid id) => fsql.Select<OaAssetRecord>().Where(item => item.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(AssetDomain asset) => fsql.Insert(ToRecord(asset)).ExecuteAffrows();

    public void Update(AssetDomain asset)
    {
        var rows = fsql.Update<OaAssetRecord>()
            .Set(item => item.AssetNo, asset.AssetNo).Set(item => item.Category, asset.Category).Set(item => item.Name, asset.Name)
            .Set(item => item.SerialNo, asset.SerialNo).Set(item => item.ResponsibleUserId, asset.ResponsibleUserId).Set(item => item.Location, asset.Location)
            .Set(item => item.Status, asset.Status).Set(item => item.OtherInfo, asset.OtherInfo).Set(item => item.UpdatedAt, asset.UpdatedAt)
            .Where(item => item.Id == asset.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("资产不存在或已被删除。");
    }

    public IReadOnlyList<AssignmentDomain> List(Guid? assetId = null, Guid? userId = null)
    {
        var query = fsql.Select<OaAssetAssignmentRecord>();
        if (assetId is Guid selectedAssetId) query = query.Where(item => item.AssetId == selectedAssetId);
        if (userId is Guid selectedUserId) query = query.Where(item => item.UserId == selectedUserId);
        return query.OrderByDescending(item => item.AssignedAt).ToList().Select(ToDomain).ToArray();
    }

    AssignmentDomain? IOaAssetAssignmentRepository.Get(Guid id) => fsql.Select<OaAssetAssignmentRecord>().Where(item => item.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public AssignmentDomain? GetActive(Guid assetId) => fsql.Select<OaAssetAssignmentRecord>().Where(item => item.AssetId == assetId && item.Status == OaAssetAssignmentStatus.Active).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(AssignmentDomain assignment) => fsql.Insert(ToRecord(assignment)).ExecuteAffrows();

    public void Update(AssignmentDomain assignment)
    {
        var rows = fsql.Update<OaAssetAssignmentRecord>()
            .Set(item => item.Status, assignment.Status).Set(item => item.ReturnedAt, assignment.ReturnedAt)
            .Where(item => item.Id == assignment.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("资产领用记录不存在或已被删除。");
    }

    public IReadOnlyList<OaAssetOperation> List(Guid assetId)
        => fsql.Select<OaAssetOperationRecord>().Where(item => item.AssetId == assetId)
            .OrderByDescending(item => item.OccurredAt).ToList().Select(ToDomain).ToArray();

    public void Add(OaAssetOperation operation)
        => fsql.Insert(new OaAssetOperationRecord
        {
            Id = operation.Id, AssetId = operation.AssetId, AssignmentId = operation.AssignmentId, Kind = operation.Kind,
            FromStatus = operation.FromStatus, ToStatus = operation.ToStatus, RelatedUserId = operation.RelatedUserId,
            ActorName = operation.ActorName, Note = operation.Note, OccurredAt = operation.OccurredAt
        }).ExecuteAffrows();

    private static AssetDomain ToDomain(OaAssetRecord item)
    {
        var asset = new AssetDomain(item.AssetNo, item.Category, item.Name, item.SerialNo, item.Location, item.OtherInfo, item.CreatedAt) { Id = item.Id };
        if (item.ResponsibleUserId is Guid userId) asset.Assign(userId, item.UpdatedAt);
        if (item.Status != OaAssetStatus.InUse) asset.SetStatus(item.Status, item.UpdatedAt);
        return asset;
    }

    private static AssignmentDomain ToDomain(OaAssetAssignmentRecord item)
    {
        var assignment = new AssignmentDomain(item.AssetId, item.UserId, item.AssignedAt) { Id = item.Id };
        if (item.Status == OaAssetAssignmentStatus.Returned) assignment.Return(item.ReturnedAt ?? item.AssignedAt);
        return assignment;
    }

    private static OaAssetOperation ToDomain(OaAssetOperationRecord item)
        => OaAssetOperation.Restore(item.Id, item.AssetId, item.Kind, item.AssignmentId, item.FromStatus, item.ToStatus,
            item.RelatedUserId, item.ActorName, item.Note, item.OccurredAt);

    private static OaAssetRecord ToRecord(AssetDomain item) => new()
    {
        Id = item.Id, AssetNo = item.AssetNo, Category = item.Category, Name = item.Name, SerialNo = item.SerialNo,
        ResponsibleUserId = item.ResponsibleUserId, Location = item.Location, Status = item.Status, OtherInfo = item.OtherInfo,
        CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt
    };

    private static OaAssetAssignmentRecord ToRecord(AssignmentDomain item) => new()
    {
        Id = item.Id, AssetId = item.AssetId, UserId = item.UserId, Status = item.Status, AssignedAt = item.AssignedAt, ReturnedAt = item.ReturnedAt
    };
}
