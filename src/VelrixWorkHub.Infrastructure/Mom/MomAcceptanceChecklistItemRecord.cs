using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomAcceptanceChecklistItem")]
[Index("MomAcceptanceChecklistItem_uk_AcceptanceLine", "AcceptanceId,LineNo", true)]
public sealed class MomAcceptanceChecklistItemRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid AcceptanceId { get; set; }
    [Column(Position = 3, IsNullable = false)] public int LineNo { get; set; }
    [Column(Position = 4, StringLength = 80, IsNullable = false)] public string ItemCode { get; set; } = string.Empty;
    [Column(Position = 5, StringLength = 200, IsNullable = false)] public string ItemName { get; set; } = string.Empty;
    [Column(Position = 6, StringLength = 1000, IsNullable = false)] public string Requirement { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, Position = 7, IsNullable = false)] public MomAcceptanceItemResult Result { get; set; }
    [Column(Position = 8, StringLength = 1000)] public string? Remark { get; set; }
    [Column(Position = 9, StringLength = 100)] public string? CheckedBy { get; set; }
    [Column(Position = 10)] public DateTime? CheckedOn { get; set; }
    [Column(StringLength = -1, Position = 11, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
