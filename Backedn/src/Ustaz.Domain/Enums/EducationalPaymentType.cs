namespace Ustaz.Domain.Enums;

/// <summary>
/// What a student/parent paid a teacher for. Entirely separate from the USTAZ
/// SaaS subscription that the teacher pays to the platform.
/// </summary>
public enum EducationalPaymentType
{
    GroupEnrollment = 1,
    CourseEnrollment = 2,
    MonthlySubscription = 3,
    OneTime = 4
}
