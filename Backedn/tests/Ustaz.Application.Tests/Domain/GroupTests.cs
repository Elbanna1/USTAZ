using Shouldly;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Events;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Tests.Domain;

public class GroupTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static TeacherProfile Teacher(bool verified = true) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        IsVerified = verified,
        VerificationStatus = verified ? VerificationStatus.Verified : VerificationStatus.Pending
    };

    private static Group Build(
        int capacity = 10,
        int enrolled = 0,
        bool published = true,
        bool active = true,
        TeacherProfile? teacher = null,
        bool withSchedule = true)
    {
        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = "فيزياء الثانوية العامة",
            Capacity = capacity,
            EnrolledStudentsCount = enrolled,
            IsPublished = published,
            IsActive = active,
            MonthlyPrice = 500m,
            Teacher = teacher ?? Teacher()
        };

        group.TeacherId = group.Teacher.Id;

        if (withSchedule)
        {
            group.Schedules.Add(new GroupSchedule
            {
                Id = Guid.NewGuid(),
                DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeOnly(16, 0),
                EndTime = new TimeOnly(18, 0),
                IsActive = true
            });
        }

        return group;
    }

    [Fact]
    public void Available_seats_and_full_flag_track_enrollment()
    {
        var group = Build(capacity: 10, enrolled: 7);

        group.AvailableSeats.ShouldBe(3);
        group.IsFull.ShouldBeFalse();

        group.EnrolledStudentsCount = 10;
        group.AvailableSeats.ShouldBe(0);
        group.IsFull.ShouldBeTrue();
    }

    [Fact]
    public void Full_group_rejects_another_student()
    {
        var group = Build(capacity: 5, enrolled: 5);

        var exception = Should.Throw<BusinessRuleException>(() => group.EnsureHasCapacity());
        exception.Code.ShouldBe("GROUP_FULL");
    }

    [Fact]
    public void Unpublished_group_rejects_enrollment()
    {
        var group = Build(published: false);

        Should.Throw<BusinessRuleException>(() => group.EnsureHasCapacity())
            .Code.ShouldBe("GROUP_NOT_PUBLISHED");
    }

    [Fact]
    public void Inactive_group_rejects_enrollment()
    {
        var group = Build(active: false);

        Should.Throw<BusinessRuleException>(() => group.EnsureHasCapacity())
            .Code.ShouldBe("GROUP_INACTIVE");
    }

    [Fact]
    public void Reaching_capacity_raises_a_domain_event()
    {
        var group = Build(capacity: 2, enrolled: 1);

        group.IncrementEnrollment(Now);

        group.EnrolledStudentsCount.ShouldBe(2);
        group.DomainEvents.OfType<GroupReachedCapacityEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Decrement_never_goes_below_zero()
    {
        var group = Build(enrolled: 0);

        group.DecrementEnrollment(Now);

        group.EnrolledStudentsCount.ShouldBe(0);
    }

    [Fact]
    public void Unverified_teacher_cannot_publish()
    {
        var group = Build(published: false, teacher: Teacher(verified: false));

        Should.Throw<BusinessRuleException>(() => group.Publish(Now))
            .Code.ShouldBe("TEACHER_NOT_VERIFIED");
    }

    [Fact]
    public void Group_without_an_active_session_cannot_be_published()
    {
        var group = Build(published: false, withSchedule: false);

        Should.Throw<BusinessRuleException>(() => group.Publish(Now))
            .Code.ShouldBe("GROUP_WITHOUT_SCHEDULE");
    }

    [Fact]
    public void Publishing_a_valid_group_raises_the_published_event()
    {
        var group = Build(published: false);

        group.Publish(Now);

        group.IsPublished.ShouldBeTrue();
        group.DomainEvents.OfType<GroupPublishedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Rating_snapshot_is_clamped_and_rounded()
    {
        var group = Build();

        group.ApplyRatingSnapshot(4.6666m, 3, Now);

        group.Rating.ShouldBe(4.67m);
        group.ReviewCount.ShouldBe(3);
    }

    [Fact]
    public void Has_discount_is_true_when_either_signal_is_present()
    {
        var byPercentage = Build();
        byPercentage.DiscountPercentage = 10;
        byPercentage.HasDiscount.ShouldBeTrue();

        var byOriginalPrice = Build();
        byOriginalPrice.OriginalPrice = 800m;
        byOriginalPrice.HasDiscount.ShouldBeTrue();

        Build().HasDiscount.ShouldBeFalse();
    }
}
