using System.Linq.Expressions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Security;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Activity;

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

/// <summary>
/// One entry on a timeline. The client renders its own copy from <see cref="Type"/>, filling in
/// <see cref="Metadata"/>; no wording is stored server-side.
/// </summary>
public sealed record ActivityEventDto
{
    public Guid Id { get; init; }

    /// <summary>Stable key from <c>ActivityTypes</c>, e.g. <c>homework.submitted</c>.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Null for something the system did rather than a person.</summary>
    public Guid? ActorUserId { get; init; }

    /// <summary>The actor's name as it stood when this happened.</summary>
    public string ActorName { get; init; } = string.Empty;

    public Guid? StudentId { get; init; }

    public string? StudentName { get; init; }

    public string? RelatedEntityType { get; init; }

    public Guid? RelatedEntityId { get; init; }

    /// <summary>Display values as a JSON object, or null when there are none.</summary>
    public string? Metadata { get; init; }

    public DateTime OccurredAt { get; init; }
}

internal static class ActivityProjections
{
    public static Expression<Func<ActivityEvent, ActivityEventDto>> ToDto =>
        entry => new ActivityEventDto
        {
            Id = entry.Id,
            Type = entry.Type,
            ActorUserId = entry.ActorUserId,
            ActorName = entry.ActorName,
            StudentId = entry.StudentId,
            StudentName = entry.Student != null ? entry.Student.User.DisplayName : null,
            RelatedEntityType = entry.RelatedEntityType,
            RelatedEntityId = entry.RelatedEntityId,
            Metadata = entry.Metadata,
            OccurredAt = entry.OccurredAt
        };
}

/// <summary>Filters every activity feed shares.</summary>
public abstract record ActivityFeedRequest : PagedRequest
{
    /// <summary>Restrict to these keys. Empty means every kind. Unknown keys are refused.</summary>
    public IReadOnlyList<string> Types { get; init; } = [];

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }
}

// ---------------------------------------------------------------------------
// Workspace feed
// ---------------------------------------------------------------------------

/// <summary>
/// What has been happening in the caller's workspace, newest first.
///
/// Only entries marked visible to the workspace are returned, so a learner's private timeline
/// never leaks into a staff feed, and the whole query is bounded by the caller's tenant.
/// </summary>
public sealed record GetWorkspaceActivityQuery : ActivityFeedRequest, IRequest<PaginatedList<ActivityEventDto>>
{
    /// <summary>Narrow to one student's activity within the workspace.</summary>
    public Guid? StudentId { get; init; }
}

public sealed class GetWorkspaceActivityQueryValidator : AbstractValidator<GetWorkspaceActivityQuery>
{
    public GetWorkspaceActivityQueryValidator() => this.ApplyActivityFeedRules();
}

