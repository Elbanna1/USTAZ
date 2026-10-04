using Microsoft.EntityFrameworkCore;
using Shouldly;
using Ustaz.Application.Features.Enrollments.Commands;
using Ustaz.Application.Tests.Infrastructure;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Tests.Handlers;

public class EnrollInGroupHandlerTests : IDisposable
{
    private readonly TestHarness _harness = new();

    private EnrollInGroupCommandHandler CreateHandler() =>
        new(_harness.Context,
            _harness.CurrentUser.Object,
            _harness.Clock,
            _harness.Notifications.Object,
            _harness.Cache.Object,
            _harness.TenantProvisioning);

    [Fact]
    public async Task Enrolling_creates_an_active_enrollment_and_takes_a_seat()
    {
        _harness.SignInAsStudent();

        var result = await CreateHandler().Handle(
            new EnrollInGroupCommand { GroupId = _harness.Group.Id }, CancellationToken.None);

        result.Status.ShouldBe(EnrollmentStatus.Active);
        result.FinalPrice.ShouldBe(500m);
        result.DiscountAmount.ShouldBe(0m);

        var group = await _harness.Context.Groups.FirstAsync(g => g.Id == _harness.Group.Id);
        group.EnrolledStudentsCount.ShouldBe(1);
    }

    [Fact]
    public async Task Enrolling_twice_in_the_same_group_is_rejected()
    {
        _harness.SignInAsStudent();
        var handler = CreateHandler();

        await handler.Handle(new EnrollInGroupCommand { GroupId = _harness.Group.Id }, CancellationToken.None);

        await Should.ThrowAsync<ConflictException>(() =>
            handler.Handle(new EnrollInGroupCommand { GroupId = _harness.Group.Id }, CancellationToken.None));
    }

    [Fact]
    public async Task Enrolling_into_a_full_group_is_rejected()
    {
        _harness.SignInAsStudent();

        var group = await _harness.Context.Groups.FirstAsync(g => g.Id == _harness.Group.Id);
        group.EnrolledStudentsCount = group.Capacity;
        await _harness.Context.SaveChangesAsync();

        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            CreateHandler().Handle(new EnrollInGroupCommand { GroupId = _harness.Group.Id }, CancellationToken.None));

        exception.Code.ShouldBe("GROUP_FULL");
    }

    [Fact]
    public async Task Enrolling_into_an_unpublished_group_is_rejected()
    {
        _harness.SignInAsStudent();

        var group = await _harness.Context.Groups.FirstAsync(g => g.Id == _harness.Group.Id);
        group.IsPublished = false;
        await _harness.Context.SaveChangesAsync();

        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            CreateHandler().Handle(new EnrollInGroupCommand { GroupId = _harness.Group.Id }, CancellationToken.None));

        exception.Code.ShouldBe("GROUP_NOT_PUBLISHED");
    }

    [Fact]
    public async Task Unknown_group_yields_not_found()
    {
        _harness.SignInAsStudent();

        await Should.ThrowAsync<NotFoundException>(() =>
            CreateHandler().Handle(new EnrollInGroupCommand { GroupId = Guid.NewGuid() }, CancellationToken.None));
    }

    [Fact]
    public async Task A_valid_discount_code_reduces_the_final_price_and_is_redeemed()
    {
        _harness.SignInAsStudent();

        _harness.Context.Discounts.Add(new Discount
        {
            Id = Guid.NewGuid(),
            TeacherId = _harness.Teacher.Id,
            Code = "WELCOME10",
            Type = DiscountType.Percentage,
            Value = 10m,
            StartDate = _harness.UtcNow.AddDays(-1),
            IsActive = true
        });
        await _harness.Context.SaveChangesAsync();

        var result = await CreateHandler().Handle(
            new EnrollInGroupCommand { GroupId = _harness.Group.Id, DiscountCode = "welcome10" },
            CancellationToken.None);

        result.DiscountAmount.ShouldBe(50m);
        result.FinalPrice.ShouldBe(450m);
        result.DiscountCode.ShouldBe("WELCOME10");

        var discount = await _harness.Context.Discounts.FirstAsync();
        discount.UsedCount.ShouldBe(1);
    }

    [Fact]
    public async Task An_expired_discount_code_blocks_the_enrollment()
    {
        _harness.SignInAsStudent();

        _harness.Context.Discounts.Add(new Discount
        {
            Id = Guid.NewGuid(),
            TeacherId = _harness.Teacher.Id,
            Code = "EXPIRED",
            Type = DiscountType.Percentage,
            Value = 10m,
            StartDate = _harness.UtcNow.AddDays(-30),
            EndDate = _harness.UtcNow.AddDays(-1),
            IsActive = true
        });
        await _harness.Context.SaveChangesAsync();

        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            CreateHandler().Handle(
                new EnrollInGroupCommand { GroupId = _harness.Group.Id, DiscountCode = "EXPIRED" },
                CancellationToken.None));

        exception.Code.ShouldBe("DISCOUNT_NOT_REDEEMABLE");
    }

    [Fact]
    public async Task A_discount_belonging_to_another_teacher_is_not_found()
    {
        _harness.SignInAsStudent();

        _harness.Context.Discounts.Add(new Discount
        {
            Id = Guid.NewGuid(),
            TeacherId = _harness.OtherTeacher.Id,
            Code = "OTHERS",
            Type = DiscountType.Percentage,
            Value = 10m,
            StartDate = _harness.UtcNow.AddDays(-1),
            IsActive = true
        });
        await _harness.Context.SaveChangesAsync();

        await Should.ThrowAsync<NotFoundException>(() =>
            CreateHandler().Handle(
                new EnrollInGroupCommand { GroupId = _harness.Group.Id, DiscountCode = "OTHERS" },
                CancellationToken.None));
    }

    [Fact]
    public async Task Cancelling_an_enrollment_frees_the_seat()
    {
        _harness.SignInAsStudent();

        var enrollment = await CreateHandler().Handle(
            new EnrollInGroupCommand { GroupId = _harness.Group.Id }, CancellationToken.None);

        var cancelHandler = new CancelGroupEnrollmentCommandHandler(
            _harness.Context,
            _harness.CurrentUser.Object,
            _harness.Clock,
            _harness.Notifications.Object,
            _harness.Cache.Object);

        await cancelHandler.Handle(
            new CancelGroupEnrollmentCommand { EnrollmentId = enrollment.Id, Reason = "تغيير الظروف" },
            CancellationToken.None);

        var group = await _harness.Context.Groups.FirstAsync(g => g.Id == _harness.Group.Id);
        group.EnrolledStudentsCount.ShouldBe(0);

        var stored = await _harness.Context.GroupEnrollments.FirstAsync(e => e.Id == enrollment.Id);
        stored.Status.ShouldBe(EnrollmentStatus.Cancelled);
    }

    [Fact]
    public async Task A_student_cannot_cancel_someone_elses_enrollment()
    {
        _harness.SignInAsStudent();
        var enrollment = await CreateHandler().Handle(
            new EnrollInGroupCommand { GroupId = _harness.Group.Id }, CancellationToken.None);

        // Switch to an unrelated teacher principal.
        _harness.SignInAsTeacher(_harness.OtherTeacher);

        var cancelHandler = new CancelGroupEnrollmentCommandHandler(
            _harness.Context,
            _harness.CurrentUser.Object,
            _harness.Clock,
            _harness.Notifications.Object,
            _harness.Cache.Object);

        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            cancelHandler.Handle(
                new CancelGroupEnrollmentCommand { EnrollmentId = enrollment.Id },
                CancellationToken.None));
    }

    public void Dispose() => _harness.Dispose();
}
