using FreeSql;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Workflow;

/// <summary>
/// Workflow 运行表的幂等兼容迁移。Revision 是新增加的非空字段，旧开发库同步结构后默认为 0，
/// 统一回填为初始版本 1，避免旧快照无法反序列化。
/// </summary>
public static class WorkflowSchemaMigration
{
    public const string RunningBusinessDefinitionUniqueIndex = "WorkflowInstance_uk_RunningBusinessDefinition";
    public const string DefinitionVersionUniqueIndex = "WorkflowDefinition_uk_CodeVersion";

    public static void EnsureRunningBusinessUniqueness(IFreeSql fsql)
    {
        ArgumentNullException.ThrowIfNull(fsql);
        switch (fsql.Ado.DataType)
        {
            case DataType.PostgreSQL:
                EnsurePostgreSqlRunningBusinessUniqueness(fsql);
                break;
            case DataType.SqlServer:
                EnsureSqlServerRunningBusinessUniqueness(fsql);
                break;
            default:
                ThrowIfRunningDuplicatesExist(fsql);
                break;
        }
    }

    private static void EnsurePostgreSqlRunningBusinessUniqueness(IFreeSql fsql)
    {
        // PostgreSQL 的 CREATE INDEX IF NOT EXISTS 在两个连接同时首次建同名索引时，
        // 仍可能竞争 pg_class 的系统目录唯一键；事务级 advisory lock 保护检查和 DDL。
        if (fsql.Ado.TransactionCurrentThread is not null)
        {
            EnsurePostgreSqlRunningBusinessUniquenessCore(fsql);
            return;
        }

        fsql.Transaction(() => EnsurePostgreSqlRunningBusinessUniquenessCore(fsql));
    }

    private static void EnsurePostgreSqlRunningBusinessUniquenessCore(IFreeSql fsql)
    {
        fsql.Ado.ExecuteNonQuery("SELECT pg_advisory_xact_lock(hashtext('VelrixWorkHub.WorkflowSchemaMigration.RunningBusinessUniqueness')); ");
        ThrowIfRunningDuplicatesExist(fsql);
        fsql.Ado.ExecuteNonQuery($"CREATE UNIQUE INDEX IF NOT EXISTS \"{RunningBusinessDefinitionUniqueIndex}\" ON \"WorkflowInstance\" (\"BusinessType\", \"BusinessId\", \"DefinitionCode\") WHERE \"Status\" = 'Running';");
    }

    private static void EnsureSqlServerRunningBusinessUniqueness(IFreeSql fsql)
    {
        // SQL Server 的 sys.indexes 检查和 CREATE INDEX 不是一个原子语句；多个 Web 实例
        // 同时启动时必须在事务级应用锁内重新检查，避免第二个实例因重复索引创建而启动失败。
        if (fsql.Ado.TransactionCurrentThread is not null)
        {
            EnsureSqlServerRunningBusinessUniquenessCore(fsql);
            return;
        }

        fsql.Transaction(() => EnsureSqlServerRunningBusinessUniquenessCore(fsql));
    }

    private static void EnsureSqlServerRunningBusinessUniquenessCore(IFreeSql fsql)
    {
        var lockResult = Convert.ToInt32(fsql.Ado.ExecuteScalar("""
            DECLARE @result int;
            EXEC @result = sp_getapplock
                @Resource = N'VelrixWorkHub.WorkflowSchemaMigration.RunningBusinessUniqueness',
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 30000;
            SELECT @result;
            """));
        if (lockResult < 0)
            throw new InvalidOperationException($"无法获取 Workflow SQL Server 迁移锁，返回码：{lockResult}。");

        ThrowIfRunningDuplicatesExist(fsql);
        fsql.Ado.ExecuteNonQuery($"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{RunningBusinessDefinitionUniqueIndex}' AND object_id = OBJECT_ID(N'WorkflowInstance')) CREATE UNIQUE INDEX [{RunningBusinessDefinitionUniqueIndex}] ON [WorkflowInstance] ([BusinessType], [BusinessId], [DefinitionCode]) WHERE [Status] = N'Running';");
    }

