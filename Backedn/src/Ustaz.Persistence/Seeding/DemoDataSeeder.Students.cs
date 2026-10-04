using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;

namespace Ustaz.Persistence.Seeding;

internal sealed partial class DemoDataSeeder
{
    private static readonly (string Email, string First, string Last, string City, string School)[] DemoStudents =
    [
        ("salma.ibrahim@ustaz.demo", "سلمى", "إبراهيم", "القاهرة", "مدرسة النصر الثانوية"),
        ("youssef.tarek@ustaz.demo", "يوسف", "طارق", "الجيزة", "مدرسة الأورمان الثانوية"),
        ("habiba.samir@ustaz.demo", "حبيبة", "سمير", "الإسكندرية", "مدرسة العباسية الثانوية"),
        ("kareem.adel@ustaz.demo", "كريم", "عادل", "المنصورة", "مدرسة المنصورة الثانوية")
    ];

    private static readonly string[] ReviewComments =
    [
        "شرح ممتاز وبسيط جداً، استفدت كثيراً من المجموعة.",
        "الأستاذ متابع للطلاب أول بأول والواجبات منظمة.",
        "المستوى تحسن بشكل واضح بعد شهرين من الاشتراك.",
        "الملخصات مفيدة جداً والامتحانات قريبة من الامتحان الرسمي."
    ];

    private async Task SeedStudentsAndEngagementAsync(
        string password,
        List<Group> groups,
        List<Course> courses,
        CancellationToken cancellationToken)
    {
        var students = new List<StudentProfile>();

        foreach (var demoStudent in DemoStudents)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = demoStudent.Email,
                Email = demoStudent.Email,
                EmailConfirmed = true,
                FirstName = demoStudent.First,
                LastName = demoStudent.Last,
                DisplayName = demoStudent.First + " " + demoStudent.Last,
                City = demoStudent.City,
                IsActive = true
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "Failed to seed demo student " + demoStudent.Email + ": " +
                    string.Join("; ", result.Errors.Select(error => error.Description)));
            }

            await userManager.AddToRoleAsync(user, RoleNames.Student);

            var profile = new StudentProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                GradeId = ThirdSecondaryId,
                SchoolName = demoStudent.School,
                City = demoStudent.City,
                ParentPhoneNumber = "+20100000000" + (students.Count + 1)
            };

            students.Add(profile);
            await context.StudentProfiles.AddAsync(profile, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);

        var reviews = new List<Review>();
        var groupEnrollments = new List<GroupEnrollment>();
        var courseEnrollments = new List<CourseEnrollment>();

        for (var index = 0; index < students.Count; index++)
        {
            var student = students[index];
            var group = groups[index % groups.Count];
            var course = courses[index % courses.Count];

            groupEnrollments.Add(new GroupEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                GroupId = group.Id,
                Status = EnrollmentStatus.Active,
                EnrolledAt = _utcNow.AddDays(-30 + index),
                PricePaid = group.MonthlyPrice,
                DiscountAmount = 0m,
                FinalPrice = group.MonthlyPrice
            });

            group.EnrolledStudentsCount++;

            // Register the student against the workspace that owns the group.
            await context.TenantStudents.AddAsync(
                new TenantStudent
                {
                    Id = Guid.NewGuid(),
                    TenantId = group.TenantId,
                    StudentId = student.Id,
                    IsActive = true,
                    JoinedAt = _utcNow,
                    CreatedAt = _utcNow
                },
                cancellationToken);

            courseEnrollments.Add(new CourseEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                CourseId = course.Id,
                Status = EnrollmentStatus.Active,
                EnrolledAt = _utcNow.AddDays(-20 + index),
                PricePaid = course.Price,
                DiscountAmount = 0m,
                FinalPrice = course.Price,
                ProgressPercentage = 25 * (index + 1) % 100
            });

            course.StudentCount++;

            reviews.Add(new Review
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                TeacherId = group.TeacherId,
                GroupId = group.Id,
                Rating = 4 + (index % 2),
                Comment = ReviewComments[index % ReviewComments.Length],
                IsApproved = true,
                ApprovedAt = _utcNow.AddDays(-10 + index)
            });
        }

        await context.GroupEnrollments.AddRangeAsync(groupEnrollments, cancellationToken);
        await context.CourseEnrollments.AddRangeAsync(courseEnrollments, cancellationToken);
        await context.Reviews.AddRangeAsync(reviews, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await RecalculateRatingsAsync(groups, cancellationToken);
    }

    private async Task RecalculateRatingsAsync(List<Group> groups, CancellationToken cancellationToken)
    {
        foreach (var group in groups)
        {
            var groupReviews = context.Reviews.Local
                .Where(review => review.GroupId == group.Id && review.IsApproved)
                .ToList();

            if (groupReviews.Count > 0)
            {
                group.ApplyRatingSnapshot(
                    (decimal)groupReviews.Average(review => review.Rating),
                    groupReviews.Count,
                    _utcNow);
            }
        }

        var teacherIds = groups.Select(group => group.TeacherId).Distinct().ToList();

        foreach (var teacherId in teacherIds)
        {
            var teacher = context.TeacherProfiles.Local.FirstOrDefault(profile => profile.Id == teacherId);
            if (teacher is null)
            {
                continue;
            }

            var teacherReviews = context.Reviews.Local
                .Where(review => review.TeacherId == teacherId && review.IsApproved)
                .ToList();

            if (teacherReviews.Count == 0)
            {
                continue;
            }

            teacher.ApplyRatingSnapshot(
                (decimal)teacherReviews.Average(review => review.Rating),
                teacherReviews.Count,
                _utcNow);

            teacher.StudentsCount = context.GroupEnrollments.Local
                .Count(enrollment => groups.Any(group => group.Id == enrollment.GroupId && group.TeacherId == teacherId));
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
