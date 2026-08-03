using GazeteDagitim.Web.Models.Entities;
using GazeteDagitim.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace GazeteDagitim.Web.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Distributor> Distributors => Set<Distributor>();
    public DbSet<DistributorDistributionDay> DistributorDistributionDays => Set<DistributorDistributionDay>();
    public DbSet<DistributorWeeklyPaymentDay> DistributorWeeklyPaymentDays => Set<DistributorWeeklyPaymentDay>();
    public DbSet<DistributorMonthlyPaymentDay> DistributorMonthlyPaymentDays => Set<DistributorMonthlyPaymentDay>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentPeriod> PaymentPeriods => Set<PaymentPeriod>();
    public DbSet<Subscriber> Subscribers => Set<Subscriber>();
    public DbSet<SubscriberPublicationDay> SubscriberPublicationDays => Set<SubscriberPublicationDay>();
    public DbSet<SubscriberDailyDelivery> SubscriberDailyDeliveries => Set<SubscriberDailyDelivery>();
    public DbSet<SubscriberDailyDeliveryCoveredDate> SubscriberDailyDeliveryCoveredDates =>
        Set<SubscriberDailyDeliveryCoveredDate>();
    public DbSet<SubscriberPaymentDeferral> SubscriberPaymentDeferrals =>
        Set<SubscriberPaymentDeferral>();
    public DbSet<CashHandover> CashHandovers => Set<CashHandover>();
    public DbSet<CashHandoverItem> CashHandoverItems => Set<CashHandoverItem>();
    public DbSet<NewspaperCashSale> NewspaperCashSales => Set<NewspaperCashSale>();
    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureDistributor(modelBuilder);
        ConfigureDelivery(modelBuilder);
        ConfigurePayment(modelBuilder);
        ConfigurePaymentPeriod(modelBuilder);
        ConfigureSubscriber(modelBuilder);
        ConfigureSubscriberDailyDelivery(modelBuilder);
        ConfigureSubscriberPaymentDeferral(modelBuilder);
        ConfigureCashHandover(modelBuilder);
        ConfigureNewspaperCashSale(modelBuilder);
        ConfigureCompanySettings(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private static void ConfigureDistributor(ModelBuilder modelBuilder)
    {
        var distributor = modelBuilder.Entity<Distributor>();
        distributor.ToTable("Distributors", table =>
        {
            table.HasCheckConstraint("CK_Distributors_Zone", "[Zone] IN (1, 2)");
            table.HasCheckConstraint("CK_Distributors_PaymentType", "[PaymentType] BETWEEN 0 AND 2");
            table.HasCheckConstraint("CK_Distributors_NewspaperPrice", "[NewspaperPrice] >= 0");
            table.HasCheckConstraint(
                "CK_Distributors_ProfileImageLength",
                "[ProfileImageDataUrl] IS NULL OR LEN([ProfileImageDataUrl]) <= 2796227");
        });
        distributor.Property(value => value.Name).HasMaxLength(120).IsRequired();
        distributor.Property(value => value.Address).HasMaxLength(500).IsRequired();
        distributor.Property(value => value.Phone).HasMaxLength(40).IsRequired();
        distributor.Property(value => value.ProfileImageDataUrl).HasColumnType("nvarchar(max)");
        distributor.Property(value => value.NewspaperPrice).HasPrecision(18, 2).HasDefaultValue(5m);
        distributor.Property(value => value.IsActive).HasDefaultValue(true);
        distributor.HasIndex(value => new { value.IsActive, value.Name })
            .HasDatabaseName("IX_Distributors_Active_Name");

        var distributionDay = modelBuilder.Entity<DistributorDistributionDay>();
        distributionDay.ToTable("DistributorDistributionDays", table =>
            table.HasCheckConstraint(
                "CK_DistributorDistributionDays_Day",
                "[Day] BETWEEN 0 AND 6"));
        distributionDay.HasKey(value => new { value.DistributorId, value.Day });
        distributionDay.HasOne(value => value.Distributor)
            .WithMany(value => value.DistributionDays)
            .HasForeignKey(value => value.DistributorId)
            .OnDelete(DeleteBehavior.Cascade);

        var weeklyPaymentDay = modelBuilder.Entity<DistributorWeeklyPaymentDay>();
        weeklyPaymentDay.ToTable("DistributorWeeklyPaymentDays", table =>
            table.HasCheckConstraint(
                "CK_DistributorWeeklyPaymentDays_Day",
                "[Day] BETWEEN 0 AND 6"));
        weeklyPaymentDay.HasKey(value => new { value.DistributorId, value.Day });
        weeklyPaymentDay.HasOne(value => value.Distributor)
            .WithMany(value => value.WeeklyPaymentDays)
            .HasForeignKey(value => value.DistributorId)
            .OnDelete(DeleteBehavior.Cascade);

        var monthlyPaymentDay = modelBuilder.Entity<DistributorMonthlyPaymentDay>();
        monthlyPaymentDay.ToTable("DistributorMonthlyPaymentDays", table =>
            table.HasCheckConstraint(
                "CK_DistributorMonthlyPaymentDays_DayOfMonth",
                "[DayOfMonth] BETWEEN 1 AND 31"));
        monthlyPaymentDay.HasKey(value => new { value.DistributorId, value.DayOfMonth });
        monthlyPaymentDay.HasOne(value => value.Distributor)
            .WithMany(value => value.MonthlyPaymentDays)
            .HasForeignKey(value => value.DistributorId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureDelivery(ModelBuilder modelBuilder)
    {
        var delivery = modelBuilder.Entity<Delivery>();
        delivery.ToTable("Deliveries", table =>
        {
            table.HasCheckConstraint("CK_Deliveries_Day", "[Day] BETWEEN 0 AND 6");
            table.HasCheckConstraint("CK_Deliveries_NewspaperCount", "[NewspaperCount] >= 0");
            table.HasCheckConstraint("CK_Deliveries_Amount", "[Amount] >= 0");
            table.HasCheckConstraint("CK_Deliveries_Status", "[Status] BETWEEN 0 AND 2");
        });
        delivery.Property(value => value.Date).HasColumnType("date");
        delivery.Property(value => value.Amount).HasPrecision(18, 2);
        delivery.Property(value => value.Notes).HasMaxLength(1000);
        delivery.HasIndex(value => new { value.DistributorId, value.Date })
            .IsUnique()
            .HasDatabaseName("UX_Deliveries_Distributor_Date");
        delivery.HasIndex(value => new { value.Date, value.Status })
            .HasDatabaseName("IX_Deliveries_Date_Status");
        delivery.HasOne(value => value.Distributor)
            .WithMany(value => value.Deliveries)
            .HasForeignKey(value => value.DistributorId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePayment(ModelBuilder modelBuilder)
    {
        var payment = modelBuilder.Entity<Payment>();
        payment.ToTable("Payments", table =>
        {
            table.HasCheckConstraint("CK_Payments_Amount", "[Amount] >= 0");
            table.HasCheckConstraint("CK_Payments_Period", "[PeriodEnd] >= [PeriodStart]");
            table.HasCheckConstraint("CK_Payments_Type", "[PaymentType] BETWEEN 0 AND 2");
            table.HasCheckConstraint("CK_Payments_Status", "[Status] BETWEEN 0 AND 1");
            table.HasCheckConstraint(
                "CK_Payments_PaidAt",
                "([Status] = 0 AND [PaidAt] IS NULL) OR " +
                "([Status] = 1 AND [PaidAt] IS NOT NULL)");
        });
        payment.Property(value => value.Amount).HasPrecision(18, 2);
        payment.Property(value => value.Date).HasColumnType("date");
        payment.Property(value => value.PeriodStart).HasColumnType("date");
        payment.Property(value => value.PeriodEnd).HasColumnType("date");
        payment.Property(value => value.Description).HasMaxLength(1000);
        payment.HasIndex(value => new
        {
            value.DistributorId,
            value.PaymentType,
            value.PeriodStart,
            value.PeriodEnd
        })
            .IsUnique()
            .HasDatabaseName("UX_Payments_Distributor_Type_Period");
        payment.HasIndex(value => new
        {
            value.DistributorId,
            value.PaymentType,
            value.PeriodEnd
        })
            .IsDescending(false, false, true)
            .HasDatabaseName("IX_Payments_Distributor_Type_PeriodEnd");
        payment.HasIndex(value => new { value.Date, value.Status })
            .HasDatabaseName("IX_Payments_Date_Status");
        payment.HasOne(value => value.Distributor)
            .WithMany(value => value.Payments)
            .HasForeignKey(value => value.DistributorId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePaymentPeriod(ModelBuilder modelBuilder)
    {
        var paymentPeriod = modelBuilder.Entity<PaymentPeriod>();
        paymentPeriod.ToTable("PaymentPeriods", table =>
        {
            table.HasCheckConstraint(
                "CK_PaymentPeriods_DayCount",
                "[DayCount] BETWEEN 1 AND 365");
            table.HasCheckConstraint(
                "CK_PaymentPeriods_Frequency",
                "[Frequency] IN (0, 1)");
            table.HasCheckConstraint(
                "CK_PaymentPeriods_DailyDayCount",
                "[Frequency] <> 1 OR [DayCount] = 1");
            table.HasCheckConstraint(
                "CK_PaymentPeriods_CollectionDay",
                "[CollectionDayOfMonth] IS NULL OR " +
                "[CollectionDayOfMonth] BETWEEN 1 AND 31");
            table.HasCheckConstraint(
                "CK_PaymentPeriods_CollectionSchedule",
                "([CollectionDayOfMonth] IS NULL AND " +
                "[CollectionTime] IS NULL AND " +
                "[CollectionAmount] IS NULL) OR " +
                "([CollectionDayOfMonth] IS NOT NULL AND " +
                "[CollectionTime] IS NOT NULL AND " +
                "[CollectionAmount] > 0)");
        });
        paymentPeriod.Property(value => value.Name)
            .HasMaxLength(120)
            .UseCollation("Turkish_CI_AS")
            .IsRequired();
        paymentPeriod.Property(value => value.CollectionTime).HasColumnType("time(0)");
        paymentPeriod.Property(value => value.CollectionAmount).HasPrecision(18, 2);
        paymentPeriod.Property(value => value.Description).HasMaxLength(500);
        paymentPeriod.Property(value => value.Frequency)
            .HasDefaultValue(PaymentPeriodFrequency.Monthly);
        paymentPeriod.Property(value => value.IsActive).HasDefaultValue(true);
        paymentPeriod.HasIndex(value => value.Name)
            .IsUnique()
            .HasDatabaseName("UX_PaymentPeriods_Name");
        paymentPeriod.HasIndex(value => new { value.IsActive, value.Name })
            .HasDatabaseName("IX_PaymentPeriods_Active_Name");
    }

    private static void ConfigureSubscriber(ModelBuilder modelBuilder)
    {
        var subscriber = modelBuilder.Entity<Subscriber>();
        subscriber.ToTable("Subscribers", table =>
        {
            table.HasCheckConstraint("CK_Subscribers_MonthlyFee", "[MonthlyFee] >= 0");
            table.HasCheckConstraint(
                "CK_Subscribers_ActivationState",
                "([IsActive] = 1 AND [DeactivatedAt] IS NULL) OR " +
                "([IsActive] = 0 AND [DeactivatedAt] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_Subscribers_LocationPair",
                "([Latitude] IS NULL AND [Longitude] IS NULL) OR " +
                "([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_Subscribers_Latitude",
                "[Latitude] IS NULL OR [Latitude] BETWEEN -90 AND 90");
            table.HasCheckConstraint(
                "CK_Subscribers_Longitude",
                "[Longitude] IS NULL OR [Longitude] BETWEEN -180 AND 180");
        });
        subscriber.Property(value => value.Name).HasMaxLength(160).IsRequired();
        subscriber.Property(value => value.Phone).HasMaxLength(40);
        subscriber.Property(value => value.Address).HasMaxLength(500);
        subscriber.Property(value => value.MonthlyFee).HasPrecision(18, 2);
        subscriber.Property(value => value.Notes).HasMaxLength(1000);
        subscriber.Property(value => value.PaymentPeriodStartedOn)
            .HasColumnType("date");
        subscriber.Property(value => value.Latitude).HasPrecision(9, 6);
        subscriber.Property(value => value.Longitude).HasPrecision(10, 6);
        subscriber.HasIndex(value => value.Name).HasDatabaseName("IX_Subscribers_Name");
        subscriber.HasIndex(value => new { value.IsActive, value.Name })
            .HasDatabaseName("IX_Subscribers_Active_Name");
        subscriber.HasIndex(value => value.PaymentPeriodId)
            .HasDatabaseName("IX_Subscribers_PaymentPeriodId");
        subscriber.HasIndex(value => value.DistributorId)
            .HasDatabaseName("IX_Subscribers_DistributorId");
        subscriber.HasOne(value => value.PaymentPeriod)
            .WithMany(value => value.Subscribers)
            .HasForeignKey(value => value.PaymentPeriodId)
            .OnDelete(DeleteBehavior.Restrict);
        subscriber.HasOne(value => value.Distributor)
            .WithMany(value => value.Subscribers)
            .HasForeignKey(value => value.DistributorId)
            .OnDelete(DeleteBehavior.SetNull);

        var publicationDay = modelBuilder.Entity<SubscriberPublicationDay>();
        publicationDay.ToTable("SubscriberPublicationDays", table =>
            table.HasCheckConstraint(
                "CK_SubscriberPublicationDays_Day",
                "[Day] BETWEEN 0 AND 7"));
        publicationDay.HasKey(value => new { value.SubscriberId, value.Day });
        publicationDay.HasOne(value => value.Subscriber)
            .WithMany(value => value.NewspaperDays)
            .HasForeignKey(value => value.SubscriberId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureSubscriberDailyDelivery(ModelBuilder modelBuilder)
    {
        var dailyDelivery = modelBuilder.Entity<SubscriberDailyDelivery>();
        dailyDelivery.ToTable("SubscriberDailyDeliveries", table =>
        {
            table.HasCheckConstraint(
                "CK_SubscriberDailyDeliveries_NewspaperCount",
                "[NewspaperCount] IN (1, 2)");
            table.HasCheckConstraint(
                "CK_SubscriberDailyDeliveries_Amount",
                "[Amount] >= 0 AND ([IsCollected] = 0 OR [Amount] > 0)");
            table.HasCheckConstraint(
                "CK_SubscriberDailyDeliveries_PaymentMethod",
                "[PaymentMethod] BETWEEN 0 AND 2");
            table.HasCheckConstraint(
                "CK_SubscriberDailyDeliveries_CollectionSnapshot",
                "[IsCollected] = 1 OR " +
                "([CollectedAt] IS NULL AND [CollectionDayCount] IS NULL AND " +
                "[CollectionPeriodName] = N'')");
        });
        dailyDelivery.Property(value => value.Date).HasColumnType("date");
        dailyDelivery.Property(value => value.DistributorName).HasMaxLength(120);
        dailyDelivery.Property(value => value.Amount).HasPrecision(18, 2);
        dailyDelivery.Property(value => value.CollectionPeriodName).HasMaxLength(120);
        dailyDelivery.HasIndex(value => new { value.SubscriberId, value.Date })
            .IsUnique()
            .HasDatabaseName("UX_SubscriberDailyDeliveries_Subscriber_Date");
        dailyDelivery.HasIndex(value => new { value.Date, value.IsCollected })
            .IsDescending(true, false)
            .HasDatabaseName("IX_SubscriberDailyDeliveries_Date_Collected");
        dailyDelivery.HasIndex(value => new
        {
            value.DistributorId,
            value.Date,
            value.IsCollected,
            value.PaymentMethod
        })
            .IsDescending(false, true, false, false)
            .HasDatabaseName("IX_SubscriberDailyDeliveries_Tracking");
        dailyDelivery.HasOne(value => value.Subscriber)
            .WithMany(value => value.DailyDeliveries)
            .HasForeignKey(value => value.SubscriberId)
            .OnDelete(DeleteBehavior.Restrict);
        dailyDelivery.HasOne(value => value.Distributor)
            .WithMany(value => value.SubscriberDailyDeliveries)
            .HasForeignKey(value => value.DistributorId)
            .OnDelete(DeleteBehavior.SetNull);

        var coveredDate = modelBuilder.Entity<SubscriberDailyDeliveryCoveredDate>();
        coveredDate.ToTable("SubscriberDailyDeliveryCoveredDates");
        coveredDate.Property(value => value.CoveredDate).HasColumnType("date");
        coveredDate.HasKey(value => new
        {
            value.SubscriberDailyDeliveryId,
            value.CoveredDate
        });
        coveredDate.HasOne(value => value.SubscriberDailyDelivery)
            .WithMany(value => value.CoveredDates)
            .HasForeignKey(value => value.SubscriberDailyDeliveryId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureSubscriberPaymentDeferral(ModelBuilder modelBuilder)
    {
        var deferral = modelBuilder.Entity<SubscriberPaymentDeferral>();
        deferral.ToTable("SubscriberPaymentDeferrals", table =>
        {
            table.HasCheckConstraint(
                "CK_SubscriberPaymentDeferrals_Dates",
                "[OriginalDueDate] <= [PreviousDueDate] AND " +
                "[DeferredUntil] > [PreviousDueDate]");
        });
        deferral.Property(value => value.OriginalDueDate).HasColumnType("date");
        deferral.Property(value => value.PreviousDueDate).HasColumnType("date");
        deferral.Property(value => value.DeferredUntil).HasColumnType("date");
        deferral.Property(value => value.Reason).HasMaxLength(500);
        deferral.HasIndex(value => new
        {
            value.SubscriberId,
            value.OriginalDueDate,
            value.CreatedAt
        })
            .IsDescending(false, false, true)
            .HasDatabaseName("IX_SubscriberPaymentDeferrals_History");
        deferral.HasIndex(value => new
        {
            value.SubscriberId,
            value.OriginalDueDate
        })
            .IsUnique()
            .HasFilter("[CancelledAt] IS NULL")
            .HasDatabaseName("UX_SubscriberPaymentDeferrals_Active");
        deferral.HasOne(value => value.Subscriber)
            .WithMany(value => value.PaymentDeferrals)
            .HasForeignKey(value => value.SubscriberId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureCashHandover(ModelBuilder modelBuilder)
    {
        var cashHandover = modelBuilder.Entity<CashHandover>();
        cashHandover.ToTable("CashHandovers", table =>
        {
            table.HasCheckConstraint("CK_CashHandovers_Total", "[Total] >= 0");
            table.HasCheckConstraint("CK_CashHandovers_Status", "[Status] BETWEEN 0 AND 1");
            table.HasCheckConstraint(
                "CK_CashHandovers_DeliveredAt",
                "([Status] = 0 AND [DeliveredAt] IS NULL) OR " +
                "([Status] = 1 AND [DeliveredAt] IS NOT NULL)");
        });
        cashHandover.Property(value => value.Date).HasColumnType("date");
        cashHandover.Property(value => value.Total).HasPrecision(18, 2);
        cashHandover.HasIndex(value => value.Date)
            .IsUnique()
            .HasDatabaseName("UX_CashHandovers_Date");
        cashHandover.HasIndex(value => new { value.Status, value.Date })
            .HasDatabaseName("IX_CashHandovers_Status_Date");

        var cashHandoverItem = modelBuilder.Entity<CashHandoverItem>();
        cashHandoverItem.ToTable("CashHandoverItems", table =>
            table.HasCheckConstraint("CK_CashHandoverItems_Amount", "[Amount] >= 0"));
        cashHandoverItem.Property(value => value.SubscriberName).HasMaxLength(200).IsRequired();
        cashHandoverItem.Property(value => value.Amount).HasPrecision(18, 2);
        cashHandoverItem.Property(value => value.Description).HasMaxLength(1000);
        cashHandoverItem.HasOne(value => value.CashHandover)
            .WithMany(value => value.Items)
            .HasForeignKey(value => value.CashHandoverId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureNewspaperCashSale(ModelBuilder modelBuilder)
    {
        var cashSale = modelBuilder.Entity<NewspaperCashSale>();
        cashSale.ToTable("NewspaperCashSales", table =>
        {
            table.HasCheckConstraint(
                "CK_NewspaperCashSales_Quantity",
                "[Quantity] BETWEEN 1 AND 1000");
            table.HasCheckConstraint(
                "CK_NewspaperCashSales_UnitPrice",
                "[UnitPrice] > 0");
            table.HasCheckConstraint(
                "CK_NewspaperCashSales_Amount",
                "[Amount] > 0");
            table.HasCheckConstraint(
                "CK_NewspaperCashSales_CancelledAt",
                "[CancelledAt] IS NULL OR [CancelledAt] >= [CreatedAt]");
        });
        cashSale.Property(value => value.Date).HasColumnType("date");
        cashSale.Property(value => value.DistributorName)
            .HasMaxLength(120)
            .IsRequired();
        cashSale.Property(value => value.UnitPrice).HasPrecision(18, 2);
        cashSale.Property(value => value.Amount).HasPrecision(18, 2);
        cashSale.HasIndex(value => value.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("UX_NewspaperCashSales_IdempotencyKey");
        cashSale.HasIndex(value => new
        {
            value.Date,
            value.CancelledAt,
            value.DistributorId
        })
            .HasDatabaseName("IX_NewspaperCashSales_Date_Active_Distributor");
        cashSale.HasOne(value => value.Distributor)
            .WithMany(value => value.NewspaperCashSales)
            .HasForeignKey(value => value.DistributorId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureCompanySettings(ModelBuilder modelBuilder)
    {
        var settings = modelBuilder.Entity<CompanySettings>();
        settings.ToTable("CompanySettings", table =>
        {
            table.HasCheckConstraint(
                "CK_CompanySettings_SingletonKey",
                "[SingletonKey] = N'company'");
            table.HasCheckConstraint(
                "CK_CompanySettings_LogoLength",
                "[LogoDataUrl] IS NULL OR LEN([LogoDataUrl]) <= 2796227");
            table.HasCheckConstraint(
                "CK_CompanySettings_NewspaperUnitPrice",
                "[NewspaperUnitPrice] IS NULL OR [NewspaperUnitPrice] > 0");
        });
        settings.Property(value => value.SingletonKey)
            .HasMaxLength(32)
            .HasDefaultValue("company")
            .IsRequired();
        settings.Property(value => value.LogoDataUrl).HasColumnType("nvarchar(max)");
        settings.Property(value => value.NewspaperUnitPrice).HasPrecision(18, 2);
        settings.HasIndex(value => value.SingletonKey)
            .IsUnique()
            .HasDatabaseName("UX_CompanySettings_SingletonKey");
        settings.HasOne(value => value.FeaturedDistributor)
            .WithMany()
            .HasForeignKey(value => value.FeaturedDistributorId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private void ApplyAuditTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default)
                {
                    entry.Entity.CreatedAt = now;
                }

                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(value => value.CreatedAt).IsModified = false;
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