    private static void ThrowIfRunningDuplicatesExist(IFreeSql fsql)
    {
        // Duplicate detection must be performed by the database. Loading every historical
        // instance into the application makes startup cost grow with the complete audit table.
        // For PostgreSQL and SQL Server this method is called while the migration lock is held,
        // so the duplicate check and unique-index DDL share one serialized critical section.
        var duplicates = QueryRunningDuplicates(fsql);
        if (duplicates.Count > 0)
            throw new InvalidOperationException($"检测到同一业务存在多个运行中的流程实例，拒绝创建唯一索引。请先处理：{string.Join("; ", duplicates.Select(x => $"{x.BusinessType}/{x.BusinessId}/{x.DefinitionCode}"))}。");
    }

    public static bool IsRunningBusinessUniquenessViolation(Exception exception)
        => exception.ToString().Contains(RunningBusinessDefinitionUniqueIndex, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 只读列出流程定义版本重复项及其全部 DefinitionId，供迁移失败后的人工处置使用。
    /// 此方法不修改任何数据，也不自动选择保留记录。
    /// </summary>
    public static IReadOnlyList<WorkflowDefinitionVersionDuplicate> FindDefinitionVersionDuplicates(IFreeSql fsql)
    {
        ArgumentNullException.ThrowIfNull(fsql);
        return QueryDefinitionVersionDuplicates(fsql)
            .Select(duplicate => new WorkflowDefinitionVersionDuplicate(
                duplicate.Code,
                duplicate.VersionNumber,
                fsql.Select<WorkflowDefinitionRecord>()
                    .Where(x => x.VersionNumber == duplicate.VersionNumber)
                    .ToList()
                    .Where(x => x.Code.Equals(duplicate.Code, StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.Id)
                    .ToArray()))
            .ToArray();
    }

    /// <summary>
    /// 只读列出重复流程版本及其 DefinitionId 对应的运行实例引用，供人工处置前审计影响范围。
    /// 此方法不修改任何数据，也不自动选择保留定义或迁移实例。
    /// </summary>
    public static IReadOnlyList<WorkflowDefinitionVersionDuplicateReferences> FindDefinitionVersionDuplicateReferences(IFreeSql fsql)
    {
        ArgumentNullException.ThrowIfNull(fsql);
        var duplicates = FindDefinitionVersionDuplicates(fsql);
        var definitionIds = duplicates.SelectMany(x => x.DefinitionIds).Distinct().ToArray();
        if (definitionIds.Length == 0) return [];

        var instances = fsql.Select<WorkflowInstanceRecord>()
            .Where(x => definitionIds.Contains(x.DefinitionId))
            .ToList()
            .GroupBy(x => x.DefinitionId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<WorkflowInstanceReference>)x
                .Select(item => new WorkflowInstanceReference(item.Id, item.BusinessType, item.BusinessId, item.Status))
                .ToArray());

        return duplicates
            .Select(duplicate => new WorkflowDefinitionVersionDuplicateReferences(
                duplicate.Code,
                duplicate.VersionNumber,
                duplicate.DefinitionIds
                    .Select(definitionId => new WorkflowDefinitionVersionReference(
                        definitionId,
                        instances.TryGetValue(definitionId, out var instanceIds) ? instanceIds : []))
                    .ToArray()))
            .ToArray();
    }

    public static void EnsureDefinitionVersionUniqueness(IFreeSql fsql)
    {
        ArgumentNullException.ThrowIfNull(fsql);
        switch (fsql.Ado.DataType)
        {
            case DataType.PostgreSQL:
                EnsurePostgreSqlDefinitionVersionUniqueness(fsql);
                break;
            case DataType.SqlServer:
                EnsureSqlServerDefinitionVersionUniqueness(fsql);
                break;
            default:
                EnsureDefinitionVersionUniquenessCore(fsql);
                break;
        }
    }

    private static void EnsurePostgreSqlDefinitionVersionUniqueness(IFreeSql fsql)
    {
        if (fsql.Ado.TransactionCurrentThread is not null)
        {
            EnsurePostgreSqlDefinitionVersionUniquenessCore(fsql);
            return;
        }

        fsql.Transaction(() => EnsurePostgreSqlDefinitionVersionUniquenessCore(fsql));
    }

    private static void EnsurePostgreSqlDefinitionVersionUniquenessCore(IFreeSql fsql)
    {
        fsql.Ado.ExecuteNonQuery("SELECT pg_advisory_xact_lock(hashtext('VelrixWorkHub.WorkflowSchemaMigration.DefinitionVersionUniqueness')); ");
        EnsureDefinitionVersionUniquenessCore(fsql);
        fsql.Ado.ExecuteNonQuery($"CREATE UNIQUE INDEX IF NOT EXISTS \"{DefinitionVersionUniqueIndex}\" ON \"WorkflowDefinition\" (\"Code\", \"VersionNumber\");");
    }

    private static void EnsureSqlServerDefinitionVersionUniqueness(IFreeSql fsql)
    {
        if (fsql.Ado.TransactionCurrentThread is not null)
        {
            EnsureSqlServerDefinitionVersionUniquenessCore(fsql);
            return;
        }

        fsql.Transaction(() => EnsureSqlServerDefinitionVersionUniquenessCore(fsql));
    }

    private static void EnsureSqlServerDefinitionVersionUniquenessCore(IFreeSql fsql)
    {
        var lockResult = Convert.ToInt32(fsql.Ado.ExecuteScalar("""
            DECLARE @result int;
            EXEC @result = sp_getapplock
                @Resource = N'VelrixWorkHub.WorkflowSchemaMigration.DefinitionVersionUniqueness',
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 30000;
            SELECT @result;
            """));
        if (lockResult < 0)
            throw new InvalidOperationException($"无法获取 Workflow 定义版本迁移锁，返回码：{lockResult}。");

        EnsureDefinitionVersionUniquenessCore(fsql);
        fsql.Ado.ExecuteNonQuery($"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{DefinitionVersionUniqueIndex}' AND object_id = OBJECT_ID(N'WorkflowDefinition')) CREATE UNIQUE INDEX [{DefinitionVersionUniqueIndex}] ON [WorkflowDefinition] ([Code], [VersionNumber]);");
    }

    private static void EnsureDefinitionVersionUniquenessCore(IFreeSql fsql)
    {
        BackfillCanonicalDefinitionCodes(fsql);
        ConsolidateUnreferencedDefinitionVersionDuplicatesCore(fsql);
        var duplicates = FindDefinitionVersionDuplicates(fsql);
        if (duplicates.Count > 0)
            throw new InvalidOperationException($"检测到重复的流程定义版本，拒绝创建唯一索引。请先处理：{string.Join("; ", duplicates.Select(x => $"{x.Code}/v{x.VersionNumber} [{string.Join(",", x.DefinitionIds)}]"))}。");
    }

    /// <summary>
    /// 在创建流程定义版本唯一索引前，只合并没有任何运行实例或待办引用的历史重复记录。
    /// 有引用的定义和全部流程历史保持原样；同一组有多个被引用定义时仍由唯一索引迁移阻断，
    /// 以免系统在不了解业务语义的情况下选择一个历史版本。
    /// </summary>
    private static void ConsolidateUnreferencedDefinitionVersionDuplicatesCore(IFreeSql fsql)
    {
        var definitions = fsql.Select<WorkflowDefinitionRecord>().ToList();
        var duplicateGroups = definitions
            .GroupBy(item => new { Code = item.Code.ToUpperInvariant(), item.VersionNumber })
            .Where(group => group.Count() > 1)
            .ToArray();

        foreach (var group in duplicateGroups)
        {
            var candidates = group.ToArray();
            var candidateIds = candidates.Select(item => item.Id).ToArray();
            var referencedIds = fsql.Select<WorkflowInstanceRecord>()
                .Where(item => candidateIds.Contains(item.DefinitionId))
                .ToList()
                .Select(item => item.DefinitionId)
                .Concat(fsql.Select<WorkflowTaskRecord>()
                    .Where(item => candidateIds.Contains(item.DefinitionId))
                    .ToList()
                    .Select(item => item.DefinitionId))
                .ToHashSet();

            if (referencedIds.Count > 1) continue;

            var retained = referencedIds.Count == 1
                ? candidates.Single(item => referencedIds.Contains(item.Id))
                : candidates
                    .OrderByDescending(item => item.Status == WorkflowDefinitionStatus.Published)
                    .ThenBy(item => item.CreatedAt)
                    .ThenBy(item => item.Id)
                    .First();

            foreach (var duplicate in candidates.Where(item => item.Id != retained.Id && !referencedIds.Contains(item.Id)))
            {
                fsql.Delete<WorkflowDefinitionRecord>()
                    .Where(item => item.Id == duplicate.Id)
                    .ExecuteAffrows();
            }
        }
    }

    public static void BackfillInitialRevisions(IFreeSql fsql)
    {
        ArgumentNullException.ThrowIfNull(fsql);
        if (fsql.Ado.TransactionCurrentThread is not null)
        {
            BackfillInitialRevisionsCore(fsql);
            return;
        }

        // Revision、活动节点和快照默认值必须作为一个迁移单元提交，避免启动中断留下半回填实例。
        fsql.Transaction(() => BackfillInitialRevisionsCore(fsql));
    }

    private static void BackfillInitialRevisionsCore(IFreeSql fsql)
    {
        fsql.Update<WorkflowInstanceRecord>()
            .Set(x => x.Revision, 1L)
            .Where(x => x.Revision == 0L)
            .ExecuteAffrows();
        fsql.Update<WorkflowTaskRecord>()
            .Set(x => x.Revision, 1L)
            .Where(x => x.Revision == 0L)
            .ExecuteAffrows();
        fsql.Update<WorkflowTaskRecord>()
            .Set(x => x.Round, 1)
            .Where(x => x.Round == 0)
            .ExecuteAffrows();
        // 先按规范化编码检查历史 Running 重复，再执行回填；否则已有原始字符串
        // 唯一索引可能在大小写合并时先抛数据库异常，无法返回可处理的业务键。
        ThrowIfRunningDuplicatesExist(fsql);
        BackfillCanonicalInstanceCodes(fsql);
        BackfillLegacyActiveNodes(fsql);
        BackfillJsonDefaults(fsql);
    }

    private static void BackfillCanonicalInstanceCodes(IFreeSql fsql)
    {
        // DefinitionCode 是运行实例唯一键的一部分。实例回填必须独立于定义表，
        // 以兼容只同步运行表的历史 SQLite 夹具和旧部署。
        switch (fsql.Ado.DataType)
        {
            case DataType.PostgreSQL:
                fsql.Ado.ExecuteNonQuery("UPDATE \"WorkflowInstance\" SET \"DefinitionCode\" = UPPER(\"DefinitionCode\") WHERE \"DefinitionCode\" <> UPPER(\"DefinitionCode\");");
                return;
            case DataType.SqlServer:
                // SQL Server 常见默认排序规则大小写不敏感，使用 <> 判断会被优化为“相等”；
                // 无条件回写 UPPER 才能可靠规范化历史值。
                fsql.Ado.ExecuteNonQuery("UPDATE [WorkflowInstance] SET [DefinitionCode] = UPPER([DefinitionCode]);");
                return;
            case DataType.Sqlite:
                fsql.Ado.ExecuteNonQuery("UPDATE \"WorkflowInstance\" SET \"DefinitionCode\" = UPPER(\"DefinitionCode\") WHERE \"DefinitionCode\" <> UPPER(\"DefinitionCode\");");
                return;
            default:
                foreach (var instance in fsql.Select<WorkflowInstanceRecord>().ToList().Where(x => x.DefinitionCode != x.DefinitionCode.ToUpperInvariant()))
                    fsql.Update<WorkflowInstanceRecord>()
                        .Set(x => x.DefinitionCode, instance.DefinitionCode.ToUpperInvariant())
                        .Where(x => x.Id == instance.Id)
                        .ExecuteAffrows();
                return;
        }
    }

    private static void BackfillCanonicalDefinitionCodes(IFreeSql fsql)
    {
        // 定义版本唯一索引建立前才访问定义表；调用方必须先同步 WorkflowDefinition。
        switch (fsql.Ado.DataType)
        {
            case DataType.PostgreSQL:
                fsql.Ado.ExecuteNonQuery("UPDATE \"WorkflowDefinition\" SET \"Code\" = UPPER(\"Code\") WHERE \"Code\" <> UPPER(\"Code\");");
                return;
            case DataType.SqlServer:
                fsql.Ado.ExecuteNonQuery("UPDATE [WorkflowDefinition] SET [Code] = UPPER([Code]);");
                return;
            case DataType.Sqlite:
                fsql.Ado.ExecuteNonQuery("UPDATE \"WorkflowDefinition\" SET \"Code\" = UPPER(\"Code\") WHERE \"Code\" <> UPPER(\"Code\");");
                return;
            default:
                foreach (var definition in fsql.Select<WorkflowDefinitionRecord>().ToList().Where(x => x.Code != x.Code.ToUpperInvariant()))
                    fsql.Update<WorkflowDefinitionRecord>()
                        .Set(x => x.Code, definition.Code.ToUpperInvariant())
                        .Where(x => x.Id == definition.Id)
                        .ExecuteAffrows();
                return;
        }
    }

    private static void BackfillJsonDefaults(IFreeSql fsql)
    {
        if (fsql.Ado.DataType == DataType.SqlServer)
        {
            // SQL Server 的旧 FreeSql 映射可能将长 JSON 列建成 text；text 不能直接与
            // varchar/nvarchar 做等值比较，必须先转换后判断空字符串。
            fsql.Ado.ExecuteNonQuery("""
                UPDATE [WorkflowInstance]
                SET [ParallelJoinArrivalsJson] = N'{}'
                WHERE [ParallelJoinArrivalsJson] IS NULL OR CONVERT(nvarchar(max), [ParallelJoinArrivalsJson]) = N'';
                UPDATE [WorkflowInstance]
                SET [LoopIterationsJson] = N'{}'
                WHERE [LoopIterationsJson] IS NULL OR CONVERT(nvarchar(max), [LoopIterationsJson]) = N'';
                UPDATE [WorkflowInstance]
                SET [ApprovalAssigneesJson] = N'{}'
                WHERE [ApprovalAssigneesJson] IS NULL OR CONVERT(nvarchar(max), [ApprovalAssigneesJson]) = N'';
                """);
            return;
        }

        fsql.Update<WorkflowInstanceRecord>()
            .Set(x => x.ParallelJoinArrivalsJson, "{}")
            .Where(x => x.ParallelJoinArrivalsJson == null || x.ParallelJoinArrivalsJson == string.Empty)
            .ExecuteAffrows();
        fsql.Update<WorkflowInstanceRecord>()
            .Set(x => x.LoopIterationsJson, "{}")
            .Where(x => x.LoopIterationsJson == null || x.LoopIterationsJson == string.Empty)
            .ExecuteAffrows();
        fsql.Update<WorkflowInstanceRecord>()
            .Set(x => x.ApprovalAssigneesJson, "{}")
            .Where(x => x.ApprovalAssigneesJson == null || x.ApprovalAssigneesJson == string.Empty)
            .ExecuteAffrows();
    }

    private static void BackfillLegacyActiveNodes(IFreeSql fsql)
    {
        switch (fsql.Ado.DataType)
        {
            case DataType.PostgreSQL:
                fsql.Ado.ExecuteNonQuery("""
                    UPDATE "WorkflowInstance"
                    SET "ActiveNodeIdsJson" = '["' || "CurrentNodeId"::text || '"]'
                    WHERE "ActiveNodeIdsJson" IS NULL OR "ActiveNodeIdsJson" = '' OR "ActiveNodeIdsJson" = '[]';
                    """);
                return;
            case DataType.SqlServer:
                fsql.Ado.ExecuteNonQuery("""
                    UPDATE [WorkflowInstance]
                    SET [ActiveNodeIdsJson] = N'["' + LOWER(CONVERT(nvarchar(36), [CurrentNodeId])) + N'"]'
                    WHERE [ActiveNodeIdsJson] IS NULL
                       OR CONVERT(nvarchar(max), [ActiveNodeIdsJson]) = N''
                       OR CONVERT(nvarchar(max), [ActiveNodeIdsJson]) = N'[]';
                    """);
                return;
            case DataType.Sqlite:
                fsql.Ado.ExecuteNonQuery("""
                    UPDATE "WorkflowInstance"
                    SET "ActiveNodeIdsJson" = '["' || CAST("CurrentNodeId" AS TEXT) || '"]'
                    WHERE "ActiveNodeIdsJson" IS NULL OR "ActiveNodeIdsJson" = '' OR "ActiveNodeIdsJson" = '[]';
                    """);
                return;
            default:
                // Keep the old portable fallback for providers not covered by the primary dialects.
                var legacyInstances = fsql.Select<WorkflowInstanceRecord>()
                    .Where(x => x.ActiveNodeIdsJson == null || x.ActiveNodeIdsJson == string.Empty || x.ActiveNodeIdsJson == "[]")
                    .ToList();
                foreach (var legacy in legacyInstances)
                {
                    fsql.Update<WorkflowInstanceRecord>()
                        .Set(x => x.ActiveNodeIdsJson, $"[\"{legacy.CurrentNodeId}\"]")
                        .Where(x => x.Id == legacy.Id)
                        .ExecuteAffrows();
                }
                return;
        }
    }

    private static IReadOnlyList<WorkflowDuplicateKey> QueryRunningDuplicates(IFreeSql fsql)
    {
        var sql = fsql.Ado.DataType switch
        {
            DataType.PostgreSQL => """
                SELECT "BusinessType", "BusinessId", MIN("DefinitionCode") AS "DefinitionCode"
                FROM "WorkflowInstance"
                WHERE "Status" = 'Running'
                GROUP BY "BusinessType", "BusinessId", UPPER("DefinitionCode")
                HAVING COUNT(*) > 1
                LIMIT 5;
                """,
            DataType.SqlServer => """
                SELECT TOP 5 [BusinessType], [BusinessId], MIN([DefinitionCode]) AS [DefinitionCode]
                FROM [WorkflowInstance]
                WHERE [Status] = N'Running'
                GROUP BY [BusinessType], [BusinessId], UPPER([DefinitionCode])
                HAVING COUNT(*) > 1;
                """,
            DataType.Sqlite => """
                SELECT "BusinessType", "BusinessId", MIN("DefinitionCode") AS "DefinitionCode"
                FROM "WorkflowInstance"
                WHERE "Status" = 'Running'
                GROUP BY "BusinessType", "BusinessId", UPPER("DefinitionCode")
                HAVING COUNT(*) > 1
                LIMIT 5;
                """,
            _ => string.Empty
        };
        if (!string.IsNullOrEmpty(sql)) return fsql.Ado.Query<WorkflowDuplicateKey>(sql);

        // Keep a portable fallback for providers outside the primary PostgreSQL/SQL Server/SQLite set.
        return fsql.Select<WorkflowInstanceRecord>()
            .Where(x => x.Status == VelrixWorkHub.Domain.WorkflowInstanceStatus.Running)
            .ToList()
            .GroupBy(x => new { x.BusinessType, x.BusinessId, DefinitionCode = x.DefinitionCode.ToUpperInvariant() })
            .Where(x => x.Count() > 1)
            .Select(x => new WorkflowDuplicateKey { BusinessType = x.Key.BusinessType, BusinessId = x.Key.BusinessId, DefinitionCode = x.Key.DefinitionCode })
            .Take(5)
            .ToArray();
    }

    private static IReadOnlyList<WorkflowDefinitionDuplicateKey> QueryDefinitionVersionDuplicates(IFreeSql fsql)
    {
        if (fsql.Ado.DataType == DataType.PostgreSQL)
            return fsql.Ado.Query<WorkflowDefinitionDuplicateKey>("""
                SELECT MIN("Code") AS "Code", "VersionNumber", COUNT(*) AS "DuplicateCount"
                FROM "WorkflowDefinition"
                GROUP BY UPPER("Code"), "VersionNumber"
                HAVING COUNT(*) > 1
                LIMIT 5
                """).ToArray();

        if (fsql.Ado.DataType == DataType.SqlServer)
            return fsql.Ado.Query<WorkflowDefinitionDuplicateKey>("""
                SELECT TOP 5 MIN([Code]) AS [Code], [VersionNumber], COUNT(*) AS [DuplicateCount]
                FROM [WorkflowDefinition]
                GROUP BY UPPER([Code]), [VersionNumber]
                HAVING COUNT(*) > 1
                """).ToArray();

        if (fsql.Ado.DataType == DataType.Sqlite)
            return fsql.Ado.Query<WorkflowDefinitionDuplicateKey>("""
                SELECT MIN("Code") AS "Code", "VersionNumber", COUNT(*) AS "DuplicateCount"
                FROM "WorkflowDefinition"
                GROUP BY UPPER("Code"), "VersionNumber"
                HAVING COUNT(*) > 1
                LIMIT 5
                """).ToArray();

        return fsql.Select<WorkflowDefinitionRecord>().ToList()
            .GroupBy(x => new { Code = x.Code.ToUpperInvariant(), x.VersionNumber })
            .Where(x => x.Count() > 1)
            .Take(5)
            .Select(x => new WorkflowDefinitionDuplicateKey { Code = x.Key.Code, VersionNumber = x.Key.VersionNumber, DuplicateCount = x.Count() })
            .ToArray();
    }

    private sealed class WorkflowDuplicateKey
    {
        public string BusinessType { get; set; } = string.Empty;
        public Guid BusinessId { get; set; }
        public string DefinitionCode { get; set; } = string.Empty;
    }

    private sealed class WorkflowDefinitionDuplicateKey
    {
        public string Code { get; set; } = string.Empty;
        public int VersionNumber { get; set; }
        public long DuplicateCount { get; set; }
    }
}

public sealed record WorkflowDefinitionVersionDuplicate(string Code, int VersionNumber, IReadOnlyList<Guid> DefinitionIds);

public sealed record WorkflowDefinitionVersionDuplicateReferences(
    string Code,
    int VersionNumber,
    IReadOnlyList<WorkflowDefinitionVersionReference> Definitions);

public sealed record WorkflowDefinitionVersionReference(Guid DefinitionId, IReadOnlyList<WorkflowInstanceReference> Instances);

public sealed record WorkflowInstanceReference(Guid InstanceId, string BusinessType, Guid BusinessId, WorkflowInstanceStatus Status);
