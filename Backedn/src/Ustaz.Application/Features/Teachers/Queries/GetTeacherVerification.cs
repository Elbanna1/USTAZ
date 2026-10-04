using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Teachers.Dtos;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Teachers.Queries;

/// <summary>Reads the calling teacher's own verification state and outstanding requirements.</summary>
public sealed record GetTeacherVerificationQuery : IRequest<TeacherVerificationDto>;

public sealed class GetTeacherVerificationQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetTeacherVerificationQuery, TeacherVerificationDto>
{
    public async Task<TeacherVerificationDto> Handle(
        GetTeacherVerificationQuery query,
        CancellationToken cancellationToken)
    {
        var teacherId = currentUser.RequireTeacherId();

        // Subjects and grades are counted in the database rather than loaded: the checklist
        // only needs to know whether any exist.
        var teacher = await context.TeacherProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == teacherId)
            .Select(profile => new
            {
                profile.VerificationStatus,
                profile.IsVerified,
                profile.VerifiedAt,
                profile.VerificationSubmittedAt,
                profile.VerificationNotes,
                profile.Headline,
                profile.Bio,
                profile.WhatsAppNumber,
                SubjectCount = profile.TeacherSubjects.Count,
                GradeCount = profile.TeacherGrades.Count
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Teacher", teacherId);

        var requirements = new List<VerificationRequirementDto>
        {
            new()
            {
                Key = "headline",
                IsMet = !string.IsNullOrWhiteSpace(teacher.Headline)
            },
            new()
            {
                Key = "bio",
                IsMet = !string.IsNullOrWhiteSpace(teacher.Bio)
                    && teacher.Bio!.Trim().Length >= TeacherProfile.MinVerifiableBioLength
            },
            new()
            {
                Key = "whatsAppNumber",
                IsMet = !string.IsNullOrWhiteSpace(teacher.WhatsAppNumber)
            },
            new() { Key = "subjects", IsMet = teacher.SubjectCount > 0 },
            new() { Key = "grades", IsMet = teacher.GradeCount > 0 }
        };

        var isComplete = requirements.TrueForAll(requirement => requirement.IsMet);
        var inReview = teacher.VerificationStatus
            is Domain.Enums.VerificationStatus.Pending
            or Domain.Enums.VerificationStatus.UnderReview;
        var canSubmitState = teacher.VerificationStatus
            is Domain.Enums.VerificationStatus.NotSubmitted
            or Domain.Enums.VerificationStatus.Rejected;

        return new TeacherVerificationDto
        {
            Status = teacher.VerificationStatus,
            IsVerified = teacher.IsVerified,
            IsUnderReview = inReview,
            IsProfileComplete = isComplete,
            CanSubmit = canSubmitState && isComplete,
            SubmittedAt = teacher.VerificationSubmittedAt,
            DecidedAt = teacher.VerifiedAt,
            Notes = teacher.VerificationNotes,
            Requirements = requirements
        };
    }
}
