using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Shouldly;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Authentication.Commands;
using Ustaz.Application.Features.Authentication.Dtos;
using Ustaz.Application.Features.Authentication.Services;
using Ustaz.Application.Features.Parents;
using Ustaz.Application.Tests.Infrastructure;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Tests.Handlers;

/// <summary>
/// Guardian self-registration, and the student side of the link it requests.
///
/// The link request is the security-sensitive half: naming a child must not grant access,
/// and the student must always be able to answer the request — which means the notification
/// has to carry the id of the <see cref="ParentStudentLink"/>, not the id of the parent.
/// </summary>
public class ParentRegistrationTests : IDisposable
{
    private readonly TestHarness _harness = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManager = BuildUserManager();
    private readonly Mock<ITransactionalEmailService> _emails = new();
    private readonly Mock<IAuthenticationTokenIssuer> _tokens = new();

    public ParentRegistrationTests()
    {
        // The real UserManager writes the user row. The mock has to as well: ParentProfile.User
        // is a required navigation, so a guardian whose account row is missing is invisible to
        // every query that projects through it.
        _userManager
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser user, string _) =>
            {
                _harness.Context.Users.Add(user);
                _harness.Context.SaveChanges();
                return IdentityResult.Success;
            });

        _tokens
            .Setup(issuer => issuer.IssueAsync(
                It.IsAny<ApplicationUser>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticationResponse
            {
                AccessToken = "token",
                RefreshToken = "refresh-token",
                AccessTokenExpiresAt = _harness.UtcNow.AddMinutes(15),
                User = new CurrentUserDto { Email = "parent@ustaz.test" }
            });
    }

    private static Mock<UserManager<ApplicationUser>> BuildUserManager()
    {
        var manager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);

        manager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        manager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        manager.Setup(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("confirm-token");

        return manager;
    }

    private RegisterParentCommandHandler Handler() =>
        new(_harness.Context,
            _userManager.Object,
            _tokens.Object,
            _harness.Clock,
            _emails.Object,
            _harness.Notifications.Object,
            new ConfigurationBuilder().Build());

    private GetMyGuardiansQueryHandler GuardiansHandler() =>
        new(_harness.Context, _harness.CurrentUser.Object);

    private RespondToGuardianLinkCommandHandler RespondHandler() =>
        new(_harness.Context, _harness.CurrentUser.Object, _harness.Clock);

    private static RegisterParentCommand Command(
        string? childEmail = null,
        string phone = "01099988877") =>
        new()
        {
            FirstName = "أحمد",
            LastName = "عبد الله",
            Email = "parent@ustaz.test",
            Password = "Passw0rd!",
            PhoneNumber = phone,
            ChildEmail = childEmail,
            Relationship = "Father"
        };

    [Fact]
    public async Task Registering_creates_the_guardian_profile_and_assigns_the_parent_role()
    {
        await Handler().Handle(Command(), CancellationToken.None);

        var profile = await _harness.Context.ParentProfiles.SingleAsync();
        profile.PhoneNumber.ShouldBe("01099988877");

        _userManager.Verify(
            m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), Ustaz.Domain.Constants.RoleNames.Parent),
            Times.Once);

        // No child named: nothing is linked and nobody is notified.
        (await _harness.Context.ParentStudentLinks.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Naming_a_child_requests_access_but_does_not_grant_it()
    {
        await Handler().Handle(Command(childEmail: "salma@ustaz.test"), CancellationToken.None);

        var link = await _harness.Context.ParentStudentLinks.SingleAsync();

        link.StudentId.ShouldBe(_harness.Student.Id);
        link.IsConfirmed.ShouldBeFalse();
        link.ConfirmedAt.ShouldBeNull();
    }

    [Fact]
    public async Task The_link_is_confirmed_when_the_phone_matches_the_number_on_the_student_profile()
    {
        _harness.Student.ParentPhoneNumber = "01099988877";
        await _harness.Context.SaveChangesAsync();

        await Handler().Handle(Command(childEmail: "salma@ustaz.test"), CancellationToken.None);

        var link = await _harness.Context.ParentStudentLinks.SingleAsync();

        link.IsConfirmed.ShouldBeTrue();
        link.ConfirmedAt.ShouldBe(_harness.UtcNow);
    }

    [Fact]
    public async Task The_student_notification_references_the_link_so_the_request_can_be_answered()
    {
        await Handler().Handle(Command(childEmail: "salma@ustaz.test"), CancellationToken.None);

        var link = await _harness.Context.ParentStudentLinks.SingleAsync();

        _harness.Notifications.Verify(
            dispatcher => dispatcher.NotifyAsync(
                _harness.Student.UserId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationType.Security,
                NotificationCategory.Security,
                null,
                "/student/guardians",
                link.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task An_unknown_child_email_does_not_fail_the_registration()
    {
        var response = await Handler().Handle(
            Command(childEmail: "nobody@ustaz.test"), CancellationToken.None);

        response.AccessToken.ShouldBe("token");
        (await _harness.Context.ParentProfiles.AnyAsync()).ShouldBeTrue();
        (await _harness.Context.ParentStudentLinks.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task A_student_sees_the_pending_request_and_can_approve_it_with_the_notified_link_id()
    {
        await Handler().Handle(Command(childEmail: "salma@ustaz.test"), CancellationToken.None);

        _harness.SignInAsStudent();

        var guardians = await GuardiansHandler().Handle(new GetMyGuardiansQuery(), CancellationToken.None);

        var guardian = guardians.ShouldHaveSingleItem();
        guardian.IsConfirmed.ShouldBeFalse();
        guardian.Relationship.ShouldBe("Father");
        guardian.DisplayName.ShouldBe("أحمد عبد الله");
        guardian.Email.ShouldBe("parent@ustaz.test");
        guardian.PhoneNumber.ShouldBe("01099988877");

        await RespondHandler().Handle(
            new RespondToGuardianLinkCommand(guardian.LinkId, Approve: true), CancellationToken.None);

        (await _harness.Context.ParentStudentLinks.SingleAsync()).IsConfirmed.ShouldBeTrue();
    }

    [Fact]
    public async Task A_student_only_sees_guardians_linked_to_their_own_account()
    {
        var otherStudentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "other@ustaz.test",
            Email = "other@ustaz.test",
            FirstName = "طالب",
            LastName = "آخر",
            DisplayName = "طالب آخر",
            IsActive = true
        };

        _harness.Context.Users.Add(otherStudentUser);
        _harness.Context.StudentProfiles.Add(new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = otherStudentUser.Id,
            User = otherStudentUser
        });
        await _harness.Context.SaveChangesAsync();

        await Handler().Handle(Command(childEmail: "other@ustaz.test"), CancellationToken.None);

        _harness.SignInAsStudent();

        var guardians = await GuardiansHandler().Handle(new GetMyGuardiansQuery(), CancellationToken.None);

        guardians.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_duplicate_email_is_rejected_before_anything_is_written()
    {
        _userManager
            .Setup(m => m.FindByEmailAsync("parent@ustaz.test"))
            .ReturnsAsync(new ApplicationUser { Id = Guid.NewGuid(), Email = "parent@ustaz.test" });

        await Should.ThrowAsync<Ustaz.Domain.Exceptions.ConflictException>(
            () => Handler().Handle(Command(), CancellationToken.None));

        (await _harness.Context.ParentProfiles.AnyAsync()).ShouldBeFalse();
    }

    public void Dispose() => _harness.Dispose();
}
