using FreeSql;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

/// <summary>用可空活动周键承载跨 PostgreSQL / SQL Server 的审批中或已批准周报唯一性。</summary>
public static class PmsWeeklyWorkLogSubmissionSchemaMigration
{
    public const string ActiveWeekKeyUniqueIndex = "PmsWeeklyWorkLogSubmission_uk_ActiveWeekKey";

    public static string? GetActiveWeekKey(Guid projectId, string memberName, DateOnly weekStart, PmsWeeklyWorkLogSubmissionStatus status)
        => status is PmsWeeklyWorkLogSubmissionStatus.Submitted or PmsWeeklyWorkLogSubmissionStatus.Approved
            ? $"{projectId:N}|{memberName.Trim().ToUpperInvariant()}|{weekStart:yyyyMMdd}"
            : null;

    public static bool IsActiveWeekUniquenessViolation(Exception exception)
    {
        var message = exception.ToString();
        var postgreSqlTruncatedName = ActiveWeekKeyUniqueIndex[..Math.Min(63, ActiveWeekKeyUniqueIndex.Length)];
        return message.Contains(ActiveWeekKeyUniqueIndex, StringComparison.OrdinalIgnoreCase)
            || message.Contains(postgreSqlTruncatedName, StringComparison.OrdinalIgnoreCase);
    }

    public static void EnsureActiveWeekUniqueness(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<PmsWeeklyWorkLogSubmissionRecord>();
        var records = fsql.Select<PmsWeeklyWorkLogSubmissionRecord>().ToList();
        foreach (var record in records)
        {
            var expected = GetActiveWeekKey(record.ProjectId, record.MemberName, DateOnly.FromDateTime(record.WeekStart), record.Status);
            if (string.Equals(record.ActiveWeekKey, expected, StringComparison.Ordinal)) continue;
            fsql.Update<PmsWeeklyWorkLogSubmissionRecord>().Set(x => x.ActiveWeekKey, expected).Where(x => x.Id == record.Id).ExecuteAffrows();
        }

        var duplicates = records
            .Select(x => GetActiveWeekKey(x.ProjectId, x.MemberName, DateOnly.FromDateTime(x.WeekStart), x.Status))
            .Where(x => x is not null)
            .GroupBy(x => x!, StringComparer.Ordinal)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .Take(5)
            .ToArray();
        if (duplicates.Length > 0)
            throw new InvalidOperationException($"检测到同一成员同周存在多个审批中或已批准工时周报，拒绝创建唯一索引。请先处理：{string.Join(", ", duplicates)}。");

        switch (fsql.Ado.DataType)
        {
            case DataType.PostgreSQL:
                fsql.Ado.ExecuteNonQuery($"CREATE UNIQUE INDEX IF NOT EXISTS \"{ActiveWeekKeyUniqueIndex}\" ON \"PmsWeeklyWorkLogSubmission\" (\"ActiveWeekKey\");");
                break;
            case DataType.SqlServer:
                fsql.Ado.ExecuteNonQuery($"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{ActiveWeekKeyUniqueIndex}' AND object_id = OBJECT_ID(N'PmsWeeklyWorkLogSubmission')) CREATE UNIQUE INDEX [{ActiveWeekKeyUniqueIndex}] ON [PmsWeeklyWorkLogSubmission] ([ActiveWeekKey]);");
                break;
        }
    }
}
