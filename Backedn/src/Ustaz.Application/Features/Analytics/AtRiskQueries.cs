using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Domain.Constants;

namespace Ustaz.Application.Features.Analytics;

/// <summary>
/// Students in the caller's workspace who need attention, worst first.
///
/// This is workspace analytics, so it sits behind the analytics permission and is scoped to
/// the caller's own tenant — a student's standing is exactly the kind of data that must never
/// cross a workspace boundary.
/// </summary>
public sealed record GetAtRiskStudentsQuery : IRequest<IReadOnlyList<AtRiskStudentDto>>
{
    /// <summary>How far back to measure. Clamped to a week–year range.</summary>
    public int WindowInDays { get; init; } = 30;

    /// <summary>Lowest level to include; defaults to everyone who is flagged at all.</summary>
    public StudentRiskLevel MinimumLevel { get; init; } = StudentRiskLevel.Low;

    /// <summary>Caps the response so a large workspace cannot return an unbounded list.</summary>
    public int Take { get; init; } = 50;
}

public sealed class GetAtRiskStudentsQueryHandler(
    ITenantAuthorizationService tenantAuthorization,
    StudentRiskAnalyzer analyzer) : IRequestHandler<GetAtRiskStudentsQuery, IReadOnlyList<AtRiskStudentDto>>
{
    public async Task<IReadOnlyList<AtRiskStudentDto>> Handle(
        GetAtRiskStudentsQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.AnalyticsView, cancellationToken: cancellationToken);

        var students = await analyzer.AnalyzeTenantAsync(
            access.TenantId, query.WindowInDays, cancellationToken);

        return students
            .Where(student => student.RiskLevel >= query.MinimumLevel)
            .OrderByDescending(student => student.RiskScore)
            .ThenBy(student => student.StudentName)
            .Take(Math.Clamp(query.Take, 1, 200))
            .ToList();
    }
}

/// <summary>One student's standing, for the student detail screen.</summary>
public sealed record GetStudentRiskQuery(Guid StudentId) : IRequest<AtRiskStudentDto>;

public sealed class GetStudentRiskQueryHandler(
    ITenantAuthorizationService tenantAuthorization,
    StudentRiskAnalyzer analyzer) : IRequestHandler<GetStudentRiskQuery, AtRiskStudentDto>
{
    public async Task<AtRiskStudentDto> Handle(GetStudentRiskQuery query, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.StudentsView, cancellationToken: cancellationToken);

        var students = await analyzer.AnalyzeTenantAsync(access.TenantId, 30, cancellationToken);

        // The analyzer only ever covers this workspace's students, so a student id from
        // elsewhere simply is not in the result.
        return students.FirstOrDefault(student => student.StudentId == query.StudentId)
            ?? throw new Domain.Exceptions.NotFoundException("StudentProfile", query.StudentId);
    }
}

/// <summary>
/// Sends guardians an early warning about a child who is slipping.
///
/// Run on a schedule rather than on every event: parents want one useful message, not a ping
/// per missed homework. Deduplication is per student per calendar week, so a family hears
/// about a sustained problem once a week and never twice for the same week.
/// </summary>
public sealed record DispatchParentEarlyWarningsCommand : IRequest<int>
{
    public StudentRiskLevel MinimumLevel { get; init; } = StudentRiskLevel.High;
}

public sealed class DispatchParentEarlyWarningsCommandHandler(
    IApplicationDbContext context,
    StudentRiskAnalyzer analyzer,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications) : IRequestHandler<DispatchParentEarlyWarningsCommand, int>
{
    public async Task<int> Handle(
        DispatchParentEarlyWarningsCommand command,
        CancellationToken cancellationToken)
    {
        var utcNow = dateTimeProvider.UtcNow;
        var week = System.Globalization.ISOWeek.GetWeekOfYear(utcNow);
        var stamp = utcNow.Year + "w" + week;

        var tenantIds = await context.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Status == Domain.Enums.TenantStatus.Active)
            .Select(tenant => tenant.Id)
            .ToListAsync(cancellationToken);

        var delivered = 0;

        foreach (var tenantId in tenantIds)
        {
            var students = await analyzer.AnalyzeTenantAsync(tenantId, 30, cancellationToken);

            var ownerUserId = await context.Tenants
                .AsNoTracking()
                .Where(tenant => tenant.Id == tenantId)
                .Select(tenant => (Guid?)tenant.OwnerUserId)
                .FirstOrDefaultAsync(cancellationToken);

            foreach (var student in students.Where(entry => entry.RiskLevel >= command.MinimumLevel))
            {
                var reasonSummary = string.Join("، ", student.Reasons.Select(reason => reason.MessageAr));

                // The teacher is the one who can actually intervene, so they hear first.
                if (ownerUserId is not null)
                {
                    delivered += await notifications.SendAsync(
                        ownerUserId.Value,
                        new NotificationRequest
                        {
                            Event = NotificationEvents.StudentAtRisk,
                            TenantId = tenantId,
                            ReferenceId = student.StudentId,
                            DeduplicationKey = "risk-teacher:" + student.StudentId + ":" + stamp,
                            Values = new Dictionary<string, string>
                            {
                                ["student"] = student.StudentName,
                                ["reasons"] = reasonSummary
                            }
                        },
                        cancellationToken)
                        ? 1
                        : 0;
                }

                var guardians = await NotificationAudience.GuardianUserIdsAsync(
                    context, student.StudentId, cancellationToken);

                if (guardians.Count == 0)
                {
                    continue;
                }

                delivered += await notifications.SendManyAsync(
                    guardians,
                    new NotificationRequest
                    {
                        Event = NotificationEvents.ChildFallingBehind,
                        TenantId = tenantId,
                        ReferenceId = student.StudentId,
                        DeduplicationKey = "risk:" + student.StudentId + ":" + stamp,
                        Values = new Dictionary<string, string>
                        {
                            ["student"] = student.StudentName,
                            ["studentId"] = student.StudentId.ToString(),
                            ["reasons"] = reasonSummary
                        }
                    },
                    cancellationToken);
            }
        }

        return delivered;
    }
}
