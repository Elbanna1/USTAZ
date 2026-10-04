using System.Linq.Expressions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Security;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Payments;

/// <summary>
/// Tuition a family pays a teacher. Kept entirely apart from the USTAZ SaaS billing feature:
/// different table, different numbering, different endpoints, different permissions.
/// </summary>
public sealed record EducationalPaymentDto
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public Guid? ParentId { get; init; }

    public EducationalPaymentType Type { get; init; }

    public string TypeName => Type.ToString();

    public Guid? GroupId { get; init; }

    public string? GroupName { get; init; }

    public Guid? CourseId { get; init; }

    public string? CourseTitle { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public decimal DiscountAmount { get; init; }

    public decimal NetAmount { get; init; }

    public string Currency { get; init; } = "EGP";

    public string? DiscountCode { get; init; }

    public PaymentStatus Status { get; init; }

    public string StatusName => Status.ToString();

    public string? Provider { get; init; }

    public DateOnly? PeriodMonth { get; init; }

    public DateTime? DueAt { get; init; }

    public DateTime? PaidAt { get; init; }

    public string? Notes { get; init; }

    public DateTime CreatedAt { get; init; }
}

internal static class EducationalPaymentProjections
{
    public static Expression<Func<EducationalPayment, EducationalPaymentDto>> ToDto =>
        payment => new EducationalPaymentDto
        {
            Id = payment.Id,
            TenantId = payment.TenantId,
            StudentId = payment.StudentId,
            StudentName = payment.Student.User.DisplayName,
            ParentId = payment.ParentId,
            Type = payment.Type,
            GroupId = payment.GroupId,
            GroupName = payment.Group != null ? payment.Group.Name : null,
            CourseId = payment.CourseId,
            CourseTitle = payment.Course != null ? payment.Course.Title : null,
            InvoiceNumber = payment.InvoiceNumber,
            Amount = payment.Amount,
            DiscountAmount = payment.DiscountAmount,
            NetAmount = payment.NetAmount,
            Currency = payment.Currency,
            DiscountCode = payment.DiscountCode,
            Status = payment.Status,
            Provider = payment.Provider,
            PeriodMonth = payment.PeriodMonth,
            DueAt = payment.DueAt,
            PaidAt = payment.PaidAt,
            Notes = payment.Notes,
            CreatedAt = payment.CreatedAt
        };
}

// ---------------------------------------------------------------------------
// Record a tuition payment
// ---------------------------------------------------------------------------
public sealed record RecordEducationalPaymentCommand : IRequest<EducationalPaymentDto>
{
    public Guid StudentId { get; init; }

    public EducationalPaymentType Type { get; init; } = EducationalPaymentType.MonthlySubscription;

    public Guid? GroupId { get; init; }

    public Guid? CourseId { get; init; }

    public decimal Amount { get; init; }

    public decimal DiscountAmount { get; init; }

    public string? DiscountCode { get; init; }

    public DateOnly? PeriodMonth { get; init; }

    public DateTime? DueAt { get; init; }

    /// <summary>"cash", "instapay", "fawry", ... Cash is the norm in Egyptian centres.</summary>
    public string? Provider { get; init; }

    /// <summary>When true the payment is recorded as already settled.</summary>
    public bool MarkAsPaid { get; init; } = true;

    public string? Notes { get; init; }
}

public sealed class RecordEducationalPaymentCommandValidator : AbstractValidator<RecordEducationalPaymentCommand>
{
    public RecordEducationalPaymentCommandValidator()
    {
        RuleFor(command => command.StudentId).NotEmpty();
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.Amount).GreaterThanOrEqualTo(0).LessThanOrEqualTo(DomainRules.MaxPrice);
        RuleFor(command => command.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Provider).MaximumLength(50);
        RuleFor(command => command.Notes).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.DiscountCode).MaximumLength(50);

        RuleFor(command => command.DiscountAmount)
            .LessThanOrEqualTo(command => command.Amount)
            .WithMessage("The discount cannot exceed the amount due.");
    }
}

