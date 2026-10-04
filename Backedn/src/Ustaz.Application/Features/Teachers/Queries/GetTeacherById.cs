using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Teachers.Dtos;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Teachers.Queries;

/// <summary>Public teacher profile. Unverified teachers are only visible to admins.</summary>
public sealed record GetTeacherByIdQuery(Guid TeacherId) : IRequest<TeacherDetailsDto>;

public sealed class GetTeacherByIdQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IWhatsAppLinkService whatsAppLinkService) : IRequestHandler<GetTeacherByIdQuery, TeacherDetailsDto>
{
    public async Task<TeacherDetailsDto> Handle(GetTeacherByIdQuery query, CancellationToken cancellationToken)
    {
        var teacher = await context.TeacherProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == query.TeacherId)
            .Select(TeacherProjections.ToDetails)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Teacher", query.TeacherId);

        var isOwner = currentUser.TeacherId == query.TeacherId;

        if (!teacher.IsVerified && !currentUser.IsAdmin && !isOwner)
        {
            throw new NotFoundException("Teacher", query.TeacherId);
        }

        var whatsAppNumber = await context.TeacherProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == query.TeacherId)
            .Select(profile => profile.WhatsAppNumber)
            .FirstOrDefaultAsync(cancellationToken);

        return teacher with
        {
            WhatsAppLink = whatsAppLinkService.BuildChatLink(
                whatsAppNumber,
                "السلام عليكم، أرغب في الاستفسار عن المجموعات المتاحة.")
        };
    }
}
