using FreeSql.DataAnnotations;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomManufacturingOperationStandard")]
[Index("MomManufacturingOperationStandard_uk_Version_Sequence", "ManufacturingVersionId,OperationSequence", true)]
[Index("MomManufacturingOperationStandard_uk_Version_Code", "ManufacturingVersionId,OperationCode", true)]
public sealed class MomManufacturingOperationStandardRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid ManufacturingVersionId { get; set; }
    [Column(Position = 3, IsNullable = false)] public int OperationSequence { get; set; }
    [Column(Position = 4, IsNullable = false, StringLength = 50)] public string OperationCode { get; set; } = string.Empty;
    [Column(Position = 5, IsNullable = false, StringLength = 200)] public string OperationName { get; set; } = string.Empty;
    [Column(Position = 6, IsNullable = false)] public Guid WorkCenterId { get; set; }
    [Column(Position = 7, IsNullable = false, DbType = "numeric(18,6)")] public decimal SetupHours { get; set; }
    [Column(Position = 8, IsNullable = false, DbType = "numeric(18,6)")] public decimal RunHoursPerUnit { get; set; }
    [Column(StringLength = -1, Position = 9, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
