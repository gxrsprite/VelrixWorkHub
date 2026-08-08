using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomMaterialPlanningLine")]
public sealed class MomMaterialPlanningLineRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid PlanningRunId { get; set; }
    [Column(Position = 3, IsNullable = false)] public int LineNo { get; set; }
    [Column(Position = 4, IsNullable = false)] public Guid ProductId { get; set; }
    [Column(Position = 5, IsNullable = false, DbType = "numeric(18,6)")] public decimal DemandQuantity { get; set; }
    [Column(Position = 6, IsNullable = false, DbType = "numeric(18,6)")] public decimal OnHandQuantity { get; set; }
    [Column(Position = 7, IsNullable = false, DbType = "numeric(18,6)")] public decimal PurchaseInTransitQuantity { get; set; }
    [Column(Position = 8, IsNullable = false, DbType = "numeric(18,6)")] public decimal OpenWorkOrderQuantity { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 9, IsNullable = false)] public MomMaterialPlanningRecommendation Recommendation { get; set; }
    [Column(Position = 10, IsNullable = false, DbType = "numeric(18,6)")] public decimal RecommendationQuantity { get; set; }
    [Column(Position = 11)] public Guid? ManufacturingVersionId { get; set; }
    [Column(StringLength = 4000, IsNullable = false, Position = 12)] public string SourceSummary { get; set; } = string.Empty;
    [Column(StringLength = -1, Position = 13, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
