using System.Reflection;
using AdminBlazor.Services;
using BootstrapBlazor.Components;
using FreeSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VelrixWorkHub.Application.Platform;
using VelrixWorkHub.Infrastructure.Platform;

namespace AdminBlazor;

public class AdminBlazorOptions
{
    public string? DebugTenantId { get; set; }
    public Assembly[]? Assemblies { get; set; }
    public Action<FreeSqlBuilder>? FreeSqlBuilder { get; set; }
    public bool IsSwagger { get; set; }
    public string? ConnectionString { get; set; }
    public DataType DatabaseType { get; set; } = DataType.PostgreSQL;
    public bool AutoSyncStructure { get; set; } = true;
    public string? InternalApiBaseAddress { get; set; }
    public long? MaxUploadBytes { get; set; }
    public Action<PasswordPolicyOptions>? ConfigurePasswordPolicy { get; set; }
    public Action<LoginAttemptLimiterOptions>? ConfigureLoginAttemptLimiter { get; set; }
    public Action<CronSchedulerOptions>? ConfigureScheduler { get; set; }
}

public static class AdminExtensions
{
    public static void AddAdminBlazor(this WebApplicationBuilder builder, AdminBlazorOptions options)
    {
        ApplyConfiguration(builder.Configuration, options);

        var connString = options.ConnectionString
            ?? builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing database connection string. Configure ConnectionStrings:Default in appsettings.json or set AdminBlazorOptions.ConnectionString.");

        // 1. FreeSql 多数据库云
        var fsql = new FreeSqlCloud<string>();
        fsql.Register("main", () =>
        {
            var fsqlBuilder = new FreeSqlBuilder()
                .UseConnectionString(options.DatabaseType, connString)
                .UseMonitorCommand(cmd => System.Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {cmd.CommandText}\r\n"))
                .UseAutoSyncStructure(options.AutoSyncStructure)
                .UseNoneCommandParameter(true);

            options.FreeSqlBuilder?.Invoke(fsqlBuilder);
            var db = fsqlBuilder.Build();

            return db;
        });

        builder.Services.AddSingleton<FreeSqlCloud<string>>(fsql);
        builder.Services.AddScoped<IFreeSql>(sp => sp.GetRequiredService<FreeSqlCloud<string>>().Use("main"));
        builder.Services.AddSingleton(options);
        var passwordPolicyOptions = new PasswordPolicyOptions();
        ApplyPasswordPolicyConfiguration(builder.Configuration, passwordPolicyOptions);
        options.ConfigurePasswordPolicy?.Invoke(passwordPolicyOptions);
        builder.Services.AddSingleton(passwordPolicyOptions);
        builder.Services.AddSingleton<PasswordPolicy>();
        var loginAttemptLimiterOptions = new LoginAttemptLimiterOptions();
        ApplyLoginAttemptLimiterConfiguration(builder.Configuration, loginAttemptLimiterOptions);
        options.ConfigureLoginAttemptLimiter?.Invoke(loginAttemptLimiterOptions);
        builder.Services.AddSingleton(loginAttemptLimiterOptions);
        builder.Services.AddDataProtection();
        builder.Services.AddSingleton<AdminAuthCookieService>();
        builder.Services.AddScoped<IAdminSessionService, AdminSessionService>();
        builder.Services.AddSingleton<LoginAttemptLimiter>();
        builder.Services.AddScoped<AdminAuthorizationService>();
        builder.Services.AddScoped<IAdminPermissionService>(sp => sp.GetRequiredService<AdminAuthorizationService>());
        builder.Services.AddScoped<IAdminPermissionAuditService, FreeSqlAdminPermissionAuditService>();
        builder.Services.AddScoped<IAdminRolePermissionService, FreeSqlAdminRolePermissionService>();
        builder.Services.AddScoped<IAdminUserRoleService, FreeSqlAdminUserRoleService>();

        // 2. AdminContext
        builder.Services.AddScoped<AdminContext>();
        builder.Services.AddScoped<IAdminContext>(sp => sp.GetRequiredService<AdminContext>());
        builder.Services.AddHttpContextAccessor();

        // 3. Repository
        builder.Services.AddScoped(typeof(IBaseRepository<>), typeof(BasicRepository<>));
        builder.Services.AddScoped(typeof(IBaseRepository<,>), typeof(BasicRepository<,>));

        // 4. File Service
        builder.Services.AddScoped<IFileService, FreeSqlFileService>();
        builder.Services.AddScoped<IPlatformCatalogService, FreeSqlPlatformCatalogService>();
        builder.Services.AddSingleton<AdminNotifyChangedService>();
        builder.Services.AddSingleton<AdminResourceLockService>();

        // 5. BootstrapBlazor
        builder.Services.AddBootstrapBlazor();

        // 6. HttpClient for internal API calls
        builder.Services.AddHttpClient();
        var internalApiBaseAddress = options.InternalApiBaseAddress
            ?? throw new InvalidOperationException("Missing AdminBlazor:InternalApiBaseAddress in appsettings.json or AdminBlazorOptions.InternalApiBaseAddress.");
        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(internalApiBaseAddress, UriKind.Absolute) });

