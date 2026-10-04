using Microsoft.EntityFrameworkCore;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;

namespace Ustaz.Persistence.Seeding;

internal sealed partial class DemoDataSeeder
{
    /// <summary>
    /// Attendance for the two groups that actually run: five past sessions each, closed, plus
    /// one still open.
    ///
    /// The statuses are distributed deterministically rather than randomly, so a dashboard
    /// figure computed from these rows is the same number on every machine — which is what
    /// makes "the attendance rate is 78%" an assertion a test can make.
    /// </summary>
    private async Task SeedAttendanceAsync(CancellationToken cancellationToken)
    {
        string[] groupKeys = ["physics-3sec-intensive", "center-math-3sec"];

        var sessions = new List<AttendanceSession>();
        var records = new List<AttendanceRecord>();

        foreach (var groupKey in groupKeys)
        {
            var group = _groups[groupKey];
            var teacherUserId = _teachers.Values.First(teacher => teacher.Id == group.TeacherId).UserId;

            var schedule = await context.GroupSchedules
                .AsNoTracking()
                .Where(entry => entry.GroupId == group.Id)
                .OrderBy(entry => entry.DayOfWeek)
                .FirstOrDefaultAsync(cancellationToken);

            // Students whose enrollment occupies a seat are the ones a register lists.
            var attendees = await context.GroupEnrollments
                .AsNoTracking()
                .Where(enrollment => enrollment.GroupId == group.Id
                    && (enrollment.Status == EnrollmentStatus.Pending
                        || enrollment.Status == EnrollmentStatus.Active))
                .OrderBy(enrollment => enrollment.EnrolledAt)
                .Select(enrollment => enrollment.StudentId)
                .ToListAsync(cancellationToken);

            if (attendees.Count == 0)
            {
                continue;
            }

            for (var week = 5; week >= 0; week--)
            {
                var isOpen = week == 0;
                // Dates are distinct per group: the schema holds one session per group per day.
                var sessionDate = _utcNow.Date.AddDays(-7 * week);
                var sessionId = SeedId.For("demo:attendance-session", group.Id, week);

                sessions.Add(new AttendanceSession
                {
                    Id = sessionId,
                    TenantId = group.TenantId,
                    GroupId = group.Id,
                    GroupScheduleId = schedule?.Id,
                    SessionDate = sessionDate,
                    StartTime = schedule?.StartTime,
                    EndTime = schedule?.EndTime,
                    Title = "حصة " + (6 - week),
                    Notes = isOpen ? "الحصة الحالية - التسجيل مفتوح." : null,
                    Status = isOpen ? AttendanceSessionStatus.Open : AttendanceSessionStatus.Closed,
                    RecordedByUserId = teacherUserId,
                    ClosedAt = isOpen ? null : sessionDate.AddHours(20),
                    CreatedAt = sessionDate
                });

                // The open session is left without records: a register waiting to be taken is
                // a state the attendance screen has to handle.
                if (isOpen)
                {
                    continue;
                }

                for (var studentIndex = 0; studentIndex < attendees.Count; studentIndex++)
                {
                    var status = StatusFor(week, studentIndex);

                    records.Add(new AttendanceRecord
                    {
                        Id = SeedId.For("demo:attendance-record", sessionId, attendees[studentIndex]),
                        TenantId = group.TenantId,
                        AttendanceSessionId = sessionId,
                        StudentId = attendees[studentIndex],
                        Status = status,
                        MinutesLate = status == AttendanceStatus.Late ? 12 : null,
                        Note = status switch
                        {
                            AttendanceStatus.Excused => "إذن مسبق من ولي الأمر.",
                            AttendanceStatus.Absent => null,
                            _ => null
                        },
                        RecordedByUserId = teacherUserId,
                        RecordedAt = sessionDate.AddHours(18),
                        // Absences were notified, so the guardian alert is not re-sent.
                        ParentNotified = status == AttendanceStatus.Absent,
                        ParentNotifiedAt = status == AttendanceStatus.Absent ? sessionDate.AddHours(19) : null,
                        CreatedAt = sessionDate.AddHours(18)
                    });
                }
            }
        }

        await AddMissingAsync("AttendanceSessions", sessions, cancellationToken);
        await AddMissingAsync("AttendanceRecords", records, cancellationToken);
    }

    /// <summary>
    /// A fixed pattern over (week, student) that produces every attendance status, weighted
    /// the way a real register is: mostly present, some lateness, the occasional absence.
    /// </summary>
    private static AttendanceStatus StatusFor(int week, int studentIndex) =>
        ((week * 7 + studentIndex * 3) % 10) switch
        {
            0 => AttendanceStatus.Absent,
            3 => AttendanceStatus.Late,
            6 => AttendanceStatus.Excused,
            _ => AttendanceStatus.Present
        };
}
