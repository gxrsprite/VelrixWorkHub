using BootstrapBlazor.Components;
using FreeSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using AdminBlazor.Services;
using VelrixWorkHub.Application.Platform;
using VelrixWorkHub.Application.Notifications;

namespace AdminBlazor;

internal static class AdminOperationalEndpoints
{
    public static void Map(WebApplication app, long maxUploadBytes)
    {
        app.MapGet("/api/admin/health", () =>
            AdminApiSupport.ApiOk(new AdminApiModels.HealthDto("ok", DateTimeOffset.UtcNow, "AdminBlazor")));

        app.MapGet("/api/admin/health/ready", async (
            IFreeSql fsql,
            CronSchedulerService scheduler,
            WorkingDayCalendar calendar) =>
        {
            try
            {
                await fsql.Ado.ExecuteScalarAsync("SELECT 1");
                var tasks = scheduler.GetTasks();
                return AdminApiSupport.ApiOk(new AdminApiModels.ReadinessDto(
                    "ready",
                    DateTimeOffset.UtcNow,
                    true,
                    tasks.Count,
                    calendar.Holidays.Count,
                    calendar.Workdays.Count,
                    calendar.IsTodayWorkingDay));
            }
            catch (Exception ex)
            {
                return AdminApiSupport.ApiError($"服务未就绪：{ex.Message}", StatusCodes.Status503ServiceUnavailable);
            }
        });

        app.MapGet("/api/admin/scheduler/tasks", async (
            HttpContext http,
            IFreeSql fsql,
            IAdminPermissionService authorization,
            ICronScheduler scheduler) =>
        {
            var (_, denied) = await AdminApiSupport.RequireMenuAccessAsync(
                http, fsql, authorization, "Admin/TaskScheduler", "没有定时任务权限");
            if (denied != null)
                return denied;

            return AdminApiSupport.ApiOk(scheduler.GetTasks()
                .OrderBy(task => task.NextFireTime)
                .Select(task => new AdminApiModels.SchedulerTaskDto(
                    task.Name,
                    task.Cron,
                    task.NextFireTime,
                    task.Enabled,
                    task.SkipHolidays))
                .ToArray());
        });

        app.MapGet("/api/admin/notification-failures", async (
            HttpContext http,
            IFreeSql fsql,
            IAdminPermissionService authorization,
            INotificationFailureRepository failures) =>
        {
            var (_, denied) = await AdminApiSupport.RequireMenuAccessAsync(
                http, fsql, authorization, "Admin/NotificationFailures", "没有通知失败处置权限");
            if (denied != null)
                return denied;

            var items = failures.ListPending(200)
                .OrderByDescending(item => item.RetryCount)
                .ThenBy(item => item.OccurredAt)
                .Select(item => new AdminApiModels.NotificationFailureDto(
                    item.Id,
                    item.Operation,
                    item.Recipient,
                    item.DedupeKey,
                    item.Error,
                    item.OccurredAt,
                    item.RetryCount,
                    item.LastRetryAt))
                .ToArray();
            return AdminApiSupport.ApiOk(items);
        });

        app.MapPost("/api/admin/notification-failures/{id:guid}/retry", async (
            Guid id,
            HttpContext http,
            IFreeSql fsql,
            IAdminPermissionService authorization,
            NotificationFailureRetryService retryService) =>
        {
            var (_, denied) = await AdminApiSupport.RequireButtonAccessAsync(
                http, fsql, authorization, "Admin/NotificationFailures", "Admin/NotificationFailures/Retry", "没有通知失败手动重试权限");
            if (denied != null)
                return denied;

            var resolved = retryService.Retry(id, DateTime.Now, http.User.Identity?.Name);
            return resolved
                ? AdminApiSupport.ApiOk(new { resolved = true })
                : AdminApiSupport.ApiError("失败记录不存在、已处理或重试失败。", StatusCodes.Status409Conflict);
        });

        app.MapPost("/api/admin/notification-failures/retry-batch", async (
            HttpContext http,
            IFreeSql fsql,
            IAdminPermissionService authorization,
            NotificationFailureRetryService retryService,
            AdminApiModels.NotificationFailureBatchRetryRequest request) =>
        {
            var (_, denied) = await AdminApiSupport.RequireButtonAccessAsync(
                http, fsql, authorization, "Admin/NotificationFailures", "Admin/NotificationFailures/BatchRetry", "没有通知失败批量重试权限");
            if (denied != null) return denied;
            try
            {
                var result = retryService.RetryMany(request.Ids ?? [], http.User.Identity?.Name ?? string.Empty, DateTime.Now);
                return AdminApiSupport.ApiOk(result);
            }
            catch (ArgumentException ex)
            {
                return AdminApiSupport.ApiError(ex.Message, StatusCodes.Status400BadRequest);
            }
        });

        app.MapGet("/api/admin/files", async (HttpContext http, IFreeSql fsql, IAdminPermissionService authorization) =>
        {
            var (_, denied) = await AdminApiSupport.RequireMenuAccessAsync(http, fsql, authorization, "Admin/File", "没有文件管理权限");
            if (denied != null)
                return denied;

            var search = http.Request.Query["search"].ToString();
            var take = int.TryParse(http.Request.Query["take"], out var parsedTake)
                ? Math.Clamp(parsedTake, 1, 200)
                : 50;

            var files = await fsql.Select<SysFile>()
                .WhereIf(!string.IsNullOrWhiteSpace(search), a =>
                    (a.OriginFileName ?? "").Contains(search) || (a.Extension ?? "").Contains(search))
                .OrderByDescending(a => a.CreatedTime)
                .Take(take)
                .ToListAsync(a => new AdminApiModels.FileInfoDto(
                    a.Id,
                    a.OriginFileName,
                    a.Extension,
                    a.Size,
                    a.SizeFormat,
                    a.LinkUrl,
                    a.CreatedTime));

            return AdminApiSupport.ApiOk(files);
        });

        app.MapPost("/api/admin/files", async (HttpContext http, IFreeSql fsql, IFileService fileService, IAdminPermissionService authorization) =>
        {
            var (_, denied) = await AdminApiSupport.RequireMenuAccessAsync(http, fsql, authorization, "Admin/File", "没有文件管理权限");
            if (denied != null)
                return denied;

            if (!http.Request.HasFormContentType)
                return AdminApiSupport.ApiError("请使用 multipart/form-data 上传文件", StatusCodes.Status400BadRequest);

            var form = await http.Request.ReadFormAsync();
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file == null || file.Length == 0)
                return AdminApiSupport.ApiError("请选择文件", StatusCodes.Status400BadRequest);

            if (file.Length > maxUploadBytes)
                return AdminApiSupport.ApiError($"文件不能超过 {FileSize.Format(maxUploadBytes)}", StatusCodes.Status413PayloadTooLarge);

            var directory = FileStoragePathPolicy.NormalizeUploadDirectory(form["directory"].FirstOrDefault());
            await using var stream = file.OpenReadStream();
            await using var buffer = new MemoryStream((int)file.Length);
            await stream.CopyToAsync(buffer);

            var saved = await fileService.UploadFileAsync(buffer.ToArray(), file.FileName, directory);
            return AdminApiSupport.ApiOk(new AdminApiModels.FileInfoDto(
                saved.Id,
                saved.OriginFileName,
                saved.Extension,
                saved.Size,
                saved.SizeFormat,
                saved.LinkUrl,
                saved.CreatedTime));
        }).DisableAntiforgery();

        app.MapDelete("/api/admin/files/{id:guid}", async (Guid id, HttpContext http, IFreeSql fsql, IFileService fileService, IAdminPermissionService authorization) =>
        {
            var (_, denied) = await AdminApiSupport.RequireMenuAccessAsync(http, fsql, authorization, "Admin/File", "没有文件管理权限");
            if (denied != null)
                return denied;

            var file = await fsql.Select<SysFile>().Where(a => a.Id == id).FirstAsync();
            if (file == null)
                return AdminApiSupport.ApiError("文件不存在", StatusCodes.Status404NotFound);

            await fileService.DeleteAsync(id);
            return AdminApiSupport.ApiOk();
        });
    }
}