public sealed class RecordEducationalPaymentCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IPlanLimitService planLimits,
    IDateTimeProvider dateTimeProvider,
    IAuditLogger auditLogger,
    INotificationDispatcher notifications) : IRequestHandler<RecordEducationalPaymentCommand, EducationalPaymentDto>
{
    public async Task<EducationalPaymentDto> Handle(
        RecordEducationalPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.PaymentsManage, cancellationToken: cancellationToken);

        await planLimits.EnsureFeatureAsync(
            access.TenantId, PlanFeatures.EducationalPayments, cancellationToken);

        // The student must belong to this workspace.
        var belongs = await context.TenantStudents.AnyAsync(
            link => link.TenantId == access.TenantId && link.StudentId == command.StudentId,
            cancellationToken);

        if (!belongs)
        {
            throw new NotFoundException("No student with this identifier belongs to your workspace.");
        }

        if (command.GroupId.HasValue)
        {
            var groupTenantId = await context.Groups
                .AsNoTracking()
                .Where(group => group.Id == command.GroupId.Value)
                .Select(group => (Guid?)group.TenantId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Group", command.GroupId.Value);

            tenantAuthorization.EnsureSameTenant(access, groupTenantId);
        }

        var utcNow = dateTimeProvider.UtcNow;

        var payment = new EducationalPayment
        {
            Id = Guid.NewGuid(),
            TenantId = access.TenantId,
            StudentId = command.StudentId,
            Type = command.Type,
            GroupId = command.GroupId,
            CourseId = command.CourseId,
            InvoiceNumber = await NextInvoiceNumberAsync(access.TenantId, utcNow, cancellationToken),
            Amount = command.Amount,
            DiscountAmount = command.DiscountAmount,
            DiscountCode = command.DiscountCode?.Trim().ToUpperInvariant(),
            PeriodMonth = command.PeriodMonth,
            DueAt = command.DueAt,
            Provider = command.Provider?.Trim().ToLowerInvariant() ?? "cash",
            Notes = command.Notes?.Trim(),
            RecordedByUserId = access.UserId,
            Status = PaymentStatus.Pending,
            CreatedAt = utcNow
        };

        payment.RecalculateNet();

        if (command.MarkAsPaid)
        {
            payment.MarkPaid(utcNow, payment.Provider);
        }

        await context.EducationalPayments.AddAsync(payment, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await PaymentNotifier.AnnounceAsync(
            context,
            notifications,
            payment,
            payment.IsSettled ? NotificationEvents.PaymentSucceeded : NotificationEvents.PaymentRecorded,
            "payment-recorded:" + payment.Id,
            cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.PaymentRecorded,
            nameof(EducationalPayment),
            payment.Id,
            newValues: new { payment.InvoiceNumber, payment.NetAmount, payment.Status },
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        return await context.EducationalPayments
            .AsNoTracking()
            .Where(entity => entity.Id == payment.Id)
            .Select(EducationalPaymentProjections.ToDto)
            .FirstAsync(cancellationToken);
    }

    /// <summary>Tuition invoice numbers run per workspace, separate from USTAZ SaaS numbering.</summary>
    private async Task<string> NextInvoiceNumberAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var prefix = "T-" + utcNow.Year + "-";

        var last = await context.EducationalPayments
            .AsNoTracking()
            .Where(payment => payment.TenantId == tenantId && payment.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(payment => payment.InvoiceNumber)
            .Select(payment => payment.InvoiceNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var sequence = 1;

        if (last is not null && int.TryParse(last[prefix.Length..], out var parsed))
        {
            sequence = parsed + 1;
        }

        return prefix + sequence.ToString("D5");
    }
}

public sealed record MarkEducationalPaymentPaidCommand(Guid PaymentId) : IRequest<EducationalPaymentDto>;

public sealed class MarkEducationalPaymentPaidCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IAuditLogger auditLogger,
    INotificationDispatcher notifications) : IRequestHandler<MarkEducationalPaymentPaidCommand, EducationalPaymentDto>
{
    public async Task<EducationalPaymentDto> Handle(
        MarkEducationalPaymentPaidCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.PaymentsManage, cancellationToken: cancellationToken);

        var payment = await context.EducationalPayments
            .FirstOrDefaultAsync(entity => entity.Id == command.PaymentId, cancellationToken)
            ?? throw new NotFoundException("EducationalPayment", command.PaymentId);

        tenantAuthorization.EnsureSameTenant(access, payment.TenantId);

        payment.MarkPaid(dateTimeProvider.UtcNow);
        await context.SaveChangesAsync(cancellationToken);

        await PaymentNotifier.AnnounceAsync(
            context,
            notifications,
            payment,
            NotificationEvents.PaymentSucceeded,
            "payment-paid:" + payment.Id,
            cancellationToken);

        await PaymentNotifier.AnnounceToWorkspaceAsync(
            context, notifications, payment, cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.PaymentRecorded,
            nameof(EducationalPayment),
            payment.Id,
            newValues: new { payment.Status },
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        return await context.EducationalPayments
            .AsNoTracking()
            .Where(entity => entity.Id == payment.Id)
            .Select(EducationalPaymentProjections.ToDto)
            .FirstAsync(cancellationToken);
    }
}

public sealed record RefundEducationalPaymentCommand(Guid PaymentId, string? Reason)
    : IRequest<EducationalPaymentDto>;

public sealed class RefundEducationalPaymentCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IAuditLogger auditLogger,
    INotificationDispatcher notifications) : IRequestHandler<RefundEducationalPaymentCommand, EducationalPaymentDto>
{
    public async Task<EducationalPaymentDto> Handle(
        RefundEducationalPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.PaymentsManage, cancellationToken: cancellationToken);

        var payment = await context.EducationalPayments
            .FirstOrDefaultAsync(entity => entity.Id == command.PaymentId, cancellationToken)
            ?? throw new NotFoundException("EducationalPayment", command.PaymentId);

        tenantAuthorization.EnsureSameTenant(access, payment.TenantId);

        payment.Refund(dateTimeProvider.UtcNow, command.Reason?.Trim());
        await context.SaveChangesAsync(cancellationToken);

        await PaymentNotifier.AnnounceAsync(
            context,
            notifications,
            payment,
            NotificationEvents.PaymentRefunded,
            "payment-refunded:" + payment.Id,
            cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.PaymentRefunded,
            nameof(EducationalPayment),
            payment.Id,
            newValues: new { payment.Status, command.Reason },
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        return await context.EducationalPayments
            .AsNoTracking()
            .Where(entity => entity.Id == payment.Id)
            .Select(EducationalPaymentProjections.ToDto)
            .FirstAsync(cancellationToken);
    }
}

/// <summary>
/// Money events go to the learner and to every confirmed guardian, because in practice it is
/// the guardian who pays. Each event is keyed on the payment, so a retried command or a
/// corrected record cannot dispatch the same receipt twice.
/// </summary>
internal static class PaymentNotifier
{
    public static async Task AnnounceAsync(
        IApplicationDbContext context,
        INotificationDispatcher notifications,
        EducationalPayment payment,
        string notificationEvent,
        string deduplicationKey,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>
        {
            ["amount"] = payment.NetAmount.ToString("0.##"),
            ["description"] = payment.Notes ?? payment.InvoiceNumber,
            ["due"] = payment.DueAt.HasValue ? payment.DueAt.Value.ToString("yyyy-MM-dd") : "قريبًا"
        };

        var recipients = new List<Guid>();

        var studentUserId = await NotificationAudience.StudentUserIdAsync(
            context, payment.StudentId, cancellationToken);

        if (studentUserId != Guid.Empty)
        {
            recipients.Add(studentUserId);
        }

        recipients.AddRange(await NotificationAudience.GuardianUserIdsAsync(
            context, payment.StudentId, cancellationToken));

        await notifications.SendManyAsync(
            recipients,
            new NotificationRequest
            {
                Event = notificationEvent,
                TenantId = payment.TenantId,
                ReferenceId = payment.Id,
                DeduplicationKey = deduplicationKey,
                Values = values
            },
            cancellationToken);
    }

    /// <summary>Tells the workspace owner that money actually arrived.</summary>
    public static async Task AnnounceToWorkspaceAsync(
        IApplicationDbContext context,
        INotificationDispatcher notifications,
        EducationalPayment payment,
        CancellationToken cancellationToken)
    {
        var ownerUserId = await context.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == payment.TenantId)
            .Select(tenant => (Guid?)tenant.OwnerUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (ownerUserId is null)
        {
            return;
        }

        await notifications.SendAsync(
            ownerUserId.Value,
            new NotificationRequest
            {
                Event = NotificationEvents.PaymentReceived,
                TenantId = payment.TenantId,
                ReferenceId = payment.Id,
                DeduplicationKey = "payment-received:" + payment.Id,
                Values = new Dictionary<string, string>
                {
                    ["student"] = await NotificationAudience.StudentNameAsync(
                        context, payment.StudentId, cancellationToken),
                    ["amount"] = payment.NetAmount.ToString("0.##")
                }
            },
            cancellationToken);
    }
}

// ---------------------------------------------------------------------------
// Queries
// ---------------------------------------------------------------------------
public sealed record GetEducationalPaymentsQuery : PagedRequest, IRequest<PaginatedList<EducationalPaymentDto>>
{
    public Guid? StudentId { get; init; }

    public Guid? GroupId { get; init; }

    public PaymentStatus? Status { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }
}

public sealed class GetEducationalPaymentsQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization)
    : IRequestHandler<GetEducationalPaymentsQuery, PaginatedList<EducationalPaymentDto>>
{
    public async Task<PaginatedList<EducationalPaymentDto>> Handle(
        GetEducationalPaymentsQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.PaymentsView, cancellationToken: cancellationToken);

        return await context.EducationalPayments
            .AsNoTracking()
            .Where(payment => payment.TenantId == access.TenantId)
            .WhereIf(query.StudentId.HasValue, payment => payment.StudentId == query.StudentId!.Value)
            .WhereIf(query.GroupId.HasValue, payment => payment.GroupId == query.GroupId!.Value)
            .WhereIf(query.Status.HasValue, payment => payment.Status == query.Status!.Value)
            .WhereIf(query.From.HasValue, payment => payment.CreatedAt >= query.From!.Value)
            .WhereIf(query.To.HasValue, payment => payment.CreatedAt <= query.To!.Value)
            .OrderByDescending(payment => payment.CreatedAt)
            .Select(EducationalPaymentProjections.ToDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

/// <summary>Tuition the signed-in student owes or has paid.</summary>
public sealed record GetMyPaymentsQuery : PagedRequest, IRequest<PaginatedList<EducationalPaymentDto>>;

public sealed class GetMyPaymentsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetMyPaymentsQuery, PaginatedList<EducationalPaymentDto>>
{
    public Task<PaginatedList<EducationalPaymentDto>> Handle(
        GetMyPaymentsQuery query,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        return context.EducationalPayments
            .AsNoTracking()
            .Where(payment => payment.StudentId == studentId)
            .OrderByDescending(payment => payment.CreatedAt)
            .Select(EducationalPaymentProjections.ToDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

/// <summary>Tuition for one of the calling parent's children.</summary>
public sealed record GetChildPaymentsQuery : PagedRequest, IRequest<PaginatedList<EducationalPaymentDto>>
{
    public Guid StudentId { get; init; }
}

public sealed class GetChildPaymentsQueryHandler(
    IApplicationDbContext context,
    IParentAccessGuard parentAccess) : IRequestHandler<GetChildPaymentsQuery, PaginatedList<EducationalPaymentDto>>
{
    public async Task<PaginatedList<EducationalPaymentDto>> Handle(
        GetChildPaymentsQuery query,
        CancellationToken cancellationToken)
    {
        await parentAccess.EnsureLinkedAsync(query.StudentId, cancellationToken);

        return await context.EducationalPayments
            .AsNoTracking()
            .Where(payment => payment.StudentId == query.StudentId)
            .OrderByDescending(payment => payment.CreatedAt)
            .Select(EducationalPaymentProjections.ToDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}
