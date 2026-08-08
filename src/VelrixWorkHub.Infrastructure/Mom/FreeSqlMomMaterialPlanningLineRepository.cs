using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomMaterialPlanningLineRepository(IFreeSql fsql) : IMomMaterialPlanningLineRepository
{
    public IReadOnlyList<MomMaterialPlanningLine> List() => fsql.Select<MomMaterialPlanningLineRecord>().OrderBy(x => x.PlanningRunId).OrderBy(x => x.LineNo).ToList().Select(ToDomain).ToArray();
    public void Add(MomMaterialPlanningLine item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    private static MomMaterialPlanningLine ToDomain(MomMaterialPlanningLineRecord x) => MomMaterialPlanningLine.Restore(x.Id, x.PlanningRunId, x.LineNo, x.ProductId, x.DemandQuantity, x.OnHandQuantity, x.PurchaseInTransitQuantity, x.OpenWorkOrderQuantity, x.Recommendation, x.RecommendationQuantity, x.ManufacturingVersionId, x.SourceSummary, x.OtherInfo);
    private static MomMaterialPlanningLineRecord ToRecord(MomMaterialPlanningLine x) => new() { Id = x.Id, PlanningRunId = x.PlanningRunId, LineNo = x.LineNo, ProductId = x.ProductId, DemandQuantity = x.DemandQuantity, OnHandQuantity = x.OnHandQuantity, PurchaseInTransitQuantity = x.PurchaseInTransitQuantity, OpenWorkOrderQuantity = x.OpenWorkOrderQuantity, Recommendation = x.Recommendation, RecommendationQuantity = x.RecommendationQuantity, ManufacturingVersionId = x.ManufacturingVersionId, SourceSummary = x.SourceSummary, OtherInfo = x.OtherInfo };
}
