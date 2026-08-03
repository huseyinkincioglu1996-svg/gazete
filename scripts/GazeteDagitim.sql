IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE TABLE [CashHandovers] (
        [Id] int NOT NULL IDENTITY,
        [Date] date NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        [DeliveredAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CashHandovers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CashHandovers_DeliveredAt] CHECK (([Status] = 0 AND [DeliveredAt] IS NULL) OR ([Status] = 1 AND [DeliveredAt] IS NOT NULL)),
        CONSTRAINT [CK_CashHandovers_Status] CHECK ([Status] BETWEEN 0 AND 1),
        CONSTRAINT [CK_CashHandovers_Total] CHECK ([Total] >= 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE TABLE [Distributors] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(120) NOT NULL,
        [Address] nvarchar(500) NOT NULL,
        [Phone] nvarchar(40) NOT NULL,
        [ProfileImageDataUrl] nvarchar(max) NULL,
        [Zone] int NOT NULL,
        [PaymentType] int NOT NULL,
        [NewspaperPrice] decimal(18,2) NOT NULL DEFAULT 5.0,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Distributors] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Distributors_NewspaperPrice] CHECK ([NewspaperPrice] >= 0),
        CONSTRAINT [CK_Distributors_PaymentType] CHECK ([PaymentType] BETWEEN 0 AND 2),
        CONSTRAINT [CK_Distributors_ProfileImageLength] CHECK ([ProfileImageDataUrl] IS NULL OR LEN([ProfileImageDataUrl]) <= 2796227),
        CONSTRAINT [CK_Distributors_Zone] CHECK ([Zone] IN (1, 2))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE TABLE [PaymentPeriods] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(120) COLLATE Turkish_CI_AS NOT NULL,
        [DayCount] int NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_PaymentPeriods] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PaymentPeriods_DayCount] CHECK ([DayCount] BETWEEN 1 AND 365)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE TABLE [CashHandoverItems] (
        [Id] int NOT NULL IDENTITY,
        [CashHandoverId] int NOT NULL,
        [SubscriberName] nvarchar(200) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        CONSTRAINT [PK_CashHandoverItems] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CashHandoverItems_Amount] CHECK ([Amount] >= 0),
        CONSTRAINT [FK_CashHandoverItems_CashHandovers_CashHandoverId] FOREIGN KEY ([CashHandoverId]) REFERENCES [CashHandovers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE TABLE [CompanySettings] (
        [Id] int NOT NULL IDENTITY,
        [SingletonKey] nvarchar(32) NOT NULL DEFAULT N'company',
        [LogoDataUrl] nvarchar(max) NULL,
        [FeaturedDistributorId] int NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CompanySettings] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CompanySettings_LogoLength] CHECK ([LogoDataUrl] IS NULL OR LEN([LogoDataUrl]) <= 2796227),
        CONSTRAINT [CK_CompanySettings_SingletonKey] CHECK ([SingletonKey] = N'company'),
        CONSTRAINT [FK_CompanySettings_Distributors_FeaturedDistributorId] FOREIGN KEY ([FeaturedDistributorId]) REFERENCES [Distributors] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE TABLE [Deliveries] (
        [Id] int NOT NULL IDENTITY,
        [DistributorId] int NOT NULL,
        [Date] date NOT NULL,
        [Day] int NOT NULL,
        [NewspaperCount] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        [Notes] nvarchar(1000) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Deliveries] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Deliveries_Amount] CHECK ([Amount] >= 0),
        CONSTRAINT [CK_Deliveries_Day] CHECK ([Day] BETWEEN 0 AND 6),
        CONSTRAINT [CK_Deliveries_NewspaperCount] CHECK ([NewspaperCount] >= 0),
        CONSTRAINT [CK_Deliveries_Status] CHECK ([Status] BETWEEN 0 AND 2),
        CONSTRAINT [FK_Deliveries_Distributors_DistributorId] FOREIGN KEY ([DistributorId]) REFERENCES [Distributors] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE TABLE [DistributorDistributionDays] (
        [DistributorId] int NOT NULL,
        [Day] int NOT NULL,
        CONSTRAINT [PK_DistributorDistributionDays] PRIMARY KEY ([DistributorId], [Day]),
        CONSTRAINT [CK_DistributorDistributionDays_Day] CHECK ([Day] BETWEEN 0 AND 6),
        CONSTRAINT [FK_DistributorDistributionDays_Distributors_DistributorId] FOREIGN KEY ([DistributorId]) REFERENCES [Distributors] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE TABLE [DistributorMonthlyPaymentDays] (
        [DistributorId] int NOT NULL,
        [DayOfMonth] int NOT NULL,
        CONSTRAINT [PK_DistributorMonthlyPaymentDays] PRIMARY KEY ([DistributorId], [DayOfMonth]),
        CONSTRAINT [CK_DistributorMonthlyPaymentDays_DayOfMonth] CHECK ([DayOfMonth] BETWEEN 1 AND 31),
        CONSTRAINT [FK_DistributorMonthlyPaymentDays_Distributors_DistributorId] FOREIGN KEY ([DistributorId]) REFERENCES [Distributors] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE TABLE [DistributorWeeklyPaymentDays] (
        [DistributorId] int NOT NULL,
        [Day] int NOT NULL,
        CONSTRAINT [PK_DistributorWeeklyPaymentDays] PRIMARY KEY ([DistributorId], [Day]),
        CONSTRAINT [CK_DistributorWeeklyPaymentDays_Day] CHECK ([Day] BETWEEN 0 AND 6),
        CONSTRAINT [FK_DistributorWeeklyPaymentDays_Distributors_DistributorId] FOREIGN KEY ([DistributorId]) REFERENCES [Distributors] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] int NOT NULL IDENTITY,
        [DistributorId] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Date] date NOT NULL,
        [PeriodStart] date NOT NULL,
        [PeriodEnd] date NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [PaymentType] int NOT NULL,
        [Status] int NOT NULL,
        [PaidAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Payments_Amount] CHECK ([Amount] >= 0),
        CONSTRAINT [CK_Payments_PaidAt] CHECK (([Status] = 0 AND [PaidAt] IS NULL) OR [Status] = 1),
        CONSTRAINT [CK_Payments_Period] CHECK ([PeriodEnd] >= [PeriodStart]),
        CONSTRAINT [CK_Payments_Status] CHECK ([Status] BETWEEN 0 AND 1),
        CONSTRAINT [CK_Payments_Type] CHECK ([PaymentType] BETWEEN 0 AND 2),
        CONSTRAINT [FK_Payments_Distributors_DistributorId] FOREIGN KEY ([DistributorId]) REFERENCES [Distributors] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE TABLE [Subscribers] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(160) NOT NULL,
        [Phone] nvarchar(40) NOT NULL,
        [Address] nvarchar(500) NOT NULL,
        [MonthlyFee] decimal(18,2) NOT NULL,
        [Notes] nvarchar(1000) NOT NULL,
        [IsActive] bit NOT NULL,
        [PaymentPeriodId] int NULL,
        [DistributorId] int NULL,
        [Latitude] decimal(9,6) NULL,
        [Longitude] decimal(10,6) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Subscribers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Subscribers_Latitude] CHECK ([Latitude] IS NULL OR [Latitude] BETWEEN -90 AND 90),
        CONSTRAINT [CK_Subscribers_LocationPair] CHECK (([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL)),
        CONSTRAINT [CK_Subscribers_Longitude] CHECK ([Longitude] IS NULL OR [Longitude] BETWEEN -180 AND 180),
        CONSTRAINT [CK_Subscribers_MonthlyFee] CHECK ([MonthlyFee] >= 0),
        CONSTRAINT [FK_Subscribers_Distributors_DistributorId] FOREIGN KEY ([DistributorId]) REFERENCES [Distributors] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Subscribers_PaymentPeriods_PaymentPeriodId] FOREIGN KEY ([PaymentPeriodId]) REFERENCES [PaymentPeriods] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE TABLE [SubscriberDailyDeliveries] (
        [Id] int NOT NULL IDENTITY,
        [SubscriberId] int NOT NULL,
        [DistributorId] int NULL,
        [DistributorName] nvarchar(120) NOT NULL,
        [Date] date NOT NULL,
        [NewspaperCount] int NOT NULL,
        [IsDelivered] bit NOT NULL,
        [IsCollected] bit NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaymentMethod] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_SubscriberDailyDeliveries] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_SubscriberDailyDeliveries_Amount] CHECK ([Amount] >= 0 AND ([IsCollected] = 0 OR [Amount] > 0)),
        CONSTRAINT [CK_SubscriberDailyDeliveries_NewspaperCount] CHECK ([NewspaperCount] IN (1, 2)),
        CONSTRAINT [CK_SubscriberDailyDeliveries_PaymentMethod] CHECK ([PaymentMethod] BETWEEN 0 AND 2),
        CONSTRAINT [FK_SubscriberDailyDeliveries_Distributors_DistributorId] FOREIGN KEY ([DistributorId]) REFERENCES [Distributors] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_SubscriberDailyDeliveries_Subscribers_SubscriberId] FOREIGN KEY ([SubscriberId]) REFERENCES [Subscribers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE TABLE [SubscriberPublicationDays] (
        [SubscriberId] int NOT NULL,
        [Day] int NOT NULL,
        CONSTRAINT [PK_SubscriberPublicationDays] PRIMARY KEY ([SubscriberId], [Day]),
        CONSTRAINT [CK_SubscriberPublicationDays_Day] CHECK ([Day] BETWEEN 0 AND 7),
        CONSTRAINT [FK_SubscriberPublicationDays_Subscribers_SubscriberId] FOREIGN KEY ([SubscriberId]) REFERENCES [Subscribers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE TABLE [SubscriberDailyDeliveryCoveredDates] (
        [SubscriberDailyDeliveryId] int NOT NULL,
        [CoveredDate] date NOT NULL,
        CONSTRAINT [PK_SubscriberDailyDeliveryCoveredDates] PRIMARY KEY ([SubscriberDailyDeliveryId], [CoveredDate]),
        CONSTRAINT [FK_SubscriberDailyDeliveryCoveredDates_SubscriberDailyDeliveries_SubscriberDailyDeliveryId] FOREIGN KEY ([SubscriberDailyDeliveryId]) REFERENCES [SubscriberDailyDeliveries] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CashHandoverItems_CashHandoverId] ON [CashHandoverItems] ([CashHandoverId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CashHandovers_Status_Date] ON [CashHandovers] ([Status], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_CashHandovers_Date] ON [CashHandovers] ([Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CompanySettings_FeaturedDistributorId] ON [CompanySettings] ([FeaturedDistributorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_CompanySettings_SingletonKey] ON [CompanySettings] ([SingletonKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Deliveries_Date_Status] ON [Deliveries] ([Date], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Deliveries_Distributor_Date] ON [Deliveries] ([DistributorId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Distributors_Active_Name] ON [Distributors] ([IsActive], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PaymentPeriods_Active_Name] ON [PaymentPeriods] ([IsActive], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_PaymentPeriods_Name] ON [PaymentPeriods] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_Date_Status] ON [Payments] ([Date], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_Distributor_Type_PeriodEnd] ON [Payments] ([DistributorId], [PaymentType], [PeriodEnd] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Payments_Distributor_Type_Period] ON [Payments] ([DistributorId], [PaymentType], [PeriodStart], [PeriodEnd]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SubscriberDailyDeliveries_Date_Collected] ON [SubscriberDailyDeliveries] ([Date] DESC, [IsCollected]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SubscriberDailyDeliveries_Tracking] ON [SubscriberDailyDeliveries] ([DistributorId], [Date] DESC, [IsCollected], [PaymentMethod]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_SubscriberDailyDeliveries_Subscriber_Date] ON [SubscriberDailyDeliveries] ([SubscriberId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Subscribers_Active_Name] ON [Subscribers] ([IsActive], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Subscribers_DistributorId] ON [Subscribers] ([DistributorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Subscribers_Name] ON [Subscribers] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Subscribers_PaymentPeriodId] ON [Subscribers] ([PaymentPeriodId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728194958_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260728194958_InitialCreate', N'9.0.18');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728202002_RequirePaidPaymentTimestamp'
)
BEGIN
    ALTER TABLE [Payments] DROP CONSTRAINT [CK_Payments_PaidAt];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728202002_RequirePaidPaymentTimestamp'
)
BEGIN
    EXEC(N'ALTER TABLE [Payments] ADD CONSTRAINT [CK_Payments_PaidAt] CHECK (([Status] = 0 AND [PaidAt] IS NULL) OR ([Status] = 1 AND [PaidAt] IS NOT NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728202002_RequirePaidPaymentTimestamp'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260728202002_RequirePaidPaymentTimestamp', N'9.0.18');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730224121_AddPaymentPeriodCollectionSchedule'
)
BEGIN
    ALTER TABLE [PaymentPeriods] ADD [CollectionAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730224121_AddPaymentPeriodCollectionSchedule'
)
BEGIN
    ALTER TABLE [PaymentPeriods] ADD [CollectionDayOfMonth] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730224121_AddPaymentPeriodCollectionSchedule'
)
BEGIN
    ALTER TABLE [PaymentPeriods] ADD [CollectionTime] time(0) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730224121_AddPaymentPeriodCollectionSchedule'
)
BEGIN
    EXEC(N'ALTER TABLE [PaymentPeriods] ADD CONSTRAINT [CK_PaymentPeriods_CollectionDay] CHECK ([CollectionDayOfMonth] IS NULL OR [CollectionDayOfMonth] BETWEEN 1 AND 31)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730224121_AddPaymentPeriodCollectionSchedule'
)
BEGIN
    EXEC(N'ALTER TABLE [PaymentPeriods] ADD CONSTRAINT [CK_PaymentPeriods_CollectionSchedule] CHECK (([CollectionDayOfMonth] IS NULL AND [CollectionTime] IS NULL AND [CollectionAmount] IS NULL) OR ([CollectionDayOfMonth] IS NOT NULL AND [CollectionTime] IS NOT NULL AND [CollectionAmount] > 0))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730224121_AddPaymentPeriodCollectionSchedule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260730224121_AddPaymentPeriodCollectionSchedule', N'9.0.18');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730232611_AddSubscriberPaymentHistory'
)
BEGIN
    ALTER TABLE [Subscribers] ADD [DeactivatedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730232611_AddSubscriberPaymentHistory'
)
BEGIN
    ALTER TABLE [SubscriberDailyDeliveries] ADD [CollectedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730232611_AddSubscriberPaymentHistory'
)
BEGIN
    ALTER TABLE [SubscriberDailyDeliveries] ADD [CollectionDayCount] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730232611_AddSubscriberPaymentHistory'
)
BEGIN
    ALTER TABLE [SubscriberDailyDeliveries] ADD [CollectionPeriodName] nvarchar(120) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730232611_AddSubscriberPaymentHistory'
)
BEGIN
    CREATE TABLE [SubscriberPaymentDeferrals] (
        [Id] int NOT NULL IDENTITY,
        [SubscriberId] int NOT NULL,
        [OriginalDueDate] date NOT NULL,
        [PreviousDueDate] date NOT NULL,
        [DeferredUntil] date NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [CancelledAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_SubscriberPaymentDeferrals] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_SubscriberPaymentDeferrals_Dates] CHECK ([OriginalDueDate] <= [PreviousDueDate] AND [DeferredUntil] > [PreviousDueDate]),
        CONSTRAINT [FK_SubscriberPaymentDeferrals_Subscribers_SubscriberId] FOREIGN KEY ([SubscriberId]) REFERENCES [Subscribers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730232611_AddSubscriberPaymentHistory'
)
BEGIN
    UPDATE [Subscribers]
    SET [DeactivatedAt] = [UpdatedAt]
    WHERE [IsActive] = 0 AND [DeactivatedAt] IS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730232611_AddSubscriberPaymentHistory'
)
BEGIN
    EXEC(N'ALTER TABLE [Subscribers] ADD CONSTRAINT [CK_Subscribers_ActivationState] CHECK (([IsActive] = 1 AND [DeactivatedAt] IS NULL) OR ([IsActive] = 0 AND [DeactivatedAt] IS NOT NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730232611_AddSubscriberPaymentHistory'
)
BEGIN
    EXEC(N'ALTER TABLE [SubscriberDailyDeliveries] ADD CONSTRAINT [CK_SubscriberDailyDeliveries_CollectionSnapshot] CHECK ([IsCollected] = 1 OR ([CollectedAt] IS NULL AND [CollectionDayCount] IS NULL AND [CollectionPeriodName] = N''''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730232611_AddSubscriberPaymentHistory'
)
BEGIN
    CREATE INDEX [IX_SubscriberPaymentDeferrals_History] ON [SubscriberPaymentDeferrals] ([SubscriberId], [OriginalDueDate], [CreatedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730232611_AddSubscriberPaymentHistory'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_SubscriberPaymentDeferrals_Active] ON [SubscriberPaymentDeferrals] ([SubscriberId], [OriginalDueDate]) WHERE [CancelledAt] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730232611_AddSubscriberPaymentHistory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260730232611_AddSubscriberPaymentHistory', N'9.0.18');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260731091425_AddNewspaperCashSales'
)
BEGIN
    CREATE TABLE [NewspaperCashSales] (
        [Id] int NOT NULL IDENTITY,
        [Date] date NOT NULL,
        [DistributorId] int NOT NULL,
        [DistributorName] nvarchar(120) NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [IdempotencyKey] uniqueidentifier NOT NULL,
        [CancelledAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_NewspaperCashSales] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_NewspaperCashSales_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [CK_NewspaperCashSales_CancelledAt] CHECK ([CancelledAt] IS NULL OR [CancelledAt] >= [CreatedAt]),
        CONSTRAINT [CK_NewspaperCashSales_Quantity] CHECK ([Quantity] BETWEEN 1 AND 1000),
        CONSTRAINT [CK_NewspaperCashSales_UnitPrice] CHECK ([UnitPrice] > 0),
        CONSTRAINT [FK_NewspaperCashSales_Distributors_DistributorId] FOREIGN KEY ([DistributorId]) REFERENCES [Distributors] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260731091425_AddNewspaperCashSales'
)
BEGIN
    CREATE INDEX [IX_NewspaperCashSales_Date_Active_Distributor] ON [NewspaperCashSales] ([Date], [CancelledAt], [DistributorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260731091425_AddNewspaperCashSales'
)
BEGIN
    CREATE INDEX [IX_NewspaperCashSales_DistributorId] ON [NewspaperCashSales] ([DistributorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260731091425_AddNewspaperCashSales'
)
BEGIN
    CREATE UNIQUE INDEX [UX_NewspaperCashSales_IdempotencyKey] ON [NewspaperCashSales] ([IdempotencyKey]);
END;

COMMIT;
GO
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260731091425_AddNewspaperCashSales'
)
BEGIN
    DECLARE @databaseName sysname = DB_NAME();
    DECLARE @statement nvarchar(max) =
        N'ALTER DATABASE ' + QUOTENAME(@databaseName) +
        N' SET AUTO_CLOSE OFF';
    EXEC sys.sp_executesql @statement;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260731091425_AddNewspaperCashSales'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260731091425_AddNewspaperCashSales', N'9.0.18');
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260731093540_AddCompanyNewspaperUnitPrice'
)
BEGIN
    ALTER TABLE [CompanySettings] ADD [NewspaperUnitPrice] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260731093540_AddCompanyNewspaperUnitPrice'
)
BEGIN
    EXEC(N'ALTER TABLE [CompanySettings] ADD CONSTRAINT [CK_CompanySettings_NewspaperUnitPrice] CHECK ([NewspaperUnitPrice] IS NULL OR [NewspaperUnitPrice] > 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260731093540_AddCompanyNewspaperUnitPrice'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260731093540_AddCompanyNewspaperUnitPrice', N'9.0.18');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802205731_AddDailyPaymentPeriod'
)
BEGIN
    ALTER TABLE [Subscribers] ADD [PaymentPeriodStartedOn] date NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802205731_AddDailyPaymentPeriod'
)
BEGIN
    ALTER TABLE [PaymentPeriods] ADD [Frequency] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802205731_AddDailyPaymentPeriod'
)
BEGIN
    EXEC(N'ALTER TABLE [PaymentPeriods] ADD CONSTRAINT [CK_PaymentPeriods_DailyDayCount] CHECK ([Frequency] <> 1 OR [DayCount] = 1)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802205731_AddDailyPaymentPeriod'
)
BEGIN
    EXEC(N'ALTER TABLE [PaymentPeriods] ADD CONSTRAINT [CK_PaymentPeriods_Frequency] CHECK ([Frequency] IN (0, 1))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802205731_AddDailyPaymentPeriod'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260802205731_AddDailyPaymentPeriod', N'9.0.18');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803115523_AddDeliveryColumnVisibilitySetting'
)
BEGIN
    ALTER TABLE [CompanySettings] ADD [ShowDistributorAndCoverage] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803115523_AddDeliveryColumnVisibilitySetting'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260803115523_AddDeliveryColumnVisibilitySetting', N'9.0.18');
END;

COMMIT;
GO
