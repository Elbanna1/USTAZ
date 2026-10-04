namespace Ustaz.Domain.Enums;

/// <summary>
/// Functional grouping used by notification preferences. Complements the existing
/// <see cref="NotificationType"/>, which describes the individual notification.
/// </summary>
public enum NotificationCategory
{
    Attendance = 1,
    Grade = 2,
    Homework = 3,
    Exam = 4,
    Schedule = 5,
    Payment = 6,
    Subscription = 7,
    Announcement = 8,
    Message = 9,
    System = 10,

    /// <summary>Courses, lessons and learning progress.</summary>
    Course = 11,

    /// <summary>Sign-in, password and account-state events. Never silently disabled.</summary>
    Security = 12,

    /// <summary>Joining, leaving and being added to groups and courses.</summary>
    Enrollment = 13,

    /// <summary>Completion certificates.</summary>
    Certificate = 14
}
