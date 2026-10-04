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
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [UserId] uniqueidentifier NULL,
        [UserEmail] nvarchar(256) NULL,
        [Action] nvarchar(100) NOT NULL,
        [EntityName] nvarchar(100) NULL,
        [EntityId] nvarchar(100) NULL,
        [OldValues] nvarchar(4000) NULL,
        [NewValues] nvarchar(4000) NULL,
        [IpAddress] nvarchar(64) NULL,
        [UserAgent] nvarchar(512) NULL,
        [Succeeded] bit NOT NULL,
        [FailureReason] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [EducationalGrades] (
        [Id] uniqueidentifier NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [Slug] nvarchar(220) NOT NULL,
        [Stage] int NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_EducationalGrades] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [EmailDeliveries] (
        [Id] uniqueidentifier NOT NULL,
        [Event] nvarchar(80) NOT NULL,
        [UserId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NULL,
        [Recipient] nvarchar(256) NOT NULL,
        [Subject] nvarchar(200) NOT NULL,
        [Language] nvarchar(10) NOT NULL,
        [Status] int NOT NULL,
        [Attempts] int NOT NULL,
        [SentAt] datetime2 NULL,
        [FailedAt] datetime2 NULL,
        [ErrorCategory] nvarchar(80) NULL,
        [ErrorMessage] nvarchar(400) NULL,
        [CorrelationId] nvarchar(80) NULL,
        [DeduplicationKey] nvarchar(200) NULL,
        [RelatedEntityId] uniqueidentifier NULL,
        [RelatedEntityType] nvarchar(80) NULL,
        [PayloadJson] nvarchar(4000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_EmailDeliveries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [PaymentWebhookEvents] (
        [Id] uniqueidentifier NOT NULL,
        [Provider] nvarchar(50) NOT NULL,
        [EventId] nvarchar(200) NOT NULL,
        [EventType] nvarchar(100) NOT NULL,
        [Status] int NOT NULL,
        [Payload] nvarchar(max) NULL,
        [SignatureHeader] nvarchar(512) NULL,
        [TenantId] uniqueidentifier NULL,
        [TenantSubscriptionId] uniqueidentifier NULL,
        [BillingTransactionId] uniqueidentifier NULL,
        [ReceivedAt] datetime2 NOT NULL,
        [ProcessedAt] datetime2 NULL,
        [ProcessingError] nvarchar(500) NULL,
        [AttemptCount] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_PaymentWebhookEvents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Description] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [Subjects] (
        [Id] uniqueidentifier NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [Slug] nvarchar(220) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Icon] nvarchar(100) NULL,
        [ColorHex] nvarchar(9) NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Subjects] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [SubscriptionPlans] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [MonthlyPrice] decimal(18,2) NOT NULL,
        [YearlyPrice] decimal(18,2) NULL,
        [Currency] nvarchar(3) NOT NULL,
        [TrialDays] int NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [IsPublic] bit NOT NULL,
        [MaxStudents] int NULL,
        [MaxGroups] int NULL,
        [MaxCourses] int NULL,
        [MaxAssistants] int NULL,
        [MaxTeamMembers] int NULL,
        [MaxStorageMegabytes] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_SubscriptionPlans] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_SubscriptionPlans_MonthlyPrice] CHECK ([MonthlyPrice] >= 0),
        CONSTRAINT [CK_SubscriptionPlans_TrialDays] CHECK ([TrialDays] >= 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [Tenants] (
        [Id] uniqueidentifier NOT NULL,
        [Slug] nvarchar(220) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [DisplayName] nvarchar(200) NULL,
        [Status] int NOT NULL,
        [OwnerUserId] uniqueidentifier NOT NULL,
        [ContactEmail] nvarchar(256) NULL,
        [ContactPhone] nvarchar(20) NULL,
        [WhatsAppNumber] nvarchar(20) NULL,
        [City] nvarchar(100) NULL,
        [SuspendedReason] nvarchar(500) NULL,
        [SuspendedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [ProfileImageUrl] nvarchar(2048) NULL,
        [ProfileImagePublicId] nvarchar(300) NULL,
        [IsActive] bit NOT NULL,
        [IsVerified] bit NOT NULL,
        [City] nvarchar(100) NULL,
        [PreferredLanguage] nvarchar(10) NOT NULL DEFAULT N'ar-EG',
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [LastLoginAt] datetime2 NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [PlanFeatures] (
        [Id] uniqueidentifier NOT NULL,
        [SubscriptionPlanId] uniqueidentifier NOT NULL,
        [FeatureKey] nvarchar(100) NOT NULL,
        [IsEnabled] bit NOT NULL,
        [DisplayValue] nvarchar(200) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_PlanFeatures] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlanFeatures_SubscriptionPlans_SubscriptionPlanId] FOREIGN KEY ([SubscriptionPlanId]) REFERENCES [SubscriptionPlans] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [TenantBrandings] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [LogoUrl] nvarchar(2048) NULL,
        [LogoPublicId] nvarchar(300) NULL,
        [CoverImageUrl] nvarchar(2048) NULL,
        [CoverImagePublicId] nvarchar(300) NULL,
        [FaviconUrl] nvarchar(2048) NULL,
        [PrimaryColorHex] nvarchar(9) NOT NULL,
        [SecondaryColorHex] nvarchar(9) NOT NULL,
        [AccentColorHex] nvarchar(9) NOT NULL,
        [FontFamily] nvarchar(100) NULL,
        [Tagline] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TenantBrandings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantBrandings_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [TenantDomains] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Host] nvarchar(253) NOT NULL,
        [IsPrimary] bit NOT NULL,
        [VerificationStatus] int NOT NULL,
        [VerificationToken] nvarchar(100) NOT NULL,
        [VerifiedAt] datetime2 NULL,
        [LastVerificationError] nvarchar(500) NULL,
        [VerificationAttempts] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TenantDomains] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantDomains_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [TenantInvitations] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [Role] int NOT NULL,
        [Title] nvarchar(150) NULL,
        [TokenHash] nvarchar(128) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [AcceptedAt] datetime2 NULL,
        [DeclinedAt] datetime2 NULL,
        [RevokedAt] datetime2 NULL,
        [InvitedByUserId] uniqueidentifier NOT NULL,
        [PermissionsCsv] nvarchar(2000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TenantInvitations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantInvitations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [TenantSettings] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [DefaultLanguage] nvarchar(10) NOT NULL,
        [TimeZoneId] nvarchar(100) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [GradeNotificationThresholdPercentage] int NOT NULL,
        [NotifyParentsOnAbsence] bit NOT NULL,
        [NotifyParentsOnLowGrade] bit NOT NULL,
        [NotifyParentsOnHomeworkMissed] bit NOT NULL,
        [AllowStudentSelfEnrollment] bit NOT NULL,
        [AutoApproveReviews] bit NOT NULL,
        [LateThresholdMinutes] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TenantSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TenantSettings_GradeThreshold] CHECK ([GradeNotificationThresholdPercentage] >= 0 AND [GradeNotificationThresholdPercentage] <= 100),
        CONSTRAINT [FK_TenantSettings_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [TenantSubscriptions] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [SubscriptionPlanId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [BillingCycle] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [TrialEndsAt] datetime2 NULL,
        [CurrentPeriodStart] datetime2 NOT NULL,
        [CurrentPeriodEnd] datetime2 NOT NULL,
        [GracePeriodEndsAt] datetime2 NULL,
        [CanceledAt] datetime2 NULL,
        [CancelAtPeriodEnd] bit NOT NULL,
        [EndedAt] datetime2 NULL,
        [CancellationReason] nvarchar(500) NULL,
        [ProviderName] nvarchar(50) NULL,
        [ProviderSubscriptionId] nvarchar(200) NULL,
        [ProviderCustomerId] nvarchar(200) NULL,
        [FailedPaymentCount] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TenantSubscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantSubscriptions_SubscriptionPlans_SubscriptionPlanId] FOREIGN KEY ([SubscriptionPlanId]) REFERENCES [SubscriptionPlans] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TenantSubscriptions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [TenantWebsites] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [IsPublished] bit NOT NULL,
        [HeroTitle] nvarchar(200) NULL,
        [HeroSubtitle] nvarchar(500) NULL,
        [HeroImageUrl] nvarchar(2048) NULL,
        [HeroImagePublicId] nvarchar(300) NULL,
        [HeroCtaLabel] nvarchar(100) NULL,
        [HeroCtaUrl] nvarchar(2048) NULL,
        [AboutTitle] nvarchar(200) NULL,
        [AboutBody] nvarchar(4000) NULL,
        [ContactEmail] nvarchar(256) NULL,
        [ContactPhone] nvarchar(20) NULL,
        [ContactAddress] nvarchar(500) NULL,
        [WhatsAppNumber] nvarchar(20) NULL,
        [MapEmbedUrl] nvarchar(2048) NULL,
        [SeoTitle] nvarchar(200) NULL,
        [SeoDescription] nvarchar(500) NULL,
        [SeoKeywords] nvarchar(500) NULL,
        [SeoImageUrl] nvarchar(2048) NULL,
        [GoogleAnalyticsId] nvarchar(50) NULL,
        [FacebookPixelId] nvarchar(50) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TenantWebsites] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantWebsites_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [NotificationPreferences] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Category] int NOT NULL,
        [Channels] int NOT NULL,
        [IsEnabled] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_NotificationPreferences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NotificationPreferences_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Message] nvarchar(500) NOT NULL,
        [Type] int NOT NULL,
        [Category] int NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [IsRead] bit NOT NULL,
        [ReadAt] datetime2 NULL,
        [ActionUrl] nvarchar(2048) NULL,
        [ReferenceId] uniqueidentifier NULL,
        [RelatedEntityType] nvarchar(80) NULL,
        [Event] nvarchar(80) NULL,
        [Priority] int NOT NULL,
        [TitleEn] nvarchar(200) NULL,
        [MessageEn] nvarchar(500) NULL,
        [Metadata] nvarchar(2000) NULL,
        [DeduplicationKey] nvarchar(200) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [ParentProfiles] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [PhoneNumber] nvarchar(20) NULL,
        [AlternatePhoneNumber] nvarchar(20) NULL,
        [City] nvarchar(100) NULL,
        [Occupation] nvarchar(150) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_ParentProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ParentProfiles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [TokenHash] nvarchar(128) NOT NULL,
        [FamilyId] uniqueidentifier NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [RevokedAt] datetime2 NULL,
        [RevokedReason] nvarchar(200) NULL,
        [ReplacedByTokenHash] nvarchar(128) NULL,
        [CreatedByIp] nvarchar(64) NULL,
        [UserAgent] nvarchar(512) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [StudentProfiles] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [GradeId] uniqueidentifier NULL,
        [SchoolName] nvarchar(200) NULL,
        [City] nvarchar(100) NULL,
        [PhoneNumber] nvarchar(20) NULL,
        [ParentPhoneNumber] nvarchar(20) NULL,
        [DateOfBirth] date NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_StudentProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StudentProfiles_EducationalGrades_GradeId] FOREIGN KEY ([GradeId]) REFERENCES [EducationalGrades] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_StudentProfiles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [TeacherProfiles] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Bio] nvarchar(4000) NULL,
        [Headline] nvarchar(500) NULL,
        [YearsOfExperience] int NOT NULL,
        [StudentsCount] int NOT NULL,
        [Rating] decimal(3,2) NOT NULL,
        [ReviewCount] int NOT NULL,
        [CoverImageUrl] nvarchar(2048) NULL,
        [CoverImagePublicId] nvarchar(300) NULL,
        [ProfileImageUrl] nvarchar(2048) NULL,
        [ProfileImagePublicId] nvarchar(300) NULL,
        [City] nvarchar(100) NULL,
        [WhatsAppNumber] nvarchar(20) NULL,
        [PreferredTeachingMode] int NOT NULL,
        [IsFeatured] bit NOT NULL,
        [IsVerified] bit NOT NULL,
        [VerificationStatus] int NOT NULL,
        [VerifiedAt] datetime2 NULL,
        [VerificationNotes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TeacherProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TeacherProfiles_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]),
        CONSTRAINT [FK_TeacherProfiles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [TenantMembers] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Role] int NOT NULL,
        [Status] int NOT NULL,
        [Title] nvarchar(150) NULL,
        [InvitedByUserId] uniqueidentifier NULL,
        [InvitedAt] datetime2 NULL,
        [JoinedAt] datetime2 NULL,
        [DeactivatedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TenantMembers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantMembers_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TenantMembers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [Invoices] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [TenantSubscriptionId] uniqueidentifier NULL,
        [Number] nvarchar(50) NOT NULL,
        [Status] int NOT NULL,
        [Subtotal] decimal(18,2) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [IssuedAt] datetime2 NOT NULL,
        [DueAt] datetime2 NULL,
        [PaidAt] datetime2 NULL,
        [Description] nvarchar(500) NULL,
        [PeriodStart] datetime2 NULL,
        [PeriodEnd] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Invoices_Total] CHECK ([Total] >= 0),
        CONSTRAINT [FK_Invoices_TenantSubscriptions_TenantSubscriptionId] FOREIGN KEY ([TenantSubscriptionId]) REFERENCES [TenantSubscriptions] ([Id]),
        CONSTRAINT [FK_Invoices_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [SubscriptionEvents] (
        [Id] uniqueidentifier NOT NULL,
        [TenantSubscriptionId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Type] int NOT NULL,
        [Description] nvarchar(500) NULL,
        [OccurredAt] datetime2 NOT NULL,
        [MetadataJson] nvarchar(4000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_SubscriptionEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SubscriptionEvents_TenantSubscriptions_TenantSubscriptionId] FOREIGN KEY ([TenantSubscriptionId]) REFERENCES [TenantSubscriptions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [Testimonials] (
        [Id] uniqueidentifier NOT NULL,
        [TenantWebsiteId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [AuthorName] nvarchar(200) NOT NULL,
        [AuthorTitle] nvarchar(150) NULL,
        [AuthorImageUrl] nvarchar(2048) NULL,
        [Quote] nvarchar(4000) NOT NULL,
        [Rating] int NOT NULL,
        [IsVisible] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Testimonials] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Testimonials_Rating] CHECK ([Rating] >= 1 AND [Rating] <= 5),
        CONSTRAINT [FK_Testimonials_TenantWebsites_TenantWebsiteId] FOREIGN KEY ([TenantWebsiteId]) REFERENCES [TenantWebsites] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [WebsiteSections] (
        [Id] uniqueidentifier NOT NULL,
        [TenantWebsiteId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Type] int NOT NULL,
        [Title] nvarchar(200) NULL,
        [Subtitle] nvarchar(500) NULL,
        [Body] nvarchar(4000) NULL,
        [ImageUrl] nvarchar(2048) NULL,
        [ImagePublicId] nvarchar(300) NULL,
        [SettingsJson] nvarchar(4000) NULL,
        [IsVisible] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_WebsiteSections] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WebsiteSections_TenantWebsites_TenantWebsiteId] FOREIGN KEY ([TenantWebsiteId]) REFERENCES [TenantWebsites] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [ParentStudentLinks] (
        [Id] uniqueidentifier NOT NULL,
        [ParentId] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [Relationship] nvarchar(50) NULL,
        [IsConfirmed] bit NOT NULL,
        [ConfirmedAt] datetime2 NULL,
        [IsPrimaryContact] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_ParentStudentLinks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ParentStudentLinks_ParentProfiles_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [ParentProfiles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ParentStudentLinks_StudentProfiles_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [StudentProfiles] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [TenantStudents] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        [JoinedAt] datetime2 NOT NULL,
        [LeftAt] datetime2 NULL,
        [StudentCode] nvarchar(50) NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TenantStudents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantStudents_StudentProfiles_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [StudentProfiles] ([Id]),
        CONSTRAINT [FK_TenantStudents_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [Courses] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [TeacherId] uniqueidentifier NOT NULL,
        [SubjectId] uniqueidentifier NOT NULL,
        [GradeId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Slug] nvarchar(220) NOT NULL,
        [Description] nvarchar(4000) NULL,
        [ThumbnailUrl] nvarchar(2048) NULL,
        [ThumbnailPublicId] nvarchar(300) NULL,
        [Price] decimal(18,2) NOT NULL,
        [OriginalPrice] decimal(18,2) NULL,
        [DiscountPercentage] int NOT NULL,
        [DurationInMinutes] int NOT NULL,
        [LessonsCount] int NOT NULL,
        [Rating] decimal(3,2) NOT NULL,
        [ReviewCount] int NOT NULL,
        [StudentCount] int NOT NULL,
        [IsPublished] bit NOT NULL,
        [IsFeatured] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Courses] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Courses_DiscountPercentage] CHECK ([DiscountPercentage] >= 0 AND [DiscountPercentage] <= 100),
        CONSTRAINT [CK_Courses_Price] CHECK ([Price] >= 0),
        CONSTRAINT [FK_Courses_EducationalGrades_GradeId] FOREIGN KEY ([GradeId]) REFERENCES [EducationalGrades] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Courses_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [Subjects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Courses_TeacherProfiles_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [TeacherProfiles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [Discounts] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [TeacherId] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Type] int NOT NULL,
        [Value] decimal(18,2) NOT NULL,
        [MinimumAmount] decimal(18,2) NULL,
        [MaximumDiscount] decimal(18,2) NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NULL,
        [UsageLimit] int NULL,
        [UsedCount] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Discounts] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Discounts_UsedCount] CHECK ([UsedCount] >= 0),
        CONSTRAINT [CK_Discounts_Value] CHECK ([Value] > 0),
        CONSTRAINT [FK_Discounts_TeacherProfiles_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [TeacherProfiles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [Groups] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [TeacherId] uniqueidentifier NOT NULL,
        [SubjectId] uniqueidentifier NOT NULL,
        [GradeId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Slug] nvarchar(220) NOT NULL,
        [Description] nvarchar(4000) NULL,
        [Type] int NOT NULL,
        [TeachingMode] int NOT NULL,
        [Location] nvarchar(500) NULL,
        [MonthlyPrice] decimal(18,2) NOT NULL,
        [OriginalPrice] decimal(18,2) NULL,
        [DiscountPercentage] int NOT NULL,
        [Capacity] int NOT NULL,
        [EnrolledStudentsCount] int NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NULL,
        [IsPublished] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [WhatsAppLink] nvarchar(2048) NULL,
        [Rating] decimal(3,2) NOT NULL,
        [ReviewCount] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Groups] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Groups_Capacity] CHECK ([Capacity] > 0),
        CONSTRAINT [CK_Groups_DiscountPercentage] CHECK ([DiscountPercentage] >= 0 AND [DiscountPercentage] <= 100),
        CONSTRAINT [CK_Groups_EnrolledStudentsCount] CHECK ([EnrolledStudentsCount] >= 0),
        CONSTRAINT [CK_Groups_MonthlyPrice] CHECK ([MonthlyPrice] >= 0),
        CONSTRAINT [FK_Groups_EducationalGrades_GradeId] FOREIGN KEY ([GradeId]) REFERENCES [EducationalGrades] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Groups_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [Subjects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Groups_TeacherProfiles_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [TeacherProfiles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [TeacherAchievements] (
        [Id] uniqueidentifier NOT NULL,
        [TeacherId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Year] int NULL,
        [ImageUrl] nvarchar(2048) NULL,
        [ImagePublicId] nvarchar(300) NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TeacherAchievements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TeacherAchievements_TeacherProfiles_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [TeacherProfiles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [TeacherFavorites] (
        [Id] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [TeacherId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TeacherFavorites] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TeacherFavorites_StudentProfiles_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [StudentProfiles] ([Id]),
        CONSTRAINT [FK_TeacherFavorites_TeacherProfiles_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [TeacherProfiles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [TeacherGrades] (
        [Id] uniqueidentifier NOT NULL,
        [TeacherId] uniqueidentifier NOT NULL,
        [GradeId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TeacherGrades] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TeacherGrades_EducationalGrades_GradeId] FOREIGN KEY ([GradeId]) REFERENCES [EducationalGrades] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TeacherGrades_TeacherProfiles_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [TeacherProfiles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [TeacherSocialLinks] (
        [Id] uniqueidentifier NOT NULL,
        [TeacherId] uniqueidentifier NOT NULL,
        [Platform] int NOT NULL,
        [Url] nvarchar(2048) NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TeacherSocialLinks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TeacherSocialLinks_TeacherProfiles_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [TeacherProfiles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [TeacherSubjects] (
        [Id] uniqueidentifier NOT NULL,
        [TeacherId] uniqueidentifier NOT NULL,
        [SubjectId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TeacherSubjects] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TeacherSubjects_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [Subjects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TeacherSubjects_TeacherProfiles_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [TeacherProfiles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [TenantMemberPermissions] (
        [Id] uniqueidentifier NOT NULL,
        [TenantMemberId] uniqueidentifier NOT NULL,
        [Permission] nvarchar(100) NOT NULL,
        [IsGranted] bit NOT NULL,
        [GrantedByUserId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TenantMemberPermissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantMemberPermissions_TenantMembers_TenantMemberId] FOREIGN KEY ([TenantMemberId]) REFERENCES [TenantMembers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [BillingTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [TenantSubscriptionId] uniqueidentifier NULL,
        [InvoiceId] uniqueidentifier NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [Status] int NOT NULL,
        [Provider] nvarchar(50) NOT NULL,
        [ProviderTransactionId] nvarchar(200) NULL,
        [ProviderReference] nvarchar(200) NULL,
        [FailureReason] nvarchar(500) NULL,
        [PaidAt] datetime2 NULL,
        [RefundedAt] datetime2 NULL,
        [MetadataJson] nvarchar(4000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_BillingTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BillingTransactions_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]),
        CONSTRAINT [FK_BillingTransactions_TenantSubscriptions_TenantSubscriptionId] FOREIGN KEY ([TenantSubscriptionId]) REFERENCES [TenantSubscriptions] ([Id]),
        CONSTRAINT [FK_BillingTransactions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [Certificates] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [CourseEnrollmentId] uniqueidentifier NOT NULL,
        [TeacherId] uniqueidentifier NOT NULL,
        [CertificateNumber] nvarchar(40) NOT NULL,
        [VerificationCode] nvarchar(64) NOT NULL,
        [Status] int NOT NULL,
        [IssuedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NOT NULL,
        [RevokedAt] datetime2 NULL,
        [RevocationReason] nvarchar(500) NULL,
        [StudentName] nvarchar(200) NOT NULL,
        [CourseTitle] nvarchar(200) NOT NULL,
        [TeacherName] nvarchar(200) NOT NULL,
        [WorkspaceName] nvarchar(200) NOT NULL,
        [LessonsCompleted] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Certificates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Certificates_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]),
        CONSTRAINT [FK_Certificates_StudentProfiles_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [StudentProfiles] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [CourseFavorites] (
        [Id] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_CourseFavorites] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CourseFavorites_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CourseFavorites_StudentProfiles_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [StudentProfiles] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [CourseSections] (
        [Id] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_CourseSections] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CourseSections_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [CourseEnrollments] (
        [Id] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [EnrolledAt] datetime2 NOT NULL,
        [CancelledAt] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        [CancellationReason] nvarchar(500) NULL,
        [PricePaid] decimal(18,2) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [FinalPrice] decimal(18,2) NOT NULL,
        [DiscountId] uniqueidentifier NULL,
        [DiscountCode] nvarchar(50) NULL,
        [ProgressPercentage] int NOT NULL,
        [LastAccessedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_CourseEnrollments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CourseEnrollments_Progress] CHECK ([ProgressPercentage] >= 0 AND [ProgressPercentage] <= 100),
        CONSTRAINT [FK_CourseEnrollments_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CourseEnrollments_Discounts_DiscountId] FOREIGN KEY ([DiscountId]) REFERENCES [Discounts] ([Id]),
        CONSTRAINT [FK_CourseEnrollments_StudentProfiles_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [StudentProfiles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [Announcements] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [GroupId] uniqueidentifier NULL,
        [Title] nvarchar(200) NOT NULL,
        [Body] nvarchar(4000) NOT NULL,
        [Audience] int NOT NULL,
        [IsPinned] bit NOT NULL,
        [IsPublished] bit NOT NULL,
        [PublishedAt] datetime2 NULL,
        [ExpiresAt] datetime2 NULL,
        [ImageUrl] nvarchar(2048) NULL,
        [AttachmentUrl] nvarchar(2048) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Announcements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Announcements_Groups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [Groups] ([Id]),
        CONSTRAINT [FK_Announcements_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [Conversations] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Type] int NOT NULL,
        [Subject] nvarchar(200) NULL,
        [GroupId] uniqueidentifier NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [LastMessageAt] datetime2 NULL,
        [LastMessagePreview] nvarchar(300) NULL,
        [IsArchived] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Conversations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Conversations_Groups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [Groups] ([Id]),
        CONSTRAINT [FK_Conversations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [Exams] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [TeacherId] uniqueidentifier NOT NULL,
        [GroupId] uniqueidentifier NULL,
        [SubjectId] uniqueidentifier NOT NULL,
        [GradeId] uniqueidentifier NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(4000) NULL,
        [Type] int NOT NULL,
        [ExamDate] datetime2 NOT NULL,
        [DurationInMinutes] int NOT NULL,
        [MaximumScore] decimal(8,2) NOT NULL,
        [PassingScore] decimal(8,2) NOT NULL,
        [IsPublished] bit NOT NULL,
        [PublishedAt] datetime2 NULL,
        [ResultsPublished] bit NOT NULL,
        [ResultsPublishedAt] datetime2 NULL,
        [IsOnline] bit NOT NULL,
        [MaxAttempts] int NOT NULL,
        [ShowResultsImmediately] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Exams] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Exams_Duration] CHECK ([DurationInMinutes] >= 0),
        CONSTRAINT [CK_Exams_MaximumScore] CHECK ([MaximumScore] > 0),
        CONSTRAINT [CK_Exams_PassingScore] CHECK ([PassingScore] >= 0 AND [PassingScore] <= [MaximumScore]),
        CONSTRAINT [FK_Exams_EducationalGrades_GradeId] FOREIGN KEY ([GradeId]) REFERENCES [EducationalGrades] ([Id]),
        CONSTRAINT [FK_Exams_Groups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [Groups] ([Id]),
        CONSTRAINT [FK_Exams_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [Subjects] ([Id]),
        CONSTRAINT [FK_Exams_TeacherProfiles_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [TeacherProfiles] ([Id]),
        CONSTRAINT [FK_Exams_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [GroupCurriculums] (
        [Id] uniqueidentifier NOT NULL,
        [GroupId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_GroupCurriculums] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GroupCurriculums_Groups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [Groups] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [GroupEnrollments] (
        [Id] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [GroupId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [EnrolledAt] datetime2 NOT NULL,
        [CancelledAt] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        [CancellationReason] nvarchar(500) NULL,
        [PricePaid] decimal(18,2) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [FinalPrice] decimal(18,2) NOT NULL,
        [DiscountId] uniqueidentifier NULL,
        [DiscountCode] nvarchar(50) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_GroupEnrollments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GroupEnrollments_Discounts_DiscountId] FOREIGN KEY ([DiscountId]) REFERENCES [Discounts] ([Id]),
        CONSTRAINT [FK_GroupEnrollments_Groups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [Groups] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GroupEnrollments_StudentProfiles_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [StudentProfiles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [GroupMaterials] (
        [Id] uniqueidentifier NOT NULL,
        [GroupId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [FileUrl] nvarchar(2048) NOT NULL,
        [FilePublicId] nvarchar(300) NULL,
        [FileType] int NOT NULL,
        [FileSizeInBytes] bigint NULL,
        [SortOrder] int NOT NULL,
        [IsEnrolledOnly] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_GroupMaterials] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GroupMaterials_Groups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [Groups] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [GroupSchedules] (
        [Id] uniqueidentifier NOT NULL,
        [GroupId] uniqueidentifier NOT NULL,
        [DayOfWeek] int NOT NULL,
        [StartTime] time NOT NULL,
        [EndTime] time NOT NULL,
        [Location] nvarchar(500) NULL,
        [MeetingUrl] nvarchar(2048) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_GroupSchedules] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_GroupSchedules_TimeRange] CHECK ([EndTime] > [StartTime]),
        CONSTRAINT [FK_GroupSchedules_Groups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [Groups] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [Homeworks] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [TeacherId] uniqueidentifier NOT NULL,
        [GroupId] uniqueidentifier NOT NULL,
        [SubjectId] uniqueidentifier NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(4000) NULL,
        [Instructions] nvarchar(4000) NULL,
        [AssignedAt] datetime2 NOT NULL,
        [DueAt] datetime2 NOT NULL,
        [MaximumScore] decimal(8,2) NOT NULL,
        [AllowLateSubmission] bit NOT NULL,
        [IsPublished] bit NOT NULL,
        [PublishedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Homeworks] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Homeworks_MaximumScore] CHECK ([MaximumScore] > 0),
        CONSTRAINT [FK_Homeworks_Groups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [Groups] ([Id]),
        CONSTRAINT [FK_Homeworks_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [Subjects] ([Id]),
        CONSTRAINT [FK_Homeworks_TeacherProfiles_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [TeacherProfiles] ([Id]),
        CONSTRAINT [FK_Homeworks_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [Inquiries] (
        [Id] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [TeacherId] uniqueidentifier NOT NULL,
        [GroupId] uniqueidentifier NULL,
        [CourseId] uniqueidentifier NULL,
        [Name] nvarchar(200) NOT NULL,
        [PhoneNumber] nvarchar(20) NOT NULL,
        [Email] nvarchar(256) NULL,
        [Message] nvarchar(4000) NOT NULL,
        [Status] int NOT NULL,
        [TeacherNote] nvarchar(500) NULL,
        [RespondedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Inquiries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Inquiries_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]),
        CONSTRAINT [FK_Inquiries_Groups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [Groups] ([Id]),
        CONSTRAINT [FK_Inquiries_StudentProfiles_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [StudentProfiles] ([Id]),
        CONSTRAINT [FK_Inquiries_TeacherProfiles_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [TeacherProfiles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [Reviews] (
        [Id] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [TeacherId] uniqueidentifier NOT NULL,
        [GroupId] uniqueidentifier NULL,
        [CourseId] uniqueidentifier NULL,
        [Rating] int NOT NULL,
        [Comment] nvarchar(4000) NULL,
        [IsApproved] bit NOT NULL,
        [ApprovedAt] datetime2 NULL,
        [ModerationNote] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Reviews_Rating] CHECK ([Rating] >= 1 AND [Rating] <= 5),
        CONSTRAINT [FK_Reviews_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reviews_Groups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [Groups] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reviews_StudentProfiles_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [StudentProfiles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Reviews_TeacherProfiles_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [TeacherProfiles] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [CourseLessons] (
        [Id] uniqueidentifier NOT NULL,
        [SectionId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [VideoUrl] nvarchar(2048) NULL,
        [VideoPublicId] nvarchar(300) NULL,
        [DurationInMinutes] int NOT NULL,
        [SortOrder] int NOT NULL,
        [IsPreview] bit NOT NULL,
        [IsPublished] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_CourseLessons] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CourseLessons_Duration] CHECK ([DurationInMinutes] >= 0),
        CONSTRAINT [FK_CourseLessons_CourseSections_SectionId] FOREIGN KEY ([SectionId]) REFERENCES [CourseSections] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [ConversationParticipants] (
        [Id] uniqueidentifier NOT NULL,
        [ConversationId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [JoinedAt] datetime2 NOT NULL,
        [LeftAt] datetime2 NULL,
        [LastReadAt] datetime2 NULL,
        [UnreadCount] int NOT NULL,
        [IsMuted] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_ConversationParticipants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ConversationParticipants_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [Conversations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ConversationParticipants_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [Messages] (
        [Id] uniqueidentifier NOT NULL,
        [ConversationId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [SenderUserId] uniqueidentifier NOT NULL,
        [Body] nvarchar(4000) NOT NULL,
        [SentAt] datetime2 NOT NULL,
        [EditedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Messages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Messages_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [Conversations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Messages_Users_SenderUserId] FOREIGN KEY ([SenderUserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [ExamAttempts] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ExamId] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [AttemptNumber] int NOT NULL,
        [Status] int NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NULL,
        [SubmittedAt] datetime2 NULL,
        [Score] decimal(8,2) NOT NULL,
        [MaximumScore] decimal(8,2) NOT NULL,
        [Percentage] decimal(5,2) NOT NULL,
        [IsPassed] bit NOT NULL,
        [RequiresManualGrading] bit NOT NULL,
        [GradedAt] datetime2 NULL,
        [GradedByUserId] uniqueidentifier NULL,
        [TeacherComment] nvarchar(500) NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_ExamAttempts] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ExamAttempts_AttemptNumber] CHECK ([AttemptNumber] > 0),
        CONSTRAINT [FK_ExamAttempts_Exams_ExamId] FOREIGN KEY ([ExamId]) REFERENCES [Exams] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ExamAttempts_StudentProfiles_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [StudentProfiles] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [ExamQuestions] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ExamId] uniqueidentifier NOT NULL,
        [Text] nvarchar(4000) NOT NULL,
        [Type] int NOT NULL,
        [Points] decimal(8,2) NOT NULL,
        [SortOrder] int NOT NULL,
        [CorrectAnswer] nvarchar(500) NULL,
        [Explanation] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_ExamQuestions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ExamQuestions_Points] CHECK ([Points] > 0),
        CONSTRAINT [FK_ExamQuestions_Exams_ExamId] FOREIGN KEY ([ExamId]) REFERENCES [Exams] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [ExamResults] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ExamId] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [Score] decimal(8,2) NOT NULL,
        [MaximumScore] decimal(8,2) NOT NULL,
        [Percentage] decimal(5,2) NOT NULL,
        [IsPassed] bit NOT NULL,
        [IsAbsent] bit NOT NULL,
        [TeacherComment] nvarchar(500) NULL,
        [RecordedByUserId] uniqueidentifier NOT NULL,
        [RecordedAt] datetime2 NOT NULL,
        [ParentNotified] bit NOT NULL,
        [ParentNotifiedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_ExamResults] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ExamResults_Percentage] CHECK ([Percentage] >= 0 AND [Percentage] <= 100),
        CONSTRAINT [CK_ExamResults_Score] CHECK ([Score] >= 0),
        CONSTRAINT [FK_ExamResults_Exams_ExamId] FOREIGN KEY ([ExamId]) REFERENCES [Exams] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ExamResults_StudentProfiles_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [StudentProfiles] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [EducationalPayments] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [ParentId] uniqueidentifier NULL,
        [Type] int NOT NULL,
        [GroupId] uniqueidentifier NULL,
        [CourseId] uniqueidentifier NULL,
        [GroupEnrollmentId] uniqueidentifier NULL,
        [CourseEnrollmentId] uniqueidentifier NULL,
        [InvoiceNumber] nvarchar(50) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [NetAmount] decimal(18,2) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [DiscountId] uniqueidentifier NULL,
        [DiscountCode] nvarchar(50) NULL,
        [Status] int NOT NULL,
        [Provider] nvarchar(50) NULL,
        [ProviderTransactionId] nvarchar(200) NULL,
        [PeriodMonth] date NULL,
        [DueAt] datetime2 NULL,
        [PaidAt] datetime2 NULL,
        [Notes] nvarchar(500) NULL,
        [RecordedByUserId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_EducationalPayments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_EducationalPayments_Amount] CHECK ([Amount] >= 0),
        CONSTRAINT [CK_EducationalPayments_Net] CHECK ([NetAmount] >= 0),
        CONSTRAINT [FK_EducationalPayments_CourseEnrollments_CourseEnrollmentId] FOREIGN KEY ([CourseEnrollmentId]) REFERENCES [CourseEnrollments] ([Id]),
        CONSTRAINT [FK_EducationalPayments_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]),
        CONSTRAINT [FK_EducationalPayments_Discounts_DiscountId] FOREIGN KEY ([DiscountId]) REFERENCES [Discounts] ([Id]),
        CONSTRAINT [FK_EducationalPayments_GroupEnrollments_GroupEnrollmentId] FOREIGN KEY ([GroupEnrollmentId]) REFERENCES [GroupEnrollments] ([Id]),
        CONSTRAINT [FK_EducationalPayments_Groups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [Groups] ([Id]),
        CONSTRAINT [FK_EducationalPayments_ParentProfiles_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [ParentProfiles] ([Id]),
        CONSTRAINT [FK_EducationalPayments_StudentProfiles_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [StudentProfiles] ([Id]),
        CONSTRAINT [FK_EducationalPayments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [AttendanceSessions] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [GroupId] uniqueidentifier NOT NULL,
        [GroupScheduleId] uniqueidentifier NULL,
        [SessionDate] datetime2 NOT NULL,
        [StartTime] time NULL,
        [EndTime] time NULL,
        [Title] nvarchar(200) NULL,
        [Notes] nvarchar(500) NULL,
        [Status] int NOT NULL,
        [RecordedByUserId] uniqueidentifier NOT NULL,
        [ClosedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_AttendanceSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AttendanceSessions_GroupSchedules_GroupScheduleId] FOREIGN KEY ([GroupScheduleId]) REFERENCES [GroupSchedules] ([Id]),
        CONSTRAINT [FK_AttendanceSessions_Groups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [Groups] ([Id]),
        CONSTRAINT [FK_AttendanceSessions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [HomeworkAttachments] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [HomeworkId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(200) NOT NULL,
        [FileUrl] nvarchar(2048) NOT NULL,
        [FilePublicId] nvarchar(300) NULL,
        [FileType] int NOT NULL,
        [FileSizeInBytes] bigint NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_HomeworkAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HomeworkAttachments_Homeworks_HomeworkId] FOREIGN KEY ([HomeworkId]) REFERENCES [Homeworks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [HomeworkSubmissions] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [HomeworkId] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [Content] nvarchar(4000) NULL,
        [FileUrl] nvarchar(2048) NULL,
        [FilePublicId] nvarchar(300) NULL,
        [SubmittedAt] datetime2 NULL,
        [IsLate] bit NOT NULL,
        [Score] decimal(8,2) NULL,
        [MaximumScore] decimal(8,2) NULL,
        [Percentage] decimal(5,2) NULL,
        [TeacherFeedback] nvarchar(500) NULL,
        [GradedByUserId] uniqueidentifier NULL,
        [GradedAt] datetime2 NULL,
        [ParentNotified] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_HomeworkSubmissions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_HomeworkSubmissions_Score] CHECK ([Score] IS NULL OR [Score] >= 0),
        CONSTRAINT [FK_HomeworkSubmissions_Homeworks_HomeworkId] FOREIGN KEY ([HomeworkId]) REFERENCES [Homeworks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_HomeworkSubmissions_StudentProfiles_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [StudentProfiles] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [CourseLessonProgress] (
        [Id] uniqueidentifier NOT NULL,
        [CourseEnrollmentId] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [LessonId] uniqueidentifier NOT NULL,
        [IsCompleted] bit NOT NULL,
        [CompletedAt] datetime2 NULL,
        [WatchedSeconds] int NOT NULL,
        [LastAccessedAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_CourseLessonProgress] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CourseLessonProgress_WatchedSeconds] CHECK ([WatchedSeconds] >= 0),
        CONSTRAINT [FK_CourseLessonProgress_CourseEnrollments_CourseEnrollmentId] FOREIGN KEY ([CourseEnrollmentId]) REFERENCES [CourseEnrollments] ([Id]),
        CONSTRAINT [FK_CourseLessonProgress_CourseLessons_LessonId] FOREIGN KEY ([LessonId]) REFERENCES [CourseLessons] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [MessageAttachments] (
        [Id] uniqueidentifier NOT NULL,
        [MessageId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(200) NOT NULL,
        [FileUrl] nvarchar(2048) NOT NULL,
        [FilePublicId] nvarchar(300) NULL,
        [FileType] int NOT NULL,
        [FileSizeInBytes] bigint NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_MessageAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MessageAttachments_Messages_MessageId] FOREIGN KEY ([MessageId]) REFERENCES [Messages] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [ExamAttemptAnswers] (
        [Id] uniqueidentifier NOT NULL,
        [AttemptId] uniqueidentifier NOT NULL,
        [QuestionId] uniqueidentifier NOT NULL,
        [SelectedOptionId] uniqueidentifier NULL,
        [TextAnswer] nvarchar(4000) NULL,
        [IsCorrect] bit NULL,
        [AwardedPoints] decimal(8,2) NOT NULL,
        [MaximumPoints] decimal(8,2) NOT NULL,
        [TeacherFeedback] nvarchar(500) NULL,
        [AnsweredAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_ExamAttemptAnswers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExamAttemptAnswers_ExamAttempts_AttemptId] FOREIGN KEY ([AttemptId]) REFERENCES [ExamAttempts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ExamAttemptAnswers_ExamQuestions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [ExamQuestions] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [ExamQuestionOptions] (
        [Id] uniqueidentifier NOT NULL,
        [QuestionId] uniqueidentifier NOT NULL,
        [Text] nvarchar(500) NOT NULL,
        [IsCorrect] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_ExamQuestionOptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExamQuestionOptions_ExamQuestions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [ExamQuestions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE TABLE [AttendanceRecords] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [AttendanceSessionId] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [MinutesLate] int NULL,
        [Note] nvarchar(500) NULL,
        [RecordedByUserId] uniqueidentifier NOT NULL,
        [RecordedAt] datetime2 NOT NULL,
        [ParentNotified] bit NOT NULL,
        [ParentNotifiedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_AttendanceRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_AttendanceRecords_MinutesLate] CHECK ([MinutesLate] IS NULL OR [MinutesLate] >= 0),
        CONSTRAINT [FK_AttendanceRecords_AttendanceSessions_AttendanceSessionId] FOREIGN KEY ([AttendanceSessionId]) REFERENCES [AttendanceSessions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AttendanceRecords_StudentProfiles_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [StudentProfiles] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Announcements_GroupId] ON [Announcements] ([GroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Announcements_TenantId_Audience] ON [Announcements] ([TenantId], [Audience]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Announcements_TenantId_Published] ON [Announcements] ([TenantId], [IsPublished], [PublishedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_AttendanceRecords_StudentId] ON [AttendanceRecords] ([StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_AttendanceRecords_TenantId_StudentId_Status] ON [AttendanceRecords] ([TenantId], [StudentId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_AttendanceRecords_SessionId_StudentId] ON [AttendanceRecords] ([AttendanceSessionId], [StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_AttendanceSessions_GroupScheduleId] ON [AttendanceSessions] ([GroupScheduleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_AttendanceSessions_TenantId_SessionDate] ON [AttendanceSessions] ([TenantId], [SessionDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_AttendanceSessions_TenantId_Status] ON [AttendanceSessions] ([TenantId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_AttendanceSessions_GroupId_SessionDate] ON [AttendanceSessions] ([GroupId], [SessionDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Action_CreatedAt] ON [AuditLogs] ([Action], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Entity] ON [AuditLogs] ([EntityName], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_TenantId_CreatedAt] ON [AuditLogs] ([TenantId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId_CreatedAt] ON [AuditLogs] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_BillingTransactions_InvoiceId] ON [BillingTransactions] ([InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_BillingTransactions_SubscriptionId] ON [BillingTransactions] ([TenantSubscriptionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_BillingTransactions_TenantId_Status_CreatedAt] ON [BillingTransactions] ([TenantId], [Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_BillingTransactions_Provider_TransactionId] ON [BillingTransactions] ([Provider], [ProviderTransactionId]) WHERE [ProviderTransactionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Certificates_CourseId] ON [Certificates] ([CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Certificates_StudentId] ON [Certificates] ([StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Certificates_TenantId_IssuedAt] ON [Certificates] ([TenantId], [IssuedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Certificates_CourseEnrollmentId] ON [Certificates] ([CourseEnrollmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Certificates_Number] ON [Certificates] ([CertificateNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Certificates_VerificationCode] ON [Certificates] ([VerificationCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_ConversationParticipants_UserId_TenantId] ON [ConversationParticipants] ([UserId], [TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ConversationParticipants_ConversationId_UserId] ON [ConversationParticipants] ([ConversationId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Conversations_GroupId] ON [Conversations] ([GroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Conversations_TenantId_LastMessageAt] ON [Conversations] ([TenantId], [LastMessageAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_CourseEnrollments_CourseId] ON [CourseEnrollments] ([CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_CourseEnrollments_DiscountId] ON [CourseEnrollments] ([DiscountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_CourseEnrollments_StudentId] ON [CourseEnrollments] ([StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_CourseEnrollments_Student_Course_Active] ON [CourseEnrollments] ([StudentId], [CourseId]) WHERE [Status] <> 3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_CourseFavorites_CourseId] ON [CourseFavorites] ([CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_CourseFavorites_Student_Course] ON [CourseFavorites] ([StudentId], [CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_CourseLessonProgress_LessonId] ON [CourseLessonProgress] ([LessonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_CourseLessonProgress_StudentId_CourseId] ON [CourseLessonProgress] ([StudentId], [CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_CourseLessonProgress_Enrollment_Lesson] ON [CourseLessonProgress] ([CourseEnrollmentId], [LessonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_CourseLessons_IsPreview] ON [CourseLessons] ([IsPreview]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_CourseLessons_SectionId_SortOrder] ON [CourseLessons] ([SectionId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Courses_GradeId] ON [Courses] ([GradeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Courses_Published_Featured_Rating] ON [Courses] ([IsPublished], [IsFeatured], [Rating]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Courses_SubjectId] ON [Courses] ([SubjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Courses_TeacherId] ON [Courses] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Courses_TenantId_IsPublished] ON [Courses] ([TenantId], [IsPublished]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Courses_Slug] ON [Courses] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_CourseSections_CourseId_SortOrder] ON [CourseSections] ([CourseId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Discounts_Active_Window] ON [Discounts] ([IsActive], [StartDate], [EndDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Discounts_Code] ON [Discounts] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Discounts_TenantId_IsActive] ON [Discounts] ([TenantId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Discounts_TeacherId_Code] ON [Discounts] ([TeacherId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_EducationalGrades_IsActive_SortOrder] ON [EducationalGrades] ([IsActive], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EducationalGrades_Slug] ON [EducationalGrades] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_EducationalPayments_CourseEnrollmentId] ON [EducationalPayments] ([CourseEnrollmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_EducationalPayments_CourseId] ON [EducationalPayments] ([CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_EducationalPayments_DiscountId] ON [EducationalPayments] ([DiscountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_EducationalPayments_GroupEnrollmentId] ON [EducationalPayments] ([GroupEnrollmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_EducationalPayments_GroupId] ON [EducationalPayments] ([GroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_EducationalPayments_PaidAt] ON [EducationalPayments] ([PaidAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_EducationalPayments_ParentId] ON [EducationalPayments] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_EducationalPayments_StudentId] ON [EducationalPayments] ([StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_EducationalPayments_TenantId_Status_CreatedAt] ON [EducationalPayments] ([TenantId], [Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_EducationalPayments_TenantId_StudentId] ON [EducationalPayments] ([TenantId], [StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_EducationalPayments_Provider_TransactionId] ON [EducationalPayments] ([Provider], [ProviderTransactionId]) WHERE [ProviderTransactionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_EducationalPayments_TenantId_InvoiceNumber] ON [EducationalPayments] ([TenantId], [InvoiceNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_EmailDeliveries_Event] ON [EmailDeliveries] ([Event]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_EmailDeliveries_Status_CreatedAt] ON [EmailDeliveries] ([Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_EmailDeliveries_UserId_CreatedAt] ON [EmailDeliveries] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_EmailDeliveries_Recipient_DeduplicationKey] ON [EmailDeliveries] ([Recipient], [DeduplicationKey]) WHERE [DeduplicationKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_ExamAttemptAnswers_QuestionId] ON [ExamAttemptAnswers] ([QuestionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ExamAttemptAnswers_Attempt_Question] ON [ExamAttemptAnswers] ([AttemptId], [QuestionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_ExamAttempts_StudentId] ON [ExamAttempts] ([StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_ExamAttempts_TenantId_Status] ON [ExamAttempts] ([TenantId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ExamAttempts_Exam_Student_Attempt] ON [ExamAttempts] ([ExamId], [StudentId], [AttemptNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_ExamQuestionOptions_QuestionId_SortOrder] ON [ExamQuestionOptions] ([QuestionId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_ExamQuestions_ExamId_SortOrder] ON [ExamQuestions] ([ExamId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_ExamQuestions_TenantId] ON [ExamQuestions] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_ExamResults_StudentId] ON [ExamResults] ([StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_ExamResults_TenantId_RecordedAt] ON [ExamResults] ([TenantId], [RecordedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_ExamResults_TenantId_StudentId] ON [ExamResults] ([TenantId], [StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ExamResults_ExamId_StudentId] ON [ExamResults] ([ExamId], [StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Exams_GradeId] ON [Exams] ([GradeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Exams_GroupId] ON [Exams] ([GroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Exams_SubjectId] ON [Exams] ([SubjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Exams_TeacherId] ON [Exams] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Exams_TenantId_ExamDate] ON [Exams] ([TenantId], [ExamDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Exams_TenantId_IsPublished] ON [Exams] ([TenantId], [IsPublished]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_GroupCurriculums_GroupId_SortOrder] ON [GroupCurriculums] ([GroupId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_GroupEnrollments_DiscountId] ON [GroupEnrollments] ([DiscountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_GroupEnrollments_EnrolledAt] ON [GroupEnrollments] ([EnrolledAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_GroupEnrollments_GroupId] ON [GroupEnrollments] ([GroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_GroupEnrollments_Student_Group_Status] ON [GroupEnrollments] ([StudentId], [GroupId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_GroupEnrollments_Student_Group_Active] ON [GroupEnrollments] ([StudentId], [GroupId]) WHERE [Status] <> 3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_GroupMaterials_GroupId_SortOrder] ON [GroupMaterials] ([GroupId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Groups_GradeId] ON [Groups] ([GradeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Groups_IsPublished] ON [Groups] ([IsPublished]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Groups_Published_Active_Subject_Grade] ON [Groups] ([IsPublished], [IsActive], [SubjectId], [GradeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Groups_SubjectId] ON [Groups] ([SubjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Groups_TeacherId] ON [Groups] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Groups_TenantId_IsActive] ON [Groups] ([TenantId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Groups_Slug] ON [Groups] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_GroupSchedules_GroupId_Day_Start] ON [GroupSchedules] ([GroupId], [DayOfWeek], [StartTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_GroupSchedules_IsActive_Day] ON [GroupSchedules] ([IsActive], [DayOfWeek]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_HomeworkAttachments_HomeworkId] ON [HomeworkAttachments] ([HomeworkId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Homeworks_GroupId_IsPublished] ON [Homeworks] ([GroupId], [IsPublished]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Homeworks_SubjectId] ON [Homeworks] ([SubjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Homeworks_TeacherId] ON [Homeworks] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Homeworks_TenantId_DueAt] ON [Homeworks] ([TenantId], [DueAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_HomeworkSubmissions_StudentId] ON [HomeworkSubmissions] ([StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_HomeworkSubmissions_TenantId_StudentId_Status] ON [HomeworkSubmissions] ([TenantId], [StudentId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_HomeworkSubmissions_HomeworkId_StudentId] ON [HomeworkSubmissions] ([HomeworkId], [StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Inquiries_CourseId] ON [Inquiries] ([CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Inquiries_CreatedAt] ON [Inquiries] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Inquiries_GroupId] ON [Inquiries] ([GroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Inquiries_StudentId] ON [Inquiries] ([StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Inquiries_TeacherId_Status] ON [Inquiries] ([TeacherId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Inquiries_TenantId_Status] ON [Inquiries] ([TenantId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_SubscriptionId] ON [Invoices] ([TenantSubscriptionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_TenantId_Status_IssuedAt] ON [Invoices] ([TenantId], [Status], [IssuedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Invoices_Number] ON [Invoices] ([Number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_MessageAttachments_MessageId] ON [MessageAttachments] ([MessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Messages_ConversationId_SentAt] ON [Messages] ([ConversationId], [SentAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Messages_SenderUserId] ON [Messages] ([SenderUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Messages_TenantId] ON [Messages] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_NotificationPreferences_UserId_Category] ON [NotificationPreferences] ([UserId], [Category]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_TenantId] ON [Notifications] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId] ON [Notifications] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_Category] ON [Notifications] ([UserId], [Category]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_CreatedAt] ON [Notifications] ([UserId], [CreatedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_IsRead_CreatedAt] ON [Notifications] ([UserId], [IsRead], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Notifications_UserId_DeduplicationKey] ON [Notifications] ([UserId], [DeduplicationKey]) WHERE [DeduplicationKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_ParentProfiles_PhoneNumber] ON [ParentProfiles] ([PhoneNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ParentProfiles_UserId] ON [ParentProfiles] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_ParentStudentLinks_ParentId_IsConfirmed] ON [ParentStudentLinks] ([ParentId], [IsConfirmed]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_ParentStudentLinks_StudentId] ON [ParentStudentLinks] ([StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ParentStudentLinks_ParentId_StudentId] ON [ParentStudentLinks] ([ParentId], [StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_PaymentWebhookEvents_Status_ReceivedAt] ON [PaymentWebhookEvents] ([Status], [ReceivedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_PaymentWebhookEvents_TenantId] ON [PaymentWebhookEvents] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_PaymentWebhookEvents_Provider_EventId] ON [PaymentWebhookEvents] ([Provider], [EventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_PlanFeatures_PlanId_FeatureKey] ON [PlanFeatures] ([SubscriptionPlanId], [FeatureKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_FamilyId] ON [RefreshTokens] ([FamilyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId_ExpiresAt] ON [RefreshTokens] ([UserId], [ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_CourseId] ON [Reviews] ([CourseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_GroupId] ON [Reviews] ([GroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_TeacherId] ON [Reviews] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_TeacherId_IsApproved] ON [Reviews] ([TeacherId], [IsApproved]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Reviews_Student_Course] ON [Reviews] ([StudentId], [CourseId]) WHERE [CourseId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Reviews_Student_Group] ON [Reviews] ([StudentId], [GroupId]) WHERE [GroupId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Reviews_Student_Teacher] ON [Reviews] ([StudentId], [TeacherId]) WHERE [GroupId] IS NULL AND [CourseId] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [Roles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_StudentProfiles_GradeId] ON [StudentProfiles] ([GradeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StudentProfiles_UserId] ON [StudentProfiles] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Subjects_IsActive] ON [Subjects] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Subjects_Slug] ON [Subjects] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_SubscriptionEvents_SubscriptionId_OccurredAt] ON [SubscriptionEvents] ([TenantSubscriptionId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_SubscriptionEvents_TenantId_Type] ON [SubscriptionEvents] ([TenantId], [Type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_SubscriptionPlans_Active_Public_Sort] ON [SubscriptionPlans] ([IsActive], [IsPublic], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_SubscriptionPlans_Code] ON [SubscriptionPlans] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_TeacherAchievements_TeacherId_SortOrder] ON [TeacherAchievements] ([TeacherId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_TeacherFavorites_TeacherId] ON [TeacherFavorites] ([TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TeacherFavorites_Student_Teacher] ON [TeacherFavorites] ([StudentId], [TeacherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_TeacherGrades_GradeId] ON [TeacherGrades] ([GradeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TeacherGrades_TeacherId_GradeId] ON [TeacherGrades] ([TeacherId], [GradeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_TeacherProfiles_City] ON [TeacherProfiles] ([City]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_TeacherProfiles_IsFeatured] ON [TeacherProfiles] ([IsFeatured]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_TeacherProfiles_IsVerified] ON [TeacherProfiles] ([IsVerified]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_TeacherProfiles_Rating] ON [TeacherProfiles] ([Rating]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TeacherProfiles_UserId] ON [TeacherProfiles] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_TeacherProfiles_Verified_Featured_Rating] ON [TeacherProfiles] ([IsVerified], [IsFeatured], [Rating]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TeacherProfiles_TenantId] ON [TeacherProfiles] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TeacherSocialLinks_TeacherId_Platform] ON [TeacherSocialLinks] ([TeacherId], [Platform]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_TeacherSubjects_SubjectId] ON [TeacherSubjects] ([SubjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TeacherSubjects_TeacherId_SubjectId] ON [TeacherSubjects] ([TeacherId], [SubjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TenantBrandings_TenantId] ON [TenantBrandings] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_TenantDomains_TenantId_Status] ON [TenantDomains] ([TenantId], [VerificationStatus]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TenantDomains_Host] ON [TenantDomains] ([Host]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_TenantDomains_TenantId_Primary] ON [TenantDomains] ([TenantId]) WHERE [IsPrimary] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_TenantInvitations_Email] ON [TenantInvitations] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_TenantInvitations_TenantId_Email] ON [TenantInvitations] ([TenantId], [Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TenantInvitations_TokenHash] ON [TenantInvitations] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TenantMemberPermissions_Member_Permission] ON [TenantMemberPermissions] ([TenantMemberId], [Permission]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_TenantMembers_TenantId_Role_Status] ON [TenantMembers] ([TenantId], [Role], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_TenantMembers_UserId] ON [TenantMembers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TenantMembers_TenantId_UserId] ON [TenantMembers] ([TenantId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Tenants_CreatedAt] ON [Tenants] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Tenants_OwnerUserId] ON [Tenants] ([OwnerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Tenants_Status] ON [Tenants] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Tenants_Slug] ON [Tenants] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TenantSettings_TenantId] ON [TenantSettings] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_TenantStudents_StudentId] ON [TenantStudents] ([StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_TenantStudents_TenantId_IsActive] ON [TenantStudents] ([TenantId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TenantStudents_TenantId_StudentId] ON [TenantStudents] ([TenantId], [StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_TenantSubscriptions_PlanId] ON [TenantSubscriptions] ([SubscriptionPlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TenantSubscriptions_ProviderSubscriptionId] ON [TenantSubscriptions] ([ProviderSubscriptionId]) WHERE [ProviderSubscriptionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_TenantSubscriptions_Status_PeriodEnd] ON [TenantSubscriptions] ([Status], [CurrentPeriodEnd]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TenantSubscriptions_TenantId] ON [TenantSubscriptions] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TenantWebsites_TenantId] ON [TenantWebsites] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Testimonials_TenantId_SortOrder] ON [Testimonials] ([TenantId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Testimonials_TenantWebsiteId] ON [Testimonials] ([TenantWebsiteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [Users] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_Users_IsActive] ON [Users] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [Users] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_WebsiteSections_TenantId_SortOrder] ON [WebsiteSections] ([TenantId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    CREATE INDEX [IX_WebsiteSections_WebsiteId_IsVisible] ON [WebsiteSections] ([TenantWebsiteId], [IsVisible]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928171424_initalCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260928171424_initalCreate', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930054644_AddTeacherVerificationSubmission'
)
BEGIN
    ALTER TABLE [TeacherProfiles] ADD [VerificationSubmittedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930054644_AddTeacherVerificationSubmission'
)
BEGIN
    ALTER TABLE [HomeworkSubmissions] ADD [ReturnReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930054644_AddTeacherVerificationSubmission'
)
BEGIN
    ALTER TABLE [HomeworkSubmissions] ADD [ReturnedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930054644_AddTeacherVerificationSubmission'
)
BEGIN
    ALTER TABLE [HomeworkSubmissions] ADD [SubmissionCount] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930054644_AddTeacherVerificationSubmission'
)
BEGIN
    ALTER TABLE [Homeworks] ADD [AllowResubmission] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930054644_AddTeacherVerificationSubmission'
)
BEGIN
    ALTER TABLE [Homeworks] ADD [ClosedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930054644_AddTeacherVerificationSubmission'
)
BEGIN
    ALTER TABLE [Homeworks] ADD [MaxSubmissions] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930054644_AddTeacherVerificationSubmission'
)
BEGIN
    CREATE TABLE [ActivityEvents] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Type] nvarchar(80) NOT NULL,
        [ActorUserId] uniqueidentifier NULL,
        [ActorName] nvarchar(200) NOT NULL,
        [StudentId] uniqueidentifier NULL,
        [RelatedEntityType] nvarchar(80) NULL,
        [RelatedEntityId] uniqueidentifier NULL,
        [Metadata] nvarchar(1000) NULL,
        [Visibility] int NOT NULL,
        [OccurredAt] datetime2 NOT NULL,
        [DeduplicationKey] nvarchar(200) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_ActivityEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ActivityEvents_StudentProfiles_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [StudentProfiles] ([Id]),
        CONSTRAINT [FK_ActivityEvents_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930054644_AddTeacherVerificationSubmission'
)
BEGIN
    CREATE INDEX [IX_HomeworkSubmissions_TenantId_Status_SubmittedAt] ON [HomeworkSubmissions] ([TenantId], [Status], [SubmittedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930054644_AddTeacherVerificationSubmission'
)
BEGIN
    EXEC(N'ALTER TABLE [HomeworkSubmissions] ADD CONSTRAINT [CK_HomeworkSubmissions_SubmissionCount] CHECK ([SubmissionCount] >= 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930054644_AddTeacherVerificationSubmission'
)
BEGIN
    CREATE INDEX [IX_Homeworks_TenantId_IsPublished_ClosedAt] ON [Homeworks] ([TenantId], [IsPublished], [ClosedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930054644_AddTeacherVerificationSubmission'
)
BEGIN
    EXEC(N'ALTER TABLE [Homeworks] ADD CONSTRAINT [CK_Homeworks_MaxSubmissions] CHECK ([MaxSubmissions] IS NULL OR [MaxSubmissions] > 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930054644_AddTeacherVerificationSubmission'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ActivityEvents_StudentId_OccurredAt] ON [ActivityEvents] ([StudentId], [OccurredAt] DESC) WHERE [StudentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930054644_AddTeacherVerificationSubmission'
)
BEGIN
    CREATE INDEX [IX_ActivityEvents_TenantId_OccurredAt] ON [ActivityEvents] ([TenantId], [OccurredAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930054644_AddTeacherVerificationSubmission'
)
BEGIN
    CREATE INDEX [IX_ActivityEvents_TenantId_Type_OccurredAt] ON [ActivityEvents] ([TenantId], [Type], [OccurredAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930054644_AddTeacherVerificationSubmission'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ActivityEvents_TenantId_DeduplicationKey] ON [ActivityEvents] ([TenantId], [DeduplicationKey]) WHERE [DeduplicationKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930054644_AddTeacherVerificationSubmission'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260930054644_AddTeacherVerificationSubmission', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001052232_AddEdits'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001052232_AddEdits', N'10.0.12');
END;

COMMIT;
GO

