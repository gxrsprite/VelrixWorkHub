using FreeSql;
using VelrixWorkHub.Application.Assets;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Assets;

public sealed class FreeSqlAssetStocktakeRepository(IFreeSql fsql) : IOaAssetStocktakeRepository
{
    public IReadOnlyList<OaAssetStocktake> List(Guid assetId)
        => fsql.Select<OaAssetStocktakeRecord>().Where(item => item.AssetId == assetId)
            .OrderByDescending(item => item.StocktakenAt).ToList().Select(ToDomain).ToArray();

    public void Add(OaAssetStocktake stocktake)
        => fsql.Insert(new OaAssetStocktakeRecord
        {
            Id = stocktake.Id, AssetId = stocktake.AssetId, ExpectedStatus = stocktake.ExpectedStatus,
            ActualStatus = stocktake.ActualStatus, ExpectedResponsibleUserId = stocktake.ExpectedResponsibleUserId,
            ActualResponsibleUserId = stocktake.ActualResponsibleUserId, ExpectedLocation = stocktake.ExpectedLocation,
            ActualLocation = stocktake.ActualLocation, Result = stocktake.Result, Reason = stocktake.Reason,
            ActorName = stocktake.ActorName, OtherInfo = stocktake.OtherInfo, StocktakenAt = stocktake.StocktakenAt,
            Resolution = stocktake.Resolution, ResolvedBy = stocktake.ResolvedBy, ResolvedAt = stocktake.ResolvedAt
        }).ExecuteAffrows();

    public void Update(OaAssetStocktake stocktake)
        => fsql.Update<OaAssetStocktakeRecord>().Set(item => item.Resolution, stocktake.Resolution)
            .Set(item => item.ResolvedBy, stocktake.ResolvedBy).Set(item => item.ResolvedAt, stocktake.ResolvedAt)
            .Where(item => item.Id == stocktake.Id).ExecuteAffrows();

    public void Remove(Guid stocktakeId)
        => fsql.Delete<OaAssetStocktakeRecord>().Where(item => item.Id == stocktakeId).ExecuteAffrows();

    private static OaAssetStocktake ToDomain(OaAssetStocktakeRecord item)
        => OaAssetStocktake.Restore(item.Id, item.AssetId, item.ExpectedStatus, item.ActualStatus,
            item.ExpectedResponsibleUserId, item.ActualResponsibleUserId, item.ExpectedLocation, item.ActualLocation,
            item.Result, item.Reason, item.ActorName, item.OtherInfo, item.StocktakenAt,
            item.Resolution, item.ResolvedBy, item.ResolvedAt);
}
