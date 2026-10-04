using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Attendance.Dtos;

public sealed record AttendanceSessionDto
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid GroupId { get; init; }

    public string GroupName { get; init; } = string.Empty;

    public Guid? GroupScheduleId { get; init; }

    public DateTime SessionDate { get; init; }

    public TimeOnly? StartTime { get; init; }

    public TimeOnly? EndTime { get; init; }

    public string? Title { get; init; }

    public string? Notes { get; init; }

    public AttendanceSessionStatus Status { get; init; }

    public string StatusName => Status.ToString();

    public int TotalStudents { get; init; }

    public int PresentCount { get; init; }

    public int AbsentCount { get; init; }

    public int LateCount { get; init; }

    public int ExcusedCount { get; init; }

    public DateTime CreatedAt { get; init; }

    public IReadOnlyList<AttendanceRecordDto> Records { get; init; } = [];
}

public sealed record AttendanceRecordDto
{
    public Guid Id { get; init; }

    public Guid AttendanceSessionId { get; init; }

    public Guid StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public string? StudentImageUrl { get; init; }

    public AttendanceStatus Status { get; init; }

    public string StatusName => Status.ToString();

    public int? MinutesLate { get; init; }

    public string? Note { get; init; }

    public DateTime RecordedAt { get; init; }

    // Context, so student and parent views need no extra call.
    public Guid GroupId { get; init; }

    public string GroupName { get; init; } = string.Empty;

    public DateTime SessionDate { get; init; }
}

/// <summary>Attendance rates for one student, group or workspace.</summary>
public sealed record AttendanceStatisticsDto
{
    public int TotalSessions { get; init; }

    public int Present { get; init; }

    public int Absent { get; init; }

    public int Late { get; init; }

    public int Excused { get; init; }

    public decimal AttendanceRate { get; init; }

    public decimal AbsenceRate { get; init; }

    public IReadOnlyList<AttendanceBreakdownDto> ByGroup { get; init; } = [];
}

public sealed record AttendanceBreakdownDto
{
    public Guid GroupId { get; init; }

    public string GroupName { get; init; } = string.Empty;

    public int TotalSessions { get; init; }

    public int Present { get; init; }

    public int Absent { get; init; }

    public decimal AttendanceRate { get; init; }
}
