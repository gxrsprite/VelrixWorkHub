using FreeSql;

namespace VelrixWorkHub.Infrastructure.Notifications;

/// <summary>修复历史通知表将可空 ReadAt 错误配置为服务端时间的兼容迁移。</summary>
public static class NotificationSchemaMigration
{
    public static void EnsureReadAtHasNoServerDefault(IFreeSql fsql)
    {
        ArgumentNullException.ThrowIfNull(fsql);
        switch (fsql.Ado.DataType)
        {
            case DataType.PostgreSQL:
                fsql.Ado.ExecuteNonQuery("ALTER TABLE IF EXISTS \"OaNotification\" ALTER COLUMN \"ReadAt\" DROP DEFAULT;");
                break;
            case DataType.SqlServer:
                fsql.Ado.ExecuteNonQuery("""
                    DECLARE @constraint nvarchar(128);
                    SELECT @constraint = dc.name
                    FROM sys.default_constraints dc
                    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                    INNER JOIN sys.tables t ON t.object_id = c.object_id
                    WHERE t.name = N'OaNotification' AND c.name = N'ReadAt';
                    IF @constraint IS NOT NULL
                    BEGIN
                        DECLARE @sql nvarchar(max) = N'ALTER TABLE [OaNotification] DROP CONSTRAINT ' + QUOTENAME(@constraint);
                        EXEC sp_executesql @sql;
                    END
                    """);
                break;
        }
    }
}
