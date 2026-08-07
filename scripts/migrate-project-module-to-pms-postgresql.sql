-- Velrix Work Hub 项目管理模块 Pmp -> Pms 数据库迁移
-- 适用：PostgreSQL。请在切换到使用 Pms 表名的新版本应用前执行。
-- 脚本可重复执行；若新旧表同时存在会主动失败，避免覆盖或合并数据。

BEGIN;

DO $$
DECLARE
    old_table text;
    new_table text;
BEGIN
    FOREACH old_table IN ARRAY ARRAY[
        'PmpDeliveryRecord',
        'PmpDeliveryRecordStatusHistory',
        'PmpProject',
        'PmpProjectBaseline',
        'PmpProjectCalendarOverride',
        'PmpProjectChange',
        'PmpProjectIssue',
        'PmpProjectMeeting',
        'PmpProjectMember',
        'PmpProjectPhase',
        'PmpProjectStatusHistory',
        'PmpProjectWorkItem',
        'PmpProjectWorkItemActivity',
        'PmpRequirement',
        'PmpWbsTask',
        'PmpWeeklyWorkLogSubmission',
        'PmpWorkLog'
    ] LOOP
        new_table := replace(old_table, 'Pmp', 'Pms');
        IF to_regclass(format('%I', old_table)) IS NOT NULL
           AND to_regclass(format('%I', new_table)) IS NOT NULL THEN
            RAISE EXCEPTION '旧表 % 与新表 % 同时存在，请人工确认后再迁移', old_table, new_table;
        ELSIF to_regclass(format('%I', old_table)) IS NOT NULL THEN
            EXECUTE format('ALTER TABLE %I RENAME TO %I', old_table, new_table);
        END IF;
    END LOOP;
END $$;

DO $$
BEGIN
    IF to_regclass('"ErpSalesOrder"') IS NOT NULL
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ErpSalesOrder' AND column_name = 'PmpProjectId')
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ErpSalesOrder' AND column_name = 'PmsProjectId') THEN
        RAISE EXCEPTION 'ErpSalesOrder.PmpProjectId 与 ErpSalesOrder.PmsProjectId 同时存在，请人工确认';
    ELSIF to_regclass('"ErpSalesOrder"') IS NOT NULL
       AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ErpSalesOrder' AND column_name = 'PmpProjectId') THEN
        ALTER TABLE "ErpSalesOrder" RENAME COLUMN "PmpProjectId" TO "PmsProjectId";
    END IF;

    IF to_regclass('"WorkflowInstance"') IS NOT NULL THEN
        UPDATE "WorkflowInstance" SET "BusinessType" = replace("BusinessType", 'Pmp', 'Pms') WHERE "BusinessType" LIKE '%Pmp%';
        UPDATE "WorkflowInstance" SET "DefinitionCode" = replace("DefinitionCode", 'PMP', 'PMS') WHERE "DefinitionCode" LIKE '%PMP%';
    END IF;
    IF to_regclass('"WorkflowTask"') IS NOT NULL THEN
        UPDATE "WorkflowTask" SET "BusinessType" = replace("BusinessType", 'Pmp', 'Pms') WHERE "BusinessType" LIKE '%Pmp%';
        UPDATE "WorkflowTask" SET "DefinitionCode" = replace("DefinitionCode", 'PMP', 'PMS') WHERE "DefinitionCode" LIKE '%PMP%';
    END IF;
    IF to_regclass('"WorkflowOperation"') IS NOT NULL THEN
        UPDATE "WorkflowOperation" SET "BusinessType" = replace("BusinessType", 'Pmp', 'Pms') WHERE "BusinessType" LIKE '%Pmp%';
        UPDATE "WorkflowOperation" SET "DedupeKey" = replace("DedupeKey", 'pmp', 'pms') WHERE "DedupeKey" LIKE '%pmp%';
    END IF;
    IF to_regclass('"WorkflowDefinition"') IS NOT NULL THEN
        UPDATE "WorkflowDefinition" SET "Code" = replace("Code", 'PMP', 'PMS') WHERE "Code" LIKE '%PMP%';
    END IF;
    IF to_regclass('"PmsProjectWorkItem"') IS NOT NULL THEN
        UPDATE "PmsProjectWorkItem" SET "SourceType" = replace("SourceType", 'Pmp', 'Pms') WHERE "SourceType" LIKE '%Pmp%';
    END IF;
    IF to_regclass('"SysMenu"') IS NOT NULL THEN
        UPDATE "SysMenu" SET "Path" = replace("Path", 'Pmp/', 'Pms/') WHERE "Path" LIKE 'Pmp/%';
    END IF;
    IF to_regclass('"SimpleFormDefinition"') IS NOT NULL THEN
        UPDATE "SimpleFormDefinition" SET "WorkflowDefinitionCode" = replace("WorkflowDefinitionCode", 'PMP', 'PMS') WHERE "WorkflowDefinitionCode" LIKE '%PMP%';
    END IF;
    IF to_regclass('"SimpleFormSubmission"') IS NOT NULL THEN
        UPDATE "SimpleFormSubmission" SET "WorkflowDefinitionCode" = replace("WorkflowDefinitionCode", 'PMP', 'PMS') WHERE "WorkflowDefinitionCode" LIKE '%PMP%';
    END IF;
    IF to_regclass('"OaNotification"') IS NOT NULL THEN
        UPDATE "OaNotification" SET "DedupeKey" = replace("DedupeKey", 'pmp', 'pms') WHERE "DedupeKey" LIKE '%pmp%';
    END IF;
    IF to_regclass('"OaNotificationFailure"') IS NOT NULL THEN
        UPDATE "OaNotificationFailure" SET "DedupeKey" = replace("DedupeKey", 'pmp', 'pms') WHERE "DedupeKey" LIKE '%pmp%';
    END IF;
    IF to_regclass('"OaExternalNotificationOutbox"') IS NOT NULL THEN
        UPDATE "OaExternalNotificationOutbox" SET "DedupeKey" = replace("DedupeKey", 'pmp', 'pms') WHERE "DedupeKey" LIKE '%pmp%';
    END IF;
END $$;

COMMIT;
