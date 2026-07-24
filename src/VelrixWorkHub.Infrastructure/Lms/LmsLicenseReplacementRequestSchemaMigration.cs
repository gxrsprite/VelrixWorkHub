using FreeSql;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Lms;

/// <summary>补齐跨进程并发下的替代申请审批中唯一性。</summary>
public static class LmsLicenseReplacementRequestSchemaMigration
{
    public const string SubmittedOriginalAuthorizationUniqueIndex = "LmsLicenseReplacementRequest_uk_OriginalAuthorizationId_Submitted";

    public static bool IsSubmittedRequestUniquenessViolation(Exception exception)
    {
        var message = exception.ToString();
        var postgreSqlTruncatedName = SubmittedOriginalAuthorizationUniqueIndex[..Math.Min(63, SubmittedOriginalAuthorizationUniqueIndex.Length)];
        return message.Contains(SubmittedOriginalAuthorizationUniqueIndex, StringComparison.OrdinalIgnoreCase)
            || message.Contains(postgreSqlTruncatedName, StringComparison.OrdinalIgnoreCase);
    }

    public static void EnsureSubmittedRequestUniqueness(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<LmsLicenseReplacementRequestRecord>();
        var duplicates = fsql.Select<LmsLicenseReplacementRequestRecord>()
            .ToList()
            .Where(x => x.Status == LmsLicenseReplacementRequestStatus.Submitted)
            .GroupBy(x => x.OriginalAuthorizationId)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .Take(5)
            .ToArray();
        if (duplicates.Length > 0)
            throw new InvalidOperationException($"检测到同一原授权存在多个审批中的替代申请，拒绝创建唯一索引。请先处理原授权：{string.Join(", ", duplicates)}。");

        switch (fsql.Ado.DataType)
        {
            case DataType.PostgreSQL:
                fsql.Ado.ExecuteNonQuery($"CREATE UNIQUE INDEX IF NOT EXISTS \"{SubmittedOriginalAuthorizationUniqueIndex}\" ON \"LmsLicenseReplacementRequest\" (\"OriginalAuthorizationId\") WHERE \"Status\" = 'Submitted';");
                break;
            case DataType.SqlServer:
                fsql.Ado.ExecuteNonQuery($"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{SubmittedOriginalAuthorizationUniqueIndex}' AND object_id = OBJECT_ID(N'LmsLicenseReplacementRequest')) CREATE UNIQUE INDEX [{SubmittedOriginalAuthorizationUniqueIndex}] ON [LmsLicenseReplacementRequest] ([OriginalAuthorizationId]) WHERE [Status] = N'Submitted';");
                break;
        }
    }
}
