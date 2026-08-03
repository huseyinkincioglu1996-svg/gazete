using System.Globalization;
using GazeteDagitim.Web.Data;
using GazeteDagitim.Web.Infrastructure;
using GazeteDagitim.Web.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("GazeteDagitim")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:GazeteDagitim MSSQL bağlantısı tanımlanmalıdır.");

builder.Services.AddControllersWithViews();
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