        // 7. Working Day Calendar — from CDN, fallback to hardcoded 2026
        var calendar = WorkingDayCalendar.China2026(); // 兜底：2026 硬编码数据
        builder.Services.AddSingleton(calendar);
        builder.Services.AddHttpClient("ChinaDays", c =>
        {
            c.DefaultRequestHeaders.UserAgent.ParseAdd("AdminBlazor/1.0");
            c.Timeout = TimeSpan.FromSeconds(15);
        });
        builder.Services.AddHostedService<ChinaDaysCalendarLoader>();
        builder.Services.AddHostedService<SysHolidayCalendarLoader>();

        // 8. Cron Scheduler
        {
            var schedulerOptions = new CronSchedulerOptions();
            options.ConfigureScheduler?.Invoke(schedulerOptions);
            builder.Services.AddSingleton(schedulerOptions);
            builder.Services.AddSingleton<CronSchedulerService>();
            builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<CronSchedulerService>());
            builder.Services.AddSingleton<ICronScheduler>(sp => sp.GetRequiredService<CronSchedulerService>());
        }

        // 9. Razor Components
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
    }

    public static void UseAdminOmniApi(this WebApplication app)
    {
        var adminOptions = app.Services.GetService<AdminBlazorOptions>();
        var maxUploadBytes = adminOptions?.MaxUploadBytes
            ?? GetRequiredLong(app.Configuration["AdminBlazor:MaxUploadBytes"], "AdminBlazor:MaxUploadBytes");

        AdminOperationalEndpoints.Map(app, maxUploadBytes);
        AdminIdentityEndpoints.Map(app, adminOptions, maxUploadBytes);
        AdminCatalogEndpoints.Map(app);
        AdminProfileEndpoints.Map(app);

    }

    private static void ApplyConfiguration(IConfiguration configuration, AdminBlazorOptions options)
    {
        var section = configuration.GetSection("AdminBlazor");

        options.ConnectionString ??= section["ConnectionString"];

        if (Enum.TryParse<DataType>(section["DatabaseType"], ignoreCase: true, out var databaseType))
            options.DatabaseType = databaseType;

        if (bool.TryParse(section["AutoSyncStructure"], out var autoSyncStructure))
            options.AutoSyncStructure = autoSyncStructure;

        var internalApiBaseAddress = section["InternalApiBaseAddress"];
        if (!string.IsNullOrWhiteSpace(internalApiBaseAddress))
            options.InternalApiBaseAddress = internalApiBaseAddress;

        options.MaxUploadBytes = GetConfiguredLong(section["MaxUploadBytes"], options.MaxUploadBytes);
    }

    private static void ApplyPasswordPolicyConfiguration(IConfiguration configuration, PasswordPolicyOptions options)
    {
        var section = configuration.GetSection("AdminBlazor:PasswordPolicy");
        if (int.TryParse(section["MinimumLength"], out var minimumLength) && minimumLength > 0)
            options.MinimumLength = minimumLength;
        if (int.TryParse(section["MaximumLength"], out var maximumLength) && maximumLength >= options.MinimumLength)
            options.MaximumLength = maximumLength;
        if (bool.TryParse(section["RequireUppercase"], out var requireUppercase))
            options.RequireUppercase = requireUppercase;
        if (bool.TryParse(section["RequireLowercase"], out var requireLowercase))
            options.RequireLowercase = requireLowercase;
        if (bool.TryParse(section["RequireDigit"], out var requireDigit))
            options.RequireDigit = requireDigit;
    }

    private static void ApplyLoginAttemptLimiterConfiguration(IConfiguration configuration, LoginAttemptLimiterOptions options)
    {
        var section = configuration.GetSection("AdminBlazor:LoginAttemptLimiter");
        if (int.TryParse(section["MaxFailures"], out var maxFailures) && maxFailures > 0)
            options.MaxFailures = maxFailures;
        if (int.TryParse(section["FailureWindowMinutes"], out var failureWindowMinutes) && failureWindowMinutes > 0)
            options.FailureWindow = TimeSpan.FromMinutes(failureWindowMinutes);
        if (int.TryParse(section["BlockDurationMinutes"], out var blockDurationMinutes) && blockDurationMinutes > 0)
            options.BlockDuration = TimeSpan.FromMinutes(blockDurationMinutes);
    }

    private static long? GetConfiguredLong(string? value, long? fallback)
    {
        return long.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
    }

    private static long GetRequiredLong(string? value, string key)
    {
        if (long.TryParse(value, out var parsed) && parsed > 0)
            return parsed;

        throw new InvalidOperationException($"Missing or invalid {key} in appsettings.json.");
    }

}
