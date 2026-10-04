BEGIN TRANSACTION;
ALTER TABLE [TeacherProfiles] ADD [VerificationSubmittedAt] datetime2 NULL;

ALTER TABLE [HomeworkSubmissions] ADD [ReturnReason] nvarchar(500) NULL;

ALTER TABLE [HomeworkSubmissions] ADD [ReturnedAt] datetime2 NULL;

ALTER TABLE [HomeworkSubmissions] ADD [SubmissionCount] int NOT NULL DEFAULT 0;

ALTER TABLE [Homeworks] ADD [AllowResubmission] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Homeworks] ADD [ClosedAt] datetime2 NULL;

ALTER TABLE [Homeworks] ADD [MaxSubmissions] int NULL;

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

CREATE INDEX [IX_HomeworkSubmissions_TenantId_Status_SubmittedAt] ON [HomeworkSubmissions] ([TenantId], [Status], [SubmittedAt]);

ALTER TABLE [HomeworkSubmissions] ADD CONSTRAINT [CK_HomeworkSubmissions_SubmissionCount] CHECK ([SubmissionCount] >= 0);

CREATE INDEX [IX_Homeworks_TenantId_IsPublished_ClosedAt] ON [Homeworks] ([TenantId], [IsPublished], [ClosedAt]);

ALTER TABLE [Homeworks] ADD CONSTRAINT [CK_Homeworks_MaxSubmissions] CHECK ([MaxSubmissions] IS NULL OR [MaxSubmissions] > 0);

CREATE INDEX [IX_ActivityEvents_StudentId_OccurredAt] ON [ActivityEvents] ([StudentId], [OccurredAt] DESC) WHERE [StudentId] IS NOT NULL;

CREATE INDEX [IX_ActivityEvents_TenantId_OccurredAt] ON [ActivityEvents] ([TenantId], [OccurredAt] DESC);

CREATE INDEX [IX_ActivityEvents_TenantId_Type_OccurredAt] ON [ActivityEvents] ([TenantId], [Type], [OccurredAt] DESC);

CREATE UNIQUE INDEX [UX_ActivityEvents_TenantId_DeduplicationKey] ON [ActivityEvents] ([TenantId], [DeduplicationKey]) WHERE [DeduplicationKey] IS NOT NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260930054644_AddTeacherVerificationSubmission', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261001052232_AddEdits', N'10.0.12');

COMMIT;
GO

