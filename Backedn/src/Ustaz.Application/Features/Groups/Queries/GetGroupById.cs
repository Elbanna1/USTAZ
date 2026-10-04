using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Groups.Dtos;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Groups.Queries;

/// <summary>
/// Full group page. Unpublished groups are only visible to their owner and to admins,
/// and restricted materials stay locked for anyone who is not enrolled.
/// </summary>
public sealed record GetGroupByIdQuery(Guid GroupId) : IRequest<GroupDetailsDto>;

public sealed class GetGroupByIdQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IWhatsAppLinkService whatsAppLinkService) : IRequestHandler<GetGroupByIdQuery, GroupDetailsDto>
{
    public async Task<GroupDetailsDto> Handle(GetGroupByIdQuery query, CancellationToken cancellationToken)
    {
        var details = await context.Groups
            .AsNoTracking()
            .Where(group => group.Id == query.GroupId)
            .Select(GroupProjectionsExtra.ToDetails)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Group", query.GroupId);

        var context2 = await context.Groups
            .AsNoTracking()
            .Where(group => group.Id == query.GroupId)
            .Select(group => new { group.TeacherId, TeacherWhatsApp = group.Teacher.WhatsAppNumber })
            .FirstAsync(cancellationToken);

        var isOwner = currentUser.TeacherId is not null && currentUser.TeacherId == context2.TeacherId;

        if (!details.IsPublished && !isOwner && !currentUser.IsAdmin)
        {
            throw new NotFoundException("Group", query.GroupId);
        }

        var studentId = currentUser.StudentId;
        var isEnrolled = studentId is not null && await context.GroupEnrollments
            .AsNoTracking()
            .AnyAsync(
                enrollment => enrollment.GroupId == query.GroupId
                    && enrollment.StudentId == studentId.Value
                    && (enrollment.Status == EnrollmentStatus.Active || enrollment.Status == EnrollmentStatus.Pending),
                cancellationToken);

        var canSeeRestricted = isEnrolled || isOwner || currentUser.IsAdmin;

        var materials = await context.GroupMaterials
            .AsNoTracking()
            .Where(material => material.GroupId == query.GroupId)
            .OrderBy(material => material.SortOrder)
            .Select(material => new GroupMaterialDto
            {
                Id = material.Id,
                GroupId = material.GroupId,
                Title = material.Title,
                Description = material.Description,
                FileUrl = !material.IsEnrolledOnly || canSeeRestricted ? material.FileUrl : null,
                FileType = material.FileType,
                FileSizeInBytes = material.FileSizeInBytes,
                IsEnrolledOnly = material.IsEnrolledOnly,
                IsLocked = material.IsEnrolledOnly && !canSeeRestricted,
                SortOrder = material.SortOrder
            })
            .ToListAsync(cancellationToken);

        return details with
        {
            IsOwner = isOwner,
            IsEnrolled = isEnrolled,
            Materials = materials,
            WhatsAppLink = details.WhatsAppLink
                ?? whatsAppLinkService.BuildChatLink(
                    context2.TeacherWhatsApp,
                    "السلام عليكم، أرغب في الاستفسار عن هذه المجموعة.")
        };
    }
}
