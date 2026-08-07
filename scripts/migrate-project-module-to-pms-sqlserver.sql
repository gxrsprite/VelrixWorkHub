-- Velrix Work Hub 项目管理模块 Pmp -> Pms 数据库迁移
-- 适用：SQL Server。请在切换到使用 Pms 表名的新版本应用前执行。
-- 脚本可重复执行；若新旧表同时存在会主动失败，避免覆盖或合并数据。

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @old_table sysname;
DECLARE @new_table sysname;
DECLARE @sql nvarchar(4000);

DECLARE table_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT old_table, new_table
FROM (VALUES
    (N'PmpDeliveryRecord', N'PmsDeliveryRecord'),
    (N'PmpDeliveryRecordStatusHistory', N'PmsDeliveryRecordStatusHistory'),
    (N'PmpProject', N'PmsProject'),
    (N'PmpProjectBaseline', N'PmsProjectBaseline'),
    (N'PmpProjectCalendarOverride', N'PmsProjectCalendarOverride'),
    (N'PmpProjectChange', N'PmsProjectChange'),
    (N'PmpProjectIssue', N'PmsProjectIssue'),
    (N'PmpProjectMeeting', N'PmsProjectMeeting'),
    (N'PmpProjectMember', N'PmsProjectMember'),
    (N'PmpProjectPhase', N'PmsProjectPhase'),
    (N'PmpProjectStatusHistory', N'PmsProjectStatusHistory'),
    (N'PmpProjectWorkItem', N'PmsProjectWorkItem'),
    (N'PmpProjectWorkItemActivity', N'PmsProjectWorkItemActivity'),
    (N'PmpRequirement', N'PmsRequirement'),
    (N'PmpWbsTask', N'PmsWbsTask'),
    (N'PmpWeeklyWorkLogSubmission', N'PmsWeeklyWorkLogSubmission'),
    (N'PmpWorkLog', N'PmsWorkLog')
) AS names(old_table, new_table);

OPEN table_cursor;
FETCH NEXT FROM table_cursor INTO @old_table, @new_table;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + @old_table, N'U') IS NOT NULL
       AND OBJECT_ID(N'dbo.' + @new_table, N'U') IS NOT NULL
        THROW 51001, '旧表与新表同时存在，请人工确认后再迁移。', 1;

    IF OBJECT_ID(N'dbo.' + @old_table, N'U') IS NOT NULL
    BEGIN
        SET @sql = N'EXEC sys.sp_rename N''dbo.' + @old_table + N''', N''' + @new_table + N''', N''OBJECT'';';
        EXEC sys.sp_executesql @sql;
    END;

    FETCH NEXT FROM table_cursor INTO @old_table, @new_table;
END;
CLOSE table_cursor;
DEALLOCATE table_cursor;

IF OBJECT_ID(N'dbo.ErpSalesOrder', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ErpSalesOrder', N'PmpProjectId') IS NOT NULL
   AND COL_LENGTH(N'dbo.ErpSalesOrder', N'PmsProjectId') IS NOT NULL
    THROW 51002, 'ErpSalesOrder.PmpProjectId 与 ErpSalesOrder.PmsProjectId 同时存在，请人工确认。', 1;

IF OBJECT_ID(N'dbo.ErpSalesOrder', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ErpSalesOrder', N'PmpProjectId') IS NOT NULL
    EXEC sys.sp_rename N'dbo.ErpSalesOrder.PmpProjectId', N'PmsProjectId', N'COLUMN';

IF OBJECT_ID(N'dbo.WorkflowInstance', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.WorkflowInstance SET BusinessType = REPLACE(BusinessType, N'Pmp', N'Pms') WHERE BusinessType LIKE N'%Pmp%';
    UPDATE dbo.WorkflowInstance SET DefinitionCode = REPLACE(DefinitionCode, N'PMP', N'PMS') WHERE DefinitionCode LIKE N'%PMP%';
END;
IF OBJECT_ID(N'dbo.WorkflowTask', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.WorkflowTask SET BusinessType = REPLACE(BusinessType, N'Pmp', N'Pms') WHERE BusinessType LIKE N'%Pmp%';
    UPDATE dbo.WorkflowTask SET DefinitionCode = REPLACE(DefinitionCode, N'PMP', N'PMS') WHERE DefinitionCode LIKE N'%PMP%';
END;
IF OBJECT_ID(N'dbo.WorkflowOperation', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.WorkflowOperation SET BusinessType = REPLACE(BusinessType, N'Pmp', N'Pms') WHERE BusinessType LIKE N'%Pmp%';
    UPDATE dbo.WorkflowOperation SET DedupeKey = REPLACE(DedupeKey, N'pmp', N'pms') WHERE DedupeKey LIKE N'%pmp%';
END;
IF OBJECT_ID(N'dbo.WorkflowDefinition', N'U') IS NOT NULL
    UPDATE dbo.WorkflowDefinition SET Code = REPLACE(Code, N'PMP', N'PMS') WHERE Code LIKE N'%PMP%';
IF OBJECT_ID(N'dbo.PmsProjectWorkItem', N'U') IS NOT NULL
    UPDATE dbo.PmsProjectWorkItem SET SourceType = REPLACE(SourceType, N'Pmp', N'Pms') WHERE SourceType LIKE N'%Pmp%';
IF OBJECT_ID(N'dbo.SysMenu', N'U') IS NOT NULL
    UPDATE dbo.SysMenu SET Path = REPLACE(Path, N'Pmp/', N'Pms/') WHERE Path LIKE N'Pmp/%';
IF OBJECT_ID(N'dbo.SimpleFormDefinition', N'U') IS NOT NULL
    UPDATE dbo.SimpleFormDefinition SET WorkflowDefinitionCode = REPLACE(WorkflowDefinitionCode, N'PMP', N'PMS') WHERE WorkflowDefinitionCode LIKE N'%PMP%';
IF OBJECT_ID(N'dbo.SimpleFormSubmission', N'U') IS NOT NULL
    UPDATE dbo.SimpleFormSubmission SET WorkflowDefinitionCode = REPLACE(WorkflowDefinitionCode, N'PMP', N'PMS') WHERE WorkflowDefinitionCode LIKE N'%PMP%';
IF OBJECT_ID(N'dbo.OaNotification', N'U') IS NOT NULL
    UPDATE dbo.OaNotification SET DedupeKey = REPLACE(DedupeKey, N'pmp', N'pms') WHERE DedupeKey LIKE N'%pmp%';
IF OBJECT_ID(N'dbo.OaNotificationFailure', N'U') IS NOT NULL
    UPDATE dbo.OaNotificationFailure SET DedupeKey = REPLACE(DedupeKey, N'pmp', N'pms') WHERE DedupeKey LIKE N'%pmp%';
IF OBJECT_ID(N'dbo.OaExternalNotificationOutbox', N'U') IS NOT NULL
    UPDATE dbo.OaExternalNotificationOutbox SET DedupeKey = REPLACE(DedupeKey, N'pmp', N'pms') WHERE DedupeKey LIKE N'%pmp%';

COMMIT TRANSACTION;
