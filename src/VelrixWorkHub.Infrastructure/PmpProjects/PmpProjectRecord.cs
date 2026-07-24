using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.PmpProjects;
[Table(Name = "PmpProject")]
public sealed class PmpProjectRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 2)] public string Code { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 3)] public string Name { get; set; } = string.Empty;
    [Column(Position = 4)] public Guid? CustomerId { get; set; }
    [Column(StringLength = 100, Position = 5)] public string? ManagerName { get; set; }
    [Column(Position = 6, DbType = "date", IsNullable = false)] public DateTime PlannedStart { get; set; }
    [Column(Position = 7, DbType = "date", IsNullable = false)] public DateTime PlannedEnd { get; set; }
    [Column(Position = 8)] public int PercentComplete { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 9)] public PmpProjectStatus Status { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = true, Position = 10)] public PmpProjectInitiationMode? InitiationMode { get; set; }
    [Column(StringLength = 200, IsNullable = true, Position = 11)] public string? ProjectAlias { get; set; }
    [Column(StringLength = 200, IsNullable = true, Position = 12)] public string? ProjectChineseName { get; set; }
    [Column(StringLength = 200, IsNullable = true, Position = 13)] public string? ProjectEnglishName { get; set; }
    [Column(StringLength = 200, IsNullable = true, Position = 14)] public string? ProductName { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 15)] public string? ProjectStage { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 16)] public string? ProductLine { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 17)] public string? ProjectCategory { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 18)] public string? ProjectSubcategory { get; set; }
    [Column(StringLength = 50, IsNullable = true, Position = 19)] public string? ProjectSubcategoryCode { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 20)] public string? VersionType { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 21)] public string? ProjectVersion { get; set; }
    [Column(DbType = "date", IsNullable = true, Position = 22)] public DateTime? ExpectedInitiationDate { get; set; }
    [Column(DbType = "date", IsNullable = true, Position = 23)] public DateTime? ActualInitiationDate { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 24)] public string? DevelopmentMode { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 25)] public string? DepartmentName { get; set; }
    [Column(StringLength = 100, IsNullable = true, Position = 26)] public string? DomainManagerName { get; set; }
    [Column(StringLength = 200, IsNullable = true, Position = 27)] public string? BusinessInitiatorName { get; set; }
    [Column(StringLength = 4000, IsNullable = true, Position = 28)] public string? Overview { get; set; }
    [Column(StringLength = 4000, IsNullable = true, Position = 29)] public string? Objective { get; set; }
    [Column(StringLength = 4000, IsNullable = true, Position = 30)] public string? OtherInfo { get; set; }
    [Column(Position = 31, ServerTime = DateTimeKind.Local)] public DateTime CreatedTime { get; set; }
    [Column(Position = 32, ServerTime = DateTimeKind.Local)] public DateTime ModifiedTime { get; set; }
}
