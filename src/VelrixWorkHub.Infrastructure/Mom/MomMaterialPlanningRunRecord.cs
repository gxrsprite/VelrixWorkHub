using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomMaterialPlanningRun")]
public sealed class MomMaterialPlanningRunRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 2)] public string PlanNo { get; set; } = string.Empty;
    [Column(Position = 3, DbType = "date", IsNullable = false)] public DateTime ReferenceDate { get; set; }
    [Column(Position = 4, DbType = "date", IsNullable = false)] public DateTime HorizonDate { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 5, IsNullable = false)] public MomMaterialPlanningRunStatus Status { get; set; }
    [Column(StringLength = -1, Position = 6, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
