using System.Globalization;
using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Infrastructure;
using GazeteDagitim.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("GazeteDagitim")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:GazeteDagitim MSSQL bağlantısı tanımlanmalıdır.");

var configuredDataProtectionKeysPath =
    builder.Configuration["DataProtection:KeysPath"];
var defaultDataProtectionKeysPath = builder.Environment.IsEnvironment("Testing")
    ? Path.Combine(
        Path.GetTempPath(),
        "GazeteDagitim.Tests",
        Environment.ProcessId.ToString(),
        "DataProtection-Keys")
    : Path.Combine(
        builder.Environment.ContentRootPath,
        "App_Data",
        "DataProtection-Keys");
var dataProtectionKeysPath = string.IsNullOrWhiteSpace(configuredDataProtectionKeysPath)
    ? defaultDataProtectionKeysPath
    : Path.IsPathRooted(configuredDataProtectionKeysPath)
        ? configuredDataProtectionKeysPath
        : Path.Combine(
            builder.Environment.ContentRootPath,
            configuredDataProtectionKeysPath);
dataProtectionKeysPath = Path.GetFullPath(dataProtectionKeysPath);
Directory.CreateDirectory(dataProtectionKeysPath);

var dataProtectionBuilder = builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("GazeteDagitim.Web");
if (OperatingSystem.IsWindows())
{
    // Machine scope keeps the key ring readable after an IIS/Plesk app-pool
    // identity change. Access is still restricted by the key directory ACL.
    dataProtectionBuilder.ProtectKeysWithDpapi(protectToLocalMachine: true);
}
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = ".GazeteDagitim.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy =
        builder.Environment.IsDevelopment()
        || builder.Environment.IsEnvironment("Testing")
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
});
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AntiforgeryFailureResultFilter()));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlServer => sqlServer.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(8),
            errorNumbersToAdd: null)));

builder.Services.AddSingleton<IBusinessClock, SystemBusinessClock>();
builder.Services.AddScoped<ISubscriberDeliveryService, SubscriberDeliveryService>();
builder.Services.AddScoped<ISubscriberPaymentDetailsService, SubscriberPaymentDetailsService>();
builder.Services.AddScoped<ICashHandoverService, CashHandoverService>();
builder.Services.AddScoped<INewspaperCashSaleService, NewspaperCashSaleService>();
builder.Services.AddScoped<IPaymentTrackingService, PaymentTrackingService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IPeriodicPaymentService, PeriodicPaymentService>();
if (builder.Configuration.GetValue<bool>("ScheduledJobs:Enabled"))
{
    builder.Services.AddHostedService<ScheduledJobsHostedService>();
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

var turkishCulture = CultureInfo.GetCultureInfo("tr-TR");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(turkishCulture),
    SupportedCultures = [turkishCulture],
    SupportedUICultures = [turkishCulture]
});

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    if (context.Request.Path.Equals("/service-worker.js", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        context.Response.Headers["Service-Worker-Allowed"] = "/";
    }

    await next();
});
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllers();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

if (builder.Configuration.GetValue("Database:ApplyMigrations", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

await app.RunAsync();

public partial class Program;
