using Shouldly;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;
using Ustaz.Domain.ValueObjects;

namespace Ustaz.Application.Tests.Domain;

public class CourseTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Course Build(bool verifiedTeacher = true, bool withContent = true, string? description = "وصف الكورس")
    {
        var teacher = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            IsVerified = verifiedTeacher,
            VerificationStatus = verifiedTeacher ? VerificationStatus.Verified : VerificationStatus.Pending
        };

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Title = "الفيزياء الحديثة",
            Description = description,
            Teacher = teacher,
            TeacherId = teacher.Id
        };

        if (withContent)
        {
            var section = new CourseSection { Id = Guid.NewGuid(), Title = "المقدمة" };
            section.Lessons.Add(new CourseLesson
            {
                Id = Guid.NewGuid(),
                Title = "الدرس الأول",
                VideoUrl = "https://example.com/video.mp4",
                DurationInMinutes = 25,
                IsPublished = true
            });

            course.Sections.Add(section);
        }

        return course;
    }

    [Fact]
    public void Course_without_content_cannot_be_published()
    {
        Should.Throw<BusinessRuleException>(() => Build(withContent: false).Publish(Now))
            .Code.ShouldBe("COURSE_WITHOUT_CONTENT");
    }

    [Fact]
    public void Course_without_a_description_cannot_be_published()
    {
        Should.Throw<BusinessRuleException>(() => Build(description: null).Publish(Now))
            .Code.ShouldBe("COURSE_WITHOUT_DESCRIPTION");
    }

    [Fact]
    public void Unverified_teacher_cannot_publish_a_course()
    {
        Should.Throw<BusinessRuleException>(() => Build(verifiedTeacher: false).Publish(Now))
            .Code.ShouldBe("TEACHER_NOT_VERIFIED");
    }

    [Fact]
    public void Publishing_recalculates_lesson_totals()
    {
        var course = Build();

        course.Publish(Now);

        course.IsPublished.ShouldBeTrue();
        course.LessonsCount.ShouldBe(1);
        course.DurationInMinutes.ShouldBe(25);
    }

    [Fact]
    public void Unpublished_lessons_are_excluded_from_totals()
    {
        var course = Build();
        course.Sections.First().Lessons.Add(new CourseLesson
        {
            Id = Guid.NewGuid(),
            Title = "مسودة",
            DurationInMinutes = 99,
            IsPublished = false
        });

        course.RecalculateContentTotals(Now);

        course.LessonsCount.ShouldBe(1);
        course.DurationInMinutes.ShouldBe(25);
    }
}

public class EgyptianPhoneNumberTests
{
    [Theory]
    [InlineData("01012345678")]
    [InlineData("+201012345678")]
    [InlineData("201012345678")]
    [InlineData("00201012345678")]
    [InlineData("0101 234 5678")]
    [InlineData("011-2345-6789")]
    public void Accepts_the_common_Egyptian_spellings(string raw) =>
        EgyptianPhoneNumber.IsValid(raw).ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("0301234567")]
    [InlineData("0101234567")]
    [InlineData("+1234567890")]
    [InlineData("not-a-number")]
    public void Rejects_invalid_numbers(string? raw) =>
        EgyptianPhoneNumber.IsValid(raw).ShouldBeFalse();

    [Fact]
    public void Normalises_to_E164()
    {
        var phone = EgyptianPhoneNumber.Create("0101 234 5678");

        phone.Value.ShouldBe("+201012345678");
        phone.NationalNumber.ShouldBe("1012345678");
    }

    [Fact]
    public void Create_throws_for_an_invalid_number() =>
        Should.Throw<BusinessRuleException>(() => EgyptianPhoneNumber.Create("123"));

    [Fact]
    public void Equal_numbers_written_differently_compare_equal() =>
        EgyptianPhoneNumber.Create("01012345678")
            .ShouldBe(EgyptianPhoneNumber.Create("+201012345678"));
}

public class SlugTests
{
    [Fact]
    public void Collapses_whitespace_and_punctuation_into_single_hyphens() =>
        Slug.Create("Physics   for  3rd  Secondary!!").Value.ShouldBe("physics-for-3rd-secondary");

    [Fact]
    public void Preserves_Arabic_letters() =>
        Slug.Create("فيزياء الثانوية العامة").Value.ShouldBe("فيزياء-الثانوية-العامة");

    [Fact]
    public void Falls_back_when_the_source_has_no_usable_characters() =>
        Slug.Create("!!!").Value.ShouldBe("item");

    [Fact]
    public void Unique_slugs_differ_for_the_same_source()
    {
        var first = Slug.CreateUnique("Physics");
        var second = Slug.CreateUnique("Physics");

        first.Value.ShouldStartWith("physics-");
        first.Value.ShouldNotBe(second.Value);
    }
}

public class TimeSlotTests
{
    [Fact]
    public void Overlapping_slots_on_the_same_day_are_detected()
    {
        var first = TimeSlot.Create(DayOfWeek.Monday, new TimeOnly(16, 0), new TimeOnly(18, 0));
        var second = TimeSlot.Create(DayOfWeek.Monday, new TimeOnly(17, 0), new TimeOnly(19, 0));

        first.Overlaps(second).ShouldBeTrue();
    }

    [Fact]
    public void Adjacent_slots_do_not_overlap()
    {
        var first = TimeSlot.Create(DayOfWeek.Monday, new TimeOnly(16, 0), new TimeOnly(18, 0));
        var second = TimeSlot.Create(DayOfWeek.Monday, new TimeOnly(18, 0), new TimeOnly(20, 0));

        first.Overlaps(second).ShouldBeFalse();
    }

    [Fact]
    public void Slots_on_different_days_never_overlap()
    {
        var first = TimeSlot.Create(DayOfWeek.Monday, new TimeOnly(16, 0), new TimeOnly(18, 0));
        var second = TimeSlot.Create(DayOfWeek.Tuesday, new TimeOnly(16, 0), new TimeOnly(18, 0));

        first.Overlaps(second).ShouldBeFalse();
    }

    [Fact]
    public void End_before_start_is_rejected() =>
        Should.Throw<BusinessRuleException>(() =>
            TimeSlot.Create(DayOfWeek.Monday, new TimeOnly(18, 0), new TimeOnly(16, 0)));
}

public class ReviewTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Ratings_outside_one_to_five_are_rejected(int rating) =>
        Should.Throw<BusinessRuleException>(() => new Review { Rating = rating }.EnsureRatingIsValid());

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Ratings_within_range_are_accepted(int rating) =>
        Should.NotThrow(() => new Review { Rating = rating }.EnsureRatingIsValid());

    [Fact]
    public void Target_type_follows_the_most_specific_reference()
    {
        new Review().TargetType.ShouldBe(ReviewTargetType.Teacher);
        new Review { GroupId = Guid.NewGuid() }.TargetType.ShouldBe(ReviewTargetType.Group);
        new Review { CourseId = Guid.NewGuid() }.TargetType.ShouldBe(ReviewTargetType.Course);
    }
}
