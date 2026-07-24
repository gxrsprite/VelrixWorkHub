using FreeSql;
using VelrixWorkHub.Application.Assets;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Assets;

public sealed class FreeSqlAssetTransferRepository(IFreeSql fsql) : IOaAssetTransferRepository
{
    public IReadOnlyList<OaAssetTransfer> List(Guid assetId)
        => fsql.Select<OaAssetTransferRecord>().Where(item => item.AssetId == assetId)
            .OrderByDescending(item => item.TransferredAt).ToList().Select(ToDomain).ToArray();

    public void Add(OaAssetTransfer transfer)
        => fsql.Insert(new OaAssetTransferRecord
        {
            Id = transfer.Id, AssetId = transfer.AssetId, FromUserId = transfer.FromUserId, ToUserId = transfer.ToUserId,
            FromLocation = transfer.FromLocation, ToLocation = transfer.ToLocation, Reason = transfer.Reason,
            ActorName = transfer.ActorName, TransferredAt = transfer.TransferredAt
        }).ExecuteAffrows();

    public void Remove(Guid transferId)
        => fsql.Delete<OaAssetTransferRecord>().Where(item => item.Id == transferId).ExecuteAffrows();

    private static OaAssetTransfer ToDomain(OaAssetTransferRecord item)
        => OaAssetTransfer.Restore(item.Id, item.AssetId, item.FromUserId, item.ToUserId, item.FromLocation,
            item.ToLocation, item.Reason, item.ActorName, item.TransferredAt);
}