public sealed class GetWorkspaceActivityQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization)
    : IRequestHandler<GetWorkspaceActivityQuery, PaginatedList<ActivityEventDto>>
{
    public async Task<PaginatedList<ActivityEventDto>> Handle(
        GetWorkspaceActivityQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.AnalyticsView, cancellationToken: cancellationToken);

        if (query.StudentId is { } studentId)
        {
            // Naming a student who is not in this workspace must not silently widen the feed.
            var inWorkspace = await context.TenantStudents
                .AsNoTracking()
                .AnyAsync(
                    link => link.TenantId == access.TenantId && link.StudentId == studentId,
                    cancellationToken);

            if (!inWorkspace)
            {
                throw new NotFoundException("Student", studentId);
            }
        }

        var types = query.Types.ToList();

        return await context.ActivityEvents
            .AsNoTracking()
            .Where(entry => entry.TenantId == access.TenantId
                && (entry.Visibility == ActivityVisibility.Workspace
                    || entry.Visibility == ActivityVisibility.Shared))
            .WhereIf(query.StudentId.HasValue, entry => entry.StudentId == query.StudentId!.Value)
            .WhereIf(types.Count > 0, entry => types.Contains(entry.Type))
            .WhereIf(query.From.HasValue, entry => entry.OccurredAt >= query.From!.Value)
            .WhereIf(query.To.HasValue, entry => entry.OccurredAt <= query.To!.Value)
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Select(ActivityProjections.ToDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

// ---------------------------------------------------------------------------
// Student feed
// ---------------------------------------------------------------------------

/// <summary>
/// One student's own timeline, newest first. A student reads their own; a guardian reads a
/// confirmed child's; a workspace member reads a student of their workspace.
/// </summary>
public sealed record GetStudentActivityQuery : ActivityFeedRequest, IRequest<PaginatedList<ActivityEventDto>>
{
    /// <summary>Omit to read the calling student's own timeline.</summary>
    public Guid? StudentId { get; init; }
}

public sealed class GetStudentActivityQueryValidator : AbstractValidator<GetStudentActivityQuery>
{
    public GetStudentActivityQueryValidator() => this.ApplyActivityFeedRules();
}

public sealed class GetStudentActivityQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    ITenantAuthorizationService tenantAuthorization,
    IParentAccessGuard parentAccess)
    : IRequestHandler<GetStudentActivityQuery, PaginatedList<ActivityEventDto>>
{
    public async Task<PaginatedList<ActivityEventDto>> Handle(
        GetStudentActivityQuery query,
        CancellationToken cancellationToken)
    {
        var studentId = await ResolveAsync(query.StudentId, cancellationToken);
        var types = query.Types.ToList();

        return await context.ActivityEvents
            .AsNoTracking()
            .Where(entry => entry.StudentId == studentId
                && (entry.Visibility == ActivityVisibility.Subject
                    || entry.Visibility == ActivityVisibility.Shared))
            .WhereIf(types.Count > 0, entry => types.Contains(entry.Type))
            .WhereIf(query.From.HasValue, entry => entry.OccurredAt >= query.From!.Value)
            .WhereIf(query.To.HasValue, entry => entry.OccurredAt <= query.To!.Value)
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Select(ActivityProjections.ToDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }

    private async Task<Guid> ResolveAsync(Guid? requested, CancellationToken cancellationToken)
    {
        var own = currentUser.StudentId;

        if (requested is null)
        {
            return own ?? throw new ForbiddenAccessException("Specify a student, or sign in as one.");
        }

        var studentId = requested.Value;

        if (own == studentId)
        {
            return studentId;
        }

        if (currentUser.ParentId is not null)
        {
            await parentAccess.EnsureLinkedAsync(studentId, cancellationToken);

            return studentId;
        }

        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.StudentsView, cancellationToken: cancellationToken);

        var inWorkspace = await context.TenantStudents
            .AsNoTracking()
            .AnyAsync(
                link => link.TenantId == access.TenantId && link.StudentId == studentId,
                cancellationToken);

        if (!inWorkspace)
        {
            throw new NotFoundException("Student", studentId);
        }

        return studentId;
    }
}

// ---------------------------------------------------------------------------
// Shared validation
// ---------------------------------------------------------------------------

internal static class ActivityFeedRules
{
    /// <summary>Caps how many keys one filter may carry, to bound the generated IN clause.</summary>
    private const int MaxTypeFilters = 20;

    public static void ApplyActivityFeedRules<T>(this AbstractValidator<T> validator)
        where T : ActivityFeedRequest
    {
        validator.RuleFor(query => query.Types)
            .Must(types => types.Count <= MaxTypeFilters)
            .WithMessage("Filter by at most " + MaxTypeFilters + " activity types.");

        // An unrecognised key would silently return nothing, which reads as "no activity" rather
        // than "you asked for something that does not exist".
        validator.RuleForEach(query => query.Types)
            .Must(ActivityTypes.IsKnown)
            .WithMessage("'{PropertyValue}' is not a known activity type.");

        validator.RuleFor(query => query)
            .Must(query => query.From!.Value <= query.To!.Value)
            .When(query => query.From.HasValue && query.To.HasValue)
            .WithMessage("The 'from' date must not be after the 'to' date.")
            .WithName(nameof(ActivityFeedRequest.From));
    }
}
